#include "fnv.h"

static uint64_t const FNV1_OFFSET_BASIS_64 = 0xcbf29ce484222325ull;
static uint64_t const FNV1_PRIME_64 = 0x100000001b3;

uint64_t fnv1_64(void const* data, size_t n) {
	uint64_t hash = FNV1_OFFSET_BASIS_64;
	auto* p = reinterpret_cast<uint8_t const*>(data), * end = p + n;
	while (p != end) {
		hash = hash * FNV1_PRIME_64;
		hash = hash ^ *p;
		++p;
	}
	return hash;
}

uint64_t fnv1a_64(void const* data, size_t n) {
	uint64_t hash = FNV1_OFFSET_BASIS_64;
	auto* p = reinterpret_cast<uint8_t const*>(data), * end = p + n;
	while (p != end) {
		hash = hash ^ *p;
		hash = hash * FNV1_PRIME_64;
		++p;
	}
	return hash;
}

FNV1A64::FNV1A64() : hash_(FNV1_OFFSET_BASIS_64) {}

uint64_t FNV1A64::feed(void const* data, size_t n) {
	auto* p = reinterpret_cast<uint8_t const*>(data), * end = p + n;
	while (p != end) {
		hash_ = hash_ ^ *p;
		hash_ = hash_ * FNV1_PRIME_64;
		++p;
	}
	return hash_;
}