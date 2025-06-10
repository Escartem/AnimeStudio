#pragma once

#include <poe/util/murmur2.hpp>
#include <poe/util/random_access_file.hpp>
#include <poe/util/sha256.hpp>

#include <mio/mio.hpp>

#include <array>
#include <map>
#include <string>
#include <vector>

namespace poe::format::ggpk {
using chunk_tag = std::array<std::byte, 4>;

chunk_tag constexpr make_text_tag(char a, char b, char c, char d) {
    return chunk_tag{
        (std::byte)a,
        (std::byte)b,
        (std::byte)c,
        (std::byte)d,
    };
}

chunk_tag constexpr make_text_tag(char const (&name)[5]) { return make_text_tag(name[0], name[1], name[2], name[3]); }

constexpr chunk_tag FILE_TAG = make_text_tag("FILE");
constexpr chunk_tag FREE_TAG = make_text_tag("FREE");
constexpr chunk_tag GGPK_TAG = make_text_tag("GGPK");
constexpr chunk_tag PDIR_TAG = make_text_tag("PDIR");

struct parsed_entry {
    virtual ~parsed_entry() {}

    uint64_t offset_{};
    std::u16string name_;
    poe::util::murmur2_32_digest name_hash_;
    poe::util::sha256_digest stored_digest_;

    struct parsed_directory *parent_{};
};

struct parsed_file : parsed_entry {
    uint64_t data_offset_;
    uint64_t data_size_;
};

struct parsed_directory : parsed_entry {
    std::vector<std::unique_ptr<parsed_entry>> entries_;
};

struct parsed_ggpk {
    uint32_t version_;
    poe::util::mmap_source mapping_;
    std::unique_ptr<parsed_entry> root_entry_;
    parsed_directory* root_;
    uint64_t free_offset_;
};

std::unique_ptr<parsed_ggpk> index_ggpk(std::filesystem::path pack_path);

} // namespace poe::format::ggpk