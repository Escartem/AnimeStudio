#pragma once
#include <cstdint>
#define AS_API(ret_type) extern "C" __declspec(dllexport) ret_type __cdecl
struct decompressed_clip
{
    float* values;
    int values_count;
    float* times;
    int times_count;
};
// Sized C ABI consumed by HoyoToon Studio's ACL decoder wrapper.
AS_API(void) DecompressTracksZZZ(void* transform_data, int transform_size,
    void* scalar_data, int scalar_size, void* database_data, int database_size,
    void* bulk_data, int bulk_size, decompressed_clip& out);
AS_API(const char*) GetLastDecompressError();
AS_API(const char*) GetLastDecompressInfo();
AS_API(void) Dispose(decompressed_clip& out);
