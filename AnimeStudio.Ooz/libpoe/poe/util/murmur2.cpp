#include <poe/util/murmur2.hpp>
#include "murmur2.hpp"

namespace poe::util {
murmur2_32_digest oneshot_murmur2_32(std::byte const *data, size_t size) {
    uint32_t const len = static_cast<uint32_t>(size);
    uint32_t const m = 0x5bd1'e995u;
    uint32_t const r = 24u;

    uint32_t h = len;

    uint32_t const rem = len % 4;
    uint32_t lim = (len - rem);

    for (size_t i = 0; i < lim; i += 4) {
        uint32_t const d0 = static_cast<uint32_t>(data[i]);
        uint32_t const d1 = static_cast<uint32_t>(data[i + 1]) << 8;
        uint32_t const d2 = static_cast<uint32_t>(data[i + 2]) << 16;
        uint32_t const d3 = static_cast<uint32_t>(data[i + 3]) << 24;
        uint32_t k = d0 | d1 | d2 | d3;
        k *= m;
        k ^= k >> r;
        k *= m;
        h *= m;
        h ^= k;
    }

    if (rem == 3) {
        h ^= static_cast<uint32_t>(data[lim + 2]) << 16;
    }
    if (rem >= 2) {
        h ^= static_cast<uint32_t>(data[lim + 1]) << 8;
    }
    if (rem >= 1) {
        h ^= static_cast<uint32_t>(data[lim]);
        h *= m;
    }

    h ^= h >> 13;
    h *= m;
    h ^= h >> 15;

    return h;
}
} // namespace poe::util