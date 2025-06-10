#pragma once

#include <stddef.h>
#include <stdint.h>

#if defined _WIN32 || defined __CYGWIN__
#ifdef BUN_BUILD_DLL
#ifdef __GNUC__
#define BUN_DLL_PUBLIC __attribute__ ((dllexport))
#else
#define BUN_DLL_PUBLIC __declspec(dllexport) // Note: actually gcc seems to also supports this syntax.
#endif
#else
#ifdef __GNUC__
#define BUN_DLL_PUBLIC __attribute__ ((dllimport))
#else
#define BUN_DLL_PUBLIC __declspec(dllimport) // Note: actually gcc seems to also supports this syntax.
#endif
#endif
#define BUN_DLL_LOCAL
#else
#if __GNUC__ >= 4
#define BUN_DLL_PUBLIC __attribute__ ((visibility ("default")))
#define BUN_DLL_LOCAL  __attribute__ ((visibility ("hidden")))
#else
#define BUN_DLL_PUBLIC
#define BUN_DLL_LOCAL
#endif
#endif

extern "C" {
	/* BunMem is returned from functions that need to allocate the returned data.
	* It keeps track of its own size and needs to be freed from the application side
	* when the application is finished with it.
	*/
	typedef uint8_t* BunMem;

	struct Bun;
	struct BunIndex;

	struct VfsFile;
	struct Vfs {
		VfsFile* (*open)(Vfs*, char const*);
		void (*close)(Vfs*, VfsFile*);
		int64_t(*size)(Vfs*, VfsFile*);
		int64_t(*read)(Vfs*, VfsFile*, uint8_t* out, int64_t offset, int64_t size);
	};

	BUN_DLL_PUBLIC BunMem BunMemAlloc(size_t size);
	BUN_DLL_PUBLIC int64_t BunMemSize(BunMem mem);
	BUN_DLL_PUBLIC void BunMemFree(BunMem mem);

	BUN_DLL_PUBLIC Bun* BunNew(char const* decompressor_path, char const* decompressor_export);
	BUN_DLL_PUBLIC void BunDelete(Bun* bun);

	BUN_DLL_PUBLIC BunIndex* BunIndexOpen(Bun* bun, Vfs* vfs, char const* bundle_dir);
	BUN_DLL_PUBLIC void BunIndexClose(BunIndex* idx);

	BUN_DLL_PUBLIC int32_t BunIndexLookupFileByPath(BunIndex* idx, char const* path);
	BUN_DLL_PUBLIC BunMem BunIndexExtractFile(BunIndex* idx, int32_t file_id);
	BUN_DLL_PUBLIC BunMem BunIndexExtractBundle(BunIndex* idx, int32_t bundle_id);

	BUN_DLL_PUBLIC int BunIndexBundleInfo(BunIndex const* idx, int32_t bundle_info_id, char const** name, uint32_t* uncompressed_size);
	BUN_DLL_PUBLIC int BunIndexFileInfo(BunIndex const* idx, int32_t file_info_id,
		uint64_t* path_hash, uint32_t* bundle_index_, uint32_t* file_offset_, uint32_t* file_size_);

	BUN_DLL_PUBLIC int BunIndexPathRepInfo(BunIndex const* idx, int32_t path_rep_id,
		uint64_t* hash, uint32_t* offset, uint32_t* size, uint32_t* recursive_size);

	BUN_DLL_PUBLIC BunMem BunIndexPathRepContents(BunIndex const* idx);
    BUN_DLL_PUBLIC int BunIndexPathRepLowercase(BunIndex const *idx);

	BUN_DLL_PUBLIC int32_t BunIndexBundleCount(BunIndex* idx);
	BUN_DLL_PUBLIC int32_t BunIndexBundleIdByName(BunIndex* idx, char const* name);
	BUN_DLL_PUBLIC int32_t BunIndexBundleFileCount(BunIndex* idx, int32_t bundle_id);
	BUN_DLL_PUBLIC BunMem BunIndexBundleName(BunIndex* idx, int32_t bundle_id);

	BUN_DLL_PUBLIC int32_t BunIndexBundleFileOffset(BunIndex* idx, int32_t bundle_id, int32_t file_id);
	BUN_DLL_PUBLIC int32_t BunIndexBundleFileSize(BunIndex* idx, int32_t bundle_id, int32_t file_id);

	/* The BunDecompress family of functions decompresses either individual raw blocks or a full PoE bundle file.
	* They can either decompress into an user-supplied buffer of sufficient size or allocate a buffer for the caller.
	* Allocating functions return an BunMem or NULL.
	* Functions with user-supplied storage returns the resulting size or -1 in case of error.
	* The buffer supplied must have an additional 64 bytes of scratch space at the end.
	*/
	BUN_DLL_PUBLIC int BunDecompressBlock(Bun* bun, uint8_t const* src_data, size_t src_size, uint8_t* dst_data, size_t dst_size);
	BUN_DLL_PUBLIC BunMem BunDecompressBlockAlloc(Bun* bun, uint8_t const* src_data, size_t src_size, size_t dst_size);

	/* If the output buffer supplied to BunDecompressBundle is NULL or of zero size, the function returns the number of bytes needed.
	*/
	BUN_DLL_PUBLIC int64_t BunDecompressBundle(Bun* bun, uint8_t const* src_data, size_t src_size, uint8_t* dst_data, size_t dst_size);
	BUN_DLL_PUBLIC BunMem BunDecompressBundleAlloc(Bun* bun, uint8_t const* src_data, size_t src_size);
}
