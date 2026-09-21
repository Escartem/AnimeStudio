#include "stdafx.h"
#include <algorithm>
#include <vector>
#include "compress.h"
#include "compr_util.h"
#include "compr_entropy.h"
#include "qsort.h"
#include "match_hasher.h"
#include "compr_match_finder.h"
#include "compr_leviathan.h"
#include "compr_kraken.h"
#include "compr_mermaid.h"

int ilog2round(uint v) {
  union { float f; uint32 u; };
  f = (float)v;
  return ((u + 0x257D86) >> 23) - 127;
}

void *LzScratchBlock::Allocate(int wanted_size) {
  if (!ptr) {
    size = wanted_size;
    ptr = new uint8[wanted_size];
  } else {
    assert(wanted_size <= size);
  }
  return ptr;
}

LzScratchBlock::~LzScratchBlock() {
  delete[](uint8*)ptr;
}

void SetupCompressionOptions(CompressOptions *copts) {
  memset(copts, 0, sizeof(CompressOptions));
  copts->maxLocalDictionarySize = 0x400000;
}

int GetHashBits(int src_len, int level, const CompressOptions *copts, int A, int B, int C, int D) {
  int len = clamp(src_len, INT_MIN, INT_MAX);
  if (copts->seekChunkReset && len > copts->seekChunkLen)
    len = copts->seekChunkLen;
  int bits = ilog2round(len);
  if (level > 2) {
    bits = clamp(bits, C, D);
  } else {
    bits = clamp(bits - 1, A, B);
  }
  if (copts->hashBits > 0) {
    if (copts->hashBits <= 100) {
      bits = clamp(std::min(bits, copts->hashBits), 12, 26);
    } else {
      bits = clamp(copts->hashBits - 100, 8, 28);
    }
  }
  return bits;
}

int GetCompressedBufferSizeNeeded(int size) {
  return size + 274 * ((size + 0x3FFFF) / 0x40000);
}

#define IS_BYTE_INSIDE(a, b, c) ((uint8)((a) - (b)) <= ((c) - (b)))

bool IsBlockProbablyText(const uint8 *p, const uint8 *p_end) {
  const uint8 *ps = p;
  // Skip in the middle of continuation
  while (p < p_end && IS_BYTE_INSIDE(*p, 0x80, 0xBF))
    p++;
  if (p - ps > 3)
    return false;
  while (p < p_end) {
    uint8 c = *p++;
    if (!IS_BYTE_INSIDE(c, 9, 0x7e)) {
      // First utf8 is always 110xxxxx with x != 0 up to 0xf
      if (!IS_BYTE_INSIDE(c, 0xc1, 0xf4))
        return false;
      size_t left = p_end - p;
      if (c < 0xe0) {
check_1:// 1 more byte
        if (left == 0)
          break;
        p += 1;
        if (!IS_BYTE_INSIDE(p[-1], 0x80, 0xBF))
          return false;
      } else if (c < 0xf0) {
check_2:// 2 more bytes starting with 0b10
        if (left < 2)
          goto check_1;
        p += 2;
        if (!(IS_BYTE_INSIDE(p[-2], 0x80, 0xBF) && IS_BYTE_INSIDE(p[-1], 0x80, 0xBF)))
          return false;
      } else {
        // 3 more bytes starting with 0b10
        if (left < 3)
          goto check_2;
        p += 3;
        if (!(IS_BYTE_INSIDE(p[-3], 0x80, 0xBF) && IS_BYTE_INSIDE(p[-2], 0x80, 0xBF) && IS_BYTE_INSIDE(p[-1], 0x80, 0xBF)))
          return false;
      }
    }
  }
  return true;
}

bool IsProbablyText(const uint8 *p, size_t size) {
  const uint8 *p_org = p;
  if (size / 32 < 32)
    return false;
  int score = 0;
  for (size_t i = 0; i != 32; i++, p += size / 32)
    score += IsBlockProbablyText(p, p_org + std::min<size_t>(p + 32 - p_org, size));
  return score >= 14;
}


uint8 *WriteBlockHdr(uint8 *dst, int compr_id, bool crc, bool keyframe, bool uncompressed) {
  dst[0] = 12 + uncompressed * 0x40 + keyframe * 0x80;
  dst[1] = compr_id + crc * 0x80;
  return dst + 2;
}

bool AreAllBytesEqual(const uint8 *data, size_t size) {
  if (size <= 1)
    return true;
  uint8 v = data[0];
  for (size_t i = 1; i != size; i++)
    if (data[i] != v)
      return false;
  return true;
}

uint8 *WriteMemsetQuantumHeader(uint8 *dst, uint8 v) {
  *dst++ = 7;
  *dst++ = 0xff;
  *dst++ = 0xff;
  *dst++ = v;
  return dst;
}

uint8 *WriteBE24(uint8 *dst, uint32 v) {
  *dst++ = (uint8)(v >> 16);
  *dst++ = (uint8)(v >> 8);
  *dst++ = (uint8)(v >> 0);
  return dst;
}

void SubtractBytesUnsafe(uint8 *dst, const uint8 *src, size_t len, size_t neg_offs) {
  if (len > 8) {
    size_t loops = (len + 7) / 16;
    do {
      simde_mm_storeu_si128((simde__m128i *)dst,
                       simde_mm_sub_epi8(simde_mm_loadu_si128((const simde__m128i *)src),
                                         simde_mm_loadu_si128((const simde__m128i *)&src[neg_offs])));
      src += 16, dst += 16;
    } while (--loops);
  }
  simde_mm_storel_epi64((simde__m128i *)dst,
                   simde_mm_sub_epi8(simde_mm_loadl_epi64((const simde__m128i *)src),
                                     simde_mm_loadl_epi64((const simde__m128i *)&src[neg_offs])));
}

void SubtractBytes(uint8 *dst, const uint8 *src, size_t len, size_t neg_offs) {
  for (; len >= 16; len -= 16, src += 16, dst += 16)
    simde_mm_storeu_si128((simde__m128i *)dst,
                     simde_mm_sub_epi8(simde_mm_loadu_si128((const simde__m128i *)src),
                                       simde_mm_loadu_si128((const simde__m128i *)&src[neg_offs])));
  for (; len; len--, src++, dst++)
    dst[0] = src[0] - src[neg_offs];
}

int CompressQuantum(LzCoder *coder, LzTemp *lztemp, MatchLenStorage *mls,
                                   uint8 *src, int src_size,
                                   uint8 *dst, uint8 *dst_end, int offset, float *cost_ptr) {
  uint8 *dst_org = dst, *src_org = src;
  uint8 *src_end = src + src_size;
  float total_cost = 0;

  while (src < src_end) {
    int round_bytes = std::min<int>(src_end - src, coder->quantum_blocksize);

    float memset_cost = GetTime_Memset(coder->platforms, round_bytes) * coder->speed_tradeoff + round_bytes + 3;

    if (round_bytes >= 32) {
      if (AreAllBytesEqual(src, round_bytes)) {
        float memset_cost = kInvalidCost;
        int n = EncodeArrayU8_Memset(dst, dst_end, src, round_bytes, coder->entropy_opts, coder->speed_tradeoff, coder->platforms, &memset_cost);
        src += round_bytes;
        dst += n;
        total_cost += memset_cost;
      } else {
        float lzcost = kInvalidCost;
        int chunk_type = -1, n;
        if (coder->codec_id == kCompressorLeviathan) {
          n = LeviathanDoCompress(coder, lztemp, mls, src, round_bytes, dst + 3, dst_end, offset + src - src_org, &chunk_type, &lzcost);
        } else if (coder->codec_id == kCompressorKraken) {
          n = KrakenDoCompress(coder, lztemp, mls, src, round_bytes, dst + 3, dst_end, offset + src - src_org, &chunk_type, &lzcost);
        } else if (coder->codec_id == kCompressorMermaid || coder->codec_id == kCompressorSelkie) {
          n = MermaidDoCompress(coder, lztemp, mls, src, round_bytes, dst + 3, dst_end, offset + src - src_org, &chunk_type, &lzcost);
        } else {
          return -1;
        }
        lzcost += 3;

        int plain_huff_n = 0;
        float plain_huff_cost = kInvalidCost;
        uint8 *plain_huff_dst = NULL;
        if (coder->check_plain_huffman) {
          plain_huff_cost = std::min(memset_cost, lzcost);

          plain_huff_dst = (uint8*)lztemp->scratch2.Allocate((round_bytes >> 4) + round_bytes + 256);
          int plain_huff_dst_size = lztemp->scratch2.size;

          plain_huff_n = EncodeArrayU8(plain_huff_dst, plain_huff_dst + plain_huff_dst_size,
                                       src, round_bytes,
                                       coder->entropy_opts, coder->speed_tradeoff, coder->platforms,
                                       &plain_huff_cost,
                                       coder->compression_level, NULL);
          if (plain_huff_n < 0 || plain_huff_n >= round_bytes)
            plain_huff_cost = kInvalidCost;
        }
        if (lzcost < memset_cost && lzcost <= plain_huff_cost && n >= 0 && n < round_bytes) {
          dst = WriteBE24(dst, n | chunk_type << 19 | 0x800000) + n;
          total_cost += lzcost;
        } else if (memset_cost <= plain_huff_cost) {
          WriteBE24(dst, round_bytes | 0x800000);
          memcpy(dst + 3, src, round_bytes);
          dst += 3 + round_bytes;
          total_cost += memset_cost;
        } else {
          memcpy(dst, plain_huff_dst, plain_huff_n);
          dst += plain_huff_n;
          total_cost += plain_huff_cost;
        }
      }
    } else {
      WriteBE24(dst, round_bytes | 0x800000);
      memcpy(dst + 3, src, round_bytes);
      dst += 3 + round_bytes;
      total_cost += memset_cost;
    }
    src += round_bytes;
  }
  *cost_ptr = total_cost;
  return dst - dst_org;
}

int CompressBlocks(LzCoder *coder, LzTemp *lzcomp,
                               uint8 *src, uint8 *dst, int src_size,
                               uint8 *dict_base, uint8 *window_base,
                               LRMTable *lrm, MatchLenStorage *mls) {
  uint8 *dst_org = dst;
  uint8 *src_org = src;
  uint8 *src_end = src + src_size;

  while (src < src_end) {
    int round_bytes = std::min<int>(src_end - src, 0x40000);
    int bufsize_needed = GetCompressedBufferSizeNeeded(round_bytes);

    bool keyframe = (src == dict_base);

    uint8 *dst_blk = WriteBlockHdr(dst, coder->compressor_file_id, coder->opts->makeQHCrc, keyframe, false);

    if (AreAllBytesEqual(src, round_bytes)) {
      dst = WriteMemsetQuantumHeader(dst_blk, src[0]);
    } else {
      uint8 *dst_qh = WriteBE24(dst_blk, round_bytes - 1);
      float cost = kInvalidCost;
      int qn = CompressQuantum(coder, lzcomp, mls, src, round_bytes, dst_qh, dst + bufsize_needed, src - window_base, &cost);

      float memset_cost = GetTime_Memset(coder->platforms, round_bytes) * coder->speed_tradeoff + round_bytes + 3;
      if (qn < 0 || qn >= round_bytes || cost > memset_cost) {
        dst = WriteBlockHdr(dst, coder->compressor_file_id, 0, keyframe, true);
        memcpy(dst, src, round_bytes);
        dst += round_bytes;
      } else {
        WriteBE24(dst_blk, qn - 1);
        dst = dst_qh + qn;
      }
    }

    src += round_bytes;
  }
  return dst - dst_org;
}

static const int lrm_step_size = 6;
static const int lrm_hash_length = 8;
static const int lrm_hash_lookup_bits_base = 10;

int Compress(LzCoder *coder, uint8 *src_in, uint8 *dst, int src_size, uint8 *src_window_base, LRMCascade *lrm_org) {
  LRMCascade *lrm = lrm_org;
  uint8 *dst_org = dst;
  LzTemp lztemp;

  if (!src_window_base || coder->opts->seekChunkReset)
    src_window_base = src_in;

  if (coder->compression_level >= 5) {
    int total_window = src_in + src_size - src_window_base;
    int src_size_left = src_size;

    int local_dictsize = coder->opts->maxLocalDictionarySize;
    if (!coder->limit_local_dictsize && !lrm)
      local_dictsize = std::max(local_dictsize, 0x4000000);

    int bytes_per_round;

    if (total_window > local_dictsize) {
      bytes_per_round = local_dictsize >> 1;
      if (coder->opts->makeLongRangeMatcher && lrm_org == NULL && coder->compression_level >= 5) {
        int lrmsize = bytes_per_round * ((total_window + bytes_per_round - 1) / bytes_per_round) - local_dictsize;
        lrm = LRM_AllocateCascade(src_window_base, lrmsize, lrm_step_size, lrm_hash_lookup_bits_base, 0, bytes_per_round, lrm_hash_length);
      }
    } else {
      bytes_per_round = total_window;
    }

    uint8 *src_cur = src_in;
    while (src_size_left > 0) {
      int round_bytes = std::min(src_size_left, bytes_per_round);
      uint8 *cur_window_base = src_window_base;
      if (src_cur == src_window_base)
        round_bytes = std::min(src_size_left, local_dictsize);
      if (src_size_left <= 5 * bytes_per_round / 4)
        round_bytes = src_size_left;
      int dictsize = 0;
      if (round_bytes < local_dictsize)
        dictsize = std::min<int>(std::min<int>(bytes_per_round, src_cur - src_window_base), local_dictsize - round_bytes);
      if (coder->opts->dictionarySize > 0)
        dictsize = std::min(dictsize, coder->opts->dictionarySize);

      uint8 *dict_base = src_cur - dictsize;
      LRMTable lrm_table_buf, *lrm_table = NULL;

      if (lrm && dict_base > src_window_base) {
        lrm_table = &lrm_table_buf;
        LRM_GetRanges(lrm, &lrm_table_buf, dict_base, src_cur);
      }
      MatchLenStorage *mls = MatchLenStorage::Create(round_bytes + 1, 8.0f);
      mls->window_base = src_cur;

      if (coder->compression_level >= 6) {
        FindMatchesSuffixTrie(dict_base, src_cur - dict_base + round_bytes, mls, 4, src_cur - dict_base, lrm_table);
      } else {
        FindMatchesHashBased(dict_base, src_cur - dict_base + round_bytes, mls, 4, src_cur - dict_base, lrm_table);
      }

      int n = CompressBlocks(coder, &lztemp, src_cur, dst, round_bytes, dict_base, cur_window_base, lrm_table, mls);
      if (mls)
        MatchLenStorage::Destroy(mls);

      dst += n;
      src_cur += round_bytes;
      src_size_left -= round_bytes;
    }

    if (lrm != lrm_org)
      LRM_FreeCascade(lrm);
  } else {
    int n = CompressBlocks(coder, &lztemp, src_in, dst, src_size, src_window_base, src_window_base, NULL, NULL);
    dst += n;
  }
  return dst - dst_org;
}

const CompressOptions *GetDefaultCompressOpts(int level) {
  static const CompressOptions compress_options_level5 = { 0, 0, 0, 0x40000, 0, 0, 0x100, 4, 0, 0x400000, 1, 0 };
  static const CompressOptions compress_options_level4 = { 0, 0, 0, 0x40000, 0, 0, 0x100, 2, 0, 0x400000, 1, 0 };
  static const CompressOptions compress_options_level0 = { 0, 0, 0, 0x40000, 0, 0, 0x100, 1, 0, 0x400000, 0, 0 };
  return (level >= 5) ? &compress_options_level5 : (level >= 4) ? &compress_options_level4 : &compress_options_level0;
}

int CompressBlock_Leviathan(uint8 *src_in, uint8 *dst_in, int src_size, int level,
                            const CompressOptions *compressopts, uint8 *src_window_base, LRMCascade *lrm) {
  LzCoder coder = { 0 };
  if (!compressopts)
    compressopts = GetDefaultCompressOpts(level);

  if (src_window_base == NULL)
    src_window_base = src_in;
  
  coder.last_chunk_type = -1;
  SetupEncoder_Leviathan(&coder, src_size, level, compressopts, src_window_base, src_in);
  int n = Compress(&coder, src_in, dst_in, src_size, src_window_base, lrm);
  return n;
}

int CompressBlock_Kraken(uint8 *src_in, uint8 *dst_in, int src_size, int level,
                         const CompressOptions *compressopts, uint8 *src_window_base, LRMCascade *lrm) {
  LzCoder coder = { 0 };
  if (!compressopts)
    compressopts = GetDefaultCompressOpts(level);

  if (src_window_base == NULL)
    src_window_base = src_in;

  coder.last_chunk_type = -1;
  SetupEncoder_Kraken(&coder, src_size, level, compressopts, src_window_base, src_in);
  int n = Compress(&coder, src_in, dst_in, src_size, src_window_base, lrm);
  return n;
}

int CompressBlock_Mermaid(int codec_id, uint8 *src_in, uint8 *dst_in, int src_size, int level,
                          const CompressOptions *compressopts, uint8 *src_window_base, LRMCascade *lrm) {
  LzCoder coder = { 0 };
  if (!compressopts)
    compressopts = GetDefaultCompressOpts(level);

  if (src_window_base == NULL)
    src_window_base = src_in;

  coder.last_chunk_type = -1;
  SetupEncoder_Mermaid(&coder, codec_id, src_size, level, compressopts, src_window_base, src_in);
  int n = Compress(&coder, src_in, dst_in, src_size, src_window_base, lrm);
  return n;
}


int CompressBlock(int codec_id, uint8 *src_in, uint8 *dst_in, int src_size, int level,
                  const CompressOptions *compressopts, uint8 *src_window_base, LRMCascade *lrm) {
  switch (codec_id) {
  case kCompressorKraken: return CompressBlock_Kraken(src_in, dst_in, src_size, level, compressopts, src_window_base, lrm);
  case kCompressorLeviathan: return CompressBlock_Leviathan(src_in, dst_in, src_size, level, compressopts, src_window_base, lrm);
  case kCompressorMermaid:
  case kCompressorSelkie: return CompressBlock_Mermaid(codec_id, src_in, dst_in, src_size, level, compressopts, src_window_base, lrm);
  default:
    return -1;
  }

}

