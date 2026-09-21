//MIT License
//
//Copyright(c) 2024 Razmoth
//
//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this softwareand associated documentation files(the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and /or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions :
//
//The above copyright noticeand this permission notice shall be included in all
//copies or substantial portions of the Software.
//
//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.

#include "dllmain.h"

#include <acl/core/iallocator.h>
#include <acl/core/ansi_allocator.h>
#include <acl/core/compressed_tracks.h>
#include <acl/core/compressed_database.h>

#include <acl/decompression/decompress.h>
#include <acl/decompression/database/database.h>
#include <acl/decompression/database/null_database_streamer.h>
#include <acl/decompression/database/impl/debug_database_streamer.h>

#include <acl/compression/track_array.h>

using namespace acl;

struct acl_writer : public track_writer
{
	acl_writer(float* values)
		: m_values(values)
	{
		m_sample_index = 0;
		m_num_tracks = 0;
		m_num_constant_tracks = 0;
	}

	void RTM_SIMD_CALL write_rotation(uint32_t track_index, rtm::quatf_arg0 rotation)
	{
		uint32_t index = calculate_qvvf_index(track_index);

		rtm::quat_store(rotation, &m_values[index]);
	}

	void RTM_SIMD_CALL write_translation(uint32_t track_index, rtm::vector4f_arg0 translation)
	{
		uint32_t index = calculate_qvvf_index(track_index);

		rtm::vector_store3(translation, &m_values[index + 4]);
	}

	void RTM_SIMD_CALL write_scale(uint32_t track_index, rtm::vector4f_arg0 scale)
	{
		uint32_t index = calculate_qvvf_index(track_index);

		rtm::vector_store3(scale, &m_values[index + 7]);
	}

	void RTM_SIMD_CALL write_float1(uint32_t track_index, rtm::scalarf_arg0 value)
	{
		uint32_t index = calculate_float1f_index(track_index);

		rtm::vector_store1(value.value, &m_values[index]);
	}

	uint32_t m_sample_index;
	uint32_t m_num_tracks;
	uint32_t m_num_constant_tracks;
	float* m_values;

	constexpr uint32_t calculate_qvvf_index(uint16_t track_index) { return m_sample_index * calculate_qvvf_size(m_num_tracks) + m_sample_index * calculate_float1f_size(m_num_constant_tracks) + calculate_qvvf_size(track_index); }
	constexpr uint32_t calculate_float1f_index(uint16_t track_index) { return (m_sample_index + 1) * calculate_qvvf_size(m_num_tracks) + m_sample_index * calculate_float1f_size(m_num_constant_tracks) + track_index; }

	constexpr static uint32_t calculate_qvvf_size(uint16_t num_track) { return num_track * 0xA; }
	constexpr static uint32_t calculate_float1f_size(uint16_t num_track) { return num_track; }
};

struct database_transform_decompression_settings : public default_transform_decompression_settings
{
	using database_settings_type = default_database_settings;
};

struct database_scalar_decompression_settings : public default_scalar_decompression_settings
{
	using database_settings_type = default_database_settings;
};

struct decompressed_clip
{
	float* values;
	int values_count;
	float* times;
	int times_count;
};

static acl::ansi_allocator Allocator;

AS_API(void) DecompressTracks(void* data, void* database, void* streamer, decompressed_clip& decompressed_clip)
{
	error_result database_error, tracks_error, transform_error, scalar_error;
	compressed_tracks* transform_compressed_tracks = nullptr;
	compressed_tracks* scalar_compressed_tracks = nullptr;

	auto databasse_context = allocate_type<database_context<default_database_settings>>(Allocator);
	auto transform_context = make_decompression_context<database_transform_decompression_settings>(Allocator);
	auto scalar_context = make_decompression_context<database_scalar_decompression_settings>(Allocator);

	auto compressed_database = make_compressed_database(database, &database_error);

	if (database_error.empty())
	{
		const uint8_t* medium_bulk_data = (const uint8_t*)streamer;
		const uint8_t* low_bulk_data = (const uint8_t*)add_offset_to_ptr<void>(streamer, align_to(compressed_database->get_bulk_data_size(quality_tier::medium_importance), 4));

		debug_database_streamer* medium_database_streamer = new debug_database_streamer(Allocator, medium_bulk_data, compressed_database->get_bulk_data_size(quality_tier::medium_importance));
		debug_database_streamer* low_database_streamer = new debug_database_streamer(Allocator, low_bulk_data, compressed_database->get_bulk_data_size(quality_tier::lowest_importance));

		databasse_context->initialize(Allocator, *compressed_database, *medium_database_streamer, *low_database_streamer);
		databasse_context->stream_in(quality_tier::medium_importance);
		databasse_context->stream_in(quality_tier::lowest_importance);
	}

	auto compressed_track = make_compressed_tracks(data, &tracks_error);

	if (tracks_error.empty())
	{
		decompressed_clip.times_count = 0;
		decompressed_clip.values_count = 0;

		if (compressed_track->get_track_type() == track_type8::qvvf) 
		{
			transform_error = tracks_error;
			transform_compressed_tracks = compressed_track;

			if (databasse_context->is_initialized())
			{
				transform_context->initialize(*transform_compressed_tracks, *databasse_context);
			}
			else
			{
				transform_context->initialize(*transform_compressed_tracks);
			}

			decompressed_clip.times_count += transform_compressed_tracks->get_num_samples_per_track();
			decompressed_clip.values_count += transform_compressed_tracks->get_num_samples_per_track() * acl_writer::calculate_qvvf_size(transform_compressed_tracks->get_num_tracks());

			data = add_offset_to_ptr<void>(data, align_to(transform_compressed_tracks->get_size(), alignof(compressed_tracks)));
			compressed_track = make_compressed_tracks(data, &tracks_error);
		}

		scalar_error = tracks_error;
		scalar_compressed_tracks = compressed_track;

		if (scalar_error.empty())
		{
			if (databasse_context->is_initialized())
			{
				scalar_context->initialize(*scalar_compressed_tracks, *databasse_context);
			}
			else
			{
				scalar_context->initialize(*scalar_compressed_tracks);
			}

			decompressed_clip.times_count += decompressed_clip.times_count != 0 ? 0 : scalar_compressed_tracks->get_num_samples_per_track();
			decompressed_clip.values_count += scalar_compressed_tracks->get_num_samples_per_track() * acl_writer::calculate_float1f_size(scalar_compressed_tracks->get_num_tracks());
		}

		if (transform_context->is_initialized() || scalar_context->is_initialized())
		{
			decompressed_clip.times = allocate_type_array<float>(Allocator, decompressed_clip.times_count);
			decompressed_clip.values = allocate_type_array<float>(Allocator, decompressed_clip.values_count);

			float step = 0;
			acl_writer writer(decompressed_clip.values);

			if(transform_error.empty() && transform_compressed_tracks != nullptr)
			{
				writer.m_num_tracks = transform_compressed_tracks->get_num_tracks();
				step = rtm::scalar_reciprocal(transform_compressed_tracks->get_sample_rate());
			}
			if (scalar_error.empty() && scalar_compressed_tracks != nullptr)
			{
				writer.m_num_constant_tracks = scalar_compressed_tracks->get_num_tracks();
				step = rtm::scalar_reciprocal(scalar_compressed_tracks->get_sample_rate());
			}
			for (int32_t sample_index = 0; sample_index < decompressed_clip.times_count; ++sample_index)
			{
				const float sample_time = sample_index * step;

				decompressed_clip.times[sample_index] = sample_time;

				writer.m_sample_index = sample_index;
				if (transform_error.empty())
				{
					transform_context->seek(sample_time, sample_rounding_policy::none);
					transform_context->decompress_tracks(writer);
				}
				if (scalar_error.empty())
				{
					scalar_context->seek(sample_time, sample_rounding_policy::none);
					scalar_context->decompress_tracks(writer);
				}
			}
		}
	}

	deallocate_type(Allocator, databasse_context);
}

AS_API(void) Dispose(decompressed_clip& decompressed_clip) {
	deallocate_type_array<float>(Allocator, decompressed_clip.times, decompressed_clip.times_count);
	deallocate_type_array<float>(Allocator, decompressed_clip.values, decompressed_clip.values_count);
}