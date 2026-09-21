#include "bun.h"

#include <stddef.h>
#include <string.h>

#include <algorithm>
#include <filesystem>
#include <fstream>
#include <map>
#include <memory>
#include <set>
#include <sstream>
#include <string>
#include <unordered_map>
#include <vector>

#include "fnv.h"
#include "murmur.h"
#include "path_rep.h"
#include "util.h"

#ifdef _WIN32
#include <Windows.h>
#define DECOMPRESS_API WINAPI
#else
#include <sys/mman.h>
#include <unistd.h>
#include <dlfcn.h>
#define DECOMPRESS_API
#endif

size_t const SAFE_SPACE = 64;

using decompress_fun = int(DECOMPRESS_API *)(uint8_t const *src_buf, int src_len, uint8_t *dst, size_t dst_size, int,
                                             int, int, uint8_t *, size_t, void *, void *, void *, size_t, int);

struct Bun {
    std::shared_ptr<void> decompress_mod_;
    decompress_fun decompress_fun_;
};

struct bundle_info {
    std::string name_;
    uint32_t uncompressed_size_;
};

struct file_info {
    uint64_t path_hash_;
    uint32_t bundle_index_;
    uint32_t file_offset_;
    uint32_t file_size_;
};

struct path_rep_info {
    uint64_t hash;
    uint32_t offset;
    uint32_t size;
    uint32_t recursive_size;
};

enum class HashAlgorithm {
    Unknown,
    FNV1A_3_11_2,
    MurmurHash2A_3_21_2,
};

struct BunIndex {
    bool read_file(char const *path, std::vector<uint8_t> &out);
    Bun *bun_;
    Vfs *vfs_;
    std::string bundle_root_;
    BunMem index_mem_;
    std::vector<bundle_info> bundle_infos_;
    std::vector<file_info> file_infos_;
    std::vector<path_rep_info> path_rep_infos_;
    std::unordered_map<uint64_t, uint32_t> path_hash_to_file_info_;
    BunMem inner_mem_;
    HashAlgorithm hash_algorithm_;
    uint64_t hash_seed_;
};

inline uint64_t hash_path_3_21_2(std::string path, uint64_t seed) {
    while (path.back() == '/') {
      path.pop_back();
    }
    for (auto &ch : path) {
      ch = (char)std::tolower((int)(unsigned char)ch);
    }
    return murmur_hash_64a(path.c_str(), (int)path.size(), seed);
}

inline uint64_t hash_directory_3_11_2(std::string path) {
    while (path.back() == '/') {
      path.pop_back();
    }
    path += "++";
    return fnv1a_64(path.data(), path.size());
}

inline uint64_t hash_file_3_11_2(std::string path) {
    for (auto &ch : path) {
      ch = (char)std::tolower((int)(unsigned char)ch);
    }
    path += "++";
    return fnv1a_64(path.data(), path.size());
}

bool BunIndex::read_file(char const *path, std::vector<uint8_t> &out) {
    std::string full_path = bundle_root_ + '/' + path;
    if (vfs_) {
        auto fh = vfs_->open(vfs_, full_path.c_str());
        if (!fh) {
            return false;
        }
        auto size = vfs_->size(vfs_, fh);
        out.resize(size);
        bool success = vfs_->read(vfs_, fh, out.data(), 0, size) == size;

        vfs_->close(vfs_, fh);
        return success;
    } else {
        std::ifstream is(full_path, std::ios::binary);
        if (!is) {
            return false;
        }
        is.seekg(0, std::ios::end);
        auto size = is.tellg();
        is.seekg(0, std::ios::beg);
        out.resize(size);
        return !!is.read(reinterpret_cast<char *>(out.data()), out.size());
    }
}

BUN_DLL_PUBLIC Bun *BunNew(char const *decompressor_path, char const *decompressor_export) {
    if (!decompressor_export) {
        decompressor_export = "OodleLZ_Decompress";
    }
    auto bun = std::make_unique<Bun>();
#ifdef _WIN32
    auto mod = LoadLibraryA(decompressor_path);
    if (!mod) {
        return nullptr;
    }
    bun->decompress_mod_.reset(mod, &FreeLibrary);

    auto fun =
        reinterpret_cast<decompress_fun>(GetProcAddress((HMODULE)bun->decompress_mod_.get(), decompressor_export));
    if (!fun) {
        return nullptr;
    }
    bun->decompress_fun_ = fun;
#else
    auto mod = dlopen(decompressor_path, RTLD_NOW | RTLD_LOCAL);
    if (!mod) {
        return nullptr;
    }
    bun->decompress_mod_.reset(mod, &dlclose);

    auto fun = reinterpret_cast<decompress_fun>(dlsym(mod, decompressor_export));
    if (!fun) {
        return nullptr;
    }
    bun->decompress_fun_ = fun;
#endif

    return bun.release();
}

BUN_DLL_PUBLIC void BunDelete(Bun *bun) { delete bun; }

std::string printable_string(uint32_t x) {
    std::string s;
    for (size_t i = 0; i < 4; ++i) {
        auto shift = i * 8;
        uint8_t c = (x >> shift) & 0xFF;
        if (c == '"') {
            s += "\"\"";
        } else if (isprint((int)c)) {
            s += c;
        } else {
            s += ".";
        }
    }
    return s;
}

std::string hex_dump(size_t width, uint8_t const *p, size_t n) {
    std::string s;
    char buf[10];
    while (n) {
        size_t k = (std::min<size_t>)(n, width);
        for (size_t i = 0; i < k; ++i) {
            sprintf(buf, "%s%02X", (i ? " " : ""), p[i]);
            s += buf;
        }
        for (size_t i = k; i < width; ++i) {
            s += "   ";
        }
        s += " | ";
        for (size_t i = 0; i < k; ++i) {
            auto c = p[i];
            if (isprint((int)c)) {
                s += c;
            } else {
                s += ".";
            }
        }
        s += "\n";
        p += k;
        n -= k;
    }
    return s;
}

BUN_DLL_PUBLIC BunIndex *BunIndexOpen(Bun *bun, Vfs *vfs, char const *root_dir) {
    auto idx = std::make_unique<BunIndex>();
    idx->bun_ = bun;
    idx->vfs_ = vfs;
    idx->bundle_root_ = vfs ? "Bundles2" : (root_dir + std::string("/Bundles2"));

    std::vector<uint8_t> index_bin_src;
    if (!idx->read_file("_.index.bin", index_bin_src)) {
        fprintf(stderr, "Could not read _.index.bin\n");
        return nullptr;
    }
    auto index_bin_mem = BunDecompressBundleAlloc(bun, index_bin_src.data(), index_bin_src.size());
    if (!index_bin_mem) {
        fprintf(stderr, "Could not decompress _.index.bin\n");
        return nullptr;
    }
    fprintf(stderr, "Index bundle decompressed, %lld bytes\n", BunMemSize(index_bin_mem));
    idx->index_mem_ = index_bin_mem;

    uint32_t bundle_count;
    reader r{idx->index_mem_, (size_t)BunMemSize(idx->index_mem_)};
    r.read(bundle_count);
    idx->bundle_infos_.reserve(bundle_count);
    std::map<std::string, size_t> bundle_index_from_name;
    for (size_t i = 0; i < bundle_count; ++i) {
        bundle_info bi;

        uint32_t name_length;
        r.read(name_length);

        std::vector<char> name_buf(name_length);
        r.read(name_buf);
        bi.name_.assign(name_buf.begin(), name_buf.end());

        r.read(bi.uncompressed_size_);

        bundle_index_from_name[bi.name_] = i;

        idx->bundle_infos_.push_back(bi);
    }

    std::map<std::string, size_t> bundle_name_to_bundle_index;

    std::vector<std::vector<std::string>> bundled_filenames(idx->bundle_infos_.size());

    using FilenameBundle = std::map<std::string, size_t>;
    FilenameBundle filename_bundle;

    std::map<size_t, std::vector<size_t>> bundle_file_seqs;
    uint32_t file_count;
    r.read(file_count);
    for (size_t i = 0; i < file_count; ++i) {
        file_info fi;

        r.read(fi.path_hash_);
        r.read(fi.bundle_index_);
        r.read(fi.file_offset_);
        r.read(fi.file_size_);

        idx->path_hash_to_file_info_[fi.path_hash_] = (uint32_t)i;

        bundle_file_seqs[fi.bundle_index_].push_back(i);

        idx->file_infos_.push_back(fi);
    }

    fprintf(stderr, "Bundle count in index binary: %zu\n", idx->bundle_infos_.size());
    // fprintf(stderr, "Bundle count in index text:   %zu\n", text_bundle_count);
    fprintf(stderr, "File count in index binary: %zu\n", idx->file_infos_.size());
    // fprintf(stderr, "File count in index text:   %zu\n", text_file_count);

    uint32_t some_count;
    r.read(some_count);

    idx->path_rep_infos_.reserve(some_count);
    for (size_t i = 0; i < some_count; ++i) {
        path_rep_info si;
        r.read(si.hash);
        r.read(si.offset);
        r.read(si.size);
        r.read(si.recursive_size);
        idx->path_rep_infos_.push_back(si);
    }

    auto inner_mem = BunDecompressBundleAlloc(idx->bun_, r.p_, r.n_);
    idx->inner_mem_ = inner_mem;
    fprintf(stderr, "Decompressed inner size: %lld\n", BunMemSize(inner_mem));

    idx->hash_algorithm_ = HashAlgorithm::Unknown;
    idx->hash_seed_ = 0;
    if (some_count) {
        auto root_hash = idx->path_rep_infos_[0].hash;
        switch (root_hash) {
        case 0x07e47507b4a92e53:
            idx->hash_algorithm_ = HashAlgorithm::FNV1A_3_11_2;
            break;
        default: {
            // Recover seed from root hash via math wizardry
            auto h = root_hash;
            h ^= h >> 47;
            h *= 0x5F7A0EA7E59B19BDULL;
            h ^= h >> 47;
            bool seed_validated = true;
            for (int i = 1; i < idx->path_rep_infos_.size(); ++i) {
                auto &ref = idx->path_rep_infos_[i];
                auto results = generate_paths(inner_mem + ref.offset, ref.size);
                if (!results.empty()) {
                  auto &r = results[0];
                  auto slash_pos = r.find_last_of('/');
                  if (slash_pos != r.npos) {
                    auto dir = r.substr(0, slash_pos);
                    auto computed_hash = hash_path_3_21_2(dir, h);
                    seed_validated = (computed_hash == ref.hash);
                    break;
                  }
                }
            }
            if (seed_validated) {
                idx->hash_algorithm_ = HashAlgorithm::MurmurHash2A_3_21_2;
                idx->hash_seed_ = h;
                fprintf(stderr, "Hash seed: 0x%016llx\n", idx->hash_seed_);
            }
            break;
        }
        }
    }

    if (idx->hash_algorithm_ == HashAlgorithm::Unknown) {
        fprintf(stderr, "Could not detect path hash algorithm/seed\n");
        return nullptr;
    }

    return idx.release();
}

BUN_DLL_PUBLIC void BunIndexClose(BunIndex *idx) {
    if (idx) {
        BunMemFree(idx->index_mem_);
        BunMemFree(idx->inner_mem_);
    }
}

BUN_DLL_PUBLIC int32_t BunIndexLookupFileByPath(BunIndex *idx, char const *path) {
    if (!idx) {
        return -1;
    }

    uint64_t path_hash{};
    switch (idx->hash_algorithm_) { case HashAlgorithm::FNV1A_3_11_2:
        path_hash = hash_file_3_11_2(path);
        break;
    case HashAlgorithm::MurmurHash2A_3_21_2:
        path_hash = hash_path_3_21_2(path, idx->hash_seed_);
        break;
    default:
        return -1;
    }

    auto I = idx->path_hash_to_file_info_.find(path_hash);
    if (I != idx->path_hash_to_file_info_.end()) {
        return I->second;
    }
    return -1;
}

BUN_DLL_PUBLIC BunMem BunIndexExtractFile(BunIndex *idx, int32_t file_id) {
    if (!idx || file_id < 0 || file_id >= idx->file_infos_.size()) {
        return nullptr;
    }

    auto &fi = idx->file_infos_[file_id];
    auto &bi = idx->bundle_infos_[fi.bundle_index_];
    std::filesystem::path bundle_path = idx->bundle_root_;
    bundle_path /= bi.name_ + ".bundle.bin";

    std::vector<uint8_t> bundle_data;
    slurp_file(bundle_path, bundle_data);
    BunMem all_data = BunDecompressBundleAlloc(idx->bun_, bundle_data.data(), bundle_data.size());
    BunMem ret_mem = BunMemAlloc(fi.file_size_);
    memcpy(ret_mem, all_data + fi.file_offset_, fi.file_size_);
    BunMemFree(all_data);
    return ret_mem;
}

BUN_DLL_PUBLIC BunMem BunIndexExtractBundle(BunIndex *idx, int32_t bundle_id) {
    if (!idx || bundle_id < 0 || bundle_id >= idx->bundle_infos_.size()) {
        return nullptr;
    }

    auto &bi = idx->bundle_infos_[bundle_id];

    std::string bundle_path = bi.name_ + ".bundle.bin";

    std::vector<uint8_t> bundle_data;
    if (!idx->read_file(bundle_path.c_str(), bundle_data)) {
        return nullptr;
    }
    return BunDecompressBundleAlloc(idx->bun_, bundle_data.data(), bundle_data.size());
}

BUN_DLL_PUBLIC int BunIndexBundleInfo(BunIndex const *idx, int32_t bundle_info_id, char const **name,
                                      uint32_t *uncompressed_size) {
    if (!idx || bundle_info_id < 0 || bundle_info_id >= idx->bundle_infos_.size()) {
        return -1;
    }
    auto &bi = idx->bundle_infos_[bundle_info_id];
    *name = bi.name_.c_str();
    *uncompressed_size = bi.uncompressed_size_;
    return 0;
}

BUN_DLL_PUBLIC int BunIndexFileInfo(BunIndex const *idx, int32_t file_info_id, uint64_t *path_hash,
                                    uint32_t *bundle_index, uint32_t *file_offset, uint32_t *file_size) {
    if (!idx || file_info_id < 0 || file_info_id >= idx->file_infos_.size()) {
        return -1;
    }
    auto &fi = idx->file_infos_[file_info_id];
    *path_hash = fi.path_hash_;
    *bundle_index = fi.bundle_index_;
    *file_offset = fi.file_offset_;
    *file_size = fi.file_size_;
    return 0;
}

BUN_DLL_PUBLIC int BunIndexPathRepInfo(BunIndex const *idx, int32_t path_rep_id, uint64_t *hash, uint32_t *offset,
                                       uint32_t *size, uint32_t *recursive_size) {
    if (!idx || path_rep_id < 0 || path_rep_id >= idx->path_rep_infos_.size()) {
        return -1;
    }
    auto si = idx->path_rep_infos_[path_rep_id];
    *hash = si.hash;
    *offset = si.offset;
    *size = si.size;
    *recursive_size = si.recursive_size;
    return 0;
}

BUN_DLL_PUBLIC BunMem BunIndexPathRepContents(BunIndex const *idx) {
    if (!idx) {
        return nullptr;
    }
    return idx->inner_mem_;
}

BUN_DLL_PUBLIC int BunIndexPathRepLowercase(BunIndex const *idx) {
    if (!idx) {
        return 0;
    }
    return idx->hash_algorithm_ == HashAlgorithm::MurmurHash2A_3_21_2;
}

BUN_DLL_PUBLIC int32_t BunIndexBundleCount(BunIndex *idx) {
    if (!idx) {
        return -1;
    }
    return static_cast<int32_t>(idx->bundle_infos_.size());
}

BUN_DLL_PUBLIC int32_t BunIndexBundleIdByName(BunIndex *idx, char const *name) {
    if (!idx) {
        return -1;
    }
    for (size_t i = 0; i < idx->bundle_infos_.size(); ++i) {
        if (idx->bundle_infos_[i].name_ == name) {
            return static_cast<int32_t>(i);
        }
    }
    return -1;
}

BUN_DLL_PUBLIC int32_t BunIndexBundleFileCount(BunIndex *idx, int32_t bundle_id) {
    if (!idx || bundle_id < 0 || bundle_id >= idx->bundle_infos_.size()) {
        return -1;
    }
    auto count = std::count_if(idx->file_infos_.begin(), idx->file_infos_.end(),
                               [&](file_info const &fi) { return fi.bundle_index_ == bundle_id; });
    return static_cast<uint32_t>(count);
}

BUN_DLL_PUBLIC BunMem BunIndexBundleName(BunIndex *idx, int32_t bundle_id) {
    if (!idx || bundle_id < 0 || bundle_id >= idx->bundle_infos_.size()) {
        return nullptr;
    }
    auto &bi = idx->bundle_infos_[bundle_id];
    auto &name = bi.name_;
    BunMem ret = BunMemAlloc(name.size() + 1);
    memcpy(ret, name.c_str(), name.size() + 1);
    return ret;
}

static file_info const *find_file_in_index(BunIndex *idx, int32_t bundle_id, int32_t file_id) {
    if (!idx || bundle_id < 0 || bundle_id >= idx->bundle_infos_.size()) {
        return nullptr;
    }
    if (file_id < 0) {
        return nullptr;
    }
    size_t bundle_matches = 0;
    for (auto &fi : idx->file_infos_) {
        if (fi.bundle_index_ == bundle_id) {
            if (bundle_matches == file_id) {
                return &fi;
            }
            ++bundle_matches;
        }
    }
    return nullptr;
}

int32_t BunIndexBundleFileOffset(BunIndex *idx, int32_t bundle_id, int32_t file_id) {
    if (auto *fi = find_file_in_index(idx, bundle_id, file_id)) {
        return fi->file_offset_;
    }
    return -1;
}

int32_t BunIndexBundleFileSize(BunIndex *idx, int32_t bundle_id, int32_t file_id) {
    if (auto *fi = find_file_in_index(idx, bundle_id, file_id)) {
        return fi->file_size_;
    }
    return -1;
}

using BunMemHeader = int64_t;

BunMem BunMemAlloc(size_t size) {
    BunMemHeader h = size;
    uint8_t *p = new uint8_t[sizeof(BunMemHeader) + size];
    memcpy(p, &h, sizeof(BunMemHeader));
    return p + sizeof(BunMemHeader);
}

int64_t BunMemSize(BunMem mem) {
    if (!mem) {
        return -1;
    }
    BunMemHeader h;
    memcpy(&h, mem - sizeof(BunMemHeader), sizeof(BunMemHeader));
    return h;
}

void BunMemShrink(BunMem mem, int64_t new_size) {
    if (mem) {
        uint8_t *header_ptr = mem - sizeof(BunMemHeader);
        size_t const header_size = sizeof(BunMemHeader);
        BunMemHeader h;
        memcpy(&h, header_ptr, header_size);
        if (h >= new_size) {
            h = new_size;
            memcpy(header_ptr, &h, header_size);
        }
    }
}

void BunMemFree(BunMem mem) {
    if (!mem) {
        return;
    }
    uint8_t *p = mem - sizeof(BunMemHeader);
    delete[] p;
}

#ifdef _WIN32
uint8_t *ro_clone(uint8_t const *src_data, size_t src_size) {
    auto *mem = (uint8_t *)malloc(src_size);
    memcpy(mem, src_data, src_size);
    return mem;
}

void ro_free(uint8_t *s, size_t src_size) { free(s); }
#else
uint8_t *ro_clone(uint8_t const *src_data, size_t src_size) {
    auto page_size = sysconf(_SC_PAGESIZE);
    auto pages = (src_size + page_size - 1) / page_size;
    auto rounded_src_size = (pages + 1) * page_size;
    auto *s = (uint8_t *)aligned_alloc(page_size, rounded_src_size);
    memcpy(s, src_data, src_size);
    mprotect(s + pages * page_size, page_size, PROT_READ);
    return s;
}

void ro_free(uint8_t *s, size_t src_size) {
    auto page_size = sysconf(_SC_PAGESIZE);
    auto pages = (src_size + page_size - 1) / page_size;
    auto rounded_src_size = (pages + 1) * page_size;
    mprotect(s + pages * page_size, page_size, PROT_READ | PROT_WRITE);
    free(s);
}
#endif

int BunDecompressBlock(Bun *bun, uint8_t const *src_data, size_t src_size, uint8_t *dst_data, size_t dst_size) {
    auto *s = ro_clone(src_data, src_size);
    int res = bun->decompress_fun_(s, (int)src_size, dst_data, (int)dst_size, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    ro_free(s, src_size);
    return res;
}

BunMem BunDecompressBlockAlloc(Bun *bun, uint8_t const *src_data, size_t src_size, size_t dst_size) {
    BunMem mem = BunMemAlloc(dst_size + SAFE_SPACE);
    for (size_t i = 0; i < SAFE_SPACE; ++i) {
        mem[dst_size + i] = 0xCD;
    }
    auto *s = ro_clone(src_data, src_size);
    int res = bun->decompress_fun_(s, (int)src_size, mem, (int)dst_size, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    ro_free(s, src_size);
    if (res != dst_size) {
        BunMemFree(mem);
        return nullptr;
    }
    for (size_t i = 0; i < SAFE_SPACE; ++i) {
        if (mem[dst_size + i] != 0xCD) {
            // fprintf(stderr, "Decompress function clobbered safe space at byte %zu\n", i);
            // fflush(stderr);
        }
    }
    BunMemShrink(mem, dst_size);
    return mem;
}

struct bundle_fixed_header {
    uint32_t uncompressed_size;
    uint32_t total_payload_size;
    uint32_t head_payload_size;
    enum encoding_schemes { Kraken_6 = 8, Mermaid_A = 9, Leviathan_C = 13 };
    uint32_t first_file_encode;
    uint32_t unk10;
    uint64_t uncompressed_size2;
    uint64_t total_payload_size2;
    uint32_t block_count;
    uint32_t unk28[5];
};

int64_t BunDecompressBundle(Bun *bun, uint8_t const *src_data, size_t src_size, uint8_t *dst_data, size_t dst_size) {
    reader r = {src_data, src_size};

    bundle_fixed_header fix_h;
    if (!r.read(fix_h.uncompressed_size) || !r.read(fix_h.total_payload_size) || !r.read(fix_h.head_payload_size) ||
        !r.read(fix_h.first_file_encode) || !r.read(fix_h.unk10) || !r.read(fix_h.uncompressed_size2) ||
        !r.read(fix_h.total_payload_size2) || !r.read(fix_h.block_count) || !r.read(fix_h.unk28)) {
        return -1;
    }

    if (dst_size < fix_h.uncompressed_size2) {
        // If an empty buffer is supplied, return the amount the caller needs to allocate.
        return fix_h.uncompressed_size2;
    }

    std::vector<uint32_t> entry_sizes(fix_h.block_count);
    if (!r.read(entry_sizes)) {
        return -1;
    }

    if (r.n_ < fix_h.total_payload_size2) {
        return -1;
    }

    uint8_t const *p = r.p_;
    size_t n = r.n_;
    uint8_t *out_p = dst_data;
    size_t out_cur = 0;
    for (size_t i = 0; i < entry_sizes.size(); ++i) {
        size_t amount_to_write = (std::min<size_t>)(fix_h.uncompressed_size2 - out_cur, fix_h.unk28[0]);
        int64_t amount_written{};
        if (out_cur + amount_to_write + SAFE_SPACE < dst_size)
            amount_written = BunDecompressBlock(bun, p, entry_sizes[i], out_p + out_cur, amount_to_write);
        else {
            auto mem = BunDecompressBlockAlloc(bun, p, entry_sizes[i], amount_to_write);
            amount_written = mem ? amount_to_write : 0;
            memcpy(out_p + out_cur, mem, amount_written);
            BunMemFree(mem);
        }
        p += entry_sizes[i];
        n -= entry_sizes[i];
        out_cur += amount_to_write;
        if (amount_written != amount_to_write) {
            return -1;
        }
    }

    return out_cur;
}

BunMem BunDecompressBundleAlloc(Bun *bun, uint8_t const *src_data, size_t src_size) {
    int64_t dst_size = BunDecompressBundle(bun, src_data, src_size, nullptr, 0);
    BunMem dst_mem = BunMemAlloc(dst_size + SAFE_SPACE);
    if (dst_size != BunDecompressBundle(bun, src_data, src_size, dst_mem, dst_size)) {
        BunMemFree(dst_mem);
        return nullptr;
    }
    BunMemShrink(dst_mem, dst_size);
    return dst_mem;
}
