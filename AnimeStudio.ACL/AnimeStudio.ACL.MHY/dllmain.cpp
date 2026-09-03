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

struct ACLWriter : public OutputWriter
{
	ACLWriter(float* values, uint32_t sample_size)
		: m_values(values)
	{
		m_sample_size = sample_size;
		m_sample_index = 0;
	}

	void write_bone_rotation(uint16_t bone_index, const Quat_32& rotation)
	{
		quat_unaligned_write(rotation, &m_values[m_sample_index * m_sample_size + bone_index * 0xA]);
	}

	void write_bone_translation(uint16_t bone_index, const Vector4_32& translation)
	{
		vector_unaligned_write3(translation, &m_values[m_sample_index * m_sample_size + bone_index * 0xA + 4]);
	}

	void write_bone_scale(uint16_t bone_index, const Vector4_32& scale)
	{
		vector_unaligned_write3(scale, &m_values[m_sample_index * m_sample_size + bone_index * 0xA + 7]);
	}

	uint32_t m_sample_size;
	uint32_t m_sample_index;
	float* m_values;
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

	auto context = make_decompression_context<DefaultDecompressionSettings>(Allocator);
	auto compressed_clip = make_compressed_clip(data, &error);

	if (error.empty()) 
	{
		context->initialize(*compressed_clip);

		const ClipHeader& clip_header = get_clip_header(*compressed_clip);
		uint32_t sample_size = clip_header.num_bones * 0xA;

		decompressed_clip.times_count = clip_header.num_samples;
		decompressed_clip.values_count = clip_header.num_samples * sample_size;
		decompressed_clip.times = allocate_type_array<float>(Allocator, decompressed_clip.times_count);
		decompressed_clip.values = allocate_type_array<float>(Allocator, decompressed_clip.values_count);

		float step = 1.0f / float(clip_header.sample_rate);
		ACLWriter writer(decompressed_clip.values, sample_size);

		for (uint32_t sample_index = 0; sample_index < clip_header.num_samples; sample_index++)
		{
			writer.m_sample_index = sample_index;

			const float sample_time = sample_index * step;

			decompressed_clip.times[sample_index] = sample_time;

			context->seek(sample_time, SampleRoundingPolicy::None);
			context->decompress_pose(writer);
		}
	}
}

AS_API(void) Dispose(DecompressedClip& decompressed_clip) {
	deallocate_type_array<float>(Allocator, decompressed_clip.times, decompressed_clip.times_count);
	deallocate_type_array<float>(Allocator, decompressed_clip.values, decompressed_clip.values_count);
}