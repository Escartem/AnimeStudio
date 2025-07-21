#pragma once

#include "acl/version.h"
#include "acl/core/compressed_tracks.h"
#include "acl/core/compressed_tracks_version.h"
#include "acl/core/interpolation_utils.h"
#include "acl/decompression/database/database.h"
#include "acl/core/impl/variable_bit_rates.h"
#include "acl/decompression/impl/decompression_context.hoyo.h"

ACL_IMPL_FILE_PRAGMA_PUSH

namespace acl
{
	ACL_IMPL_VERSION_NAMESPACE_BEGIN

	namespace acl_impl
	{
		template<class decompression_settings_type, class database_settings_type>
		inline bool initialize_v0(persistent_hoyo_decompression_context_v0& context, const compressed_tracks& tracks, const database_context<database_settings_type>* database)
		{
			ACL_ASSERT(tracks.get_algorithm_type() == algorithm_type8::uniformly_sampled, "Invalid algorithm type [" ACL_ASSERT_STRING_FORMAT_SPECIFIER "], expected [" ACL_ASSERT_STRING_FORMAT_SPECIFIER "]", get_algorithm_name(tracks.get_algorithm_type()), get_algorithm_name(algorithm_type8::uniformly_sampled));
			ACL_ASSERT(tracks.get_version() == compressed_tracks_version16::vHoYo, "Invalid compressed tracks version for HoYo decompression (!= %d)", (uint16_t)compressed_tracks_version16::vHoYo);

			const tracks_header& header = get_tracks_header(tracks);
			const hoyo_tracks_header& hoyo_header = get_hoyo_tracks_header(tracks);

			// Context is always the first member and versions should always match
			const database_context_v0* db = bit_cast<const database_context_v0*>(database);

			if (database != nullptr && tracks.get_version() == compressed_tracks_version16::vHoYo) {
				context.db = db;
				context.db_hash = db->db_hash;
				context.has_database = true;
			}

			context.tracks = &tracks;
			context.tracks_hash = tracks.get_hash();
			context.sample_time = -1;

			if (decompression_settings_type::is_wrapping_supported())
			{
				context.duration = tracks.get_finite_duration();
				context.looping_policy = static_cast<uint8_t>(tracks.get_looping_policy());
			}
			else
			{
				context.duration = tracks.get_finite_duration(sample_looping_policy::clamp);
				context.looping_policy = static_cast<uint8_t>(sample_looping_policy::clamp);
			}

			return true;
		}

		template<class decompression_settings_type, class database_settings_type>
		inline bool relocated_v0(persistent_hoyo_decompression_context_v0& context, const compressed_tracks& tracks, const database_context<database_settings_type>* database)
		{
			if (context.tracks_hash != tracks.get_hash())
				return false;	// Hash is different, this instance did not relocate, it is different

			// Context is always the first member and versions should always match
			const database_context_v0* db = bit_cast<const database_context_v0*>(database);
			const uint32_t db_hash = db != nullptr ? db->db_hash : 0;

			if (context.db_hash != db_hash)
				return false;	// Hash is different, this instance did not relocate, it is different

			// The instances are identical and might have relocated, update our metadata
			context.tracks = &tracks;
			context.db = db;

			// Reset the sample time to force seek() to be called again.
			// The context otherwise contains pointers within the tracks and database instances
			// that are populated during seek.
			context.sample_time = -1.0F;

			return true;
		}

		inline bool is_bound_to_v0(const persistent_hoyo_decompression_context_v0& context, const compressed_tracks& tracks)
		{
			if (context.tracks != &tracks)
				return false;	// Different pointer, no guarantees

			if (context.tracks_hash != tracks.get_hash())
				return false;	// Different hash

			// Must be bound to it!
			return true;
		}

		inline bool is_bound_to_v0(const persistent_hoyo_decompression_context_v0& context, const compressed_database& database)
		{
			if (context.db == nullptr)
				return false;	// Not bound to any database

			if (context.db->db != &database)
				return false;	// Different pointer, no guarantees

			if (context.db_hash != database.get_hash())
				return false;	// Different hash

			// Must be bound to it!
			return true;
		}

		template<class decompression_settings_type>
		inline void set_looping_policy_v0(persistent_hoyo_decompression_context_v0& context, sample_looping_policy policy)
		{
			if (!decompression_settings_type::is_wrapping_supported())
				return;	// Only clamping is supported

			const compressed_tracks* tracks = context.tracks;

			if (policy == sample_looping_policy::as_compressed)
				policy = tracks->get_looping_policy();

			const sample_looping_policy current_policy = static_cast<sample_looping_policy>(context.looping_policy);
			if (current_policy != policy)
			{
				// Policy changed
				context.duration = tracks->get_finite_duration(policy);
				context.looping_policy = static_cast<uint8_t>(policy);
			}
		}

		template<class decompression_settings_type>
		inline void seek_v0(persistent_hoyo_decompression_context_v0& context, float sample_time, sample_rounding_policy rounding_policy)
		{
			ACL_ASSERT(false, "scalar seek_v0 not implemented");
		}

		template<class decompression_settings_type, class track_writer_type>
		inline void decompress_tracks_v0(const persistent_hoyo_decompression_context_v0& context, track_writer_type& writer)
		{
			ACL_ASSERT(false, "scalar decompress_tracks_v0 not implemented");
		}

		template<class decompression_settings_type, class track_writer_type>
		inline void decompress_track_v0(const persistent_hoyo_decompression_context_v0& context, uint32_t track_index, track_writer_type& writer)
		{
			ACL_ASSERT(false, "scalar decompress_track_v0 not implemented");
		}
	}

	ACL_IMPL_VERSION_NAMESPACE_END
}

ACL_IMPL_FILE_PRAGMA_POP
