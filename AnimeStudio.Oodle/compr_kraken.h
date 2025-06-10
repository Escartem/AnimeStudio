#pragma once

#include "compress.h"

struct LzCoder;

void SetupEncoder_Kraken(LzCoder *coder, int src_len, int level,
                         const CompressOptions *copts,
                         const uint8 *src_base, const uint8 *src_start);

int KrakenDoCompress(LzCoder *coder, LzTemp *lztemp, MatchLenStorage *mls_unused,
                     const uint8 *src, int src_size,
                     uint8 *dst, uint8 *dst_end,
                     int start_pos, int *chunk_type_ptr, float *cost_ptr);
