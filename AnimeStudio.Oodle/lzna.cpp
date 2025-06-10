#include "stdafx.h"


typedef uint16 LznaBitModel;

// State for a 4-bit value RANS model
struct LznaNibbleModel {
  uint16 prob[17];
};

// State for a 3-bit value RANS model
struct Lzna3bitModel {
  uint16 prob[9];
};

// State for the literal model
struct LznaLiteralModel {
  LznaNibbleModel upper[16];
  LznaNibbleModel lower[16];
  LznaNibbleModel nomatch[16];
};

// State for a model representing a far distance
struct LznaFarDistModel {
  LznaNibbleModel first_lo;
  LznaNibbleModel first_hi;
  LznaBitModel second[31];
  LznaBitModel third[2][31];
};

// State for a model representing a near distance
struct LznaNearDistModel {
  LznaNibbleModel first;
  LznaBitModel second[16];
  LznaBitModel third[2][16];
};

// State for model representing the low bits of a distance
struct LznaLowBitsDistanceModel {
  LznaNibbleModel d[2];
  LznaBitModel v;
};

// State for model used for the short lengths for recent matches
struct LznaShortLengthRecentModel {
  Lzna3bitModel a[4];
};

// State for model for long lengths
struct LznaLongLengthModel {
  LznaNibbleModel first[4];
  LznaNibbleModel second;
  LznaNibbleModel third;
};

// Complete LZNA state
struct LznaState {
  uint32 match_history[8];
  LznaLiteralModel literal[4];
  LznaBitModel is_literal[12 * 8];
  LznaNibbleModel type[12 * 8];
  LznaShortLengthRecentModel short_length_recent[4];
  LznaLongLengthModel long_length_recent;
  LznaLowBitsDistanceModel low_bits_of_distance[2];
  LznaBitModel short_length[12][4];
  LznaNearDistModel near_dist[2];
  Lzna3bitModel medium_length;
  LznaLongLengthModel long_length;
  LznaFarDistModel far_distance;
};

static LznaNibbleModel lzna_initializer_4bit = {
  0x0, 0x800, 0x1000, 0x1800, 0x2000, 0x2800, 0x3000, 0x3800, 0x4000, 0x4800, 0x5000, 0x5800, 0x6000, 0x6800, 0x7000, 0x7800, 0x8000,
};

static Lzna3bitModel lzna_initializer_3bit = {
  0x0, 0x1000, 0x2000, 0x3000, 0x4000, 0x5000, 0x6000, 0x7000, 0x8000
};

static void LznaNibbleModel_Init(LznaNibbleModel *d) {
  *d = lzna_initializer_4bit;
}

static void Lzna3bitModel_Init(Lzna3bitModel *d) {
  *d = lzna_initializer_3bit;
}

static void LznaNibbleModel_InitN(LznaNibbleModel *d, int n) {
  do LznaNibbleModel_Init(d++); while (--n);
}

static void LznaLiteralModel_InitN(LznaLiteralModel *d, int n) {
  do {
    LznaNibbleModel_InitN(d->upper, 16);
    LznaNibbleModel_InitN(d->lower, 16);
    LznaNibbleModel_InitN(d->nomatch, 16);
  } while (d++, --n);
}

static void LznaShortLengthRecentModel_InitN(LznaShortLengthRecentModel *d, int n) {
  do {
    Lzna3bitModel_Init(&d->a[0]);
    Lzna3bitModel_Init(&d->a[1]);
    Lzna3bitModel_Init(&d->a[2]);
    Lzna3bitModel_Init(&d->a[3]);
  } while (d++, --n);
}

static void LznaNearDistModel_Init(LznaNearDistModel *d, int n) {
  int i;
  do {
    LznaNibbleModel_Init(&d->first);

    for (i = 0; i < 16; i++) {
      d->second[i] = 0x2000;
      d->third[0][i] = 0x2000;
      d->third[1][i] = 0x2000;
    }

  } while (d++, --n);
}

static void LznaLowBitsDistanceModel_Init(LznaLowBitsDistanceModel *d, int n) {
  do {
    d->v = 0x2000;
    LznaNibbleModel_InitN(d->d, 2);
  } while (d++, --n);
}

static void LznaFarDistModel_Init(LznaFarDistModel *d) {
  int i;
  LznaNibbleModel_Init(&d->first_lo);
  LznaNibbleModel_Init(&d->first_hi);
  for (i = 0; i < 31; i++) {
    d->second[i] = 0x2000;
    d->third[0][i] = 0x2000;
    d->third[1][i] = 0x2000;
  }
}

void LZNA_InitLookup(LznaState *lut) {
  int i, j;

  for (i = 0; i < 4; i++)
    lut->match_history[i + 4] = 1;

  for (i = 0; i < 96; i++)
    lut->is_literal[i] = 0x1000;

  LznaNibbleModel_InitN(lut->type, 96);
  LznaLiteralModel_InitN(lut->literal, 4);
  LznaShortLengthRecentModel_InitN(lut->short_length_recent, 4);

  LznaNibbleModel_InitN(lut->long_length_recent.first, 4);
  LznaNibbleModel_Init(&lut->long_length_recent.second);
  LznaNibbleModel_InitN(&lut->long_length_recent.third, 1);

  for (i = 0; i < 12; i++)
    for (j = 0; j < 4; j++)
      lut->short_length[i][j] = 0x2000;

  LznaNearDistModel_Init(lut->near_dist, 2);
  LznaLowBitsDistanceModel_Init(lut->low_bits_of_distance, 2);
  
  Lzna3bitModel_Init(&lut->medium_length);

  LznaNibbleModel_InitN(lut->long_length.first, 4);  
  LznaNibbleModel_Init(&lut->long_length.second);
  LznaNibbleModel_InitN(&lut->long_length.third, 1);
  LznaFarDistModel_Init(&lut->far_distance);
}

struct LznaBitReader {
  uint64 bits_a, bits_b;
  const uint32 *src, *src_start;
};

// Initialize bit reader with 2 parallel streams. Every decode operation
// swaps the two streams.
static void LznaBitReader_Init(LznaBitReader *tab, const byte *src) {
  int d, n, i;
  uint64 v;
  
  tab->src_start = (uint32*)src;

  d = *src++;
  n = d >> 4;
  assert(n <= 8);
  for (i = 0, v = 0; i < n; i++)
    v = (v << 8) | *src++;
  tab->bits_a = (v << 4) | (d & 0xF);

  d = *src++;
  n = d >> 4;
  assert(n <= 8);
  for (i = 0, v = 0; i < n; i++)
    v = (v << 8) | *src++;
  tab->bits_b = (v << 4) | (d & 0xF);
  tab->src = (uint32*)src;
}

// Renormalize by filling up the RANS state and swapping the two streams
static void __forceinline LznaRenormalize(LznaBitReader *tab) {
  uint64 x = tab->bits_a;
  if (x < 0x80000000)
    x = (x << 32) | *tab->src++;
  tab->bits_a = tab->bits_b;
  tab->bits_b = x;
}

// Read a single bit with a uniform distribution.
static uint32 __forceinline LznaReadBit(LznaBitReader *tab) {
  int r = tab->bits_a & 1;
  tab->bits_a >>= 1;
  LznaRenormalize(tab);
  return r;
}

// Read a number of bits with a uniform distribution.
static uint32 __forceinline LznaReadNBits(LznaBitReader *tab, int bits) {
  uint32 rv = tab->bits_a & ((1 << bits) - 1);
  tab->bits_a >>= bits;
  LznaRenormalize(tab);
  return rv;
}


// Read a 4-bit value using an adaptive RANS model
static uint32 __forceinline LznaReadNibble(LznaBitReader *tab, LznaNibbleModel *model) {
  simde__m128i t, t0, t1, c0, c1;
  unsigned long bitindex;
  unsigned int start, end;
  uint64 x = tab->bits_a;

  t0 = simde_mm_loadu_si128((const simde__m128i *)&model->prob[0]);
  t1 = simde_mm_loadu_si128((const simde__m128i *)&model->prob[8]);

  t = simde_mm_cvtsi32_si128((int16)x);
  t = simde_mm_and_si128(simde_mm_shuffle_epi32(simde_mm_unpacklo_epi16(t, t), 0), simde_mm_set1_epi16(0x7FFF));

  c0 = simde_mm_cmpgt_epi16(t0, t);
  c1 = simde_mm_cmpgt_epi16(t1, t);

  _BitScanForward(&bitindex, simde_mm_movemask_epi8(simde_mm_packs_epi16(c0, c1)) | 0x10000);
  start = model->prob[bitindex - 1];
  end = model->prob[bitindex];

  c0 = simde_mm_and_si128(simde_mm_set1_epi16(0x7FD9), c0);
  c1 = simde_mm_and_si128(simde_mm_set1_epi16(0x7FD9), c1);

  c0 = simde_mm_add_epi16(c0, simde_mm_set_epi16(56, 48, 40, 32, 24, 16, 8, 0));
  c1 = simde_mm_add_epi16(c1, simde_mm_set_epi16(120, 112, 104, 96, 88, 80, 72, 64));

  t0 = simde_mm_add_epi16(simde_mm_srai_epi16(simde_mm_sub_epi16(c0, t0), 7), t0);
  t1 = simde_mm_add_epi16(simde_mm_srai_epi16(simde_mm_sub_epi16(c1, t1), 7), t1);

  simde_mm_storeu_si128((simde__m128i *)&model->prob[0], t0);
  simde_mm_storeu_si128((simde__m128i *)&model->prob[8], t1);

  tab->bits_a = (end - start) * (x >> 15) + (x & 0x7FFF) - start;
  LznaRenormalize(tab);
  return (int)bitindex - 1;
}

// Read a 3-bit value using an adaptive RANS model
static uint32 __forceinline LznaRead3bit(LznaBitReader *tab, Lzna3bitModel *model) {
  simde__m128i t, t0, c0;
  unsigned long bitindex;
  unsigned int start, end;
  uint64 x = tab->bits_a;

  t0 = simde_mm_loadu_si128((const simde__m128i *)&model->prob[0]);
  t = simde_mm_cvtsi32_si128(x & 0x7FFF);
  t = simde_mm_shuffle_epi32(simde_mm_unpacklo_epi16(t, t), 0);
  c0 = simde_mm_cmpgt_epi16(t0, t);

  _BitScanForward(&bitindex, simde_mm_movemask_epi8(c0) | 0x10000);
  bitindex >>= 1;
  start = model->prob[bitindex - 1];
  end = model->prob[bitindex];

  c0 = simde_mm_and_si128(simde_mm_set1_epi16(0x7FE5), c0);
  c0 = simde_mm_add_epi16(c0, simde_mm_set_epi16(56, 48, 40, 32, 24, 16, 8, 0));
  t0 = simde_mm_add_epi16(simde_mm_srai_epi16(simde_mm_sub_epi16(c0, t0), 7), t0);
  simde_mm_storeu_si128((simde__m128i *)&model->prob[0], t0);

  tab->bits_a = (end - start) * (x >> 15) + (x & 0x7FFF) - start;
  LznaRenormalize(tab);
  return bitindex - 1;
}

// Read a 1-bit value using an adaptive RANS model
static uint32 __forceinline LznaRead1Bit(LznaBitReader *tab, LznaBitModel *model, int nbits, int shift) {
  uint64 q;
  int magn = 1 << nbits;
  q = *model * (tab->bits_a >> nbits);
  if ((tab->bits_a & (magn - 1)) >= *model) {
    tab->bits_a -= q + *model;
    *model = *model - (*model >> shift);
    LznaRenormalize(tab);
    return 1;
  } else {
    tab->bits_a = (tab->bits_a & (magn - 1)) + q;
    *model = *model + ((magn - *model) >> shift);
    LznaRenormalize(tab);
    return 0;
  }
}

// Read a far distance using the far distance model
static uint32 __forceinline LznaReadFarDistance(LznaBitReader *tab, LznaState *lut) {
  uint32 n = LznaReadNibble(tab, &lut->far_distance.first_lo);
  uint32 hi;
  if (n >= 15)
    n = 15 + LznaReadNibble(tab, &lut->far_distance.first_hi);
  hi = 0;
  if (n != 0) {
    hi = LznaRead1Bit(tab, &lut->far_distance.second[n - 1], 14, 6) + 2;
    if (n != 1) {
      hi = (hi << 1) + LznaRead1Bit(tab, &lut->far_distance.third[hi - 2][n - 1], 14, 6);
      if (n != 2)
        hi = (hi << (n - 2)) + LznaReadNBits(tab, n - 2);
    }
    hi -= 1;
  }
  LznaLowBitsDistanceModel *lutd = &lut->low_bits_of_distance[hi == 0];
  uint32 low_bit = LznaRead1Bit(tab, &lutd->v, 14, 6);
  uint32 low_nibble = LznaReadNibble(tab, &lutd->d[low_bit]);
  return low_bit + (2 * low_nibble) + (32 * hi) + 1;
}

// Read a near distance using a near distance model
static uint32 __forceinline LznaReadNearDistance(LznaBitReader *tab, LznaState *lut, LznaNearDistModel *model) {
  uint32 nb = LznaReadNibble(tab, &model->first);
  uint32 hi = 0;
  if (nb != 0) {
    hi = LznaRead1Bit(tab, &model->second[nb - 1], 14, 6) + 2;
    if (nb != 1) {
      hi = (hi << 1) + LznaRead1Bit(tab, &model->third[hi - 2][nb - 1], 14, 6);
      if (nb != 2)
        hi = (hi << (nb - 2)) + LznaReadNBits(tab, nb - 2);
    }
    hi -= 1;
  }
  LznaLowBitsDistanceModel *lutd = &lut->low_bits_of_distance[hi == 0];
  uint32 low_bit = LznaRead1Bit(tab, &lutd->v, 14, 6);
  uint32 low_nibble = LznaReadNibble(tab, &lutd->d[low_bit]);
  return low_bit + (2 * low_nibble) + (32 * hi) + 1;
}

// Read a length using the length model.
static uint32 __forceinline LznaReadLength(LznaBitReader *tab, LznaLongLengthModel *model, int64 dst_offs) {
  uint32 length = LznaReadNibble(tab, &model->first[dst_offs & 3]);
  if (length >= 12) {
    uint32 b = LznaReadNibble(tab, &model->second);
    if (b >= 15)
      b = 15 + LznaReadNibble(tab, &model->third);
    uint32 n = 0;
    uint32 base = 0;
    if (b) {
      n = (b - 1) >> 1;
      base = ((((b - 1) & 1) + 2) << n) - 1;
    }
    length += (LznaReadNBits(tab, n) + base) * 4;
  }
  return length;
}

static const uint8 next_state_lit[12] = {
  0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 4, 5
};

static void LznaCopyLongDist(byte *dst, size_t dist, size_t length) {
  const byte *src = dst - dist;
  ((uint64*)dst)[0] = ((uint64*)src)[0];
  ((uint64*)dst)[1] = ((uint64*)src)[1];
  if (length > 16) {
    do {
      ((uint64*)dst)[2] = ((uint64*)src)[2];
      dst += 8;
      src += 8;
      length -= 8;
    } while (length > 16);
  }
}

static void LznaCopyShortDist(byte *dst, size_t dist, size_t length) {
  const byte *src = dst - dist;
  if (dist >= 4) {
    ((uint32*)dst)[0] = ((uint32*)src)[0];
    ((uint32*)dst)[1] = ((uint32*)src)[1];
    ((uint32*)dst)[2] = ((uint32*)src)[2];
    if (length > 12) {
      ((uint32*)dst)[3] = ((uint32*)src)[3];
      if (length > 16) {
        do {
          ((uint32*)dst)[4] = ((uint32*)src)[4];
          length -= 4;
          dst += 4;
          src += 4;
        } while (length > 16);
      }
    }
  } else if (dist == 1) {
    memset(dst, *src, length);
  } else {
    ((byte*)dst)[0] = ((byte*)src)[0];
    ((byte*)dst)[1] = ((byte*)src)[1];
    ((byte*)dst)[2] = ((byte*)src)[2];
    ((byte*)dst)[3] = ((byte*)src)[3];
    ((byte*)dst)[4] = ((byte*)src)[4];
    ((byte*)dst)[5] = ((byte*)src)[5];
    ((byte*)dst)[6] = ((byte*)src)[6];
    ((byte*)dst)[7] = ((byte*)src)[7];
    ((byte*)dst)[8] = ((byte*)src)[8];
    while (length > 9) {
      ((byte*)dst)[9] = ((byte*)src)[9];
      dst += 1;
      src += 1;
      length -= 1;
    }
  }
}

static void LznaCopy4to12(byte *dst, size_t dist, size_t length) {
  const byte *src = dst - dist;
  dst[0] = src[0];
  dst[1] = src[1];
  dst[2] = src[2];
  dst[3] = src[3];
  if (length > 4) {
    dst[4] = src[4];
    dst[5] = src[5];
    dst[6] = src[6];
    dst[7] = src[7];
    if (length > 8) {
      dst[8] = src[8];
      dst[9] = src[9];
      dst[10] = src[10];
      dst[11] = src[11];
    }
  }
}

static void LznaPreprocessMatchHistory(LznaState *lut) {
  if (lut->match_history[4] >= 0xc000) {
    size_t i = 0;
    while (lut->match_history[4 + i] >= 0xC000) {
      ++i;
      if (i >= 4) {
        lut->match_history[7] = lut->match_history[6];
        lut->match_history[6] = lut->match_history[5];
        lut->match_history[5] = lut->match_history[4];
        lut->match_history[4] = 4;
        return;
      }
    }
    uint32 t = lut->match_history[i + 4];
    lut->match_history[i + 4] = lut->match_history[i + 3];
    lut->match_history[i + 3] = lut->match_history[i + 2];
    lut->match_history[i + 2] = lut->match_history[i + 1];
    lut->match_history[4] = t;
  }
}

int LZNA_DecodeQuantum(byte *dst, byte *dst_end, byte *dst_start,
                       const byte *src_in, const byte *src_end,
                       LznaState *lut) {
  LznaBitReader tab;
  uint32 x;
  uint32 dst_offs = dst - dst_start;
  uint32 match_val;
  uint32 state;
  uint32 length;
  uint32 dist;

  LznaPreprocessMatchHistory(lut);
  LznaBitReader_Init(&tab, src_in);
  dist = lut->match_history[4];

  state = 5;
  dst_end -= 8;

  if (dst_offs == 0) {
    if (LznaReadBit(&tab)) {
      x = 0;
    } else {
      LznaLiteralModel *model = &lut->literal[0];
      x = LznaReadNibble(&tab, &model->upper[0]);
      x = (x << 4) + LznaReadNibble(&tab, (x != 0) ? &model->nomatch[x] : &model->lower[0]);
    }
    *dst++ = x;
    dst_offs += 1;
  }
  while (dst < dst_end) {
    match_val = *(dst - dist);

    if (LznaRead1Bit(&tab, &lut->is_literal[(dst_offs & 7) + 8 * state], 13, 5)) {
      x = LznaReadNibble(&tab, &lut->type[(dst_offs & 7) + 8 * state]);
      if (x == 0) {
        // Copy 1 byte from most recent distance
        *dst++ = match_val;
        dst_offs += 1;
        state = (state >= 7) ? 11 : 9;
      } else if (x < 4) {
        if (x == 1) {
          // Copy count 3-4
          length = 3 + LznaRead1Bit(&tab, &lut->short_length[state][dst_offs & 3], 14, 4);
          dist = LznaReadNearDistance(&tab, lut, &lut->near_dist[length - 3]);
          dst[0] = (dst - dist)[0];
          dst[1] = (dst - dist)[1];
          dst[2] = (dst - dist)[2];
          dst[3] = (dst - dist)[3];
        } else if (x == 2) {
          // Copy count 5-12
          length = 5 + LznaRead3bit(&tab, &lut->medium_length);
          dist = LznaReadFarDistance(&tab, lut);
          if (dist >= 8) {
            ((uint64*)dst)[0] = ((uint64*)(dst - dist))[0];
            ((uint64*)dst)[1] = ((uint64*)(dst - dist))[1];
          } else {
            LznaCopy4to12(dst, dist, length);
          }
        } else {
          // Copy count 13-
          length = LznaReadLength(&tab, &lut->long_length, dst_offs) + 13;
          dist = LznaReadFarDistance(&tab, lut);
          if (dist >= 8)
            LznaCopyLongDist(dst, dist, length);
          else
            LznaCopyShortDist(dst, dist, length);
        }
        state = (state >= 7) ? 10 : 7;
        lut->match_history[7] = lut->match_history[6];
        lut->match_history[6] = lut->match_history[5];
        lut->match_history[5] = lut->match_history[4];
        lut->match_history[4] = dist;
        dst += length;
        dst_offs += length;
      } else if (x >= 12) {
        // Copy 2 bytes from a recent distance
        size_t idx = x - 12;
        dist = lut->match_history[4 + idx];
        lut->match_history[4 + idx] = lut->match_history[3 + idx];
        lut->match_history[3 + idx] = lut->match_history[2 + idx];
        lut->match_history[2 + idx] = lut->match_history[1 + idx];
        lut->match_history[4] = dist;
        dst[0] = *(dst - dist + 0);
        dst[1] = *(dst - dist + 1);
        state = (state >= 7) ? 11 : 8;
        dst_offs += 2;
        dst += 2;
      } else {
        size_t idx = (x - 4) >> 1;
        dist = lut->match_history[4 + idx];
        lut->match_history[4 + idx] = lut->match_history[3 + idx];
        lut->match_history[3 + idx] = lut->match_history[2 + idx];
        lut->match_history[2 + idx] = lut->match_history[1 + idx];
        lut->match_history[4] = dist;
        if (x & 1) {
          // Copy 11- bytes from recent distance
          length = 11 + LznaReadLength(&tab, &lut->long_length_recent, dst_offs);
          if (dist >= 8) {
            LznaCopyLongDist(dst, dist, length);
          } else {
            LznaCopyShortDist(dst, dist, length);
          }
        } else {
          // Copy 3-10 bytes from recent distance
          length = 3 + LznaRead3bit(&tab, &lut->short_length_recent[idx].a[dst_offs & 3]);
          if (dist >= 8) {
            ((uint64*)dst)[0] = ((uint64*)(dst - dist))[0];
            ((uint64*)dst)[1] = ((uint64*)(dst - dist))[1];
          } else {
            LznaCopy4to12(dst, dist, length);
          }
        }
        state = (state >= 7) ? 11 : 8;
        dst_offs += length;
        dst += length;
      }
    } else {
      // Output a literal
      LznaLiteralModel *model = &lut->literal[dst_offs & 3];
      x = LznaReadNibble(&tab, &model->upper[match_val >> 4]);
      x = (x << 4) + LznaReadNibble(&tab, ((match_val >> 4) != x) ? &model->nomatch[x] : &model->lower[match_val & 0xF]);
      *dst++ = x;
      dst_offs += 1;
      state = next_state_lit[state];
    }
  }

  if (dst != dst_end)
    return -1;

  *(uint64*)dst = (uint32)tab.bits_a | (tab.bits_b << 32);

  return (byte*)tab.src - src_in;
}
