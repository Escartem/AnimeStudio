#pragma once

#include <filesystem>
#include <optional>
#include <vector>

namespace poe::util {
enum class install_kind {
    Steam,
    Standalone,
    Bundled,
    Manual,
};

std::optional<std::filesystem::path> own_install_dir();
std::vector<std::pair<install_kind, std::filesystem::path>> install_locations();
} // namespace poe::util