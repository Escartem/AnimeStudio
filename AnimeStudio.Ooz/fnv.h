#pragma once

#include <cstddef>
#include <cstdint>

uint64_t fnv1_64(void const* data, size_t n);
uint64_t fnv1a_64(void const* data, size_t n);

struct FNV1A64 {
    FNV1A64();
    
    uint64_t feed(void const* data, size_t n);

    uint64_t hash_;
};