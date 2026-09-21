#pragma once

#include <cstdint>
#include <cstddef>

namespace poe::util {
using murmur2_32_digest = uint32_t;

murmur2_32_digest oneshot_murmur2_32(std::byte const *data, size_t size);
} // namespace poe::util