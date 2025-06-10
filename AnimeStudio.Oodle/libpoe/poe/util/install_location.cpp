#include <poe/util/install_location.hpp>

#include <fstream>
#include <optional>
#include <regex>
#include <string>

#ifdef _WIN32
#include <Windows.h>
#endif

namespace poe::util {
namespace {
#ifdef _WIN32
std::optional<std::filesystem::path> get_reg_path(wchar_t const *subkey, wchar_t const *value) {
    std::vector<char> buf;
    while (true) {
        DWORD path_cb = (DWORD)buf.size();
        void *p = buf.empty() ? nullptr : buf.data();
        if (ERROR_SUCCESS == RegGetValueW(HKEY_CURRENT_USER, subkey, value, RRF_RT_REG_SZ, nullptr, p, &path_cb)) {
            if (path_cb <= buf.size()) {
                return std::filesystem::path(reinterpret_cast<char16_t *>(buf.data()));
            }
            buf.resize(path_cb);
        } else {
            return {};
        }
    }
}
#endif

std::optional<std::filesystem::path> standalone_install_dir() {
#if _WIN32
    return get_reg_path(LR"(SOFTWARE\GrindingGearGames\Path of Exile)", L"InstallLocation");
#else
    return {};
#endif
}

std::optional<std::filesystem::path> steam_install_dir() {
#if _WIN32
    auto steam_root = get_reg_path(LR"(SOFTWARE\Valve\Steam)", L"SteamPath");
    if (!steam_root || !exists(*steam_root)) {
        return {};
    }

    auto check_for_poe_dir = [](auto steamapps) -> std::optional<std::filesystem::path> {
        uint32_t const poe_appid = 238960;
        char buf[128];
        sprintf(buf, "appmanifest_%u.acf", poe_appid);
        std::string const manifest_filename = buf;
        auto manifest_path = steamapps / manifest_filename;
        if (exists(manifest_path)) {
            auto poe_path = steamapps / "common/Path of Exile";
            if (exists(poe_path)) {
                return poe_path;
            }
        }
        return {};
    };

    auto steamapps_path = *steam_root / "steamapps";
    if (auto dir = check_for_poe_dir(*steam_root / "steamapps")) {
        return dir;
    }

    auto lib_folders_path = steamapps_path / "libraryfolders.vdf";
    if (std::ifstream lib_folders_file(lib_folders_path); lib_folders_file) {
        enum class parse_step {
            Nothing,
            LibraryFoldersLiteral,
            LibraryFoldersOpenCurly,
            LibraryFoldersCloseCurly,
        };

        parse_step step = parse_step::Nothing;
        std::string line;
        while (std::getline(lib_folders_file, line)) {
            switch (step) {
            case parse_step::Nothing: {
                if (line == R"("LibraryFolders")") {
                    step = parse_step::LibraryFoldersLiteral;
                }
            } break;
            case parse_step::LibraryFoldersLiteral: {
                if (line == "{") {
                    step = parse_step::LibraryFoldersOpenCurly;
                }
            } break;
            case parse_step::LibraryFoldersOpenCurly: {
                if (line == "}") {
                    step = parse_step::LibraryFoldersCloseCurly;
                } else {
                    std::regex rx(R"!!(\s*"(\d+)"\s+"([^"]*)"\s*)!!");
                    std::smatch match;
                    if (regex_match(line, match, rx)) {
                        std::filesystem::path p = match.str(2);
                        if (auto dir = check_for_poe_dir(p / "steamapps")) {
                            return dir;
                        }
                    }
                }
            } break;
            case parse_step::LibraryFoldersCloseCurly: {
                return {};
            } break;
            }
        }
    }
    return {};
#else
    return {};
#endif
}
} // namespace

std::optional<std::filesystem::path> own_install_dir() {
#ifdef _WIN32
    std::vector<wchar_t> buf(1 << 20);
    GetModuleFileNameW(nullptr, buf.data(), static_cast<DWORD>(buf.size()));
    std::filesystem::path p = buf.data();
    if (p.has_parent_path()) {
        return p.parent_path();
    } else {
        return {};
    }
#else
    return {}; // TODO(LV): Implement
#endif
}

std::vector<std::pair<install_kind, std::filesystem::path>> install_locations() {
    std::vector<std::pair<install_kind, std::optional<std::filesystem::path>>> candidate_dirs = {
        {install_kind::Standalone, standalone_install_dir()},
        {
            install_kind::Steam,
            steam_install_dir(),
        },
        {
            install_kind::Bundled,
            own_install_dir(),
        },
    };

    std::vector<std::pair<install_kind, std::filesystem::path>> found_ggpks;
    for (auto [kind, cand] : candidate_dirs) {
        if (cand) {
            auto p = *cand / "Content.ggpk";
            if (exists(p)) {
                found_ggpks.push_back({kind, *cand});
            }
        }
    }
    return found_ggpks;
}
} // namespace poe::util
