add_library(libpoe STATIC
    "poe/format/ggpk.cpp"
    "poe/format/ggpk.hpp"
	"poe/util/install_location.cpp"
    "poe/util/install_location.hpp"
    "poe/util/murmur2.cpp"
    "poe/util/murmur2.hpp"
    "poe/util/random_access_file.cpp"
    "poe/util/random_access_file.hpp"
    "poe/util/sha256.cpp"
    "poe/util/sha256.hpp"
    "poe/util/utf.cpp"
    "poe/util/utf.hpp"
)

target_include_directories(libpoe PUBLIC ${CMAKE_CURRENT_SOURCE_DIR} ${CMAKE_CURRENT_SOURCE_DIR}/mio/single_include)