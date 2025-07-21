#ifdef ANIMESTUDIO.ACLNATIVE2_EXPORTS
#define ACLNATIVE_API __declspec(dllexport)
#else
#define ACLNATIVE_API __declspec(dllexport)
#endif

#define RTM_NO_DEPRECATION
#define ACL_ON_ASSERT_ABORT
#define FMT_UNICODE 0

#include "acl/core/ansi_allocator.h"
#include "acl/core/compressed_database.h"
#include "acl/core/compressed_tracks.h"
#include "acl/decompression/database/database.h"
#include "acl/decompression/database/database_settings.h"
#include "acl/decompression/database/database_streamer.h"
#include "acl/decompression/database/null_database_streamer.h"
#include "acl/decompression/decompress.h"
#include "acl/decompression/decompression_settings.h"
#include "spdlog/spdlog.h"

struct DecompressedClip {
	float* Values;
	int32_t ValuesCount;
	uint8_t padding0[4];
	float* Times;
	int32_t TimesCount;

	void Reset() {
		Values = nullptr;
		ValuesCount = 0;
		Times = nullptr;
		TimesCount = 0;
	}

	void Dispose() {
		delete Values;
		delete Times;
		Reset();
	}
};

static_assert(sizeof(DecompressedClip) == 32, "DecompressedClip has incorrect size");

extern "C" ACLNATIVE_API void DecompressTracksZZZ(const acl::compressed_tracks* transform_tracks, const acl::compressed_tracks* scalar_tracks, const acl::compressed_database* database, const uint8_t* bulk_data, DecompressedClip* decompressedClip);
extern "C" ACLNATIVE_API void Dispose(DecompressedClip* decompressedClip);