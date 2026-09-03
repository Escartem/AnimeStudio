#pragma once

#include "spdlog/spdlog.h"
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
			spdlog::info("initialize_v0 called");
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
			spdlog::info("seek_v0 called");
			const compressed_tracks* tracks = context.tracks;
			const tracks_header& header = get_tracks_header(*tracks);
			if (header.num_tracks == 0)
				return;	// Empty track list

			// Clamp for safety, the caller should normally handle this but in practice, it often isn't the case
			if (decompression_settings_type::clamp_sample_time())
				sample_time = rtm::scalar_clamp(sample_time, 0.0F, context.duration);

			if (context.sample_time == sample_time && context.get_rounding_policy() == rounding_policy)
				return;

			const hoyo_tracks_header& hoyo_header = get_hoyo_tracks_header(*tracks);

			context.sample_time = sample_time;

			// If the wrap looping policy isn't supported, use our statically known value
			const sample_looping_policy looping_policy_ = decompression_settings_type::is_wrapping_supported() ? static_cast<sample_looping_policy>(context.looping_policy) : sample_looping_policy::clamp;

			uint32_t key_frame0;
			uint32_t key_frame1;
			find_linear_interpolation_samples_with_sample_rate(header.num_samples, header.sample_rate, sample_time, rounding_policy, looping_policy_, key_frame0, key_frame1, context.interpolation_alpha);

			context.rounding_policy = static_cast<uint8_t>(rounding_policy);

			if (!context.has_database) {
				context.key_frame_bit_offsets[0] = key_frame0 * hoyo_header.num_bits_per_frame;
				context.key_frame_bit_offsets[1] = key_frame1 * hoyo_header.num_bits_per_frame;
				return;
			}

			uint32_t segment_key_frame0;
			uint32_t segment_key_frame1;

			const segment_header* segment_header0;
			const segment_header* segment_header1;

			const uint8_t* db_animated_track_data0 = nullptr;
			const uint8_t* db_animated_track_data1 = nullptr;

			// These two pointers are the same, the compiler should optimize one out, only here for type safety later
			const segment_header* segment_headers = hoyo_header.get_segment_headers();
			const stripped_segment_header_t* segment_tier0_headers = hoyo_header.get_stripped_segment_headers();

			const uint32_t num_segments = hoyo_header.num_segments;

			constexpr bool is_database_supported = is_database_supported_impl<decompression_settings_type>();
			ACL_ASSERT(is_database_supported || !tracks->has_database(), "Cannot have a database when it isn't supported");

			const bool has_database = is_database_supported && tracks->has_database();
			const database_context_v0* db = context.db;

			const bool has_stripped_keyframes = has_database || tracks->has_stripped_keyframes();

			spdlog::info("Decompressing {} segments", num_segments);
			if (num_segments == 1) {
				if (has_stripped_keyframes)
				{
					spdlog::info("Has stripped keyframes");
					const stripped_segment_header_t* segment_tier0_header0 = segment_tier0_headers;

					// This will cache miss
					uint32_t sample_indices0 = segment_tier0_header0->sample_indices;

					// Calculate our clip relative sample index, we'll remap it later relative to the samples we'll use
					const float sample_index = context.interpolation_alpha + float(key_frame0);

					// When we load our sample indices and offsets from the database, there can be another thread writing
					// to those memory locations at the same time (e.g. streaming in/out).
					// To ensure thread safety, we atomically load the offset and sample indices.
					uint64_t medium_importance_tier_metadata0 = 0;
					uint64_t low_importance_tier_metadata0 = 0;

					// Combine all our loaded samples into a single bit set to find which samples we need to interpolate
					if (is_database_supported && db != nullptr)
					{
						// Possible cache miss for the clip header offset
						// Cache miss for the db clip segment headers pointer
						const tracks_database_header* tracks_db_header = hoyo_header.get_database_header();
						const database_runtime_clip_header* db_clip_header = tracks_db_header->get_clip_header(db->clip_segment_headers);
						const database_runtime_segment_header* db_segment_headers = db_clip_header->get_segment_headers();

						// Cache miss for the db segment headers
						const database_runtime_segment_header* db_segment_header0 = db_segment_headers;
						medium_importance_tier_metadata0 = db_segment_header0->tier_metadata[0].load(k_memory_order_relaxed);
						low_importance_tier_metadata0 = db_segment_header0->tier_metadata[1].load(k_memory_order_relaxed);

						sample_indices0 |= uint32_t(medium_importance_tier_metadata0);
						sample_indices0 |= uint32_t(low_importance_tier_metadata0);
					}

					// Find the closest loaded samples
					// Mask all trailing samples to find the first sample by counting trailing zeros
					const uint32_t candidate_indices0 = sample_indices0 & (0xFFFFFFFFU << (31 - key_frame0));
					key_frame0 = 31 - count_trailing_zeros(candidate_indices0);

					// Mask all leading samples to find the second sample by counting leading zeros
					const uint32_t candidate_indices1 = sample_indices0 & (0xFFFFFFFFU >> key_frame1);
					key_frame1 = count_leading_zeros(candidate_indices1);

					// Calculate our new interpolation alpha
					// We used the rounding policy above to snap to the correct key frame earlier but we might need to interpolate now
					// if key frames have been removed
					context.interpolation_alpha = find_linear_interpolation_alpha(sample_index, key_frame0, key_frame1, sample_rounding_policy::none, looping_policy_);

					// Find where our data lives (clip or database tier X)
					sample_indices0 = segment_tier0_header0->sample_indices;
					uint32_t sample_indices1 = sample_indices0;	// Identical

					if (is_database_supported && db != nullptr)
					{
						const uint64_t sample_index0 = uint64_t(1) << (31 - key_frame0);
						const uint64_t sample_index1 = uint64_t(1) << (31 - key_frame1);

						const uint8_t* bulk_data_medium = db->bulk_data[0];		// Might be nullptr if we haven't streamed in yet
						const uint8_t* bulk_data_low = db->bulk_data[1];		// Might be nullptr if we haven't streamed in yet
						if ((medium_importance_tier_metadata0 & sample_index0) != 0)
						{
							sample_indices0 = uint32_t(medium_importance_tier_metadata0);
							db_animated_track_data0 = bulk_data_medium + uint32_t(medium_importance_tier_metadata0 >> 32);
						}
						else if ((low_importance_tier_metadata0 & sample_index0) != 0)
						{
							sample_indices0 = uint32_t(low_importance_tier_metadata0);
							db_animated_track_data0 = bulk_data_low + uint32_t(low_importance_tier_metadata0 >> 32);
						}

						// Only one segment, our metadata is the same for our second key frame
						if ((medium_importance_tier_metadata0 & sample_index1) != 0)
						{
							sample_indices1 = uint32_t(medium_importance_tier_metadata0);
							db_animated_track_data1 = bulk_data_medium + uint32_t(medium_importance_tier_metadata0 >> 32);
						}
						else if ((low_importance_tier_metadata0 & sample_index1) != 0)
						{
							sample_indices1 = uint32_t(low_importance_tier_metadata0);
							db_animated_track_data1 = bulk_data_low + uint32_t(low_importance_tier_metadata0 >> 32);
						}
					}

					// Remap our sample indices within the ones actually stored (e.g. index 3 might be the second frame stored)
					segment_key_frame0 = count_set_bits(and_not(0xFFFFFFFFU >> key_frame0, sample_indices0));
					segment_key_frame1 = count_set_bits(and_not(0xFFFFFFFFU >> key_frame1, sample_indices1));

					// Nasty but safe since they have the same layout
					segment_header0 = static_cast<const segment_header*>(segment_tier0_header0);
					segment_header1 = static_cast<const segment_header*>(segment_tier0_header0);
				}
				else {
					segment_header0 = segment_headers;
					segment_header1 = segment_headers;

					segment_key_frame0 = key_frame0;
					segment_key_frame1 = key_frame1;
				}
			}
			else {
				const uint32_t* segment_start_indices = hoyo_header.get_segment_start_indices();

				// See segment_streams(..) for implementation details. This implementation is directly tied to it.
				const uint32_t approx_num_samples_per_segment = header.num_samples / num_segments;	// TODO: Store in header?
				const uint32_t approx_segment_index = key_frame0 / approx_num_samples_per_segment;

				uint32_t segment_index0 = 0;
				uint32_t segment_index1 = 0;

				// Our approximate segment guess is just that, a guess. The actual segments we need could be just before or after.
				// We start looking one segment earlier and up to 2 after. If we have too few segments after, we will hit the
				// sentinel value of 0xFFFFFFFF and exit the loop.
				// TODO: Can we do this with SIMD? Load all 4 values, set key_frame0, compare, move mask, count leading zeroes
				const uint32_t start_segment_index = approx_segment_index > 0 ? (approx_segment_index - 1) : 0;
				const uint32_t end_segment_index = start_segment_index + 4;

				for (uint32_t segment_index = start_segment_index; segment_index < end_segment_index; ++segment_index)
				{
					if (key_frame0 < segment_start_indices[segment_index])
					{
						// We went too far, use previous segment
						ACL_ASSERT(segment_index > 0, "Invalid segment index: %u", segment_index);
						segment_index0 = segment_index - 1;

						// If wrapping is enabled and we wrapped, use the first segment
						if (decompression_settings_type::is_wrapping_supported() && key_frame1 == 0)
							segment_index1 = 0;
						else
							segment_index1 = key_frame1 < segment_start_indices[segment_index] ? segment_index0 : segment_index;

						break;
					}
				}

				segment_key_frame0 = key_frame0 - segment_start_indices[segment_index0];
				segment_key_frame1 = key_frame1 - segment_start_indices[segment_index1];

				if (has_stripped_keyframes)
				{
					spdlog::info("Has stripped keyframes");
					const stripped_segment_header_t* segment_tier0_header0 = segment_tier0_headers + segment_index0;
					const stripped_segment_header_t* segment_tier0_header1 = segment_tier0_headers + segment_index1;

					// This will cache miss
					uint32_t sample_indices0 = segment_tier0_header0->sample_indices;
					uint32_t sample_indices1 = segment_tier0_header1->sample_indices;

					// Calculate our clip relative sample index, we'll remap it later relative to the samples we'll use
					const float sample_index = context.interpolation_alpha + float(key_frame0);

					// When we load our sample indices and offsets from the database, there can be another thread writing
					// to those memory locations at the same time (e.g. streaming in/out).
					// To ensure thread safety, we atomically load the offset and sample indices.
					uint64_t medium_importance_tier_metadata0 = 0;
					uint64_t medium_importance_tier_metadata1 = 0;
					uint64_t low_importance_tier_metadata0 = 0;
					uint64_t low_importance_tier_metadata1 = 0;

					// Combine all our loaded samples into a single bit set to find which samples we need to interpolate
					if (is_database_supported && db != nullptr)
					{
						// Possible cache miss for the clip header offset
						// Cache miss for the db clip segment headers pointer
						const tracks_database_header* tracks_db_header = hoyo_header.get_database_header();
						const database_runtime_clip_header* db_clip_header = tracks_db_header->get_clip_header(db->clip_segment_headers);
						const database_runtime_segment_header* db_segment_headers = db_clip_header->get_segment_headers();

						// Cache miss for the db segment headers
						const database_runtime_segment_header* db_segment_header0 = db_segment_headers + segment_index0;
						medium_importance_tier_metadata0 = db_segment_header0->tier_metadata[0].load(k_memory_order_relaxed);
						low_importance_tier_metadata0 = db_segment_header0->tier_metadata[1].load(k_memory_order_relaxed);

						sample_indices0 |= uint32_t(medium_importance_tier_metadata0);
						sample_indices0 |= uint32_t(low_importance_tier_metadata0);

						const database_runtime_segment_header* db_segment_header1 = db_segment_headers + segment_index1;
						medium_importance_tier_metadata1 = db_segment_header1->tier_metadata[0].load(k_memory_order_relaxed);
						low_importance_tier_metadata1 = db_segment_header1->tier_metadata[1].load(k_memory_order_relaxed);

						sample_indices1 |= uint32_t(medium_importance_tier_metadata1);
						sample_indices1 |= uint32_t(low_importance_tier_metadata1);
					}

					// Find the closest loaded samples
					// Mask all trailing samples to find the first sample by counting trailing zeros
					const uint32_t candidate_indices0 = sample_indices0 & (0xFFFFFFFFU << (31 - segment_key_frame0));
					segment_key_frame0 = 31 - count_trailing_zeros(candidate_indices0);

					// Mask all leading samples to find the second sample by counting leading zeros
					const uint32_t candidate_indices1 = sample_indices1 & (0xFFFFFFFFU >> segment_key_frame1);
					segment_key_frame1 = count_leading_zeros(candidate_indices1);

					// Calculate our clip relative sample indices
					const uint32_t clip_key_frame0 = segment_start_indices[segment_index0] + segment_key_frame0;
					const uint32_t clip_key_frame1 = segment_start_indices[segment_index1] + segment_key_frame1;

					// Calculate our new interpolation alpha
					// We used the rounding policy above to snap to the correct key frame earlier but we might need to interpolate now
					// if key frames have been removed
					context.interpolation_alpha = find_linear_interpolation_alpha(sample_index, clip_key_frame0, clip_key_frame1, sample_rounding_policy::none, looping_policy_);

					// Find where our data lives (clip or database tier X)
					sample_indices0 = segment_tier0_header0->sample_indices;
					sample_indices1 = segment_tier0_header1->sample_indices;

					if (is_database_supported && db != nullptr)
					{
						const uint64_t sample_index0 = uint64_t(1) << (31 - segment_key_frame0);
						const uint64_t sample_index1 = uint64_t(1) << (31 - segment_key_frame1);

						const uint8_t* bulk_data_medium = db->bulk_data[0];		// Might be nullptr if we haven't streamed in yet
						const uint8_t* bulk_data_low = db->bulk_data[1];		// Might be nullptr if we haven't streamed in yet
						if ((medium_importance_tier_metadata0 & sample_index0) != 0)
						{
							sample_indices0 = uint32_t(medium_importance_tier_metadata0);
							db_animated_track_data0 = bulk_data_medium + uint32_t(medium_importance_tier_metadata0 >> 32);
						}
						else if ((low_importance_tier_metadata0 & sample_index0) != 0)
						{
							sample_indices0 = uint32_t(low_importance_tier_metadata0);
							db_animated_track_data0 = bulk_data_low + uint32_t(low_importance_tier_metadata0 >> 32);
						}

						if ((medium_importance_tier_metadata1 & sample_index1) != 0)
						{
							sample_indices1 = uint32_t(medium_importance_tier_metadata1);
							db_animated_track_data1 = bulk_data_medium + uint32_t(medium_importance_tier_metadata1 >> 32);
						}
						else if ((low_importance_tier_metadata1 & sample_index1) != 0)
						{
							sample_indices1 = uint32_t(low_importance_tier_metadata1);
							db_animated_track_data1 = bulk_data_low + uint32_t(low_importance_tier_metadata1 >> 32);
						}
					}

					// Remap our sample indices within the ones actually stored (e.g. index 3 might be the second frame stored)
					segment_key_frame0 = count_set_bits(and_not(0xFFFFFFFFU >> segment_key_frame0, sample_indices0));
					segment_key_frame1 = count_set_bits(and_not(0xFFFFFFFFU >> segment_key_frame1, sample_indices1));

					// Nasty but safe since they have the same layout
					segment_header0 = static_cast<const segment_header*>(segment_tier0_header0);
					segment_header1 = static_cast<const segment_header*>(segment_tier0_header1);
				}
				else
				{
					segment_header0 = segment_headers + segment_index0;
					segment_header1 = segment_headers + segment_index1;
				}
			}

			const bool uses_single_segment = segment_header0 == segment_header1;
			context.uses_single_segment = uses_single_segment;

			// Cache miss if we don't access the db data
			hoyo_header.get_segment_data(*segment_header0, context.format_per_track_data[0], context.animated_track_data[0]);

			// More often than not the two segments are identical, when this is the case, just copy our pointers
			if (!uses_single_segment)
			{
				hoyo_header.get_segment_data(*segment_header1, context.format_per_track_data[1], context.animated_track_data[1]);
			}
			else
			{
				context.format_per_track_data[1] = context.format_per_track_data[0];
				context.animated_track_data[1] = context.animated_track_data[0];
			}

			if (has_database)
			{
				// Update our pointers if the data lives within the database
				if (db_animated_track_data0 != nullptr)
					context.animated_track_data[0] = db_animated_track_data0;

				if (db_animated_track_data1 != nullptr)
					context.animated_track_data[1] = db_animated_track_data1;
			}

			context.key_frame_bit_offsets[0] = segment_key_frame0 * segment_header0->animated_pose_bit_size;
			context.key_frame_bit_offsets[1] = segment_key_frame1 * segment_header1->animated_pose_bit_size;
		}

		template<class decompression_settings_type, class track_writer_type>
		inline void decompress_tracks_v0(const persistent_hoyo_decompression_context_v0& context, track_writer_type& writer)
		{
			const tracks_header& header = get_tracks_header(*context.tracks);
			const uint32_t num_tracks = header.num_tracks;
			if (num_tracks == 0)
				return;	// Empty track list

			ACL_ASSERT(context.sample_time >= 0.0f, "Context not set to a valid sample time");
			if (context.sample_time < 0.0F)
				return;	// Invalid sample time, we didn't seek yet

			// Due to the SIMD operations, we sometimes overflow in the SIMD lanes not used.
			// Disable floating point exceptions to avoid issues.
			fp_environment fp_env;
			if (decompression_settings_type::disable_fp_exeptions())
				disable_fp_exceptions(fp_env);

			const hoyo_tracks_header& hoyo_header = get_hoyo_tracks_header(*context.tracks);
			const rtm::scalarf interpolation_alpha = rtm::scalar_set(context.interpolation_alpha);

			const sample_rounding_policy rounding_policy = static_cast<sample_rounding_policy>(context.rounding_policy);

			float interpolation_alpha_per_policy[k_num_sample_rounding_policies] = {};
			if (decompression_settings_type::is_per_track_rounding_supported())
			{
				const float alpha = context.interpolation_alpha;
				const float no_rounding_alpha = apply_rounding_policy(alpha, sample_rounding_policy::none);

				interpolation_alpha_per_policy[static_cast<int>(sample_rounding_policy::none)] = no_rounding_alpha;
				interpolation_alpha_per_policy[static_cast<int>(sample_rounding_policy::floor)] = apply_rounding_policy(alpha, sample_rounding_policy::floor);
				interpolation_alpha_per_policy[static_cast<int>(sample_rounding_policy::ceil)] = apply_rounding_policy(alpha, sample_rounding_policy::ceil);
				interpolation_alpha_per_policy[static_cast<int>(sample_rounding_policy::nearest)] = apply_rounding_policy(alpha, sample_rounding_policy::nearest);
				// We'll assert if we attempt to use this, but in case they are skipped/disabled, we interpolate
				interpolation_alpha_per_policy[static_cast<int>(sample_rounding_policy::per_track)] = no_rounding_alpha;
			}

			track_metadata* per_track_metadata = nullptr;
			float* constant_values = nullptr;
			float* range_values = nullptr;
			uint8_t* animated_values = nullptr;
			if (context.has_database) {
				constant_values = bit_cast<float*>(hoyo_header.get_database_constant_values());
				range_values = bit_cast<float*>(hoyo_header.get_database_range_values());
			}
			else {
				per_track_metadata = bit_cast<track_metadata*>(hoyo_header.get_track_metadata());
				constant_values = bit_cast<float*>(hoyo_header.get_track_constant_values());
				range_values = bit_cast<float*>(hoyo_header.get_track_range_values());
				animated_values = bit_cast<uint8_t*>(hoyo_header.get_track_animated_values());
			}

			uint32_t track_bit_offset0 = context.key_frame_bit_offsets[0];
			uint32_t track_bit_offset1 = context.key_frame_bit_offsets[1];

			const track_type8 track_type = header.track_type;

			const compressed_tracks_version16 version = context.get_version();
			const uint8_t* num_bits_at_bit_rate = version == compressed_tracks_version16::v02_00_00 ? k_bit_rate_num_bits_v0 : k_bit_rate_num_bits;

#if defined(ACL_HAS_ASSERT_CHECKS)
			const uint32_t max_bit_rate = version == compressed_tracks_version16::v02_00_00 ? sizeof(k_bit_rate_num_bits_v0) : sizeof(k_bit_rate_num_bits);
#endif

			uint32_t num_bits_per_component = num_tracks;
			for (uint32_t track_index = 0; track_index < num_tracks; ++track_index)
			{
				if (!context.has_database) {
					const track_metadata& metadata = per_track_metadata[track_index];
					const uint32_t bit_rate = metadata.bit_rate;
					ACL_ASSERT(bit_rate < max_bit_rate, "Invalid bit rate: %u", bit_rate);
					num_bits_per_component = num_bits_at_bit_rate[bit_rate];
				}

				rtm::scalarf alpha = interpolation_alpha;
				if (decompression_settings_type::is_per_track_rounding_supported())
				{
					const sample_rounding_policy rounding_policy_ = writer.get_rounding_policy(rounding_policy, track_index);
					ACL_ASSERT(rounding_policy_ != sample_rounding_policy::per_track, "track_writer::get_rounding_policy() cannot return per_track");

					alpha = rtm::scalar_set(interpolation_alpha_per_policy[static_cast<int>(rounding_policy_)]);
				}

				if (track_type == track_type8::float1f && decompression_settings_type::is_track_type_supported(track_type8::float1f))
				{
					rtm::scalarf value;
					if (num_bits_per_component == 0)	// Constant bit rate
					{
						value = rtm::scalar_load(constant_values);
						constant_values += 1;
					}
					else
					{
						rtm::scalarf value0;
						rtm::scalarf value1;
						if (num_bits_per_component == 32)	// Raw bit rate
						{
							value0 = unpack_scalarf_32_unsafe(animated_values, track_bit_offset0);
							value1 = unpack_scalarf_32_unsafe(animated_values, track_bit_offset1);
						}
						else
						{
							value0 = unpack_scalarf_uXX_unsafe(num_bits_per_component, animated_values, track_bit_offset0);
							value1 = unpack_scalarf_uXX_unsafe(num_bits_per_component, animated_values, track_bit_offset1);

							const rtm::scalarf range_min = rtm::scalar_load(range_values);
							const rtm::scalarf range_extent = rtm::scalar_load(range_values + 1);
							value0 = rtm::scalar_mul_add(value0, range_extent, range_min);
							value1 = rtm::scalar_mul_add(value1, range_extent, range_min);
							range_values += 2;
						}

						value = rtm::scalar_lerp(value0, value1, alpha);

						const uint32_t num_sample_bits = num_bits_per_component;
						track_bit_offset0 += num_sample_bits;
						track_bit_offset1 += num_sample_bits;
					}

					writer.write_float1(track_index, value);
				}
				else if (track_type == track_type8::float2f && decompression_settings_type::is_track_type_supported(track_type8::float2f))
				{
					rtm::vector4f value;
					if (num_bits_per_component == 0)	// Constant bit rate
					{
						value = rtm::vector_load(constant_values);
						constant_values += 2;
					}
					else
					{
						rtm::vector4f value0;
						rtm::vector4f value1;
						if (num_bits_per_component == 32)	// Raw bit rate
						{
							value0 = unpack_vector2_64_unsafe(animated_values, track_bit_offset0);
							value1 = unpack_vector2_64_unsafe(animated_values, track_bit_offset1);
						}
						else
						{
							value0 = unpack_vector2_uXX_unsafe(num_bits_per_component, animated_values, track_bit_offset0);
							value1 = unpack_vector2_uXX_unsafe(num_bits_per_component, animated_values, track_bit_offset1);

							const rtm::vector4f range_min = rtm::vector_load(range_values);
							const rtm::vector4f range_extent = rtm::vector_load(range_values + 2);
							value0 = rtm::vector_mul_add(value0, range_extent, range_min);
							value1 = rtm::vector_mul_add(value1, range_extent, range_min);
							range_values += 4;
						}

						value = rtm::vector_lerp(value0, value1, alpha);

						const uint32_t num_sample_bits = num_bits_per_component * 2;
						track_bit_offset0 += num_sample_bits;
						track_bit_offset1 += num_sample_bits;
					}

					writer.write_float2(track_index, value);
				}
				else if (track_type == track_type8::float3f && decompression_settings_type::is_track_type_supported(track_type8::float3f))
				{
					rtm::vector4f value;
					if (num_bits_per_component == 0)	// Constant bit rate
					{
						value = rtm::vector_load(constant_values);
						constant_values += 3;
					}
					else
					{
						rtm::vector4f value0;
						rtm::vector4f value1;
						if (num_bits_per_component == 32)	// Raw bit rate
						{
							value0 = unpack_vector3_96_unsafe(animated_values, track_bit_offset0);
							value1 = unpack_vector3_96_unsafe(animated_values, track_bit_offset1);
						}
						else
						{
							value0 = unpack_vector3_uXX_unsafe(num_bits_per_component, animated_values, track_bit_offset0);
							value1 = unpack_vector3_uXX_unsafe(num_bits_per_component, animated_values, track_bit_offset1);

							const rtm::vector4f range_min = rtm::vector_load(range_values);
							const rtm::vector4f range_extent = rtm::vector_load(range_values + 3);
							value0 = rtm::vector_mul_add(value0, range_extent, range_min);
							value1 = rtm::vector_mul_add(value1, range_extent, range_min);
							range_values += 6;
						}

						value = rtm::vector_lerp(value0, value1, alpha);

						const uint32_t num_sample_bits = num_bits_per_component * 3;
						track_bit_offset0 += num_sample_bits;
						track_bit_offset1 += num_sample_bits;
					}

					writer.write_float3(track_index, value);
				}
				else if (track_type == track_type8::float4f && decompression_settings_type::is_track_type_supported(track_type8::float4f))
				{
					rtm::vector4f value;
					if (num_bits_per_component == 0)	// Constant bit rate
					{
						value = rtm::vector_load(constant_values);
						constant_values += 4;
					}
					else
					{
						rtm::vector4f value0;
						rtm::vector4f value1;
						if (num_bits_per_component == 32)	// Raw bit rate
						{
							value0 = unpack_vector4_128_unsafe(animated_values, track_bit_offset0);
							value1 = unpack_vector4_128_unsafe(animated_values, track_bit_offset1);
						}
						else
						{
							value0 = unpack_vector4_uXX_unsafe(num_bits_per_component, animated_values, track_bit_offset0);
							value1 = unpack_vector4_uXX_unsafe(num_bits_per_component, animated_values, track_bit_offset1);

							const rtm::vector4f range_min = rtm::vector_load(range_values);
							const rtm::vector4f range_extent = rtm::vector_load(range_values + 4);
							value0 = rtm::vector_mul_add(value0, range_extent, range_min);
							value1 = rtm::vector_mul_add(value1, range_extent, range_min);
							range_values += 8;
						}

						value = rtm::vector_lerp(value0, value1, alpha);

						const uint32_t num_sample_bits = num_bits_per_component * 4;
						track_bit_offset0 += num_sample_bits;
						track_bit_offset1 += num_sample_bits;
					}

					writer.write_float4(track_index, value);
				}
				else if (track_type == track_type8::vector4f && decompression_settings_type::is_track_type_supported(track_type8::vector4f))
				{
					rtm::vector4f value;
					if (num_bits_per_component == 0)	// Constant bit rate
					{
						value = rtm::vector_load(constant_values);
						constant_values += 4;
					}
					else
					{
						rtm::vector4f value0;
						rtm::vector4f value1;
						if (num_bits_per_component == 32)	// Raw bit rate
						{
							value0 = unpack_vector4_128_unsafe(animated_values, track_bit_offset0);
							value1 = unpack_vector4_128_unsafe(animated_values, track_bit_offset1);
						}
						else
						{
							value0 = unpack_vector4_uXX_unsafe(num_bits_per_component, animated_values, track_bit_offset0);
							value1 = unpack_vector4_uXX_unsafe(num_bits_per_component, animated_values, track_bit_offset1);

							const rtm::vector4f range_min = rtm::vector_load(range_values);
							const rtm::vector4f range_extent = rtm::vector_load(range_values + 4);
							value0 = rtm::vector_mul_add(value0, range_extent, range_min);
							value1 = rtm::vector_mul_add(value1, range_extent, range_min);
							range_values += 8;
						}

						value = rtm::vector_lerp(value0, value1, alpha);

						const uint32_t num_sample_bits = num_bits_per_component * 4;
						track_bit_offset0 += num_sample_bits;
						track_bit_offset1 += num_sample_bits;
					}

					writer.write_vector4(track_index, value);
				}

				if (context.has_database) {
					
				}
				else {

				}
			}
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
