#pragma once

#include <mio/mio.hpp>

#include <array>
#include <atomic>
#include <cstdint>
#include <cstring>
#include <filesystem>
#include <optional>
#include <vector>

namespace poe::util {
class random_access_file {
  public:
    explicit random_access_file(std::filesystem::path path);
    ~random_access_file();

    random_access_file(random_access_file &) = delete;
    random_access_file &operator=(random_access_file &) = delete;

    uint64_t size() const;
    bool read_exact(uint64_t offset, std::byte *p, uint64_t n) const;
    uint64_t read_some(uint64_t offset, std::byte *p, uint64_t n) const;

    uint64_t debug_number_of_os_reads() const { return number_of_os_reads_; }
    uint64_t debug_number_of_exact_reads() const { return number_of_exact_reads_; }
    std::string debug_render_histogram() const;

  private:
    uintptr_t os_handle_;
    std::optional<uint64_t> cached_size_;
    mutable std::atomic<uint64_t> number_of_os_reads_ = 0;
    mutable std::atomic<uint64_t> number_of_exact_reads_ = 0;
    mutable std::array<std::atomic<uint64_t>, 32> histogram_buckets_;
};

using mmap_source = mio::basic_mmap_source<std::byte>;

inline uint64_t read_some(std::byte const *src_data, uint64_t src_size, uint64_t offset, std::byte *p, size_t n) {
    if (!n) {
        return 0;
    }
    uint64_t end = (std::min)(offset + n, src_size);
    uint64_t amount = end - offset;
    if (!amount) {
        return 0;
    }
    std::memcpy(p, src_data + offset, amount);
    return amount;
}

inline uint64_t read_some(mmap_source const &source, uint64_t offset, std::byte *p, size_t n) {
    return read_some(source.data(), source.size(), offset, p, n);
}

inline bool read_exact(mmap_source const &source, uint64_t offset, std::byte *p, size_t n) {
    if (!n) {
        return true;
    }
    if (offset + n > source.size()) {
        return false;
    }
    std::memcpy(p, source.data() + offset, n);
    return true;
}

struct stream_reader {
    stream_reader(std::byte const *p, size_t n, uint64_t offset)
        : data_(p), size_(n), cursor_(offset), buffer_(8 << 10), buffer_valid_begin_(0), buffer_valid_end_(0),
          buffer_cursor_(offset) {}
    stream_reader(mmap_source const &file, uint64_t offset) : stream_reader(file.data(), file.size(), offset) {}

    bool read_exact(std::byte *p, size_t n) {
        if (!n) {
            return true;
        }
        while (true) {
            size_t in_buffer = buffer_valid_end_ - buffer_valid_begin_;
            size_t amount = (std::min)(in_buffer, n);
            memcpy(p, buffer_.data() + buffer_valid_begin_, amount);
            buffer_valid_begin_ += amount;
            p += amount;
            n -= amount;
            cursor_ += amount;
            if (!n) {
                break;
            }
            buffer_valid_begin_ = 0;
            if (n > buffer_.size()) {
                if (!read_some(data_, size_, buffer_cursor_, p, n)) {
                    return false;
                }
                buffer_cursor_ += n;
                buffer_valid_end_ = 0;
                return true;
            } else {
                buffer_valid_begin_ = 0;
                buffer_valid_end_ = read_some(data_, size_, buffer_cursor_, buffer_.data(), buffer_.size());
                buffer_cursor_ += buffer_valid_end_;
                if (buffer_valid_end_ == 0) {
                    return false;
                }
            }
        }
        return true;
    }

    template <typename T> bool read_one(T &t) {
        constexpr size_t N = sizeof(T);
        std::array<std::byte, N> buf;
        if (!read_exact(buf.data(), buf.size())) {
            return false;
        }
        memcpy(&t, buf.data(), buf.size());

        return true;
    }

    template <typename T> bool read_many(T *t, size_t n) {
        if (!n) {
            return true;
        }
        size_t const N = sizeof(T) * n;
        std::vector<std::byte> buf(N);
        if (!read_exact(buf.data(), buf.size())) {
            return false;
        }
        memcpy(t, buf.data(), buf.size());

        return true;
    }

    bool read_terminated_u16string(std::u16string &s, size_t cch_including_terminator) {
        std::vector<char16_t> buf((std::max<size_t>)(1u, cch_including_terminator));
        if (!read_many(buf.data(), buf.size())) {
            return false;
        }
        s = std::u16string(buf.data());
        return true;
    }

    bool skip(uint64_t n) {
        auto buffer_cut = (std::min)(buffer_valid_end_ - buffer_valid_begin_, n);
        if (buffer_cut != 0) {
            n -= buffer_cut;
            buffer_valid_begin_ += buffer_cut;
        }
        if (cursor_ + n <= size_) {
            cursor_ += n;
            return true;
        }
        return false;
    }

    uint64_t cursor() const { return cursor_; }

  private:
    std::byte const *data_;
    uint64_t size_;
    uint64_t cursor_;
    std::vector<std::byte> buffer_;
    uint64_t buffer_valid_begin_;
    uint64_t buffer_valid_end_;
    uint64_t buffer_cursor_;
};

inline std::unique_ptr<stream_reader> make_stream_reader(std::byte const *p, size_t n, uint64_t offset) {
    return std::make_unique<stream_reader>(p, n, offset);
}

inline std::unique_ptr<stream_reader> make_stream_reader(mmap_source const &file, uint64_t offset) {
    return std::make_unique<stream_reader>(std::cref(file), offset);
}
} // namespace poe::util