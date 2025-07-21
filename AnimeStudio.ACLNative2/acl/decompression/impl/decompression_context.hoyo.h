#pragma once

#include "acl/version.h"
#include "acl/core/compressed_tracks.h"
#include "acl/core/compressed_tracks_version.h"
#include "acl/core/interpolation_utils.h"
#include "acl/core/track_formats.h"
#include "acl/decompression/database/impl/database_context.h"

namespace acl
{
	ACL_IMPL_VERSION_NAMESPACE_BEGIN

	namespace acl_impl
	{
		struct persistent_hoyo_decompression_context_v0
		{
			const compressed_tracks* tracks = nullptr;
			const database_context_v0* db = nullptr;

			uint32_t tracks_hash = 0;
			uint32_t db_hash = 0;

			float duration = 0.0f;
			float interpolation_alpha = 0.0F;
			float sample_time = 0.0F;

			uint32_t key_frame_bit_offsets[2] = { 0 };

			uint8_t looping_policy = 0;
			uint8_t rounding_policy = 0;
			uint8_t uses_single_segment = 0;
			bool has_database = 0;

			const compressed_tracks* get_compressed_tracks() const { return tracks; }
			compressed_tracks_version16 get_version() const { return tracks->get_version(); }
			sample_looping_policy get_looping_policy() const { return static_cast<sample_looping_policy>(looping_policy); }
			sample_rounding_policy get_rounding_policy() const { return static_cast<sample_rounding_policy>(rounding_policy); }
			bool is_initialized() const { return tracks != nullptr; }
			void reset()
			{
				// Just reset the tracks pointer, this will mark us as no longer initialized indicating everything is stale
				tracks = nullptr;
			}
		};
	}
}