// The decompressor will write outside of the target buffer.
#define SAFE_SPACE 64

#include <array>
#include <cstdint>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <sstream>
#include <string_view>
#include <vector>

#ifdef _MSC_VER
#include <Windows.h>

using sha256_digest = std::array<uint8_t, 32>;

std::string digest_to_string(sha256_digest const &digest) {
    char buf[128];
    char *p = buf;
    for (auto b : digest) {
        sprintf(p, "%02x", b);
    }
    return buf;
}

struct sha256_win32 {
    sha256_win32() {
        BCryptOpenAlgorithmProvider(&alg_handle_, BCRYPT_SHA256_ALGORITHM, nullptr, BCRYPT_HASH_REUSABLE_FLAG);
        BCryptCreateHash(alg_handle_, &hash_handle_, nullptr, 0, nullptr, 0, BCRYPT_HASH_REUSABLE_FLAG);
    }

    ~sha256_win32() {
        BCryptDestroyHash(hash_handle_);
        BCryptCloseAlgorithmProvider(alg_handle_, 0);
    }

    sha256_win32(sha256_win32 &) = delete;
    sha256_win32 operator=(sha256_win32 &) = delete;

    void reset() { finish(); }

    void feed(uint8_t const *data, size_t size) {
        UCHAR *p = const_cast<UCHAR *>(reinterpret_cast<UCHAR const *>(data));
        ULONG n = static_cast<ULONG>(size);
        BCryptHashData(hash_handle_, p, n, 0);
    }

    sha256_digest finish() {
        sha256_digest ret{};

        UCHAR *p = const_cast<UCHAR *>(reinterpret_cast<UCHAR const *>(ret.data()));
        ULONG n = static_cast<ULONG>(ret.size());
        BCryptFinishHash(hash_handle_, p, n, 0);
        return ret;
    }

  private:
    BCRYPT_ALG_HANDLE alg_handle_;
    BCRYPT_HASH_HANDLE hash_handle_;
};

std::string HashBytes(uint8_t const *data, size_t size) {
    sha256_win32 hasher;
    hasher.feed(data, size);
    return digest_to_string(hasher.finish());
}
#else
#include <sodium.h>

std::string HashBytes(uint8_t const *data, size_t size) {
    unsigned char hash_out[crypto_hash_sha256_BYTES];
    crypto_hash_sha256(hash_out, data, size);
    char hash_text[crypto_hash_sha256_BYTES * 2 + 1]{};
    sodium_bin2hex(hash_text, sizeof(hash_text), hash_out, sizeof(hash_out));
    return hash_text;
}
#endif

using byte = uint8_t;

int Kraken_Decompress(const byte *src, size_t src_len, byte *dst, size_t dst_len);

struct Bundle {
    Bundle(std::filesystem::path path) {
        std::ifstream is(path, std::ios::binary);
#define READ_POD(Name) is.read((char *)&Name, sizeof(Name))
        READ_POD(header.uncompressed_size);
        READ_POD(header.total_payload_size);
        READ_POD(header.head_payload_size);
        READ_POD(header.first_block_type);
        READ_POD(header.unk10);
        READ_POD(header.uncompressed_size2);
        READ_POD(header.total_payload_size2);
        READ_POD(header.block_count);
        READ_POD(header.uncompressed_block_granularity);
        READ_POD(header.unk28);

        block_sizes.resize(header.block_count);
        block_offsets.resize(header.block_count);
        is.read((char *)block_sizes.data(), sizeof(uint32_t) * block_sizes.size());

        auto file_pos = is.tellg();
        is.seekg(0, std::ios::end);
        auto tail_size = is.tellg() - file_pos;
        is.seekg(file_pos);
        chunk_data.resize(tail_size);
        is.read(chunk_data.data(), chunk_data.size());

        uint32_t last_offset = 0;
        for (size_t i = 0; i < header.block_count; ++i) {
            block_offsets[i] = last_offset;
            last_offset += block_sizes[i];
        }
    }

    struct Header {
        uint32_t uncompressed_size;
        uint32_t total_payload_size;
        uint32_t head_payload_size;
        enum : uint32_t { Kraken_6 = 8, Mermaid_A = 9, Leviathan_C = 13 } first_block_type;
        uint32_t unk10;
        uint64_t uncompressed_size2;
        uint64_t total_payload_size2;
        uint32_t block_count;
        uint32_t uncompressed_block_granularity;
        uint32_t unk28[4];
    };

    Header header;
    std::vector<uint32_t> block_sizes;
    std::vector<uint32_t> block_offsets;
    std::vector<char> chunk_data;
};

enum class Mode { Output, Verify, Write, };

int main(int argc, char *argv[]) {
    using namespace std::literals::string_view_literals;
    std::vector<uint8_t> output_buf(256 * 1024 + SAFE_SPACE);

    int first_file = 1;
    Mode mode = Mode::Output;
    if (argv[1] == "--verify"sv) {
        mode = Mode::Verify;
        ++first_file;
    } else if (argv[1] == "--write"sv) {
        mode = Mode::Write;
        ++first_file;
    }

    for (int file_idx = first_file; file_idx < argc; ++file_idx) {
        std::filesystem::path path = argv[file_idx];
        Bundle b(path);

        auto checksum_path = path;
        checksum_path.replace_extension(path.extension().string() + ".sum");
        std::ifstream is(checksum_path);

        std::vector<std::string> checksums;
        std::ofstream checksum_os;
        if (mode == Mode::Verify) {
            if (!is) {
                std::cerr << "expected file missing: " << checksum_path << std::endl;
                return 1;
            }
            std::string checksum, filename;
            int index;
            while (is >> checksum >> filename >> index) {
                if (checksum.size() != 64) {
                    std::cerr << "invalid checksum \"" << checksum << "\" in file " << checksum_path << std::endl;
                    return 1;
                }
                checksums.push_back(checksum);
            }
        } else if (mode == Mode::Write) {
            checksum_os.open(checksum_path);
        }
        for (size_t block_idx = 0; block_idx < b.header.block_count; ++block_idx) {
            auto dst_size = (std::min<size_t>)(256 * 1024, b.header.uncompressed_size - block_idx * 256 * 1024);
            auto src_offset = b.block_offsets[block_idx];
            auto src_size = b.block_sizes[block_idx];
            auto res =
                Kraken_Decompress((byte const *)&b.chunk_data[src_offset], src_size, output_buf.data(), dst_size);

            auto hash_text = HashBytes(output_buf.data(), dst_size);
            std::ostringstream line_os;
            line_os << hash_text << "\t" << path.filename().generic_string() << "\t" << block_idx;
            if (mode == Mode::Output) {
                puts(line_os.str().c_str());
            } else if (mode == Mode::Write) {
                checksum_os << line_os.str() << std::endl;
            } else if (mode == Mode::Verify) {
                auto &expected = checksums[block_idx];
                if (hash_text != expected) {
                    std::cerr << "expected checksum " << expected << ", got " << hash_text << std::endl;
                    return 1;
                }
            }
        }
    }
    return 0;
}
