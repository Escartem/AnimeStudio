#pragma once

#include <poe/format/ggpk.hpp>
#include "bun.h"

#include <memory>

namespace ggpk = poe::format::ggpk;

struct GgpkVfs;

std::shared_ptr<GgpkVfs> open_ggpk(std::filesystem::path path, bool mmap_data = false);
Vfs* borrow_vfs(std::shared_ptr<GgpkVfs>& vfs);