#include "ggpk_vfs.h"

#include <poe/util/utf.hpp>
#include <filesystem>
#include <fstream>

struct GgpkVfs {
	Vfs vfs;
	std::filesystem::path path;
	std::unique_ptr<poe::format::ggpk::parsed_ggpk> pack;
};

std::shared_ptr<GgpkVfs> open_ggpk(std::filesystem::path ggpk_path, bool mmap_data) {
	auto ret = std::make_shared<GgpkVfs>();
	ret->path = ggpk_path;
	ret->pack = poe::format::ggpk::index_ggpk(ggpk_path);
	if (!ret->pack) {
		return {};
	}

	ret->vfs.open = [](Vfs* vfs, char const* c_path) -> VfsFile* {
		auto* gvfs = reinterpret_cast<GgpkVfs*>(vfs);
		ggpk::parsed_directory const* dir = gvfs->pack->root_;
		std::u16string path = poe::util::lowercase(poe::util::to_u16string(c_path));
		std::u16string_view tail(path);
		while (!tail.empty() && dir) {
			size_t delim = tail.find(u'/');
			if (delim == 0) {
				continue;
			}
			std::u16string_view head = tail.substr(0, delim);
			bool next_found = false;
			for (auto& child : dir->entries_) {
				if (poe::util::lowercase(child->name_) == head) {
					if (delim == std::string_view::npos) {
						return (VfsFile*)dynamic_cast<ggpk::parsed_file const*>(child.get());
					}
					else {
						dir = dynamic_cast<ggpk::parsed_directory const*>(child.get());
						tail = tail.substr(delim + 1);
						next_found = true;
					}
				}
			}
			if (!next_found) {
				return nullptr;
			}
		}
		return nullptr;
	};
	ret->vfs.close = [](Vfs* vfs, VfsFile* file) {};
	ret->vfs.size = [](Vfs*, VfsFile* file) -> int64_t {
		auto* f = reinterpret_cast<ggpk::parsed_file const*>(file);
		return f ? f->data_size_ : -1;
	};

	if (mmap_data) {
		auto const read_via_mmap = [](Vfs* vfs, VfsFile* file, uint8_t* out, int64_t offset, int64_t size) -> int64_t {
			auto* gvfs = reinterpret_cast<GgpkVfs*>(vfs);
			auto* f = reinterpret_cast<ggpk::parsed_file const*>(file);
			if (offset + size > (int64_t)f->data_size_) {
				return -1;
			}
			memcpy(out, gvfs->pack->mapping_.data() + f->data_offset_ + offset, size);
			return size;
		};

		ret->vfs.read = read_via_mmap;
	} else {
		auto const read_via_file = [](Vfs* vfs, VfsFile* file, uint8_t* out, int64_t offset, int64_t size) -> int64_t {
			auto* gvfs = reinterpret_cast<GgpkVfs*>(vfs);
			auto* f = reinterpret_cast<ggpk::parsed_file const*>(file);
			if (offset + size > (int64_t)f->data_size_) {
				return -1;
			}
			std::ifstream ifs(gvfs->path, std::ios::binary);
			ifs.seekg(f->data_offset_ + offset);
			ifs.read((char *)out, size);
			return size;
		};

		ret->vfs.read = read_via_file;
	}
	return ret;
}

Vfs* borrow_vfs(std::shared_ptr<GgpkVfs>& vfs) {
	return vfs ? &vfs->vfs : nullptr;
}