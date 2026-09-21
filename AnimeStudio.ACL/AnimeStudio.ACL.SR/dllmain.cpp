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

#include <acl/core/ansi_allocator.h>
#include <acl/decompression/default_output_writer.h>
#include <acl/algorithm/uniformly_sampled/decoder.h>

using namespace acl;
using namespace acl::uniformly_sampled;

struct SRDecompressionSettings : public DefaultDecompressionSettings
{
	constexpr bool is_rotation_format_supported(rotation_format8 format) const { return format == rotation_format8::quatf_variable; }
	constexpr rotation_format8 get_rotation_format(rotation_format8 /*format*/) const { return rotation_format8::quatf_variable; }
};

struct SRWriter : public OutputWriter
{
	SRWriter(float* values)
		: m_values(values)
	{
		index = 0;
	}

	void write_bone_rotation(uint16_t bone_index, rtm::quatf_arg0 rotation)
	{
		rtm::quat_store(rotation, &m_values[index]);
		index += 4;
	}

	void write_bone_translation(uint16_t bone_index, rtm::vector4f_arg0 translation)
	{
		rtm::vector_store3(translation, &m_values[index]);
		index += 3;
	}

	void write_bone_scale(uint16_t bone_index, rtm::vector4f_arg0 scale)
	{
		rtm::vector_store3(scale, &m_values[index]);
		index += 3;
	}

	void write_constant_track(const uint8_t* pData, uint16_t size)
	{
		memmove(&m_values[index], pData, size);
		index += (size / 4);
	}

	int32_t index;
	float* m_values;

	constexpr static uint32_t calculate_size(ClipHeader clip) { return clip.num_bones * (clip.has_scale ? 0xA : 0x7); }
};

struct DecompressedClip
{
	float* values;
	int values_count;
	float* times;
	int times_count;
};

static ANSIAllocator Allocator;

AS_API(void) DecompressClip(void* data, DecompressedClip& decompressed_clip)
{
	ErrorResult error;

	auto context = make_decompression_context<SRDecompressionSettings>(Allocator);
	auto compressed_clip = make_compressed_clip(data, &error);

	if (error.empty())
	{
		context->initialize(*compressed_clip);

		if (context->is_initialized()) 
		{
			const ClipHeader& clip_header = get_clip_header(*compressed_clip);

			decompressed_clip.times_count = clip_header.num_samples;
			decompressed_clip.values_count = clip_header.num_samples * SRWriter::calculate_size(clip_header);
			decompressed_clip.times = allocate_type_array<float>(Allocator, decompressed_clip.times_count);
			decompressed_clip.values = allocate_type_array<float>(Allocator, decompressed_clip.values_count);

			float step = rtm::scalar_reciprocal(clip_header.sample_rate);
			SRWriter pose_writer(decompressed_clip.values);

			for (uint32_t sample_index = 0; sample_index < clip_header.num_samples; ++sample_index)
			{
				const float sample_time = sample_index * step;

				decompressed_clip.times[sample_index] = sample_time;

				context->seek(sample_time, sample_rounding_policy::none);
				context->decompress_pose(pose_writer);
				pose_writer.write_constant_track(clip_header.get_constant_track_data(), clip_header.const_curve_count);
			}
		}
	}

	deallocate_type(Allocator, context);
}

AS_API(void) Dispose(DecompressedClip& decompressed_clip)
{
	deallocate_type_array<float>(Allocator, decompressed_clip.times, decompressed_clip.times_count);
	deallocate_type_array<float>(Allocator, decompressed_clip.values, decompressed_clip.values_count);
}