#pragma once
#include <vector>
#include "compr_util.h"

struct MatchLenStorage;

struct LengthAndOffset {
  int length;
  int offset;
  void Set(int l, int o) { length = l; offset = o; }
  bool operator<(LengthAndOffset &o) { return (length == o.length) ? offset < o.offset : length > o.length; }
};

static inline int GetLazyScore(LengthAndOffset a, LengthAndOffset b) {
  int bits_a = (a.offset > 0) ? BSR(a.offset) + 3 : 0;
  int bits_b = (b.offset > 0) ? BSR(b.offset) + 3 : 0;
  return 4 * (a.length - b.length) - 4 - (bits_a - bits_b);
}

struct HashPos {
  uint32 hash;
  int position;

  bool operator<(const HashPos &o) { return hash < o.hash; }
};

struct LRMTable;
struct LRMCascade;

enum {
  kCompressorKraken = 8,
  kCompressorMermaid = 9,
  kCompressorSelkie = 11,
  kCompressorLeviathan = 13,
};

struct CompressOptions {
  int unknown_0;
  int min_match_length;
  int seekChunkReset;
  int seekChunkLen;
  int unknown_1;
  int dictionarySize;
  int spaceSpeedTradeoffBytes;
  int unknown_2;
  int makeQHCrc;
  int maxLocalDictionarySize;
  int makeLongRangeMatcher;
  int hashBits;
};

struct LzScratchBlock {
  void *ptr;
  int size;

  LzScratchBlock() : ptr(0), size(0) {}
  void *Allocate(int wanted_size);
  ~LzScratchBlock();
};

struct LzTemp {
  LzScratchBlock scratch0;
  LzScratchBlock scratch1;
  LzScratchBlock scratch2;
  LzScratchBlock lztoken_scratch;
  LzScratchBlock lztoken2_scratch;
  LzScratchBlock allmatch_scratch;
  LzScratchBlock kraken_states;
  LzScratchBlock states;
  LzScratchBlock scratch8;
};

struct LzCoder {
  int codec_id;
  int compression_level;
  int platforms;
  const CompressOptions *opts;
  int quantum_blocksize;
  void *hasher;
  int max_matches_to_consider;
  float speed_tradeoff;
  int entropy_opts;
  int encode_flags;
  char limit_local_dictsize;
  char check_plain_huffman;
  int compressor_file_id;
  LzScratchBlock lvsymstats_scratch;
  int last_chunk_type;
};

int EncodeLzOffsets(uint8 *dst, uint8 *dst_end, uint8 *u8_offs, uint32 *u32_offs, int offs_count,
                              int opts, float speed_tradeoff, int platforms,
                              float *cost_ptr, int min_match_len, bool use_offset_modulo_coding,
                              int *offs_encode_type_ptr, int level, HistoU8 *histo_ptr, HistoU8 *histolo_ptr);

int WriteLzOffsetBits(uint8 *dst, uint8 *dst_end, uint8 *u8_offs, uint32 *u32_offs,
                                           int offs_count, int offs_encode_type,
                                           uint32 *u32_len, int u32_len_count,
                                           int flag_ignore_u32_length, size_t a10);


int CompressBlock(int codec_id, uint8 *src_in, uint8 *dst_in, int src_size, int level,
                  const CompressOptions *compressopts, uint8 *src_window_base, LRMCascade *lrm);

int GetHashBits(int src_len, int level, const CompressOptions *copts, int A, int B, int C, int D);
void ConvertHistoToCost(const HistoU8 &src, uint *dst, int extra, int q=255);

void SubtractBytes(uint8 *dst, const uint8 *src, size_t len, size_t neg_offs);
void SubtractBytesUnsafe(uint8 *dst, const uint8 *src, size_t len, size_t neg_offs);

template<typename T, int MaxPreload = 0x4000000>
void CreateLzHasher(LzCoder *coder, const uint8 *src_base, const uint8 *src_start, int hash_bits, int min_match_len = 0) {
  T *hasher = new T;
  coder->hasher = hasher;
  hasher->AllocateHash(hash_bits, min_match_len);
  if (src_start == src_base) {
    hasher->SetBaseWithoutPreload(src_start);
  } else {
    const CompressOptions *copts = coder->opts;
    int preload_len = src_start - src_base;

    if (coder->compression_level >= 5 && copts->makeLongRangeMatcher)
      preload_len = std::min(preload_len, copts->maxLocalDictionarySize);

    if (copts->dictionarySize > 0)
      preload_len = std::min(preload_len, copts->dictionarySize);
    preload_len = std::min(preload_len, MaxPreload);

    int pos = src_start - src_base;
    if (!copts->seekChunkReset || ((pos & 0x3FFFF || pos & (copts->seekChunkLen - 1)) && pos <= copts->seekChunkLen)) {
      hasher->SetBaseAndPreload(src_base, src_start, preload_len);
    } else {
      hasher->SetBaseWithoutPreload(src_start);
    }
  }
}
