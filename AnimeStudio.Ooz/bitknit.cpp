#include "stdafx.h"

struct BitknitLiteral {
  uint16 lookup[512 + 4];
  uint16 a[300 + 1];
  uint16 freq[300];
  uint32 adapt_interval;
};

struct BitknitDistanceLsb {
  uint16 lookup[64 + 4];
  uint16 a[40 + 1];
  uint16 freq[40];
  uint32 adapt_interval;
};

struct BitknitDistanceBits {
  uint16 lookup[64 + 4];
  uint16 a[21 + 1];
  uint16 freq[21];
  uint32 adapt_interval;
};


struct BitknitState {
  uint32 recent_dist[8];
  uint32 last_match_dist;
  uint32 recent_dist_mask;
  uint32 bits, bits2;

  BitknitLiteral literals[4];
  BitknitDistanceLsb distance_lsb[4];
  BitknitDistanceBits distance_bits;
};

void BitknitLiteral_Init(BitknitLiteral *model) {
  size_t i;
  uint16 *p, *p_end;

  for (i = 0; i < 264; i++)
    model->a[i] = (0x8000 - 300 + 264) * i / 264;
  for (; i <= 300; i++)
    model->a[i] = (0x8000 - 300) + i;

  model->adapt_interval = 1024;
  for (i = 0; i < 300; i++)
    model->freq[i] = 1;

  for (i = 0, p = model->lookup; i < 300; i++) {
    p_end = &model->lookup[(model->a[i + 1] - 1) >> 6];
    do {
      p[0] = p[1] = p[2] = p[3] = i;
      p += 4;
    } while (p <= p_end);
    p = p_end + 1;
  }
}

void BitknitDistanceLsb_Init(BitknitDistanceLsb *model) {
  size_t i;
  uint16 *p, *p_end;

  for (i = 0; i <= 40; i++)
    model->a[i] = 0x8000 * i / 40;

  model->adapt_interval = 1024;
  for (i = 0; i < 40; i++)
    model->freq[i] = 1;

  for (i = 0, p = model->lookup; i < 40; i++) {
    p_end = &model->lookup[(model->a[i + 1] - 1) >> 9];
    do {
      p[0] = p[1] = p[2] = p[3] = i;
      p += 4;
    } while (p <= p_end);
    p = p_end + 1;
  }
}

void BitknitDistanceBits_Init(BitknitDistanceBits *model) {
  size_t i;
  uint16 *p, *p_end;

  for (i = 0; i <= 21; i++)
    model->a[i] = 0x8000 * i / 21;

  model->adapt_interval = 1024;
  for (i = 0; i < 21; i++)
    model->freq[i] = 1;

  for (i = 0, p = model->lookup; i < 21; i++) {
    p_end = &model->lookup[(model->a[i + 1] - 1) >> 9];
    do {
      p[0] = p[1] = p[2] = p[3] = i;
      p += 4;
    } while (p <= p_end);
    p = p_end + 1;
  }
}

void BitknitState_Init(BitknitState *bk) {
  size_t i;

  bk->last_match_dist = 1;
  for (i = 0; i < 8; i++)
    bk->recent_dist[i] = 1;

  bk->recent_dist_mask =
      (7 << (7 * 3)) | (6 << (6 * 3)) |
      (5 << (5 * 3)) | (4 << (4 * 3)) |
      (3 << (3 * 3)) | (2 << (2 * 3)) |
      (1 << (1 * 3)) | (0 << (0 * 3));

  for (i = 0; i < 4; i++)
    BitknitLiteral_Init(&bk->literals[i]);

  for (i = 0; i < 4; i++)
    BitknitDistanceLsb_Init(&bk->distance_lsb[i]);

  BitknitDistanceBits_Init(&bk->distance_bits);

}

void BitknitLiteral_Adaptive(BitknitLiteral *model, uint32 sym) {
  size_t i;
  uint32 sum;
  uint16 *p, *p_end;

  model->adapt_interval = 1024;
  model->freq[sym] += 725;

  sum = 0;
  for (i = 0; i < 300; i++) {
    sum += model->freq[i];
    model->freq[i] = 1;
    model->a[i + 1] = model->a[i + 1] + ((sum - model->a[i + 1]) >> 1);
  }

  for (i = 0, p = model->lookup; i < 300; i++) {
    p_end = &model->lookup[(model->a[i + 1] - 1) >> 6];
    do {
      p[0] = p[1] = p[2] = p[3] = i;
      p += 4;
    } while (p <= p_end);
    p = p_end + 1;
  }
}

uint32 BitknitLiteral_Lookup(BitknitLiteral *model, uint32 *bits) {
  uint32 masked = *bits & 0x7FFF;
  size_t sym = model->lookup[masked >> 6];
  sym += masked > model->a[sym + 1];
  while (masked >= model->a[sym + 1])
    sym += 1;
  *bits = masked + (*bits >> 15) * (model->a[sym + 1] - model->a[sym]) - model->a[sym];
  model->freq[sym] += 31;
  if (--model->adapt_interval == 0)
    BitknitLiteral_Adaptive(model, sym);
  return sym;
}

void BitknitDistanceLsb_Adaptive(BitknitDistanceLsb *model, uint32 sym) {
  size_t i;
  uint32 sum;
  uint16 *p, *p_end;

  model->adapt_interval = 1024;
  model->freq[sym] += 985;

  sum = 0;
  for (i = 0; i < 40; i++) {
    sum += model->freq[i];
    model->freq[i] = 1;
    model->a[i + 1] = model->a[i + 1] + ((sum - model->a[i + 1]) >> 1);
  }

  for (i = 0, p = model->lookup; i < 40; i++) {
    p_end = &model->lookup[(model->a[i + 1] - 1) >> 9];
    do {
      p[0] = p[1] = p[2] = p[3] = i;
      p += 4;
    } while (p <= p_end);
    p = p_end + 1;
  }
}

uint32 BitknitDistanceLsb_Lookup(BitknitDistanceLsb *model, uint32 *bits) {
  uint32 masked = *bits & 0x7FFF;
  size_t sym = model->lookup[masked >> 9];
  sym += masked > model->a[sym + 1];
  while (masked >= model->a[sym + 1])
    sym += 1;
  *bits = masked + (*bits >> 15) * (model->a[sym + 1] - model->a[sym]) - model->a[sym];
  model->freq[sym] += 31;
  if (--model->adapt_interval == 0)
    BitknitDistanceLsb_Adaptive(model, sym);
  return sym;
}


void BitknitDistanceBits_Adaptive(BitknitDistanceBits *model, uint32 sym) {
  size_t i;
  uint32 sum;
  uint16 *p, *p_end;

  model->adapt_interval = 1024;
  model->freq[sym] += 1004;

  sum = 0;
  for (i = 0; i < 21; i++) {
    sum += model->freq[i];
    model->freq[i] = 1;
    model->a[i + 1] = model->a[i + 1] + ((sum - model->a[i + 1]) >> 1);
  }

  for (i = 0, p = model->lookup; i < 21; i++) {
    p_end = &model->lookup[(model->a[i + 1] - 1) >> 9];
    do {
      p[0] = p[1] = p[2] = p[3] = i;
      p += 4;
    } while (p <= p_end);
    p = p_end + 1;
  }
}

uint32 BitknitDistanceBits_Lookup(BitknitDistanceBits *model, uint32 *bits) {
  uint32 masked = *bits & 0x7FFF;
  size_t sym = model->lookup[masked >> 9];
  sym += masked > model->a[sym + 1];
  while (masked >= model->a[sym + 1])
    sym += 1;
  *bits = masked + (*bits >> 15) * (model->a[sym + 1] - model->a[sym]) - model->a[sym];
  model->freq[sym] += 31;
  if (--model->adapt_interval == 0)
    BitknitDistanceBits_Adaptive(model, sym);
  return sym;
}


#define RENORMALIZE() { if (bits < 0x10000) bits = (bits << 16) | *(uint16*)src, src += 2; bitst = bits; bits = bits2; bits2 = bitst; }


static void BitknitCopyLongDist(byte *dst, size_t dist, size_t length) {
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

static void BitknitCopyShortDist(byte *dst, size_t dist, size_t length) {
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


size_t Bitknit_Decode(const byte *src, const byte *src_end, byte *dst, byte *dst_end, byte *dst_start, BitknitState *bk) {
  const byte *src_in = src;
  BitknitLiteral *litmodel[4];
  BitknitDistanceLsb *distancelsb[4];
  size_t i;
  intptr_t last_match_negative;
  uint32 bits, bits2, bitst;
  uint32 v, a, n;
  uint32 copy_length;
  uint32 recent_dist_mask;
  uint32 match_dist;

  for (i = 0; i < 4; i++)
    litmodel[i] = &bk->literals[(i - (intptr_t)dst_start) & 3];

  for (i = 0; i < 4; i++)
    distancelsb[i] = &bk->distance_lsb[(i - (intptr_t)dst_start) & 3];


  recent_dist_mask = bk->recent_dist_mask;
  bits = 0x10000;
  bits2 = 0x10000;
  last_match_negative = -(intptr_t)bk->last_match_dist;

  v = *(uint32*)src, src += 4;
  if (v < 0x10000)
    return 0;

  a = v >> 4;
  n = v & 0xF;
  if (a < 0x10000)
    a = (a << 16) | *(uint16*)src, src += 2;
  bits = a >> n;
  if (bits < 0x10000)
    bits = (bits << 16) | *(uint16*)src, src += 2;
  a = (a << 16) | *(uint16*)src, src += 2;
  
  bits2 = (1 << (n + 16)) | (a & ((1 << (n + 16)) - 1));
  
  if (dst == dst_start) {
    *dst++ = bits;
    bits >>= 8;
    RENORMALIZE();
  }
  
  while (dst + 4 < dst_end) {
    uint32 sym = BitknitLiteral_Lookup(litmodel[(intptr_t)dst & 3], &bits);
    RENORMALIZE();

    if (sym < 256) {
      *dst = sym + dst[last_match_negative];
      dst++;

      if (dst + 4 >= dst_end)
        break;

      sym = BitknitLiteral_Lookup(litmodel[(intptr_t)dst & 3], &bits);
      RENORMALIZE();

      if (sym < 256) {
        *dst = sym + dst[last_match_negative];
        dst++;
        continue;
      }
    }

    if (sym >= 288) {
      uint32 nb = sym - 287;
      sym = (bits & ((1 << nb) - 1)) + (1 << nb) + 286;
      bits >>= nb;
      RENORMALIZE();
    }

    copy_length = sym - 254;

    sym = BitknitDistanceLsb_Lookup(distancelsb[(intptr_t)dst & 3], &bits);
    RENORMALIZE();

    if (sym >= 8) {
      uint32 nb = BitknitDistanceBits_Lookup(&bk->distance_bits, &bits);
      RENORMALIZE();

      match_dist = bits & ((1 << (nb & 0xF)) - 1);
      bits >>= (nb & 0xF);
      RENORMALIZE();
      if (nb >= 0x10)
        match_dist = (match_dist << 16) | *(uint16*)src, src += 2;
      match_dist = (32 << nb) + (match_dist << 5) + sym - 39;

      bk->recent_dist[(recent_dist_mask >> 21) & 7] = bk->recent_dist[(recent_dist_mask >> 18) & 7];
      bk->recent_dist[(recent_dist_mask >> 18) & 7] = match_dist;

    } else {
      size_t idx = (recent_dist_mask >> (3 * sym)) & 7;
      uint32 mask = ~7 << (3 * sym);
      match_dist = bk->recent_dist[idx];
      recent_dist_mask = (recent_dist_mask & mask) | ((idx + 8 * recent_dist_mask) & ~mask);
    }
    
    if (match_dist >= 8) {
      BitknitCopyLongDist(dst, match_dist, copy_length);
    } else {
      BitknitCopyShortDist(dst, match_dist, copy_length);
    }

    dst += copy_length;

    last_match_negative = -(intptr_t)match_dist;
  }
  *(uint32*)dst = (uint16)bits | bits2 << 16;

  bk->last_match_dist = -last_match_negative;
  bk->recent_dist_mask = recent_dist_mask;
  return src - src_in;
}

