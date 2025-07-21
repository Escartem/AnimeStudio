#include "pch.h"
#include "ACLLibrary.h"

struct DatabaseSettings : public acl::default_database_settings
{
	static constexpr acl::compressed_tracks_version16 version_supported() { return acl::compressed_tracks_version16::vHoYo; }
};

struct TransformDecompressionSettings : public acl::default_transform_decompression_settings
{
	using database_settings_type = DatabaseSettings;
	static constexpr acl::compressed_tracks_version16 version_supported() { return acl::compressed_tracks_version16::vHoYo; }
};

struct ScalarDecompressionSettings : public acl::default_hoyo_scalar_decompression_settings
{
	using database_settings_type = DatabaseSettings;
	static constexpr acl::compressed_tracks_version16 version_supported() { return acl::compressed_tracks_version16::vHoYo; }
};

#define TRANSFORM_TRACK_SIZE 10

class TransformTrackWriter : public acl::track_writer
{
private:
	int frame = 0;
	uint32_t num_transform_tracks;
	uint32_t num_scalar_tracks;
	float* m_outputBuffer;

public:
	TransformTrackWriter(float* outputBuffer, int num_transform_tracks, int num_scalar_tracks) : num_scalar_tracks(num_scalar_tracks), num_transform_tracks(num_transform_tracks), m_outputBuffer(outputBuffer) {}

	void set_frame(int new_frame) {
		frame = new_frame;
	}

	RTM_FORCE_INLINE void RTM_SIMD_CALL write_float1(uint32_t track_index, rtm::scalarf_arg0 value) {
		float* frame_output_buffer = m_outputBuffer + ((TRANSFORM_TRACK_SIZE * num_transform_tracks) + num_scalar_tracks) * frame;
		spdlog::debug("Write float {}", (void*)frame_output_buffer);
		rtm::scalar_store(value, &frame_output_buffer[track_index]);
	}

	RTM_FORCE_INLINE void RTM_SIMD_CALL write_rotation(uint32_t track_index, rtm::quatf_arg0 rotation)
	{
		float* frame_output_buffer = m_outputBuffer + ((TRANSFORM_TRACK_SIZE * num_transform_tracks) + num_scalar_tracks) * frame;
		spdlog::debug("Write rotation {}", (void*)frame_output_buffer);
		rtm::quat_store(rotation, &frame_output_buffer[track_index * TRANSFORM_TRACK_SIZE]);
	}

	RTM_FORCE_INLINE void RTM_SIMD_CALL write_translation(uint32_t track_index, rtm::vector4f_arg0 translation)
	{
		float* frame_output_buffer = m_outputBuffer + ((TRANSFORM_TRACK_SIZE * num_transform_tracks) + num_scalar_tracks) * frame;
		spdlog::debug("Write translation {}", (void*)frame_output_buffer);
		rtm::vector_store3(translation, &frame_output_buffer[(track_index * TRANSFORM_TRACK_SIZE) + 4]);
	}

	RTM_FORCE_INLINE void RTM_SIMD_CALL write_scale(uint32_t track_index, rtm::vector4f_arg0 scale)
	{
		float* frame_output_buffer = m_outputBuffer + ((TRANSFORM_TRACK_SIZE * num_transform_tracks) + num_scalar_tracks) * frame;
		spdlog::debug("Write scale {}", (void*)frame_output_buffer);
		rtm::vector_store3(scale, &frame_output_buffer[(track_index * TRANSFORM_TRACK_SIZE) + 7]);
	}
};

static acl::ansi_allocator ansi_allocator = acl::ansi_allocator();

void DecompressTracksZZZ(const acl::compressed_tracks* transform_tracks, const acl::compressed_tracks* scalar_tracks, const acl::compressed_database* database, const uint8_t* bulk_data, DecompressedClip* decompressedClip) {
	acl::iallocator& allocator = ansi_allocator;

	acl::database_context<DatabaseSettings> database_context;

	if (bulk_data != nullptr) {
		// TODO: This is slightly incorrect as the bulk data should be different between tiers. However, ZZZ only uses 1 tier, so it's not relevant for this special case.
		// I suspect if this breaks in the future it will be because they move to using two tiers that are concatenated together in the stream attached to the AnimationClip.
		ACL_ASSERT(database->get_bulk_data_size(acl::quality_tier::medium_importance) == 0, "Support for multiple streamers is not implemented.");
		acl::null_database_streamer medium_streamer(bulk_data, database->get_bulk_data_size(acl::quality_tier::medium_importance));
		acl::null_database_streamer low_streamer(bulk_data, database->get_bulk_data_size(acl::quality_tier::lowest_importance));
		spdlog::info("Initializing database with stripped bulk data");
		database_context.initialize(allocator, *database, medium_streamer, low_streamer);
		// The lowest importance data is the highest fidelity data
		database_context.stream_in(acl::quality_tier::lowest_importance);
	}
	else {
		spdlog::info("Initializing database with integral bulk data");
		database_context.initialize(allocator, *database);
	}

	acl::decompression_context<TransformDecompressionSettings> transform_context;
	if (transform_tracks != nullptr) {
		spdlog::info("Initializing transform context from tracks: buf_sz: {}", transform_tracks->get_size());
		transform_context.initialize(*transform_tracks, database_context);
	}

	acl::decompression_context<ScalarDecompressionSettings> scalar_context;
	if (scalar_tracks != nullptr) {
		spdlog::info("Initializing scalar context from tracks: buf_sz: {}", scalar_tracks->get_size());
		scalar_context.initialize(*scalar_tracks);
	}

	decompressedClip->Reset();
	float step = 0.0f;
	uint32_t num_transform_tracks = 0;
	uint32_t num_scalar_tracks = 0;
	if (transform_context.is_initialized()) {
		ACL_ASSERT(transform_tracks != nullptr, "Transform context is initialized with null tracks!");
		const auto num_samples = transform_tracks->get_num_samples_per_track();
		const auto num_tracks = transform_tracks->get_num_tracks();
		num_transform_tracks = num_tracks;
		decompressedClip->TimesCount += num_samples;
		decompressedClip->ValuesCount += 10 * num_samples * num_tracks;
		step = 1.0f / transform_tracks->get_sample_rate();
	}
	if (scalar_context.is_initialized()) {
		ACL_ASSERT(scalar_tracks != nullptr, "Scalar context is initialized with null tracks!");
		const auto num_samples = scalar_tracks->get_num_samples_per_track();
		const auto num_tracks = scalar_tracks->get_num_tracks();
		num_scalar_tracks = num_tracks;
		if (decompressedClip->TimesCount == 0) {
			decompressedClip->TimesCount += num_samples;
		}
		decompressedClip->ValuesCount += num_samples * num_tracks;
		if (step == 0.0f) {
			step = 1.0f / scalar_tracks->get_sample_rate();
		}
	}

	decompressedClip->Times = new float[decompressedClip->TimesCount] { 0.0f };
	decompressedClip->Values = new float[decompressedClip->ValuesCount] { 0.0f };

	TransformTrackWriter writer(decompressedClip->Values, num_transform_tracks, num_scalar_tracks);

	spdlog::info("Decompressing {} frames of {} tracks ({} curves)...", decompressedClip->TimesCount, (num_transform_tracks * 3) + num_scalar_tracks, decompressedClip->ValuesCount);
	for (int i = 0; i < decompressedClip->TimesCount; i++) {
		spdlog::info("Decompressing frame {}", i);
		float timestep = static_cast<float>(i) * step;
		decompressedClip->Times[i] = timestep;
		writer.set_frame(i);

		if (transform_context.is_initialized()) {
			transform_context.seek(timestep, acl::sample_rounding_policy::none);
			transform_context.decompress_tracks(writer);
		}

		if (scalar_context.is_initialized()) {
			// TODO
		}
	}
}

void Dispose(DecompressedClip* decompressedClip) {
	decompressedClip->Dispose();
}