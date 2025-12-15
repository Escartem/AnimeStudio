#include "pch.h"
#include "AnimeStudio.ACLNative/AnimeStudio.ACLNative.h"

void DecompressedClip::Reset() {
	Values = nullptr;
	ValuesCount = 0;
	Times = nullptr;
	TimesCount = 0;
}

void DecompressedClip::Dispose() {
	delete Values;
	delete Times;
	Reset();
}

void DecompressTracksWithDatabaseAndData(const acl::compressed_tracks* tracks, const acl::compressed_database* database, const uint8_t* bulk_data_med, const uint8_t* bulk_data_low, DecompressedClip* decompressedClip) {

}

void DecompressTracksWithDatabase(const acl::compressed_tracks* tracks, DecompressedClip* decompressedClip) {
	DecompressTracksWithDatabaseAndData(tracks, nullptr, nullptr, nullptr, decompressedClip);
}

void DecompressTracks(const acl::compressed_tracks* tracks, const acl::compressed_database* database, DecompressedClip* decompressedClip) {
	DecompressTracksWithDatabaseAndData(tracks, database, nullptr, nullptr, decompressedClip);
}

void Dispose(DecompressedClip* decompressedClip) {
	decompressedClip->Dispose();
}