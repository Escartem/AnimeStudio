#include "pch.h"
#include "AnimeStudio.ACLNative/AnimeStudio.ACLNative.h"
#include "acl/core/ansi_allocator.h"

#include <acl/decompression/decompress.h>
#include "acl/decompression/database/database.h"
#include "acl/decompression/database/database_settings.h"
#include <acl/decompression/database/null_database_streamer.h>

static acl::ansi_allocator Allocator = acl::ansi_allocator();

struct database_transform_decompression_settings : public acl::default_transform_decompression_settings
{
	using database_settings_type = acl::default_database_settings;
};

struct database_scalar_decompression_settings : public acl::default_scalar_decompression_settings
{
	using database_settings_type = acl::default_database_settings;
};

void DecompressedClip::Reset() {
	Values = nullptr;
	ValuesCount = 0;
	Times = nullptr;
	TimesCount = 0;
}

void DecompressedClip::Dispose() {
	deallocate_type_array<float>(Allocator, Times, TimesCount);
	deallocate_type_array<float>(Allocator, Values, ValuesCount);
	Reset();
}

void DecompressTracksWithDatabaseAndData(
	const acl::compressed_tracks* tracks,
	const acl::compressed_database* database,
	const uint8_t* bulk_data_med, const uint8_t* bulk_data_low,
	DecompressedClip& decompressedClip
) {
	acl::error_result database_error, tracks_error;
	
	auto databasse_context = acl::allocate_type<acl::database_context<acl::default_database_settings>>(Allocator);

	auto compressed_database = make_compressed_database(database, &database_error);

	if (database_error.empty())
	{
		acl::null_database_streamer medium_streamer(bulk_data_med, database->get_bulk_data_size(acl::quality_tier::medium_importance));
		acl::null_database_streamer low_streamer(bulk_data_low, database->get_bulk_data_size(acl::quality_tier::lowest_importance));

		databasse_context->initialize(Allocator, *compressed_database, medium_streamer, low_streamer);
		databasse_context->stream_in(acl::quality_tier::medium_importance);
		databasse_context->stream_in(acl::quality_tier::lowest_importance);
	}

	auto compressed_tracks = make_compressed_tracks(tracks, &tracks_error);

	if (tracks_error.empty())
	{
		decompressedClip.Reset();

		if (compressed_tracks->get_track_type() == acl::track_type8::qvvf)
		{
			auto transform_context = acl::make_decompression_context<database_transform_decompression_settings>(Allocator);
			if (databasse_context->is_initialized())
			{
				transform_context->initialize(*compressed_tracks, *databasse_context);
			}
			else
			{
				transform_context->initialize(*compressed_tracks);
			}

			decompressedClip.TimesCount = compressed_tracks->get_num_samples_per_track();
			decompressedClip.ValuesCount = compressed_tracks->get_num_samples_per_track() \
				* compressed_tracks->get_num_tracks() * 10;

			if (transform_context->is_initialized()) {

			}
		}
		else
		{
			auto scalar_context = acl::make_decompression_context<database_scalar_decompression_settings>(Allocator);
			if (databasse_context->is_initialized())
			{
				scalar_context->initialize(*compressed_tracks, *databasse_context);
			}
			else
			{
				scalar_context->initialize(*compressed_tracks);
			}

			decompressedClip.TimesCount = compressed_tracks->get_num_samples_per_track();
			decompressedClip.ValuesCount = compressed_tracks->get_num_samples_per_track() \
				* compressed_tracks->get_num_tracks();

			if (scalar_context->is_initialized()) {

			}
		}
	}

	deallocate_type(Allocator, databasse_context);
}

void DecompressTracksWithDatabase(
	const acl::compressed_tracks* tracks,
	DecompressedClip& decompressedClip
) {
	DecompressTracksWithDatabaseAndData(tracks, nullptr, nullptr, nullptr, decompressedClip);
}

void DecompressTracks(
	const acl::compressed_tracks* tracks,
	const acl::compressed_database* database,
	DecompressedClip& decompressedClip
) {
	DecompressTracksWithDatabaseAndData(tracks, database, nullptr, nullptr, decompressedClip);
}

void Dispose(DecompressedClip& decompressedClip) {
	decompressedClip.Dispose();
}