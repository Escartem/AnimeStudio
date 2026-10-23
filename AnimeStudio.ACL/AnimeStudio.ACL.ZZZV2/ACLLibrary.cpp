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

// The vendored acl/ tree here is the HoYo-patched acl 2.1.0 (compressed_tracks_version16::vHoYo).
// It is the only one of the three vendored trees that can decode ZZZ clips; the stock decoder
// rejects them in is_valid() with "Invalid algorithm version".
//
// ZZZ splits its clip data into two independently compressed_tracks blobs -- transform tracks
// (qvvf) followed by scalar tracks (float1f) -- so the entry point takes them separately rather
// than walking a single buffer.

#include "pch.h"
#include "ACLLibrary.h"

#include <cstring>
#include <exception>
#include <stdexcept>
#include <cstdio>

#define RTM_NO_DEPRECATION
#define ACL_ON_ASSERT_THROW
#define RTM_ON_ASSERT_THROW
#define FMT_UNICODE 0

#include <acl/core/iallocator.h>
#include <acl/core/ansi_allocator.h>
#include <acl/core/compressed_tracks.h>
#include <acl/core/compressed_database.h>

#include <acl/decompression/decompress.h>
#include <acl/decompression/decompression_settings.h>
#include <acl/decompression/database/database.h>
#include <acl/decompression/database/database_settings.h>
#include <acl/decompression/database/database_streamer.h>
#include <acl/decompression/database/null_database_streamer.h>

using namespace acl;

namespace
{
	// Every vendored tree aliases `latest` to its own HoYo version constant (100), so this
	// stays correct if the tree is ever swapped again.
	// acl static_asserts that the database settings and the decompression settings agree on
	// version_supported(), and one database_context has to serve both the transform and the
	// scalar context, so all three use `any` and dispatch on the version in the blob header.
	struct hoyo_database_settings : public default_database_settings
	{
		static constexpr compressed_tracks_version16 version_supported() { return compressed_tracks_version16::any; }
	};

	// ZZZ's two blobs are NOT both HoYo-versioned. Observed on live data (Avatar_*_Ani_*):
	//   transform blob: version 8 (v02_01_99), track_type 12 (qvvf)  -> stock decoder
	//   scalar blob:    version 100 (vHoYo),   track_type 0 (float1f) -> HoYo decoder
	// That matches acl's own context selector, which routes <transform, hoyo> to the stock
	// transform context and only <scalar, hoyo> to the patched HoYo context.
	struct hoyo_transform_decompression_settings : public default_transform_decompression_settings
	{
		using database_settings_type = hoyo_database_settings;
		// Accept both stock and HoYo versions rather than pinning one.
		static constexpr compressed_tracks_version16 version_supported() { return compressed_tracks_version16::any; }
	};

	struct hoyo_scalar_decompression_settings : public default_hoyo_scalar_decompression_settings
	{
		using database_settings_type = hoyo_database_settings;
		// is_hoyo() stays inherited (true), so this still selects the patched HoYo scalar context;
		// only the version dispatch is widened to match hoyo_database_settings.
		static constexpr compressed_tracks_version16 version_supported() { return compressed_tracks_version16::any; }
		// The stock safety checks cast the header to a transform_track header and trip on scalar
		// clips. The tracks themselves are validated by make_compressed_tracks() before we get here.
		static constexpr bool skip_initialize_safety_checks() { return true; }
	};

	// One frame is num_transform_tracks * QVVF_STRIDE floats followed by num_scalar_tracks floats.
	constexpr uint32_t QVVF_STRIDE = 10;

	// Writes into the flat float buffer the managed side unpacks. Transform tracks occupy
	// [0, 10*T) of each frame, scalar tracks [10*T, 10*T + S).
	struct acl_writer : public track_writer
	{
		acl_writer(float* values, uint32_t num_transform_tracks, uint32_t num_scalar_tracks)
			: m_values(values)
			, m_num_transform_tracks(num_transform_tracks)
			, m_num_scalar_tracks(num_scalar_tracks)
			, m_frame(0)
		{
		}

		void set_frame(uint32_t frame) { m_frame = frame; }

		RTM_FORCE_INLINE void RTM_SIMD_CALL write_rotation(uint32_t track_index, rtm::quatf_arg0 rotation)
		{
			rtm::quat_store(rotation, &frame_base()[track_index * QVVF_STRIDE]);
		}

		RTM_FORCE_INLINE void RTM_SIMD_CALL write_translation(uint32_t track_index, rtm::vector4f_arg0 translation)
		{
			rtm::vector_store3(translation, &frame_base()[track_index * QVVF_STRIDE + 4]);
		}

		RTM_FORCE_INLINE void RTM_SIMD_CALL write_scale(uint32_t track_index, rtm::vector4f_arg0 scale)
		{
			rtm::vector_store3(scale, &frame_base()[track_index * QVVF_STRIDE + 7]);
		}

		RTM_FORCE_INLINE void RTM_SIMD_CALL write_float1(uint32_t track_index, rtm::scalarf_arg0 value)
		{
			// Scalar tracks start after the transform block of the same frame. Omitting this
			// offset overwrites the first transform track of every frame.
			rtm::scalar_store(value, &frame_base()[QVVF_STRIDE * m_num_transform_tracks + track_index]);
		}

	private:
		float* frame_base() const { return m_values + frame_stride() * m_frame; }
		uint32_t frame_stride() const { return QVVF_STRIDE * m_num_transform_tracks + m_num_scalar_tracks; }

		float* m_values;
		uint32_t m_num_transform_tracks;
		uint32_t m_num_scalar_tracks;
		uint32_t m_frame;
	};

	acl::ansi_allocator Allocator;
}

// Message from the last failed decompression on this thread; read back via GetLastDecompressError.
static thread_local char g_last_error[512];
static thread_local char g_last_info[512];


static void set_last_error(const char* message)
{
	if (message == nullptr)
		message = "unknown native exception";
	std::strncpy(g_last_error, message, sizeof(g_last_error) - 1);
	g_last_error[sizeof(g_last_error) - 1] = '\0';
}

// transform_data / scalar_data / database_data / bulk_data are raw 16-byte-aligned buffers or null.
// bulk_data may be null even when database_data is present: GI concatenates the streamed bulk data
// directly behind the database blob, so we derive the pointer in that case.
// Parses a compressed_tracks blob and rejects it unless it fits entirely inside the buffer we
// were given. acl trusts the size field in the header, so a truncated or inconsistent blob
// otherwise reads past the allocation and takes the process down with an access violation --
// a C++ try/catch cannot recover from that, which is why the check has to happen up front.
static compressed_tracks* parse_tracks(void* data, int size, const char* what)
{
	if (data == nullptr || size <= 0)
		return nullptr;

	if (size < 32) throw std::runtime_error("Truncated ACL header");
	error_result err;
	compressed_tracks* tracks = make_compressed_tracks(data, &err);
	if (!err.empty() || tracks == nullptr)
		throw std::runtime_error("Invalid ACL tracks");

	if (tracks->get_size() > static_cast<uint32_t>(size))
	{
		std::snprintf(g_last_error, sizeof(g_last_error),
			"%s blob declares %u bytes but only %d were provided", what, tracks->get_size(), size);
		return nullptr;
	}

	return tracks;
}

static void decompress_zzz(void* transform_data, int transform_size, void* scalar_data, int scalar_size,
	void* database_data, int database_size, void* bulk_data, int bulk_size, decompressed_clip& out)
{
	out.values = nullptr;
	out.values_count = 0;
	out.times = nullptr;
	out.times_count = 0;

	compressed_tracks* transform_tracks = parse_tracks(transform_data, transform_size, "transform");
	compressed_tracks* scalar_tracks = parse_tracks(scalar_data, scalar_size, "scalar");

	if (transform_tracks == nullptr && scalar_tracks == nullptr)
		return;

	const compressed_database* database = nullptr;
	if (database_data != nullptr && database_size > 0)
	{
		if (database_size < 64) throw std::runtime_error("Truncated ACL database header");
		error_result err;
		database = make_compressed_database(database_data, &err);
		if (!err.empty())
			database = nullptr;
		else if (database != nullptr && database->get_size() > static_cast<uint32_t>(database_size))
		{
			std::snprintf(g_last_error, sizeof(g_last_error),
				"database declares %u bytes but only %d were provided", database->get_size(), database_size);
			database = nullptr;
		}
	}

	// Resolve the bulk data layout before constructing the streamers, so they can be declared
	// at function scope -- the database context keeps references to them and outlives any
	// narrower scope we would otherwise put them in.
	const bool inline_bulk = database != nullptr && database->is_bulk_data_inline();
	const uint8_t* bulk = nullptr;
	uint32_t medium_size = 0;
	uint32_t low_size = 0;

	if (database != nullptr && !inline_bulk)
	{
		int available = bulk_size;
		bulk = static_cast<const uint8_t*>(bulk_data);
		if (bulk == nullptr || available <= 0)
		{
			// GI appends the bulk data directly behind the database blob rather than passing
			// it separately, so derive the pointer from the database size.
			bulk = add_offset_to_ptr<const uint8_t>(database_data, database->get_size());
			available = database_size - static_cast<int>(database->get_size());
		}

		medium_size = database->get_bulk_data_size(quality_tier::medium_importance);
		low_size = database->get_bulk_data_size(quality_tier::lowest_importance);

		// The header can claim more bulk data than the clip actually carries. Streaming that in
		// reads past the buffer, so drop the database instead and decode what is inline.
		const uint64_t needed = static_cast<uint64_t>(align_to(medium_size, 4)) + low_size;
		if (available <= 0 || needed > static_cast<uint64_t>(available))
		{
			std::snprintf(g_last_error, sizeof(g_last_error),
				"database bulk needs %llu bytes (medium %u + low %u) but only %d are available; decoding without it",
				static_cast<unsigned long long>(needed), medium_size, low_size, available);
			bulk = nullptr;
			medium_size = 0;
			low_size = 0;
		}
	}

	// A stripped database with nothing to stream is unusable; decode without it rather than
	// feeding the context an empty streamer.
	const bool use_streamed_database = bulk != nullptr && (medium_size != 0 || low_size != 0);

	// null_database_streamer asserts on a null buffer in its constructor even for a zero-sized
	// tier (unlike its own is_initialized(), which tolerates it), so both tiers always get the
	// base pointer. ZZZ ships a single tier; when both exist they are concatenated, medium first.
	const uint8_t* safe_bulk = bulk != nullptr ? bulk : reinterpret_cast<const uint8_t*>(&medium_size);
	std::snprintf(g_last_info, sizeof(g_last_info),
		"inline=%d medium=%u low=%u dbSize=%u bulkGiven=%d tTracks=%u tSamples=%u tHasDb=%d sTracks=%u sSamples=%u sHasDb=%d",
		inline_bulk ? 1 : 0, medium_size, low_size,
		database != nullptr ? database->get_size() : 0u,
		bulk_data != nullptr ? 1 : 0,
		transform_tracks != nullptr ? transform_tracks->get_num_tracks() : 0u,
		transform_tracks != nullptr ? transform_tracks->get_num_samples_per_track() : 0u,
		transform_tracks != nullptr && transform_tracks->has_database() ? 1 : 0,
		scalar_tracks != nullptr ? scalar_tracks->get_num_tracks() : 0u,
		scalar_tracks != nullptr ? scalar_tracks->get_num_samples_per_track() : 0u,
		scalar_tracks != nullptr && scalar_tracks->has_database() ? 1 : 0);

	null_database_streamer medium_streamer(safe_bulk, medium_size);
	null_database_streamer low_streamer(medium_size != 0 ? safe_bulk + align_to(medium_size, 4) : safe_bulk, low_size);
	database_context<hoyo_database_settings> database_ctx;

	if (inline_bulk)
	{
		database_ctx.initialize(Allocator, *database);
	}
	else if (use_streamed_database)
	{
		database_ctx.initialize(Allocator, *database, medium_streamer, low_streamer);
		if (medium_size != 0)
			database_ctx.stream_in(quality_tier::medium_importance);
		if (low_size != 0)
			database_ctx.stream_in(quality_tier::lowest_importance);
	}

    if (((transform_tracks && transform_tracks->has_database()) ||
         (scalar_tracks && scalar_tracks->has_database())) && !database_ctx.is_initialized())
        throw std::runtime_error("Required ACL database could not be initialized");

	decompression_context<hoyo_transform_decompression_settings> transform_ctx;
	if (transform_tracks != nullptr)
	{
		if (transform_tracks->has_database() && database_ctx.is_initialized())
			transform_ctx.initialize(*transform_tracks, database_ctx);
		else
			transform_ctx.initialize(*transform_tracks);
	}

	// Database-backed scalar tracks decode again. The branch upstream left unfinished is
	// implemented in decompression.hoyo.h; the layout was recovered from live ZZZ facial clips
	// and checked against the classic streamed/constant variant of the same clips: 294 constant
	// and default tracks over 12 clips and two characters with very different distributions
	// (8/31/8 and 16/18/13 default/constant/variable), plus 70 variable tracks. Worst deviation
	// 9.2e-4, which is acl's own quantisation threshold, not a mapping error.
	decompression_context<hoyo_scalar_decompression_settings> scalar_ctx;
	if (scalar_tracks != nullptr)
	{
		if (scalar_tracks->has_database() && database_ctx.is_initialized())
			scalar_ctx.initialize(*scalar_tracks, database_ctx);
		else
			scalar_ctx.initialize(*scalar_tracks);
	}

	float step = 0.0f;
	uint32_t num_transform_tracks = 0;
	uint32_t num_scalar_tracks = 0;

	if (transform_ctx.is_initialized())
	{
		const uint32_t num_samples = transform_tracks->get_num_samples_per_track();
		num_transform_tracks = transform_tracks->get_num_tracks();
		out.times_count += static_cast<int>(num_samples);
		out.values_count += static_cast<int>(QVVF_STRIDE * num_samples * num_transform_tracks);
		step = rtm::scalar_reciprocal(transform_tracks->get_sample_rate());
	}

	// The scalar region is reserved even when it cannot be decoded. The managed converters index
	// the buffer as frameIndex * m_CurveCount, and m_CurveCount comes from the clip, not from what
	// was decoded -- dropping the region would shift every frame after the first and read past the
	// end. Leaving it zero-filled keeps the layout intact and only loses the float curves.
	if (scalar_tracks != nullptr)
	{
		const uint32_t num_samples = scalar_tracks->get_num_samples_per_track();
		num_scalar_tracks = scalar_tracks->get_num_tracks();
		if (out.times_count == 0)
			out.times_count = static_cast<int>(num_samples);
		out.values_count += static_cast<int>(num_samples * num_scalar_tracks);
		if (step == 0.0f)
			step = rtm::scalar_reciprocal(scalar_tracks->get_sample_rate());
	}

	if (transform_tracks && scalar_tracks &&
        (transform_tracks->get_num_samples_per_track() != scalar_tracks->get_num_samples_per_track() ||
         transform_tracks->get_sample_rate() != scalar_tracks->get_sample_rate()))
        throw std::runtime_error("Transform and scalar sampling grids differ");

    if (out.times_count <= 0 || out.values_count <= 0)
	{
		out.times_count = 0;
		out.values_count = 0;
		return;
	}

	out.times = allocate_type_array<float>(Allocator, out.times_count);
	out.values = allocate_type_array<float>(Allocator, out.values_count);
	std::memset(out.values, 0, sizeof(float) * static_cast<size_t>(out.values_count));

	acl_writer writer(out.values, num_transform_tracks, num_scalar_tracks);

	for (int sample_index = 0; sample_index < out.times_count; ++sample_index)
		out.times[sample_index] = sample_index * step;

	// Transform and scalar tracks are decoded in separate passes with independent error handling.
	// They occupy disjoint regions of every frame, so a scalar track that the decoder rejects
	// still leaves a fully usable transform animation behind instead of discarding the clip.
	if (transform_ctx.is_initialized())
	{
		try
		{
			for (int sample_index = 0; sample_index < out.times_count; ++sample_index)
			{
				writer.set_frame(static_cast<uint32_t>(sample_index));
				transform_ctx.seek(out.times[sample_index], sample_rounding_policy::none);
				transform_ctx.decompress_tracks(writer);
			}
		}
		catch (const std::exception& e)
		{
			set_last_error(e.what());
			std::strncat(g_last_error, " [transform pass]", sizeof(g_last_error) - std::strlen(g_last_error) - 1);
		}
	}

	if (scalar_ctx.is_initialized())
	{
		try
		{
			for (int sample_index = 0; sample_index < out.times_count; ++sample_index)
			{
				writer.set_frame(static_cast<uint32_t>(sample_index));
				scalar_ctx.seek(out.times[sample_index], sample_rounding_policy::none);
				scalar_ctx.decompress_tracks(writer);
			}
		}
		catch (const std::exception& e)
		{
			set_last_error(e.what());
			std::strncat(g_last_error, " [scalar pass]", sizeof(g_last_error) - std::strlen(g_last_error) - 1);
		}
	}
}

// acl's checks are compiled as ACL_ON_ASSERT_THROW rather than the usual abort, so a malformed
// clip cannot take the host process down with it. The exception must not cross the C ABI
// boundary, so it is swallowed here and reported as an empty clip; the managed side logs that.
AS_API(void) DecompressTracksZZZ(void* transform_data, int transform_size, void* scalar_data, int scalar_size,
	void* database_data, int database_size, void* bulk_data, int bulk_size, decompressed_clip& out)
{
	out.values = nullptr;
	out.values_count = 0;
	out.times = nullptr;
	out.times_count = 0;

	g_last_error[0] = '\0';

	try
	{
		decompress_zzz(transform_data, transform_size, scalar_data, scalar_size,
			database_data, database_size, bulk_data, bulk_size, out);
		// A per-pass failure inside decompress_zzz is recorded in g_last_error but keeps
		// whatever the other pass produced, so the clip is returned as-is.
		return;
	}
	catch (const std::exception& e)
	{
		set_last_error(e.what());
	}
	catch (...)
	{
		set_last_error("unknown native exception");
	}

	// Only reached when the run aborted outright; release anything it had already allocated.
	if (out.times != nullptr)
		deallocate_type_array<float>(Allocator, out.times, out.times_count);
	if (out.values != nullptr)
		deallocate_type_array<float>(Allocator, out.values, out.values_count);

	out.values = nullptr;
	out.values_count = 0;
	out.times = nullptr;
	out.times_count = 0;
}

// Returns the message from the last DecompressTracksZZZ call on this thread, or an empty string
// if it succeeded. Lets the managed side explain an empty clip instead of silently dropping it.
AS_API(const char*) GetLastDecompressInfo()
{
	return g_last_info;
}

AS_API(const char*) GetLastDecompressError()
{
	return g_last_error;
}

AS_API(void) Dispose(decompressed_clip& out)
{
	deallocate_type_array<float>(Allocator, out.times, out.times_count);
	deallocate_type_array<float>(Allocator, out.values, out.values_count);
	out.values = nullptr;
	out.values_count = 0;
	out.times = nullptr;
	out.times_count = 0;
}
