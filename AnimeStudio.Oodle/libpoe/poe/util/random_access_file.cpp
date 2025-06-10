#include <poe/util/random_access_file.hpp>

#ifdef _WIN32
#include <Windows.h>
#else
#include <sys/types.h>
#include <sys/stat.h>
#include <fcntl.h>
#include <unistd.h>
#endif

namespace {
uint64_t get_file_size(uintptr_t os_handle) {
#ifdef _WIN32
    HANDLE h = reinterpret_cast<HANDLE>(os_handle);
    LARGE_INTEGER size{};
    GetFileSizeEx(h, &size);
    return static_cast<uint64_t>(size.QuadPart);
#else
    int fd = static_cast<int>(os_handle);
    struct stat buf;
    fstat(fd, &buf);
    return static_cast<uint64_t>(buf.st_size);
#endif
}
} // namespace

namespace poe::util {
random_access_file::random_access_file(std::filesystem::path path) {
#ifdef _WIN32
    HANDLE h =
        CreateFileW(path.wstring().c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, nullptr);
    if (h == INVALID_HANDLE_VALUE) {
        throw std::runtime_error("Could not load file");
    }
    this->os_handle_ = reinterpret_cast<uintptr_t>(h);
#else
    int fd = open(path.c_str(), O_RDONLY);
    if (fd < 0) {
        throw std::runtime_error("Could not load file");
    }
    this->os_handle_ = static_cast<uintptr_t>(fd);
#endif
    cached_size_ = get_file_size(this->os_handle_);
}

random_access_file::~random_access_file() {
#ifdef _WIN32
    HANDLE h = reinterpret_cast<HANDLE>(this->os_handle_);
    CloseHandle(h);
#else
    int fd = static_cast<int>(this->os_handle_);
    close(fd);
#endif
}

uint64_t random_access_file::size() const { return *cached_size_; }

void record_histogram_entry(std::array<std::atomic<uint64_t>, 32> &buckets, uint64_t n) {
    for (size_t i = 0; i < 32; ++i) {
        uint64_t upper_bound = 2ull << i;
        if (n < upper_bound) {
            ++buckets[i];
            break;
        }
    }
}

bool random_access_file::read_exact(uint64_t offset, std::byte *p, uint64_t n) const {
    if (offset + n > size()) {
        return false;
    }
#ifdef _WIN32
    HANDLE h = reinterpret_cast<HANDLE>(this->os_handle_);
    HANDLE guard = CreateEvent(nullptr, TRUE, FALSE, nullptr);
    uint64_t const BLOCK_SIZE = 1 << 20;
    uint64_t num_read = 0;
    for (uint64_t i = 0; i < n; i += BLOCK_SIZE) {
        uint64_t const block_n = (std::min)(BLOCK_SIZE, n - i);
        OVERLAPPED overlapped{};
        uint64_t off = offset + i;
        overlapped.Offset = off & 0xFFFFFFFF;
        overlapped.OffsetHigh = off >> 32;
        overlapped.hEvent = guard;
        BOOL res = ReadFile(h, p + i, static_cast<DWORD>(block_n), nullptr, &overlapped);
        if (res == FALSE) {
            if (GetLastError() == ERROR_IO_PENDING) {
                DWORD block_read = 0;
                GetOverlappedResult(h, &overlapped, &block_read, TRUE);
                num_read += block_read;
            } else {
                CloseHandle(guard);
                return false;
            }
        } else {
            DWORD block_read = 0;
            GetOverlappedResult(h, &overlapped, &block_read, FALSE);
            num_read += block_read;
        }
        ++number_of_os_reads_;
    }
    record_histogram_entry(histogram_buckets_, n);
    ++number_of_exact_reads_;
    CloseHandle(guard);
    return num_read == n;
#else
    int fd = static_cast<int>(this->os_handle_);
    ssize_t num_read = TEMP_FAILURE_RETRY(pread(fd, p, n, offset));
    return num_read == n;
#endif
}

uint64_t random_access_file::read_some(uint64_t offset, std::byte *p, uint64_t n) const {
    if (offset < size()) {
        uint64_t actual_n = (std::min)(n, size() - offset);
        if (this->read_exact(offset, p, actual_n)) {
            return actual_n;
        }
    }
    return 0;
}

} // namespace poe::util