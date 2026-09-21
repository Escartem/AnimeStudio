// This file is not GPL. It may be used for educational purposes only.
#include "stdafx.h"
#include "compr_entropy.h"
#include "compr_util.h"
#include <algorithm>
#include <vector>
#include "qsort.h"

#pragma warning (disable: 4018)

float CombineCostComponents(int platforms, float a, float b, float c, float d) {
  if ((platforms & 0xf) == 0)
    return (a + b + c + d) * 0.25f;
  int n = 0;
  float sum = 0;
  if (platforms & 1) sum += c * 0.762f, n++;
  if (platforms & 2) sum += a * 1.130f, n++;
  if (platforms & 4) sum += d * 1.310f, n++;
  if (platforms & 8) sum += b * 0.961f, n++;
  return sum / n;
}

float CombineCostComponents1(int platforms, float v, float a, float b, float c, float d) {
  return CombineCostComponents(platforms, v * a, v * b, v * c, v * d);
}

float CombineCostComponents1A(int platforms, float v, float a, float b, float c, float d,
                              float x, float y, float z, float w) {
  return CombineCostComponents(platforms, v * a + x, v * b + y, v * c + z, v * d + w);
}

float GetTime_SingleHuffman(int platforms, int count, int numsyms) {
  return CombineCostComponents(
    platforms,
    1880.931f + count * 3.243f + numsyms * 10.960f,
    2219.6531f + count * 2.993f + numsyms * 24.622f,
    2889.8579f + count * 2.468f + numsyms * 21.296f,
    2029.866f + count * 2.699f + numsyms * 8.459f);
}

float GetTime_DoubleHuffman(int platforms, int count, int numsyms) {
  return CombineCostComponents(
    platforms,
    2029.917f + count * 2.436f + numsyms * 10.792f,
    2540.026f + count * 2.087f + numsyms * 20.994f,
    3227.433f + count * 2.501f + numsyms * 18.925f,
    2084.978f + count * 1.875f + numsyms * 8.9510f);
}

float GetTime_AdvRLE(int platforms, int src_size) {
  return CombineCostComponents1A(platforms, src_size,
                                 0.172f, 0.282f, 0.377f, 0.161f,
                                 284.970f, 326.121f, 388.669f, 274.267f);
}

float GetTime_Memset(int platforms, int src_size) {
  return CombineCostComponents1A(platforms, src_size,
                                 0.125f, 0.171f, 0.256f, 0.083f,
                                 28.0f, 53.0f, 58.0f, 29.0f);
}

void CountBytesHistoU8(const uint8 *data, size_t data_size, HistoU8 *histo) {
  // todo: optimize
  memset(histo, 0, sizeof(HistoU8));
  for (size_t i = 0; i < data_size; i++)
    histo->count[data[i]]++;
}

uint GetHistoSum(const uint *a, size_t n) {
  // todo: optimize
  uint sum = 0;
  for (size_t i = 0; i < n; i++)
    sum += a[i];
  return sum;
}

uint GetHistoSum(const HistoU8 &h) {
  // todo: optimize
  uint sum = 0;
  for (size_t i = 0; i < 256; i++)
    sum += h.count[i];
  return sum;
}

uint GetHistoMax(const HistoU8 &histo) {
  // todo: optimize
  uint m = 0;
  for (size_t i = 0; i < 256; i++)
    m = std::max(m, histo.count[i]);
  return m;
}

void ConvertHistoToCost(const HistoU8 &src, uint *dst, int extra, int q) {
  int total_count = 256 + 4 * GetHistoSum(src);
  int bits = 32 - BSR(total_count);
  int base = (bits << 13) - GetLog2Interpolate(total_count << bits);
  int sum_of_bits = 0;
  for (size_t i = 0; i < 256; i++) {
    int count = src.count[i] * 4 + 1;
    bits = 32 - BSR(count);
    int bp = (32 * ((bits << 13) - GetLog2Interpolate(count << bits) - base)) >> 13;
    sum_of_bits += count * bp;
    dst[i] = bp + extra;
  }
  if (sum_of_bits > q * total_count) {
    for (size_t i = 0; i < 256; i++)
      dst[i] = 8 * 32 + extra;
  }
}

uint GetHistoCostApprox(const uint *histo, size_t arrsize, int histo_sum) {
  if (histo_sum <= 1)
    return 40;

  int factor = 0x40000000u / histo_sum;
  uint zeros_run = 0, nonzero_entries = 0;
  uint32 bit_usagez = 0, bit_usage = 0;
  uint64 bit_usagef = 0;
  for (size_t i = 0; i != arrsize; i++) {
    uint32 v = histo[i];
    if (!v) {
      zeros_run++;
      continue;
    }
    nonzero_entries++;
    if (zeros_run) {
      bit_usagez += 2 * BSR(zeros_run + 1) + 1;
      zeros_run = 0;
    } else {
      bit_usagez += 1;
    }
    bit_usage += BSR(v) * 2 + 1;
    bit_usagef += v * (uint64)kLog2LookupTable[factor * v >> 17];
  }
  if (nonzero_entries == 1)
    return 6 * 8;
  bit_usagez += 2 * BSR(zeros_run + 1) + 1;

  bit_usagez = std::min<uint>(bit_usagez, 8 * nonzero_entries);
  return (int)(bit_usagef >> 13) + bit_usage + bit_usagez + 5 * 8;
}

uint GetHistoCostApprox(const HistoU8 &h, int histo_sum) {
  return GetHistoCostApprox(h.count, 256, histo_sum);
}

float GetCost_SingleHuffman(const HistoU8 &histo, int histo_sum, float speed_tradeoff, int platforms) {
  double a = GetTime_SingleHuffman(platforms, histo_sum, 128);
  uint b = GetHistoCostApprox(histo, histo_sum);
  return a * speed_tradeoff + b * 0.125f;
}


#include "log_lookup.h"

static const uint16 kSomeLookup[65] = {
  0, 183, 364, 541, 716, 889, 1059, 1227, 1392, 1555, 1716, 1874,
  2031, 2186, 2338, 2489, 2637, 2784, 2929, 3072, 3214, 3354, 3492,
  3629, 3764, 3897, 4029, 4160, 4289, 4417, 4543, 4668, 4792, 4914,
  5036, 5156, 5274, 5392, 5509, 5624, 5738, 5851, 5963, 6074, 6184,
  6293, 6401, 6508, 6614, 6719, 6823, 6926, 7029, 7130, 7231, 7330,
  7429, 7527, 7625, 7721, 7817, 7912, 8006, 8099, 8192,
};

int GetLog2Interpolate(uint x) {
  return kSomeLookup[x >> 26] + ((((x >> 10) & 0xFFFF) * (kSomeLookup[(x >> 26) + 1] - kSomeLookup[x >> 26]) + 0x8000) >> 16);
}


int EncodeArrayU8_Memcpy(uint8 *dst, uint8 *dst_end, const uint8 *src, int size) {
  if (size > 0x3FFFF)
    return -1;
  if (dst_end - dst < size + 3)
    return -1;
  dst[0] = (uint8)(size >> 16);
  dst[1] = (uint8)(size >> 8);
  dst[2] = (uint8)(size >> 0);
  memcpy(dst + 3, src, size);
  return size + 3;
}

int EncodeArrayU8(uint8 *dst, uint8 *dst_end, const uint8 *src, int src_size, int encode_opts, float speed_tradeoff, int platforms, float *cost_ptr, int level, HistoU8 *histo_ptr) {
  HistoU8 histo;

  if (src_size > 32) {
    CountBytesHistoU8(src, src_size, &histo);
    if (histo_ptr)
      *histo_ptr = histo;
    return EncodeArrayU8WithHisto(dst, dst_end, src, src_size, histo, encode_opts, speed_tradeoff, platforms, cost_ptr, level);
  } else {
    *cost_ptr = src_size + 3;
    return EncodeArrayU8_Memcpy(dst, dst_end, src, src_size);
  }
}

int MakeCompactChunkHdr(uint8 *src, int size, float *cost) {
  int size_org = size;
  if (size > 4095 + 5)
    return size;
  Kraken_GetBlockSize(src, &src[size], &size, 0x20000);

  uint chunk_type = *src >> 4;
  if (chunk_type == 0) {
    if (size > 4095)
      return size_org;
    src[0] = 0x80 | (size >> 8);
    src[1] = (uint8)size;
    memmove(src + 2, src + 3, size);
    *cost -= 1;
    return size + 2;
  } else {
    int csize = size_org - 5;
    if (csize > 1023)
      return size_org;
    int dsize = size - csize - 1;
    if (dsize > 1023)
      return size_org;
    *(uint32*)src = _byteswap_ulong((((chunk_type | 0x8) << 20) + (dsize << 10) + csize) << 8);
    memmove(src + 3, src + 5, csize);
    *cost -= 2;
    return csize + 3;
  }
}

int EncodeArrayU8CompactHeader(uint8 *dst, uint8 *dst_end, const uint8 *src, int src_size, int opts, float speed_tradeoff, int platforms, float *cost_ptr, int level, HistoU8 *histo) {
  int n = EncodeArrayU8(dst, dst_end, src, src_size, opts, speed_tradeoff, platforms, cost_ptr, level, histo);
  return n >= 0 ? MakeCompactChunkHdr(dst, n, cost_ptr) : -1;
}

uint8 *WriteChunkHeader(uint8 *dst, int mode, int dsize, int csize) {
  dst[0] = (mode << 4) + ((dsize - 1) >> 14);
  *(uint32*)(dst + 1) = _byteswap_ulong(((dsize - 1) << 18) + csize);
  return dst + 5 + csize;
}

int EncodeArrayU8_MaybeConcat(uint8 *dst, uint8 *dst_end, const uint8 *src, int src_size, int opts, float speed_tradeoff, int platforms, float *cost_ptr, int level, HistoU8 *histo, int part_size) {
  if (!part_size || part_size == src_size || src_size <= 32)
    return EncodeArrayU8(dst, dst_end, src, src_size, opts, speed_tradeoff, platforms, cost_ptr, level, histo);

  float cost1 = kInvalidCost;
  float cost2 = kInvalidCost;
  int n1 = EncodeArrayU8CompactHeader(dst + 6, dst_end, src, part_size,
                                      opts & ~kEntropyOpt_MultiArray,
                                      speed_tradeoff, platforms, &cost1, level, 0);
  int n2 = EncodeArrayU8CompactHeader(dst + n1 + 6, dst_end, src + part_size, src_size - part_size,
                                      opts & ~kEntropyOpt_MultiArray,
                                      speed_tradeoff, platforms, &cost2, level, 0);

  int total_bytes = 6 + n1 + n2;
  float total_cost = 6.0f + cost1 + cost2;

  WriteChunkHeader(dst, 5, src_size, total_bytes - 5);
  dst[5] = 2;
  *cost_ptr = total_cost;

  int n = EncodeArrayU8(dst, dst_end, src, src_size, opts, speed_tradeoff, platforms, cost_ptr, level, histo);
  if (n <= 0) {
    if (histo)
      CountBytesHistoU8(src, src_size, histo);
    return total_bytes;
  }
  return n;
}

int EncodeArrayU8_Memset(uint8 *dst, uint8 *dst_end, const uint8 *src, int src_size, int opts, float speed_tradeoff, int platforms, float *cost_ptr) {
  if (src_size < 6) {
    *cost_ptr = src_size + 3;
    return EncodeArrayU8_Memcpy(dst, dst_end, src, src_size);
  }
  int dst_size = dst_end - dst;
  if (opts & kEntropyOpt_SupportsShortMemset) {
    if (dst_size < 6)
      return -1;
    float cost = GetTime_Memset(platforms, src_size) * speed_tradeoff + 6;
    if (cost < *cost_ptr) {
      *cost_ptr = cost;
      dst[5] = *src;
      return WriteChunkHeader(dst, 3, src_size, 1) - dst;
    }
  } else {
    if (dst_size < 8)
      return -1;
    float cost = GetTime_Memset(platforms, src_size) * speed_tradeoff + 8;
    if (cost < *cost_ptr) {
      *cost_ptr = cost;
      uint8 v = *src;
      dst[5] = 0;
      dst[6] = 0x40 | (v >> 2);
      dst[7] = v << 6;
      return WriteChunkHeader(dst, 2, src_size, 3) - dst;
    }
  }
  return -1;
}

static const uint8 *ScanForNextRLE3(const uint8 *src, const uint8 *src_end) {
  while (src < src_end) {
    simde__m128i v0 = simde_mm_loadu_si128((simde__m128i*)src);
    simde__m128i v1 = simde_mm_loadu_si128((simde__m128i*)(src + 1));
    simde__m128i v2 = simde_mm_loadu_si128((simde__m128i*)(src + 2));
    int eq_mask = simde_mm_movemask_epi8(simde_mm_and_si128(simde_mm_cmpeq_epi8(v0, v1), simde_mm_cmpeq_epi8(v0, v2)));
    if (eq_mask != 0)
      return src + BSF(eq_mask);
    src += 16;
  }
  return src;
}

static const uint8 *GetRLELength(const uint8 *src, const uint8 *src_end, const uint8 *safe_end) {
  uint64 v0 = *(uint64*)src;
  uint64 v1 = *(uint64*)(src + 1);
  simde__m128i p;

  if (v0 != v1)
    return src + (BSF64(v0 ^ v1) >> 3) + 1;

  if (src_end - src >= 25)
    src += 8;

  p = simde_mm_loadu_si128((simde__m128i*)src);
  src += 1;

  while (src < safe_end) {
    int mask = simde_mm_movemask_epi8(simde_mm_cmpeq_epi8(simde_mm_loadu_si128((simde__m128i *)src), p));
    if (mask != 0xffff)
      return src + BSF(~mask);
    src += 16;
  }
  while (src < src_end && *src == (uint8)v0)
    src++;
  return src;
}

static inline void CopyBytesFastOverflow(uint8 *dst, const uint8 *src, size_t n) {
  uint8 *dst_end = dst + n;
  do {
    simde_mm_storeu_si128((simde__m128i*)dst, simde_mm_loadu_si128((simde__m128i*)src));
    dst += 16, src += 16;
  } while (dst < dst_end);
}

static inline uint8 *WriteShortLrlRle(uint8 *dst_b_ptr, uint lrl, uint rlel) {
  if (lrl > 15) {
    *--dst_b_ptr = 0, lrl -= 15;
    *--dst_b_ptr = 16 * rlel | (15 - lrl);
  } else if (rlel > 15) {
    *--dst_b_ptr = 16 * (rlel >> 1) | (15 - lrl);
    *--dst_b_ptr = 16 * (rlel - (rlel >> 1)) | 0xF;
  } else {
    *--dst_b_ptr = 16 * rlel | (15 - lrl);
  }
  return dst_b_ptr;
}

int EncodeArray_AdvRLE(uint8 *dst, uint8 *dst_end, const uint8 *src, int src_size, float speed_tradeoff, int platforms, float *cost_ptr, int opts, int level) {
  const uint8 *src_end = src + src_size;
  const uint8 *safe_end = src + (src_size >= 18 ? src_size - 18 : 0);
  const uint8 *start = src;

  uint8 *dst_b_ptr = dst_end;
  uint8 *dst_f_ptr = dst + 1;
  uint8 last_rle_byte = 0;
  *dst = 0;

  while (src < safe_end) {
    const uint8 *first_rle = ScanForNextRLE3(src, safe_end);
    if (first_rle >= safe_end)
      break;

    src = GetRLELength(first_rle, src_end, safe_end);

    uint lrl = first_rle - start;
    uint rlel = src - first_rle;

    if (dst_b_ptr - dst_f_ptr < lrl + 18)
      return -1;

    if (*first_rle != last_rle_byte) {
      if (rlel < 8)
        continue;
      last_rle_byte = *first_rle;
      *--dst_b_ptr = 1;
      *dst_f_ptr++ = last_rle_byte;
    }
    CopyBytesFastOverflow(dst_f_ptr, start, lrl);
    dst_f_ptr += lrl;
    start = src;

    if ((lrl <= 30 && rlel <= 15) || (lrl <= 15 && rlel <= 30)) {
      dst_b_ptr = WriteShortLrlRle(dst_b_ptr, lrl, rlel);
      continue;
    }

    // handle very long literal lengths, lrl is always < 0x40 after this.
    if (lrl >= 0x40) {
      if (lrl < 0x4f)
        *--dst_b_ptr = 0, lrl -= 15;
      while (lrl >= 0x40) {
        if (dst_b_ptr - dst_f_ptr < 4)
          return -1;
        uint n = std::min(0x700u, lrl >> 6);
        *--dst_b_ptr = (uint8)(((n - 1) >> 8) + 2);
        *--dst_b_ptr = (uint8)(n - 1);
        lrl -= n << 6;
      }
    }

    uint rle_big = rlel >> 7;
    rlel &= 0x7f;

    if (rlel >= 3 && ((lrl <= 30 && rlel <= 15) || (lrl <= 15 && rlel <= 30))) {
      dst_b_ptr = WriteShortLrlRle(dst_b_ptr, lrl, rlel);
    } else if (lrl | rlel) {
      uint n = lrl | rlel << 6;
      *--dst_b_ptr = (uint8)((n >> 8) + 16);
      *--dst_b_ptr = (uint8)n;
    }

    while (rle_big) {
      if (dst_b_ptr - dst_f_ptr < 2)
        return -1;
      uint n = std::min(0x700u, rle_big);
      *--dst_b_ptr = (uint8)(((n - 1) >> 8) + 9);
      *--dst_b_ptr = (uint8)(n - 1);
      rle_big -= n;
    }
  }

  uint lrl = src_end - start;
  if (lrl) {
    if (dst_b_ptr - dst_f_ptr < lrl + 16)
      return -1;
    memmove(dst_f_ptr, start, lrl);
    dst_f_ptr += lrl;

    if (lrl >= 0x40) {
      if (lrl < 0x4f)
        *--dst_b_ptr = 0, lrl -= 15;
      while (lrl >= 0x40) {
        if (dst_b_ptr - dst_f_ptr < 4)
          return -1;
        uint n = std::min(0x700u, lrl >> 6);
        *--dst_b_ptr = (uint8)(((n - 1) >> 8) + 2);
        *--dst_b_ptr = (uint8)(n - 1);
        lrl -= n << 6;
      }
    }

    if (lrl) {
      *--dst_b_ptr = (uint8)((lrl >> 8) + 16);
      *--dst_b_ptr = (uint8)lrl;
    }
  }

  uint size_f = dst_f_ptr - (dst + 1);
  uint size_b = dst_end - dst_b_ptr;
  int mode = 0;
  float cost = kInvalidCost;
  int outbytes = 0;
  uint8 *temp = NULL;

  if (opts & kEntropyOpt_RLEEntropy) {
    HistoU8 histo;
    if (size_f >= 32 && size_b + size_f <= 0xc000) {
      temp = new uint8[size_f];

      CountBytesHistoU8(dst + 1, size_f, &histo);
      if (GetHistoMax(histo) == size_f) {
        cost = GetTime_Memset(platforms, size_f) * speed_tradeoff + 6;
        temp[0] = dst[1];
        mode = 3;
        outbytes = 1;
      } else {
        float huff_cost = size_f + 1;
        int huff_mode = 0;
        int n = EncodeArray_Huff(temp, temp + size_f, dst + 1, size_f, histo, speed_tradeoff, platforms, &huff_cost, &huff_mode, opts, level);
        if (n >= 0 && n < size_f) {
          mode = huff_mode;
          outbytes = n;
          cost = huff_cost;
        }
      }
    }
  }

  if (mode && cost < size_f + 1) {
    WriteChunkHeader(dst, mode, size_f, outbytes);
    memmove(dst + 5, temp, outbytes);
    int after_compact = MakeCompactChunkHdr(dst, outbytes + 5, &cost);
    memmove(dst + after_compact, dst_b_ptr, size_b);
    outbytes = after_compact + size_b;
    cost += size_b;
  } else {
    memmove(dst_f_ptr, dst_b_ptr, size_b);
    outbytes = size_f + size_b + 1;
    cost = outbytes;
  }
  *cost_ptr = cost + GetTime_AdvRLE(platforms, src_size) * speed_tradeoff + 5;
  delete temp;
  return outbytes;
}

class HuffBuilder {
public:
  HuffBuilder();

  void BuildCodeLens(const HistoU8 &histo, int src_size, int limit, bool use_package_merge);
  enum {
    kAlphabetSize = 256,
    kMaxCodeLen = 16,
  };

  void WriteTableNew(BitWriter64<1> *bits);
  void WriteTableOld(BitWriter64<1> *bits);

  void AssignSyms();

  struct Entry {
    uint16 sym;
    uint16 count;

    bool operator<(const Entry &e) { return count < e.count; }
  };
  int num_symbols_, highest_sym_;
  int max_code_len_, min_code_len_;
  int numsyms_of_len_[kMaxCodeLen];
  uint base_syms_[kMaxCodeLen + 1];
  uint8 sym2len_[kAlphabetSize];
  union {
    Entry ents_[kAlphabetSize + 1]; // +1 to account for terminator
    uint32 sym2bits_[kAlphabetSize];
  };

private:
  static const HistoU8*ScaleCounts(const HistoU8 &counts, HistoU8 *out_counts);

  void LimitCodeLensPackageMerge(Entry *ents, const HistoU8 &histo, int limit);
  void LimitCodeLensHeuristic(Entry *ents, const HistoU8 &histo, int limit, Entry *he);
  void CalcNumsymsEtc();
};

HuffBuilder::HuffBuilder() {

}

const HistoU8 *HuffBuilder::ScaleCounts(const HistoU8 &histo, HistoU8 *out_counts) {
  uint total_counts = 0, max_count = 0;
  size_t best_index = 0;
  for (size_t i = 0; i < 256; i++) {
    total_counts += histo.count[i];
    if (histo.count[i] > max_count) {
      max_count = histo.count[i];
      best_index = i;
    }
  }
  if (total_counts <= 0xffff)
    return &histo;
  float ratio = (float)0xffff / total_counts;
  uint new_total_counts = 0;
  for (size_t i = 0; i < 256; i++) {
    uint v = histo.count[i];
    if (v) {
      v = std::max(std::min((uint)(ratio * v + 0.5f), 65535u), 1u);
      new_total_counts += v;
    }
    out_counts->count[i] = v;
  }
  if (new_total_counts > 0xffff) {
    assert(out_counts->count[best_index] >= new_total_counts - 0xffff);
    out_counts->count[best_index] -= new_total_counts - 0xffff;
  }
  return out_counts;
}

static void RadixSortEnts(HuffBuilder::Entry *begin, size_t num_syms) {
  int counts[2][256] = { 0 };

  for (size_t i = 0; i != num_syms; i++) {
    counts[0][(uint8)(begin[i].count)]++;
    counts[1][(uint8)(begin[i].count >> 8)]++;
  }

  int y_buf[256];
  HuffBuilder::Entry syms[256];

  size_t iteration = 0;

  HuffBuilder::Entry *read = begin, *write = syms;

  do {
    int *count_cur = counts[iteration];
    for (size_t i = 0, k = 0; k != num_syms; i++) {
      y_buf[i] = k;
      k += count_cur[i];
    }

    if (iteration == 0) {
      for (size_t i = 0; i != num_syms; i++)
        write[y_buf[read[i].count & 0xFF]++] = read[i];
    } else {
      if (*count_cur == num_syms) {
        memmove(write, read, 4 * num_syms);
        break;
      } else {
        for (size_t i = 0; i != num_syms; i++)
          write[y_buf[read[i].count >> 8]++] = read[i];
      }
    }
    read = syms, write = begin;
  } while (++iteration < 2);
}


void HuffBuilder::BuildCodeLens(const HistoU8 &histo_in, int src_size, int limit, bool use_package_merge) {
  HistoU8 scaled_counts;
  

  memset(sym2len_, 0, sizeof(sym2len_));
  memset(numsyms_of_len_, 0, sizeof(numsyms_of_len_));

  // Rescale the counts so that the sum is 65535 or less to make it fit inside the uint16
  const HistoU8 *histop = (src_size > 65535) ? ScaleCounts(histo_in, &scaled_counts) : &histo_in;

  Entry *e = ents_, *ents = ents_;
  for (size_t i = 0; i < kAlphabetSize; i++) {
    if (histop->count[i]) {
      e->count = (uint16)histop->count[i];
      e->sym = (uint8)i;
      e++;
    }
  }
  uint n = e - ents;

  num_symbols_ = n;
  highest_sym_ = n ? e[-1].sym : 0;

  if (n < 2)
    return;

  // use radix sort for long arrays?
  if (n <= 32)
    MySort(ents, e);
  else
    RadixSortEnts(ents, n);

  // Inplace huffman table construction using moffat's algorithm
  ents[0].count += ents[1].count;
  unsigned int r = 0, s = 2, t;

  for (t = 1; t < n - 1; t++) {
    int sum;
    if (s >= n || ents[r].count < ents[s].count) {
      sum = ents[r].count;
      ents[r].count = t;
      r++;
    } else {
      sum = ents[s].count;
      s++;
    }
    if (s >= n || (r < t && ents[r].count < ents[s].count)) {
      sum += ents[r].count;
      ents[r].count = t;
      r++;
    } else {
      sum += ents[s].count;
      s++;
    }
    ents[t].count = sum;
  }

  ents[(size_t)n - 2].count = 0;
  for (t = n - 2; t--; )
    ents[t].count = ents[ents[t].count].count + 1;

  unsigned int a = 1, u = 0, d = 0, x = n - 1;
  t = n - 2;
  do {
    while ((int)t >= 0 && ents[t].count == d)
      u += 1, t -= 1;
    for (; (int)a > (int)u; a--, x--)
      ents[x].count = d;
    a = 2 * u, d++, u = 0;
  } while (a > 0);

  int min_code_len = ents[(size_t)n - 1].count;
  int max_code_len = ents[0].count;

 
  if (max_code_len <= limit) {
    // common case when it fits inside of limit
    for (size_t i = 0; i != n; i++) {
      unsigned len = ents[i].count;
      sym2len_[ents[i].sym] = len;
      numsyms_of_len_[len]++;
    }
    max_code_len_ = max_code_len;
    min_code_len_ = min_code_len;
  } else {
    // Max code length too long, use slow limited package merge or fast heuristic
    if (use_package_merge)
      LimitCodeLensPackageMerge(ents, *histop, limit);
    else
      LimitCodeLensHeuristic(ents, *histop, limit, ents);
  }
  assert(max_code_len_ <= limit);
}

void HuffBuilder::LimitCodeLensPackageMerge(Entry *ents, const HistoU8 &histo, int limit) {
  Entry *e = ents;
  for (size_t i = 0; i < kAlphabetSize; i++) {
    if (histo.count[i]) {
      e->count = (uint16)histo.count[i];
      e->sym = (uint16)i;
      e++;
    }
  }

  int num_symbols = e - ents;
  num_symbols_ = num_symbols;
  e->count = 0xFFFF;

  if (num_symbols <= 32)
    MySort(ents, e);
  else
    RadixSortEnts(ents, num_symbols);

  int ents_per_codelen = 2 * (num_symbols - 1);
  Entry *tempmem = new Entry[ents_per_codelen * (limit - 1)];

  Entry *huffents[16];
  huffents[0] = ents;
  for (int i = 1; i < limit; i++)
    huffents[i] = tempmem + (i - 1) * ents_per_codelen;

  int numsyms[16] = { num_symbols };

  for (int i = 1; i < limit; i++) {
    int x = 0, k;
    Entry *e = ents;
    for (k = 0; k < ents_per_codelen; k++) {
      int num = e->count, countsum;
      if (x + 1 >= numsyms[i - 1] || (countsum = huffents[i - 1][x].count + huffents[i - 1][x + 1].count) > num) {
        if (num == 0xFFFF)
          break;
        huffents[i][k].count = num;
        huffents[i][k].sym = e->sym;
        e++;
      } else {
        huffents[i][k].count = countsum;
        huffents[i][k].sym = x | 0x8000;
        x += 2;
      }
    }
    numsyms[i] = k;
  }

  memset(sym2len_, 0, sizeof(sym2len_));

  int loop_ctr = numsyms[limit - 1];
  for (int i = limit - 1; i >= 0; i--) {
    int loop_ctr_next = 0;
    for (int j = 0; j < loop_ctr; j++) {
      uint16 sym = huffents[i][j].sym;
      if (sym & 0x8000)
        loop_ctr_next = sym - 0x7FFE;
      else
        sym2len_[sym]++;
    }
    loop_ctr = loop_ctr_next;
  }
  CalcNumsymsEtc();
  delete[] tempmem;
}

void HuffBuilder::CalcNumsymsEtc() {
  memset(numsyms_of_len_, 0, sizeof(numsyms_of_len_));
  for (size_t i = 0; i != 256; i++) {
    if (sym2len_[i])
      numsyms_of_len_[sym2len_[i]]++;
  }
  int min_len = 1;
  while (!numsyms_of_len_[min_len])
    min_len++;
  min_code_len_ = min_len;

  int max_len = 31;
  while (!numsyms_of_len_[max_len])
    max_len--;
  max_code_len_ = max_len;
}

template<bool SoftRestriction>
static inline int FindSymbolToInc(int limit, int *B, uint32 *shifts, const HistoU8 &histo, uint64 kraft_sum, HuffBuilder::Entry *he) {
  int best_idx = -1, best_score = 0x80000000;
  for (int j = 1; j < limit; j++) {
    if (B[j - 1] != B[j] &&
      (!SoftRestriction || shifts[j + 1] < 2 * (uint)kraft_sum)) {
      int score = -(int)(histo.count[he[B[j]].sym] << j);
      if (score > best_score) {
        best_score = score;
        best_idx = B[j];
      }
    }
  }
  return best_idx;
}

template<bool SoftRestriction>
static inline int FindSymbolToDec(int limit, int *B, uint32 *shifts, const HistoU8 &histo, uint64 kraft_sum, HuffBuilder::Entry *he) {
  int best_idx = -1, best_score = 0x80000000;
  for (int j = 1; j < limit; j++) {
    if (B[j] != B[j + 1] &&
      (SoftRestriction ? (shifts[j + 1] < 2u * -(int)kraft_sum) : (kraft_sum + shifts[j + 1] <= 0x100000000ull))) {
      int score = histo.count[he[B[j] - 1].sym] << (j + 1);
      if (score > best_score) {
        best_score = score;
        best_idx = B[j] - 1;
      }
    }
  }
  return best_idx;
}


void HuffBuilder::LimitCodeLensHeuristic(Entry *ents, const HistoU8 &histo, int limit, Entry *he) {
  limit = std::min(limit, 15);

  uint32 shifts[16];
  int B[18];

  int q = limit + 1, best_idx;

  for (int i = 0; i <= q; i++) {
    shifts[i] = 1 << (32 - i);
  }

  int num_symbols = num_symbols_;
  uint64 kraft_sum = 0;
  B[q] = 0;

  // Truncate code lengths that are too long
  for (int i = 0; i < num_symbols; i++) {
    int codelen = he[i].count;
    if (codelen > limit)
      he[i].count = codelen = limit;
    kraft_sum += shifts[codelen];
    while (q > codelen)
      B[--q] = i;
  }
  while (q > 0)
    B[--q] = num_symbols;

  while (kraft_sum != 0x100000000ull) {
    if (kraft_sum > 0x100000000ull) {
      if ((best_idx = FindSymbolToInc<true>(limit, B, shifts, histo, kraft_sum, he)) >= 0) {
        int count = he[best_idx].count++;
        kraft_sum -= shifts[count + 1];
        B[count]++;
        continue;
      }
      break;
    } else {
      // kraft too low
      int best_idx = FindSymbolToDec<true>(limit, B, shifts, histo, kraft_sum, he);
      int count = he[best_idx].count--;
      kraft_sum += shifts[count];
      B[count - 1]--;
    }
  }
  while (kraft_sum > 0x100000000ull) {
    best_idx = FindSymbolToInc<false>(limit, B, shifts, histo, kraft_sum, he);
    int count = he[best_idx].count++;
    kraft_sum -= shifts[count + 1];
    B[count]++;
  }
  while (kraft_sum < 0x100000000ull) {
    int best_idx = FindSymbolToDec<false>(limit, B, shifts, histo, kraft_sum, he);
    int count = he[best_idx].count--;
    kraft_sum += shifts[count];
    B[count - 1]--;
  }

  uint max_code_len = ents[0].count;
  uint min_code_len = ents[num_symbols - 1].count;
  for (size_t i = 0; i != num_symbols; i++) {
    unsigned len = ents[i].count;
    sym2len_[ents[i].sym] = len;
    numsyms_of_len_[len]++;
  }
  max_code_len_ = max_code_len;
  min_code_len_ = min_code_len;
}

uint GetSymbolRiceSpaceUsageForK(uint *histo, int histo_size, int k) {
  uint result = 0;
  for (int i = 0; i < histo_size; i++) {
    if (histo[i])
      result += histo[i] * ((i >> k) + k + 1);
  }
  return result;
}

int EncodeSymRange(uint8 *rice, uint8 *bits, uint8 *bitcount, int used_syms, int *range, int numrange) {
  if (used_syms >= 256)
    return 0;
  int which = (*range == 0);
  int num = (*range != 0) + 2 * ((numrange - 3) / 2);
  range += (*range == 0);
  for (int i = 0; i < num; i++) {
    int v = range[i];
    int ebit = ~which++ & 1;
    v += (1 << ebit) - 1;
    int nb = BSR(v >> ebit);
    rice[i] = nb;
    nb += ebit;
    bits[i] = v & ((1 << nb) - 1);
    bitcount[i] = (uint8)nb;
  }
  return num;
}

void WriteNumSymRange(BitWriter64<1> *bits, int num_symrange, int used_syms) {
  if (used_syms == 256)
    return;

  int x = std::min(used_syms, 257 - used_syms);
  int nb = BSR(2 * x - 1) + 1;
  int base = (1 << nb) - 2 * x;
  if (num_symrange >= base) {
    bits->Write(num_symrange + base, nb);
  } else {
    bits->Write(num_symrange, nb - 1);
  }
}

void WriteManyRiceCodes(BitWriter64<1> *bits, const uint8 *data, size_t num) {
  BitWriter64<1> tmp = *bits;
  for (size_t i = 0; i != num; i++) {
    uint v = data[i];
    for (; v >= 24; v -= 24)
      tmp.Write(0, 24);
    tmp.Write(1, v + 1);
  }
  *bits = tmp;
}

void SplitRiceLowBits(uint8 *rest, uint8 *forced_bits, const uint8 *input, size_t num, int k) {
  uint8 mask = (1 << k) - 1;
  for (size_t i = 0; i != num; i++) {
    uint v = input[i];
    forced_bits[i] = v & mask;
    rest[i] = v >> k;
  }
}

void WriteRiceLowBits(BitWriter64<1> *bits, const uint8 *data, size_t num, int k) {
  if (k == 0)
    return;
  BitWriter64<1> tmp = *bits;
  for (size_t i = 0; i != num; i++)
    tmp.Write(data[i], k);
  *bits = tmp;
}

void WriteSymRangeLowBits(BitWriter64<1> *bits, const uint8 *data, const uint8 *bitcount, size_t num) {
  BitWriter64<1> tmp = *bits;
  for (size_t i = 0; i != num; i++)
    tmp.Write(data[i], bitcount[i]);
  *bits = tmp;
}

void HuffBuilder::WriteTableNew(BitWriter64<1> *bits) {
  int range[kAlphabetSize + 4], *range_cur;

  int sym = 0;
  int symlen_count = 0;
  uint8 enc_symlen[256];
  uint lencount[21] = { 0 };

  while (sym < kAlphabetSize && sym2len_[sym] == 0)
    sym++;

  range_cur = range;
  *range_cur++ = sym;

  int avg_4x = 32;

  while (sym < kAlphabetSize) {
    int symstart = sym, len;

    while (sym < kAlphabetSize && (len = sym2len_[sym]) != 0) {
      sym++;
      int t = len - ((avg_4x + 2) >> 2);
      avg_4x += t;
      uint32 x = EncodeZigZag32(t);
      enc_symlen[symlen_count] = x;
      lencount[x]++;
      symlen_count++;
    }
    *range_cur++ = sym - symstart;

    symstart = sym;
    while (sym < kAlphabetSize && sym2len_[sym] == 0)
      sym++;
    *range_cur++ = sym - symstart;
  }

  int symlen_k = 0, symlen_k_score = INT_MAX;
  for (int i = 0; i < 4; i++) {
    int space = GetSymbolRiceSpaceUsageForK(lencount, 21, i);
    if (space < symlen_k_score) {
      symlen_k_score = space;
      symlen_k = i;
    }
  }
  bits->WriteNoFlush(symlen_k, 2);

  uint8 sr_rice[256], sr_bits[256], sr_bitcount[256];

  int symrange_count = EncodeSymRange(sr_rice, sr_bits, sr_bitcount, symlen_count, range, range_cur - range);
  bits->Write(symlen_count - 1, 8);
  WriteNumSymRange(bits, symrange_count, symlen_count);

  uint8 symlen_rice[256], symlen_low[256];

  SplitRiceLowBits(symlen_rice, symlen_low, enc_symlen, symlen_count, symlen_k);

  WriteManyRiceCodes(bits, symlen_rice, symlen_count);
  WriteManyRiceCodes(bits, sr_rice, symrange_count);

  WriteRiceLowBits(bits, symlen_low, symlen_count, symlen_k);
  WriteSymRangeLowBits(bits, sr_bits, sr_bitcount, symrange_count);
}

void HuffBuilder::WriteTableOld(BitWriter64<1> *bits) {
  BitWriter64<1> tmp = *bits;
  if (num_symbols_ > 4) {
    tmp.WriteNoFlush(1, 1);

    // dense legacy encoding
    uint lencount[32] = { 0 };
    int avg_bits_x4 = 32;

    for (int i = 0; i <= highest_sym_; i++) {
      int codelen = sym2len_[i];
      if (codelen) {
        lencount[EncodeZigZag32(codelen - ((avg_bits_x4 + 2) >> 2))]++;
        avg_bits_x4 = codelen + ((3 * avg_bits_x4 + 2) >> 2);
      }
    }
    int symlen_k = 0, symlen_k_score = INT_MAX;
    for (int k = 0; k < 4; k++) {
      int space = GetSymbolRiceSpaceUsageForK(lencount, 32, k);
      if (space < symlen_k_score) {
        symlen_k_score = space;
        symlen_k = k;
      }
    }
    tmp.WriteNoFlush(symlen_k * 2 + (sym2len_[0] != 0), 3);

    avg_bits_x4 = 32;

    int start_pos, pos = 0, num;

    if (sym2len_[0] != 0)
      goto skip_initial_zeros;

    do {
      // count the # of initial zeros
      start_pos = pos;
      while (pos < kAlphabetSize && sym2len_[pos] == 0)
        pos++;
      num = pos - start_pos;
      assert(num > 0);

      // Write out a gamma value for the # of zeros reduced by 1 with one forced bit,
      // ie. 1 => 10, 2 => 11, 3 => 0100, 4 => 0101, etc.
      // Since num_zeros is small we can write it out in one go.
      tmp.Write(num + 1, BSR(((num - 1) >> 1) + 1) * 2 + 2);

      if (pos >= kAlphabetSize)
        break;
skip_initial_zeros:
      // count the # of symbols and write out.
      start_pos = pos;
      while (pos < kAlphabetSize && sym2len_[pos] != 0)
        pos++;
      num = pos - start_pos;
      assert(num > 0);
      // Same type of encoding as for the zeros
      tmp.Write(num + 1, BSR(((num - 1) >> 1) + 1) * 2 + 2);

      // Actually write the symbols
      while (start_pos < pos) {
        int codelen = sym2len_[start_pos];
        uint32 v = EncodeZigZag32(codelen - ((avg_bits_x4 + 2) >> 2));
        // Symbol is encoded as follows. 0-3 forced bits always follow.
        // 0 => 1, 1 => 01, 2 => 001, 3 => 0001.
        tmp.Write((1 << symlen_k) + (v & ((1 << symlen_k) - 1)),
          (v >> symlen_k) + symlen_k + 1);
        avg_bits_x4 = codelen + ((3 * avg_bits_x4 + 2) >> 2);
        start_pos++;
      }
    } while (pos < kAlphabetSize);

  } else {
    // sparse encoding
    tmp.WriteNoFlush(0, 1);
    tmp.WriteNoFlush(num_symbols_, 8);
    if (num_symbols_ == 1) {
      tmp.WriteNoFlush(highest_sym_, 8);
    } else {
      int codelen_bits = (max_code_len_ > 1) ? BSR(max_code_len_ - 1) + 1 : 0;
      tmp.WriteNoFlush(codelen_bits, 3);
      for (size_t i = 0; i < kAlphabetSize; i++) {
        if (sym2len_[i])
          tmp.Write(((uint)i << codelen_bits) | (sym2len_[i] - 1), codelen_bits + 8);
      }
    }
  }
  *bits = tmp;
}

bool IsDoubleHuffmanFaster(int platforms, float speed_tradeoff, int src_size, int num_syms) {
  float time_d = GetTime_DoubleHuffman(platforms, src_size, num_syms);
  float time_s = GetTime_SingleHuffman(platforms, src_size, num_syms);
  return (time_d - time_s) * speed_tradeoff + 6.3125f < 0.0f;
}

uint Huff_ComputeSizeBits(const HistoU8 &histo, int histo_sum, const uint8 *sym2len, int num_syms) {
  // todo: sse
  uint result = 0;
  for (size_t i = 0; i != num_syms; i++)
    result += histo.count[i] * sym2len[i];
  return result;
}

#include "bits_rev_table.h"

void HuffBuilder::AssignSyms() {
  if (num_symbols_ < 2) {
    sym2bits_[highest_sym_] = 0;
    return;
  }
  base_syms_[min_code_len_] = 0;
  uint t = 0;
  for (int i = min_code_len_; i < max_code_len_; i++) {
    t = 2 * (t + numsyms_of_len_[i]);
    base_syms_[i + 1] = t;
  }

  for (int i = 0; i < kAlphabetSize; i++)
    sym2bits_[i] = base_syms_[sym2len_[i]]++;
}

int Huff_WriteDataDoubleEnded(uint8 *dst, uint8 *dst_end, const uint8 *src, int src_size, const uint8 *sym2len, const uint32 *sym2bits) {
  const uint8 *src_end = src + src_size;
  enum { kMaxBitsPerSym = 11 };
  uint8 *temp = new uint8[(src_size * kMaxBitsPerSym + 23) / 24 + 8];
  Bitwriter32<1> bw1(dst + 2), bw3(temp);
  Bitwriter32<-1> bw2(dst_end);

  while (src + 3 <= src_end) {
    bw1.Write(sym2bits[src[0]], sym2len[src[0]]);
    bw2.Write(sym2bits[src[1]], sym2len[src[1]]);
    bw3.Write(sym2bits[src[2]], sym2len[src[2]]);
    bw1.Push32();
    bw2.Push32();
    bw3.Push32();
    src += 3;
  }

  if (src < src_end) {
    bw1.Write(sym2bits[src[0]], sym2len[src[0]]);
    if (src + 1 < src_end)
      bw2.Write(sym2bits[src[1]], sym2len[src[1]]);
  }

  bw1.PushFinal();
  bw2.PushFinal();
  bw3.PushFinal();

  size_t len1 = bw1.ptr - (dst + 2);
  size_t len2 = dst_end - bw2.ptr;
  size_t len3 = bw3.ptr - temp;
  int totlen = 2 + len1 + len2 + len3;
  *(uint16*)dst = (uint16)len1;
  dst += 2 + len1;
  memcpy(dst, temp, len3);
  memmove(dst + len3, bw2.ptr, len2);
  delete[] temp;
  return totlen;
}

int EncodeArray_Huff(uint8 *dst, uint8 *dst_end, const uint8 *src, int src_size, const HistoU8 &histo, float speed_tradeoff, int platforms, float *cost_ptr, int *mode_ptr, int opts, int level) {
  HuffBuilder huff;
  uint8 temp[256];

  huff.BuildCodeLens(histo, src_size, 11, level >= 6);

  int mode = (opts & kEntropyOpt_AllowDoubleHuffman) && IsDoubleHuffmanFaster(platforms, speed_tradeoff, src_size, huff.num_symbols_) ? 4 : 2;
  *mode_ptr = mode;
  float cost = ((mode == 2) ? GetTime_SingleHuffman(platforms, src_size, huff.num_symbols_) : GetTime_DoubleHuffman(platforms, src_size, huff.num_symbols_)) * speed_tradeoff + 5;

  int datasizebytes = 0;
  if (huff.num_symbols_ >= 2) {
    datasizebytes = BITSUP(Huff_ComputeSizeBits(histo, src_size, huff.sym2len_, huff.highest_sym_ + 1)) + 13;
    if (cost + datasizebytes >= *cost_ptr)
      return -1;
  }

  BitWriter64<1> bits(temp);

  if (huff.num_symbols_ > 4 && (opts & kEntropyOpt_SupportsNewHuffman)) {
    bits.WriteNoFlush(2, 2); // 10
    huff.WriteTableNew(&bits);
  } else {
    bits.WriteNoFlush(0, 1); // 0
    huff.WriteTableOld(&bits);
  }
  int tablesizebytes = bits.GetFinalPtr() - temp;

  int totalbytes = datasizebytes + tablesizebytes;
  if (cost + totalbytes >= *cost_ptr ||
    (totalbytes + 8) >= dst_end - dst)
    return -1;

  uint8 *dst_cur = dst;
  memcpy(dst_cur, temp, tablesizebytes);
  dst_cur += tablesizebytes;

  if (huff.num_symbols_ >= 2) {
    huff.AssignSyms();

    int max_sym = huff.highest_sym_ + 1;
    uint32 sym2bits[256];

    for (size_t i = 0; i < max_sym; i++) {
      if (huff.sym2len_[i])
        sym2bits[i] = kReverseBits[huff.sym2bits_[i]] >> (11 - huff.sym2len_[i]);
    }
    if (mode == 4) {
      int half = (src_size + 1) >> 1;
      int n_bytes = Huff_WriteDataDoubleEnded(dst_cur + 3, dst_end, src, half, huff.sym2len_, sym2bits);
      dst_cur[0] = (uint8)n_bytes;
      dst_cur[1] = (uint8)(n_bytes >> 8);
      dst_cur[2] = (uint8)(n_bytes >> 16);
      dst_cur += n_bytes + 3;
      src += half, src_size -= half;
    }
    int n = Huff_WriteDataDoubleEnded(dst_cur, dst_end, src, src_size, huff.sym2len_, sym2bits);
    dst_cur += n;
  }

  *cost_ptr = cost + (dst_cur - dst);
  return dst_cur - dst;
}

int EncodeArrayU8WithHisto(uint8 *dst, uint8 *dst_end, const uint8 *src, int src_size, const HistoU8 &histo, int opts, float speed_tradeoff, int platforms, float *cost_ptr, int level) {
  uint8 *dst_org = dst;
  if (src_size + 5 > dst_end - dst)
    return -1;

  int histo_max_value = GetHistoMax(histo);
  if (src_size != 0 && histo_max_value == src_size)
    return EncodeArrayU8_Memset(dst, dst_end, src, src_size, opts, speed_tradeoff, platforms, cost_ptr);

  float cost_thres = std::min<float>(*cost_ptr, src_size + 3);
  int comp_size = src_size;
  int mode = 0;

  float best_cost = kInvalidCost;

  if (level >= 3 || histo_max_value >= (src_size >> 7)) {
    if (opts & kEntropyOpt_RLE) {
      float costx = GetTime_AdvRLE(platforms, src_size) * speed_tradeoff + 5;
      int rle_size = std::min<int>(cost_thres - costx, dst_end - dst - 5);
      if (rle_size > 0) {
        uint8 *temp = new uint8[rle_size];

        float cost_rle = kInvalidCost;
        int n = EncodeArray_AdvRLE(temp, temp + rle_size, src, src_size, speed_tradeoff, platforms, &cost_rle, opts, level);
        if (n <= rle_size && cost_rle < cost_thres) {
          memcpy(dst + 5, temp, n);
          comp_size = n;
          best_cost = cost_rle;
          cost_thres = cost_rle;
          mode = 3;
        }
        delete[] temp;
      }
    }

    if (src_size >= 32) {
      if (1) { //  no way to turn off huff
        float cost = cost_thres;
        int huff_mode = 0;
        int n_huff = EncodeArray_Huff(dst + 5, dst_end, src, src_size, histo, speed_tradeoff, platforms, &cost, &huff_mode, opts, level);
        if (n_huff <= src_size) {
          if (n_huff >= 0) {
            mode = huff_mode;
            cost_thres = best_cost = cost;
            comp_size = n_huff;
          }
        } else {
          best_cost = kInvalidCost;
        }
      }

      if (opts & kEntropyOpt_tANS) {
        float cost = cost_thres;
        int n = EncodeArrayU8_tANS(dst + 5, dst_end, src, src_size, histo, speed_tradeoff, platforms, &cost);
        if (n >= 0) {
          mode = 1;
          cost_thres = best_cost = cost;
          comp_size = n;
        }
      }

      if ((opts & kEntropyOpt_MultiArray) && (best_cost < *cost_ptr || src_size < 0x20000)) {
        float cost = kInvalidCost;
        int n = EncodeArrayU8_MultiArray(dst + 5, dst_end, src, src_size, histo, level, opts, speed_tradeoff, platforms, cost_thres, &cost);
        if (n >= 0) {
          mode = 5;
          best_cost = cost;
          comp_size = n;
        }
      }
    }
  }

  if (best_cost < src_size + 3) {
    *cost_ptr = best_cost;
    dst = WriteChunkHeader(dst, mode, src_size, comp_size);
  } else {
    if (src_size + 3 >= *cost_ptr || src_size >= 0x40000)
      return -1;
    dst[2] = (uint8)src_size;
    dst[1] = (uint8)(src_size >> 8);
    dst[0] = (uint8)(src_size >> 16);
    memcpy(dst + 3, src, src_size);
    dst += 3 + src_size;
    *cost_ptr = src_size + 3;
  }

  return dst - dst_org;
}
