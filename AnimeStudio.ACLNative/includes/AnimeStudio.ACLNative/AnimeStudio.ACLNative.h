#pragma once
#ifdef ANIMESTUDIO.ACLNATIVE_EXPORTS
#define ACLNATIVE_API __declspec(dllexport)
#else
#define ACLNATIVE_API __declspec(dllexport)
#endif

#define RTM_NO_DEPRECATION
#define ACL_ON_ASSERT_ABORT
#define FMT_UNICODE 0

#include "acl/fwd.h"

struct DecompressedClip {
	float* Values;
	int32_t ValuesCount;
	float* Times;
	int32_t TimesCount;

	void Reset();
	void Dispose();
};

// TODO: Make this work across x86 and x64 configurations.
// static_assert(sizeof(DecompressedClip) == 32, "DecompressedClip has incorrect size");

extern "C" ACLNATIVE_API void DecompressTracks(const acl::compressed_tracks*, DecompressedClip*);
extern "C" ACLNATIVE_API void DecompressTracks(const acl::compressed_tracks*, const acl::compressed_database*, DecompressedClip*);
extern "C" ACLNATIVE_API void DecompressTracks(const acl::compressed_tracks*, const acl::compressed_database*, const uint8_t*, const uint8_t*, DecompressedClip*);
extern "C" ACLNATIVE_API void Dispose(DecompressedClip*);