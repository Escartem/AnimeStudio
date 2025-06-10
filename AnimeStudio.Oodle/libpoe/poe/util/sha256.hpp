#pragma once

#include <array>
#include <memory>
#include <string>

namespace poe::util {
using sha256_digest = std::array<uint8_t, 32>;

std::string digest_to_string(sha256_digest const &digest);

struct sha256 {
    virtual ~sha256() {}

    virtual void reset() = 0;
    virtual void feed(uint8_t const *data, size_t size) = 0;
    virtual sha256_digest finish() = 0;
};

sha256_digest oneshot_sha256(uint8_t const *data, size_t size);
std::unique_ptr<sha256> incremental_sha256();
} // namespace poe::util