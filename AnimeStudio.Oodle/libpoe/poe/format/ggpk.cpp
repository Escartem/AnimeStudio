#include <poe/format/ggpk.hpp>

#include <poe/util/murmur2.hpp>
#include <poe/util/random_access_file.hpp>
#include <poe/util/utf.hpp>

#include <variant>

namespace poe::format::ggpk {

struct raw_entry {
    uint32_t rec_len_;
    chunk_tag tag_;
    uint64_t offset_;
    std::u16string name_;
    poe::util::murmur2_32_digest name_hash_;
    poe::util::sha256_digest stored_digest_;

    struct directory {
        std::vector<uint32_t> hashes_;
        std::vector<uint64_t> offsets_;
    };

    struct file {
        uint64_t data_offset_;
        uint64_t data_size_;
    };

    using info = std::variant<directory, file>;
    info info_;
};

using entry_lookup = std::map<uint64_t, raw_entry>;

struct index_builder {
    index_builder(poe::util::mmap_source const& source, entry_lookup const& entries)
        : source_(source), entries_(entries)
    {}

    poe::util::mmap_source const &source_;
    entry_lookup const &entries_;

    std::unique_ptr<parsed_entry> index_entry(uint64_t offset);
    std::unique_ptr<parsed_file> index_file(raw_entry const &e);
    std::unique_ptr<parsed_directory> index_directory(raw_entry const &e);
};

std::unique_ptr<parsed_file> index_builder::index_file(raw_entry const &e) {
    auto info = std::get_if<raw_entry::file>(&e.info_);
    if (!info) {
        return {};
    }

    auto ret = std::make_unique<parsed_file>();
    ret->offset_ = e.offset_;
    ret->name_ = e.name_;
    ret->stored_digest_ = e.stored_digest_;
    ret->data_offset_ = info->data_offset_;
    ret->data_size_ = info->data_size_;
    return ret;
}

std::unique_ptr<parsed_directory> index_builder::index_directory(raw_entry const &e) {
    auto info = std::get_if<raw_entry::directory>(&e.info_);
    if (!info) {
        return {};
    }

    auto dir_obj = std::make_unique<parsed_directory>();
    dir_obj->offset_ = e.offset_;
    dir_obj->name_ = e.name_;
    dir_obj->stored_digest_ = e.stored_digest_;

    {
        size_t child_count = info->hashes_.size();
        for (size_t i = 0; i < child_count; ++i) {
            auto child_obj = index_entry(info->offsets_[i]);
            if (!child_obj) {
                return {};
            }
            child_obj->name_hash_ = info->hashes_[i];
            child_obj->parent_ = dir_obj.get();
            dir_obj->entries_.push_back(std::move(child_obj));
        }
    }

    return dir_obj;
}

std::unique_ptr<parsed_entry> index_builder::index_entry(uint64_t offset) {
    auto raw = entries_.find(offset);
    if (raw == entries_.end()) {
        return {};
    }
    auto &e = raw->second;

    if (e.tag_ == FILE_TAG) {
        return index_file(e);
    } else if (e.tag_ == PDIR_TAG) {
        return index_directory(e);
    }
    return {};
}

std::unique_ptr<parsed_ggpk> index_ggpk(std::filesystem::path pack_path) {
    std::error_code ec;
    auto source = mio::make_mmap<poe::util::mmap_source>(pack_path.string(), 0, mio::map_entire_file, ec);
    if (ec) {
        return {};
    }
    std::map<uint64_t, raw_entry> entries;

    {
        // Sweep file linearly for FILE and PDIR entries, jumping over FREE and GGPK chunks
        uint64_t offset = 0;
        uint64_t end = source.size();
        while (offset < end) {
            if (end - offset < 8) {
                return {};
            }
            uint32_t rec_len{};
            ggpk::chunk_tag tag{};
            memcpy(&rec_len, source.data() + offset, 4);
            if (offset + rec_len > end) {
                return {};
            }
            memcpy(&tag, source.data() + offset + 4, 4);

            if (tag == ggpk::FILE_TAG || tag == ggpk::PDIR_TAG) {
                bool is_dir = tag == ggpk::PDIR_TAG;
                auto o = offset + 8;

                uint32_t name_len;
                memcpy(&name_len, source.data() + o, 4);
                o += 4;

                uint32_t child_count{};
                if (is_dir) {
                    memcpy(&child_count, source.data() + o, 4);
                    o += 4;
                }

                poe::util::sha256_digest digest;
                memcpy(digest.data(), source.data() + o, digest.size());
                o += digest.size();

                if (name_len == 0) {
                    return {};
                }
                std::vector<char16_t> name_buf(name_len);
                memcpy(name_buf.data(), source.data() + o, name_buf.size() * 2);
                o += name_buf.size() * 2;

                std::u16string name(name_buf.data());
                std::u16string name_lower = poe::util::lowercase(name.data());
                auto name_hash = poe::util::oneshot_murmur2_32(reinterpret_cast<std::byte const*>(name_lower.data()),
                    name_lower.size() * 2);

                std::vector<uint32_t> child_hashes(child_count);
                std::vector<uint64_t> child_offsets(child_count);
                if (is_dir) {
                    for (size_t i = 0; i < child_count; ++i) {
                        memcpy(&child_hashes[i], source.data() + o, 4);
                        o += 4;
                        memcpy(&child_offsets[i], source.data() + o, 8);
                        o += 8;
                    }
                }

                raw_entry::info info;
                if (is_dir) {
                    raw_entry::directory d;
                    d.hashes_ = child_hashes;
                    d.offsets_ = child_offsets;
                    info = d;
                }
                else {
                    raw_entry::file f;
                    f.data_offset_ = o;
                    f.data_size_ = rec_len - (o - offset);
                    info = f;
                }
                raw_entry entry;
                entry.rec_len_ = rec_len;
                entry.tag_ = tag;
                entry.offset_ = offset;
                entry.name_ = std::move(name);
                entry.name_hash_ = name_hash;
                entry.stored_digest_ = digest;
                entry.info_ = std::move(info);

                entries[offset] = std::move(entry);
            } else if (tag != ggpk::FREE_TAG && tag != ggpk::GGPK_TAG) {
                return {};
            }
            offset += rec_len;
        }
    }

    auto reader = poe::util::make_stream_reader(source, 0);

    struct ggpk_header {
        uint32_t rec_len;
        poe::format::ggpk::chunk_tag tag;
        uint32_t version;
        std::array<uint64_t, 2> children;
    } ggpk_h;

    if (!reader->read_one(ggpk_h.rec_len) || !reader->read_one(ggpk_h.tag) ||
        ggpk_h.tag != poe::format::ggpk::GGPK_TAG || !reader->read_one(ggpk_h.version) ||
        (uint64_t)ggpk_h.children.size() * 12 > source.size()) {
        return {};
    }

    if (!reader->read_many(ggpk_h.children.data(), ggpk_h.children.size())) {
        return {};
    }

    bool free_encountered = false;
    bool pdir_encountered = false;
    auto ret = std::make_unique<parsed_ggpk>();
    for (auto child : ggpk_h.children) {
        if (child > source.size()) {
            return {};
        }
        if (child == 0) {
            free_encountered = true; // assume zero offset means no FREE chunk
            continue;
        }
        uint32_t rec_len;
        poe::format::ggpk::chunk_tag tag;
        auto reader = poe::util::make_stream_reader(source, child);
        if (!reader->read_one(rec_len) || !reader->read_one(tag)) {
            return {};
        }
        if (tag == poe::format::ggpk::FREE_TAG) {
            if (free_encountered) {
                return {};
            }
            ret->free_offset_ = child;
            free_encountered = true;
        } else if (tag == poe::format::ggpk::PDIR_TAG) {
            if (pdir_encountered) {
                return {};
            }

            auto indexer = index_builder{source, entries};
            auto root_entry = indexer.index_entry(child);
            auto root_dir = dynamic_cast<parsed_directory*>(root_entry.get());
            if (!root_dir) {
                return {};
            }
            ret->root_entry_ = std::move(root_entry);
            ret->root_ = root_dir;
            pdir_encountered = true;
        } else {
            return {};
        }
    }
    if (!free_encountered || !pdir_encountered) {
        return {};
    }

    ret->version_ = ggpk_h.version;
    ret->mapping_ = std::move(source);
    return ret;
}

} // namespace poe::format::ggpk