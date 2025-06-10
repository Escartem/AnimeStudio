// This file is not GPL. It may be used for educational purposes only.
#include "stdafx.h"
#include "compr_mermaid.h"
#include "compress.h"
#include "compr_util.h"
#include "compr_entropy.h"
#include <algorithm>
#include <memory>
#include "match_hasher.h"
#include "compr_match_finder.h"

struct MermaidWriter {
  uint8 *lit_start;
  uint8 *lit_cur;
  uint8 *litsub_start;
  uint8 *litsub_cur;
  uint8 *token_start;
  uint8 *token_cur;
  uint16 *off16_start;
  uint16 *off16_cur;
  uint8 *off32_start;
  uint8 *off32_cur;
  uint8 *length_start;
  uint8 *length_cur;
  int complex_token_count;
  int off32_count;
  int src_len;
  const uint8 *src_ptr;
  int cur_block_start_2;
  int block1_size;
  int block2_size;
  int tok_stream_2_offs;
  int off32_count_1;
  int off32_count_2;
};

void MermaidWriter_Init(MermaidWriter *mw, uint src_len, const uint8 *src, bool use_litsub) {

  mw->src_ptr = src;
  mw->src_len = src_len;
  mw->complex_token_count = 0;
  mw->off32_count = 0;
  
  uint lit_size = src_len + 8;
  uint token_size = src_len / 2 + 8;
  uint off16_size = src_len / 3;
  uint off32_size = src_len / 8;
  uint length_size = src_len / 29;
  
  uint total_size = lit_size + token_size + off16_size * 2 + length_size + off32_size * 4 + 256;
  if (use_litsub)
    total_size += lit_size;
  uint8 *temp = new uint8[total_size];

  mw->lit_start = mw->lit_cur = temp;
  temp += lit_size;
  if (use_litsub) {
    mw->litsub_start = mw->litsub_cur = temp;
    temp += lit_size;
  } else {
    mw->litsub_start = mw->litsub_cur = NULL;
  }
  mw->token_start = mw->token_cur = temp;
  temp += token_size;
  mw->off16_start = mw->off16_cur = (uint16*)temp;
  temp += (size_t)off16_size * 2;
  mw->off32_start = mw->off32_cur = temp;
  temp += (size_t)off32_size * 4;
  mw->length_start = mw->length_cur = temp;
  temp += length_size;
  mw->tok_stream_2_offs = 0;
  mw->off32_count_1 = mw->off32_count_2 = 0;
  mw->block1_size = std::min<uint>(src_len, 0x10000);
  mw->block2_size = src_len - mw->block1_size;
}

static inline const uint8 *MermaidMatchLength(const uint8 *src, const uint8 *src_end, ptrdiff_t last_offs) {
  while (src < src_end) {
    uint32 xorval = *(uint32*)src ^ *(uint32*)(src + last_offs);
    src += 4;
    if (xorval) {
      src = src + (BSF(xorval) >> 3) - 4;
      break;
    }
  }
  return std::min(src, src_end);
}

static inline void CopyBytesUnsafe(uint8 *dst, const uint8 *src, size_t n) {
  uint8 *dst_end = dst + n;
  do {
    *(uint32*)dst = *(uint32*)src;
    dst += 4, src += 4;
  } while (dst < dst_end);
}

static inline void Mermaid_WriteLenValue(MermaidWriter &mw, uint t) {
  uint8 *len_ptr = mw.length_cur;
  if (t > 251) {
    *len_ptr = (t & 3) - 4;
    *(uint16*)(len_ptr + 1) = (t - ((t & 3) + 252)) >> 2;
    len_ptr += 3;
  } else {
    *len_ptr++ = t;
  }
  mw.length_cur = len_ptr;
}

static inline void Mermaid_WriteOff32(MermaidWriter &mw, uint off) {
  uint8 *p = mw.off32_cur;
  if (off >= 0xC00000) {
    uint t = (off & 0x3FFFFF) | 0xC00000;
    p[0] = t;
    p[1] = t >> 8;
    p[2] = t >> 16;
    p[3] = (off - t) >> 22;
    p += 4;
  } else {
    p[0] = off;
    p[1] = off >> 8;
    p[2] = off >> 16;
    p += 3;
  }
  mw.off32_cur = p;
  mw.off32_count++;
}

static inline void Mermaid_WriteComplexOffs(MermaidWriter &mw, uint ml, uint lrl, uint offs, ptrdiff_t last_offs, const uint8 *lit_ptr) {
  const uint8 *lit_end = lit_ptr + lrl;
  if (mw.litsub_cur)
    SubtractBytesUnsafe(postadd(mw.litsub_cur, lrl), lit_ptr, lrl, last_offs);
  CopyBytesUnsafe(postadd(mw.lit_cur, lrl), lit_ptr, lrl);

  if (lrl < 64) {
    while (lrl > 7) {
      *mw.token_cur++ = 0x87;
      lrl -= 7;
    }
  } else {
    Mermaid_WriteLenValue(mw, lrl - 64);
    *mw.token_cur++ = 0x00;
    mw.complex_token_count++;
    lrl = 0;

    if (ml == 0)
      return;
  }

  if (offs <= 0xffff && ml < 91) {
    uint ml_cur = std::min(ml, 15u);
    uint8 token = lrl + 8 * ml_cur;
    if (offs == 0) {
      token += 0x80;
    } else {
      *mw.off16_cur++ = offs;
    }
    ml -= ml_cur;
    *mw.token_cur++ = token;

    while (ml) {
      uint ml_cur = std::min(ml, 15u);
      *mw.token_cur++ = 0x80 + 8 * ml_cur;
      ml -= ml_cur;
    }
  } else {
    mw.complex_token_count++;
    if (lrl)
      *mw.token_cur++ = 0x80 + lrl;

    if (offs == 0)
      offs = -(int)last_offs;

    uint8 tok;
    int lenval;

    if (offs > 0xffff) {
      if (ml - 5 <= 23) {
        tok = ml - 5;
        lenval = -1;
      } else {
        tok = 2;
        lenval = ml - 29;
      }
    } else {
      tok = 1;
      lenval = ml - 91;
    }
    *mw.token_cur++ = tok;
    if (lenval >= 0)
      Mermaid_WriteLenValue(mw, lenval);
      
    if (offs > 0xffff) {
      Mermaid_WriteOff32(mw, offs + mw.src_ptr + mw.cur_block_start_2 - lit_end);
    } else {
      *mw.off16_cur++ = offs;
    }
  }
}

static inline void Mermaid_WriteOffs(MermaidWriter &mw, int ml, int lrl, int offs, ptrdiff_t last_offs, const uint8 *lit_start) {
  if (lrl <= 7 && ml <= 15 && offs <= 0xffff) {
    *(uint64*)mw.lit_cur = *(uint64*)lit_start;
    mw.lit_cur += lrl;
    if (mw.litsub_cur) {
      simde_mm_storel_epi64((simde__m128i *)mw.litsub_cur,
                            simde_mm_sub_epi8(simde_mm_loadl_epi64((const simde__m128i *)lit_start),
                                              simde_mm_loadl_epi64((const simde__m128i *)&lit_start[last_offs])));
      mw.litsub_cur += lrl;
    }
    *mw.token_cur++ = ((offs == 0) << 7) + lrl + 8 * ml;
    if (offs != 0)
      *mw.off16_cur++ = offs;
  } else {
    Mermaid_WriteComplexOffs(mw, ml, lrl, offs, last_offs, lit_start);
  }
}

static inline int MermaidMod7(int v) {
  return (v > 7) ? (v - 1) % 7 + 1 : v;
}

static void Mermaid_WriteOffsWithLit1Inner(MermaidWriter &mw, int ml, int lrl, int offs, ptrdiff_t last_offs, const uint8 *lit_start) {
  int i = 1, last = 0;
  int found[33];
  size_t found_ctr = 0;
  while (i < lrl) {
    int mask = simde_mm_movemask_epi8(simde_mm_cmpeq_epi8(simde_mm_loadu_si128((const simde__m128i *)&lit_start[i]), simde_mm_loadu_si128((const simde__m128i *)&lit_start[i + last_offs])));
    if (!mask) {
      i += 16;
    } else {
      int j = i + BSF(mask);
      if (j >= lrl)
        break;
      i = j + 1;
      if (j - last != 0) {
        found[found_ctr++] = j - last;
        last = i;
      }
    }
  }
  if (found_ctr != 0) {
    found[found_ctr] = lrl - last;
    for (size_t i = 0; i < found_ctr; i++) {
      int cur = found[i];
      if (MermaidMod7(cur) + MermaidMod7(found[i + 1]) + 1 > 7) {
        Mermaid_WriteOffs(mw, 1, cur, 0, last_offs, lit_start);
        lit_start += cur + 1;
        lrl -= cur + 1;
      } else {
        found[i + 1] += cur + 1;
      }
    }
  }
  Mermaid_WriteOffs(mw, ml, lrl, offs, last_offs, lit_start);
}

static inline void Mermaid_WriteOffsWithLit1(MermaidWriter &mw, int ml, int lrl, int offs, ptrdiff_t last_offs, const uint8 *lit_start) {
  if ((uint)(lrl - 8) > 55) {
    Mermaid_WriteOffs(mw, ml, lrl, offs, last_offs, lit_start);
  } else {
    Mermaid_WriteOffsWithLit1Inner(mw, ml, lrl, offs, last_offs, lit_start);
  }

}

int GetScratchUsage(int coder, int chunk_len) {
  int result = 2 * chunk_len + 32;
  if (coder != 9 && coder != 11)
    result += chunk_len;
  return std::min(result + 0xD000, 0x6C000);
}

struct MermaidHistos {
  HistoU8 lit, tok;
  HistoU8 off16lo, off16hi;
};

static float GetTime_MermaidOff16(int platforms, int num) {
  return CombineCostComponents1A(platforms, num, 0.270f, 0.428f, 0.550f, 0.213f,
                                                 24.0f, 53.0f, 62.0f, 33.0f);
}

static float GetTime_Mermaid(int platforms, int len, int toks, int ctoks) {
  return CombineCostComponents(platforms,
                               200.0f + len * 0.363f + toks * 5.393f + ctoks * 29.655f,
                               200.0f + len * 0.429f + toks * 6.977f + ctoks * 49.739f,
                               200.0f + len * 0.538f + toks * 8.676f + ctoks * 69.864f,
                               200.0f + len * 0.255f + toks * 5.364f + ctoks * 30.818f);
}

static float GetTime_Selkie(int platforms, int len, int toks, int ctoks, int lit) {
  return CombineCostComponents(platforms,
                               200.0f + len * 0.371f + toks * 5.259f + ctoks * 25.474f + lit * 0.131f,
                               200.0f + len * 0.414f + toks * 6.678f + ctoks * 62.007f + lit * 0.065f,
                               200.0f + len * 0.562f + toks * 8.190f + ctoks * 75.523f + lit * 0.008f,
                               200.0f + len * 0.272f + toks * 5.018f + ctoks * 29.297f + lit * 0.070f);
}

static float GetTime_MermaidSelkieOff32(int platforms, int num) {
  return CombineCostComponents1A(platforms, num, 1.285f, 3.369f, 2.446f, 1.032f,
                                                 56.010f, 33.347f, 133.394f, 67.640f);
}

int Mermaid_WriteLzTable(float *cost_ptr, int *chunk_type_ptr, MermaidHistos *mh, uint8 *dst, uint8 *dst_end, LzCoder *coder, LzTemp *lztemp, MermaidWriter *mw, int start_pos) {
  bool is_mermaid = coder->codec_id == kCompressorMermaid;
  const uint8 *src = mw->src_ptr;
  int src_len = mw->src_len;
  int token_count = mw->token_cur - mw->token_start;
  uint8 *dst_org = dst;
  if (token_count == 0 && (!is_mermaid || mw->litsub_start == NULL))
    return src_len;

  int eopts = coder->entropy_opts;
  int level = coder->compression_level;
  int platforms = coder->platforms;
  float speed_tradeoff = coder->speed_tradeoff;

  int initial_bytes = 0;
  if (start_pos == 0) {
    *(uint64*)dst = *(uint64*)src;
    dst += 8;
    initial_bytes = 8;
  }

  int lit_count = mw->lit_cur - mw->lit_start;
  int litsub_count = mw->litsub_cur - mw->litsub_start;

  HistoU8 litsub_histo;
  HistoU8 lit_histo;

  float lit_cost = kInvalidCost;
  float raw_lit_cost = lit_count + 3;
  
  if (lit_count == 0 && litsub_count > 0) {
    *chunk_type_ptr = 0;

    CountBytesHistoU8(mw->litsub_start, litsub_count, &litsub_histo);
    int n_lits = EncodeArrayU8WithHisto(dst, dst_end, mw->litsub_start, litsub_count, litsub_histo, eopts, speed_tradeoff, platforms, &lit_cost, level);
    if (n_lits < 0 || n_lits > litsub_count)
      return src_len;
    if (mh)
      mh->lit = litsub_histo;
    dst += n_lits;
  } else if (is_mermaid && lit_count >= 32) {
    CountBytesHistoU8(mw->lit_start, lit_count, &lit_histo);
    int n, n_lits = -1;
    if (mw->litsub_start) {
      CountBytesHistoU8(mw->litsub_start, lit_count, &litsub_histo);
      float litsub_cost = CombineCostComponents1(platforms, lit_count, 0.324f, 0.433f, 0.550f, 0.289f) * speed_tradeoff;
      if (level >= 6 || GetHistoCostApprox(lit_histo, lit_count) * 0.125f > GetHistoCostApprox(litsub_histo, lit_count) * 0.125f + litsub_cost) {
        *chunk_type_ptr = 0;
        n_lits = EncodeArrayU8WithHisto(dst, dst_end, mw->litsub_start, lit_count, litsub_histo, eopts, speed_tradeoff, platforms, &lit_cost, level);
        lit_cost += litsub_cost;
        if (n_lits < 0 || n_lits >= lit_count || lit_cost > raw_lit_cost) {
          lit_cost = kInvalidCost;
          n_lits = -1;
        }
      }
    }
    if (n_lits < 0 || level >= 6) {
      n = EncodeArrayU8WithHisto(dst, dst_end, mw->lit_start, lit_count, lit_histo, eopts, speed_tradeoff, platforms, &lit_cost, level);
      if (n > 0) {
        n_lits = n;
        *chunk_type_ptr = 1;
      } else if (n_lits < 0) {
        return src_len;
      }
    }
    if (mh)
      mh->lit = (*chunk_type_ptr == 1) ? lit_histo : litsub_histo;
    dst += n_lits;
  } else {
    int n_lits = lit_count + 3;
    lit_cost = raw_lit_cost;
    *chunk_type_ptr = 1;
    if (is_mermaid) {
      EncodeArrayU8_Memcpy(dst, dst_end, mw->lit_start, lit_count);
    } else {
      // selkie always writes lits directly to the stream
      dst[0] = lit_count >> 16;
      dst[1] = lit_count >> 8;
      dst[2] = lit_count;
    }
    dst += n_lits;
  }

  // Encode tokens
  float tok_cost = kInvalidCost;
  int n_token;
  if (is_mermaid) {
    n_token = EncodeArrayU8(dst, dst_end, mw->token_start, token_count, eopts, speed_tradeoff, platforms, &tok_cost, level, mh ? &mh->tok : NULL);
  } else {
    tok_cost = token_count + 3;
    n_token = EncodeArrayU8_Memcpy(dst, dst_end, mw->token_start, token_count);
  }
  if (n_token < 0)
    return src_len;
  dst += n_token;

  uint8 *dst_after_tokens = dst;

  if (dst_end - dst <= 16)
    return src_len;

  if (src_len > 0x10000)
    *(uint16*)postadd(dst,2) = mw->tok_stream_2_offs;

  int off16_count = mw->off16_cur - mw->off16_start;
  float off16_cost = off16_count * 2;
  uint off16_bytes;

  if (is_mermaid && off16_count >= 32) {
    // reuse the space used for lits
    uint8 *lo_off16 = mw->lit_start;
    uint8 *hi_off16 = lo_off16 + off16_count;
    for (int i = 0; i < off16_count; i++) {
      uint v = mw->off16_start[i];
      lo_off16[i] = (uint8)v;
      hi_off16[i] = (uint8)(v >> 8);
    }
    uint8 *off16_dst = (uint8 *)(mw->lit_start + 2 * off16_count);
    float cost_off16_lo = kInvalidCost;
    float cost_off16_hi = kInvalidCost;
    int n_hi = EncodeArrayU8(off16_dst,        (uint8*)mw->off16_start, hi_off16, off16_count, eopts, speed_tradeoff, platforms, &cost_off16_hi, level, mh ? &mh->off16hi : NULL);
    int n_lo = EncodeArrayU8(off16_dst + n_hi, (uint8*)mw->off16_start, lo_off16, off16_count, eopts, speed_tradeoff, platforms, &cost_off16_lo, level, mh ? &mh->off16lo : NULL);
    off16_bytes = n_hi + n_lo;
    float cost = cost_off16_lo + cost_off16_hi + GetTime_MermaidOff16(platforms, off16_count) * speed_tradeoff;
    if (cost >= off16_cost)
      goto encode_off16_memcpy;
    if (off16_bytes + 2 >= dst_end - dst)
      return src_len;
    off16_cost = cost;
    *(uint16*)postadd(dst,2) = 0xffff;
    memcpy(postadd(dst, off16_bytes), off16_dst, off16_bytes);
  } else {
encode_off16_memcpy:
    off16_bytes = (uintptr_t)mw->off16_cur - (uintptr_t)mw->off16_start;
    if (off16_bytes + 2 >= dst_end - dst)
      return src_len;
    *(uint16*)postadd(dst,2) = off16_count;
    memcpy(postadd(dst, off16_bytes), mw->off16_start, off16_bytes);
  }
  int off32_count = mw->off32_count_1 + mw->off32_count_2;
  int required_scratch = token_count + lit_count + 4 * (off32_count + off16_count) + 0xd000 + 0x40 + 4;

  if (required_scratch > GetScratchUsage(coder->codec_id, src_len)) {
    assert(0);
    return src_len;
  }

  if (dst_end - dst <= 7)
    return src_len;

  uint v = (std::min(mw->off32_count_1, 4095) << 12) + std::min(mw->off32_count_2, 4095);
  *(uint32*)postadd(dst, 3) = v;

  if (mw->off32_count_1 >= 4095)
    *(uint16*)postadd(dst, 2) = mw->off32_count_1;
  if (mw->off32_count_2 >= 4095)
    *(uint16*)postadd(dst, 2) = mw->off32_count_2;

  uint off32_byte_count = mw->off32_cur - mw->off32_start;
  if (off32_byte_count >= dst_end - dst)
    return src_len;
  memcpy(postadd(dst, off32_byte_count), mw->off32_start, off32_byte_count);

  uint len_count = mw->length_cur - mw->length_start;
  if (len_count >= dst_end - dst)
    return src_len;
  memcpy(postadd(dst, len_count), mw->length_start, len_count);

  if (dst - dst_org >= src_len)
    return src_len;

  float time = (is_mermaid) ? GetTime_Mermaid(platforms, src_len, token_count, mw->complex_token_count) : 
                              GetTime_Selkie(platforms, src_len, token_count, mw->complex_token_count, lit_count);
  
  float off32_time = GetTime_MermaidSelkieOff32(platforms, off32_count) * speed_tradeoff;
  int extra = dst - dst_after_tokens - off16_bytes;
  *cost_ptr = off32_time + (tok_cost + lit_cost + time * speed_tradeoff + extra + off16_cost + initial_bytes);
  return dst - dst_org;
}

static __forceinline bool MermaidIsBetterThanRecent(int rml, int ml, int offs) {
  return rml < 2 || (rml + 1 < ml && (rml + 4 < ml || offs < 65536));
}

static __forceinline bool MermaidIsMatchBetter(int ml, int offs, int best_ml, int best_offs) {
  if (ml == best_ml)
    return offs < best_offs;
  if ((offs <= 0xffff) == (best_offs <= 0xffff))
    return ml > best_ml;
  if (best_offs <= 0xffff)
    return ml > best_ml + 5;
  return ml >= best_ml - 5;
}


template<typename Hasher>
__forceinline LengthAndOffset MermaidFast_GetMatch(const uint8 *src_cur, const uint8 *src_end_safe, const uint8 *lit_start,
                                                   ptrdiff_t last_offs, Hasher *hasher,
                                                   const uint8 *next_cur_ptr, int dict_size, int min_match_length, const uint minlen[32]) {
  typename Hasher::HashPos hp = hasher->GetHashPos(src_cur);
  uint32 u32_at_src = *(uint32*)src_cur;
  hasher->SetHashPosPrefetch(next_cur_ptr);
  
  uint32 xorval = *(uint32*)&src_cur[last_offs] ^ u32_at_src;
  if (xorval == 0) {
    LengthAndOffset match = { 4 + CountMatchingBytes(src_cur + 4, src_end_safe, -last_offs),  0 };
    hasher->Insert(hp);
    return match;
  }
  uint32 recent_ml = BSF(xorval) >> 3;

  if (src_cur - lit_start >= 64) {
    if (recent_ml < 3)
      recent_ml = 0;
    min_match_length += 1;
  }
  uint32 best_offs = 0;
  uint32 best_ml = min_match_length - 1;
  uint32 *cur_hash_ptr = hp.ptr1;
  for (;;) {
    for (size_t hashidx = 0; hashidx != Hasher::NumHash; hashidx++) {
      if ((cur_hash_ptr[hashidx] & 0xfc000000) == (hp.hi & 0xfc000000)) {
        uint32 cur_offs = (hp.pos - cur_hash_ptr[hashidx]) & 0x3ffffff;
        if (cur_offs > 8 && cur_offs < dict_size && *(uint32*)(src_cur - cur_offs) == u32_at_src) {
          uint32 cur_ml = 4 + CountMatchingBytes(src_cur + 4, src_end_safe, cur_offs);
          if (cur_ml > best_ml && cur_ml >= minlen[31 - BSR(cur_offs)] &&
              MermaidIsMatchBetter(cur_ml, cur_offs, best_ml, best_offs)) {
            best_offs = cur_offs, best_ml = cur_ml;
          }
        }
      }
    }
    if (!Hasher::DualHash || cur_hash_ptr == hp.ptr2)
      break;
    cur_hash_ptr = hp.ptr2;
  }

  if (*(uint32*)(src_cur - 8) == u32_at_src) {
    uint32 cur_ml = 4 + CountMatchingBytes(src_cur + 4, src_end_safe, 8);
    if (cur_ml >= best_ml && cur_ml >= min_match_length)
      best_ml = cur_ml, best_offs = 8;
  }
  hasher->Insert(hp);
  if (best_offs == 0 || !MermaidIsBetterThanRecent(recent_ml, best_ml, best_offs))
    best_ml = recent_ml, best_offs = 0;
  LengthAndOffset match = { (int)best_ml, (int)best_offs };
  return match;
}

template<>
__forceinline LengthAndOffset MermaidFast_GetMatch(const uint8 *src_cur, const uint8 *src_end_safe, const uint8 *lit_start,
                                                          ptrdiff_t last_offs, MatchHasher2 *hasher,
                                                          const uint8 *next_cur_ptr, int dict_size, int min_match_length, const uint minlen[32]) {
  MatchHasher2::HashPos hp = hasher->GetHashPos(src_cur);
  uint32 u32_at_src = *(uint32*)src_cur;
  hasher->SetHashPosPrefetch(next_cur_ptr);

  uint32 xorval = *(uint32*)&src_cur[last_offs] ^ u32_at_src;
  if (xorval == 0) {
    LengthAndOffset match = { 4 + CountMatchingBytes(src_cur + 4, src_end_safe, -last_offs),  0 };
    hasher->Insert(hp);
    return match;
  }
  uint32 recent_ml = BSF(xorval) >> 3;
  if (src_cur - lit_start >= 64) {
    if (recent_ml < 3)
      recent_ml = 0;
    min_match_length += 1;
  }
  // Look in the short table
  uint32 best_ml = 0, best_offs = 0;
  uint32 hashval = hasher->firsthash_[hp.hash_a];
  uint32 offs = hp.pos - hashval;
  if (offs <= 0xffff) {
    if (offs) {
      int max_loops = 8;
      while (offs < dict_size) {
        if (offs > 8) {
          if (*(uint32*)(src_cur - offs) == u32_at_src && (best_ml < 4 || (src_cur + best_ml < src_end_safe && *(src_cur + best_ml) == *(src_cur - offs + best_ml)) )) {
            uint32 ml = 4 + CountMatchingBytes(src_cur + 4, src_end_safe, offs);
            if (ml > best_ml && ml >= min_match_length)
              best_ml = ml, best_offs = offs;
          }
          if (!--max_loops)
            break;
        }
        uint32 offs_old = offs;
        hashval = hasher->nexthash_[(uint16)hashval];
        offs = (uint16)(hp.pos - hashval);
        if (offs <= offs_old)
          break;
      }
    }
  } else if (offs < dict_size && *(uint32*)(src_cur - offs) == u32_at_src) {
    uint32 ml = 4 + CountMatchingBytes(src_cur + 4, src_end_safe, offs);
    if (ml > min_match_length && ml >= minlen[31 - BSR(offs)])
      best_ml = ml, best_offs = offs;
  }
  // Look in the long table
  hashval = hasher->longhash_[hp.hash_b];
  if (((hp.hash_b_hi ^ hashval) & 0x3F) == 0) {
    offs = hp.pos - (hashval >> 6);
    if (offs >= 8 && offs < dict_size && *(uint32*)(src_cur - offs) == u32_at_src) {
      uint32 ml = 4 + CountMatchingBytes(src_cur + 4, src_end_safe, offs);
      if (ml >= minlen[31 - BSR(offs)] && MermaidIsMatchBetter(ml, offs, best_ml, best_offs))
        best_ml = ml, best_offs = offs;
    }
  }
  // Look at offs -8
  if (*(uint32*)(src_cur - 8) == u32_at_src) {
    uint32 ml = 4 + CountMatchingBytes(src_cur + 4, src_end_safe, 8);
    if (ml >= best_ml && ml >= min_match_length)
      best_ml = ml, best_offs = 8;
  }
  hasher->Insert(hp);
  if (!MermaidIsBetterThanRecent(recent_ml, best_ml, best_offs))
    best_ml = recent_ml, best_offs = 0;
  LengthAndOffset match = { (int)best_ml, (int)best_offs };
  return match;
}

static inline bool MermaidIsLazyBetter(LengthAndOffset a, LengthAndOffset b, int step) {
  int bits_a = (a.offset > 0) ? (a.offset > 0xffff ? 32 : 16) : 0;
  int bits_b = (b.offset > 0) ? (b.offset > 0xffff ? 32 : 16) : 0;
  return 5 * (a.length - b.length) - 5 - (bits_a - bits_b) > step * 4;
}

template<int _Level, typename _Hasher>
struct MermaidCompressFast {
  typedef _Hasher Hasher;
  enum { Level = _Level };
  static __forceinline void Run(MermaidWriter &mw, Hasher *hasher, const uint8 *src_cur, const uint8 *src_end_safe, const uint8 *src_end, ptrdiff_t &last_offs, uint dict_size, const uint min_match_length_table[32], int min_match_length) {
    const uint8 *lit_start = src_cur;
    if (src_cur < src_end_safe - 5) {
      hasher->SetHashPos(src_cur);

      while (src_cur < src_end_safe - 5 - 1) {
        LengthAndOffset m = MermaidFast_GetMatch<Hasher>(src_cur, src_end_safe, lit_start, last_offs, hasher, src_cur + 1, dict_size, min_match_length, min_match_length_table);
        if (m.length < 2) {
          src_cur++;
          continue;
        }
        while (src_cur + 1 < src_end_safe - 5) {
          LengthAndOffset m1 = MermaidFast_GetMatch<Hasher>(src_cur + 1, src_end_safe, lit_start, last_offs, hasher, src_cur + 2, dict_size, min_match_length, min_match_length_table);
          if (m1.length >= 2 && MermaidIsLazyBetter(m1, m, 0)) {
            src_cur += 1;  // lazy1 is better
            m = m1;
          } else {
            if (Level <= 3 || src_cur + 2 > src_end_safe - 5 || m.length == 2)
              break;
            LengthAndOffset m2 = MermaidFast_GetMatch<Hasher>(src_cur + 2, src_end_safe, lit_start, last_offs, hasher, src_cur + 3, dict_size, min_match_length, min_match_length_table);
            if (m2.length >= 2 && MermaidIsLazyBetter(m2, m, 1)) {
              src_cur += 2;  // lazy2 is better
              m = m2;
            } else {
              break;
            }
          }
        }
        ptrdiff_t actual_offs = (m.offset == 0) ? -last_offs : m.offset;

        // Reduce literal length and increase match length for bytes preceding the match.
        while (src_cur > lit_start && src_cur - hasher->src_base_ > actual_offs && src_cur[-1] == src_cur[-actual_offs - 1])
          src_cur--, m.length++;

        Mermaid_WriteOffsWithLit1(mw, m.length, src_cur - lit_start, m.offset, last_offs, lit_start);

        last_offs = -actual_offs;
        src_cur += m.length;
        lit_start = src_cur;

        if (src_cur >= src_end_safe - 5)
          break;
        hasher->InsertRange(src_cur - m.length, m.length);
      }
    }
    ptrdiff_t remains = src_end - lit_start;
    if (remains > 0) {
      memcpy(postadd(mw.lit_cur, remains), lit_start, remains);
      if (mw.litsub_cur)
        SubtractBytes(postadd(mw.litsub_cur, remains), lit_start, remains, last_offs);
    }
  }
};

template<int _Level, typename _Hasher>
struct MermaidCompressVeryfast {
  typedef _Hasher Hasher;
  enum { Level = _Level };
  static __forceinline void Run(MermaidWriter &mw, Hasher *hasher, const uint8 *src_cur, const uint8 *src_end_safe, const uint8 *src_end, ptrdiff_t &last_offs, uint dict_size, const uint min_match_length_table[32], int min_match_length) {
    const uint8 *match_end;
    const uint8 *src_base = hasher->src_base_;
    uint64 hashmult = hasher->hashmult_;
    typename Hasher::ElemType *hash_ptr = hasher->hash_ptr_, *hash;
    int hashshift = 64 - hasher->hash_bits_;
    int offs_or_recent;
    enum {
      SkipFactor = (Level <= -3) ? 3 : (Level <= 1) ? 4 : 5,
    };
    int skip = 1 << SkipFactor;
    ptrdiff_t cur_offs;

    const uint8 *lit_start = src_cur;
    if (src_cur < src_end_safe - 5) {
      for (;;) {
        uint32 u32_at_cur = *(uint32*)src_cur;
        hash = &hash_ptr[(size_t)(*(uint64*)src_cur * hashmult >> hashshift)];
        uint32 hashval = *hash;
        *hash = src_cur - src_base;

        uint32 xor_val = u32_at_cur ^ *(uint32*)(src_cur + last_offs);
        if ((xor_val & 0xffffff00) == 0) {
          // 1 byte literal and at least 3 recent match
          src_cur += 1;
          offs_or_recent = 0;
          hash_ptr[(size_t)(*(uint64*)src_cur * hashmult >> hashshift)] = src_cur - src_base;
          cur_offs = last_offs;
          match_end = MermaidMatchLength(src_cur + 3, src_end_safe, cur_offs);
        } else {
          offs_or_recent = (typename Hasher::ElemType)(src_cur - src_base - hashval);
          if (u32_at_cur != *(uint32*)&src_cur[-offs_or_recent])
            goto no_match;

          if ((uint)(offs_or_recent - 8) < (uint)(dict_size - 8)) {
            match_end = MermaidMatchLength(src_cur + 4, src_end_safe, -offs_or_recent);
            if (match_end - src_cur < min_match_length_table[31 - BSR(offs_or_recent)])
              goto no_match;
            cur_offs = -offs_or_recent;
          } else if (u32_at_cur == *(uint32*)(src_cur - 8)) {
            offs_or_recent = 8;
            cur_offs = -8;
            match_end = MermaidMatchLength(src_cur + 4, src_end_safe, cur_offs);
          } else {
no_match:
            if (Level >= 2 && (xor_val & 0xffff) == 0) {
              // check for 2/3 byte recent0 match
              offs_or_recent = 0;
              cur_offs = last_offs;
              match_end = src_cur + ((xor_val & 0xffffff) == 0 ? 3 : 2);
            } else {
              if (src_end_safe - 5 - src_cur <= (skip >> SkipFactor))
                break;
              const uint8 *src_cur_next = src_cur + (skip >> SkipFactor);

              if (Level >= -2)
                skip++;
              else
                skip = std::min<int>(skip + ((src_cur - lit_start) >> 1), 296);

              src_cur = src_cur_next;
              continue;
            }
          }
        }
        // Reduce literal length and increase match length for bytes preceding the match.
        while (src_cur > lit_start && (src_base - src_cur) < cur_offs &&
               src_cur[-1] == src_cur[cur_offs - 1])
          src_cur--;

        int ml = match_end - src_cur;
        Mermaid_WriteOffs(mw, ml,
                          src_cur - lit_start, offs_or_recent, last_offs, lit_start);

        lit_start = src_cur = match_end;
        skip = 1 << SkipFactor;
        last_offs = cur_offs;
        if (src_cur >= src_end_safe - 5)
          break;
        if (Level >= 2) {
          const uint8 *matchstart = src_cur - ml;
          for (int i = 1; i < ml; i *= 2)
            hash_ptr[(size_t)(*(uint64*)(i + matchstart) * hashmult >> hashshift)] = i + matchstart - src_base;
        }
      }
    }
    ptrdiff_t remains = src_end - lit_start;
    if (remains > 0) {
      memcpy(postadd(mw.lit_cur, remains), lit_start, remains);
      if (mw.litsub_cur)
        SubtractBytes(postadd(mw.litsub_cur, remains), lit_start, remains, last_offs);
    }
  }
};

static void MermaidBuildMatchLengths(uint *tab, int min_matchlen, int v) {
  tab[0] = tab[1] = tab[2] = tab[3] = tab[4] = tab[5] = tab[6] = tab[7] = tab[8] = tab[9] = 32;
  tab[10] = tab[11] = v * 2 - 6;
  tab[12] = tab[13] = tab[14] = tab[15] = v;
  tab[16] = tab[17] = tab[18] = tab[19] = tab[20] = tab[21] = tab[22] = tab[23] = min_matchlen;
  tab[24] = tab[25] = tab[26] = tab[27] = tab[28] = tab[29] = tab[30] = tab[31] = min_matchlen;
}

template<typename Function>
static int MermaidCompressLoop(LzCoder *coder, LzTemp *lztemp, MatchLenStorage *mls_unused,
                    const uint8 *src, int src_size,
                    uint8 *dst, uint8 *dst_end,
                    int start_pos, int *chunk_type_ptr, float *cost_ptr) {
  *chunk_type_ptr = -1;
  *cost_ptr = kInvalidCost;
  if (src_size <= 128)
    return src_size;

  bool is_mermaid = coder->codec_id == kCompressorMermaid;

  uint dict_size = coder->opts->dictionarySize > 0 && coder->opts->dictionarySize <= 0x40000000 ?
      coder->opts->dictionarySize : 0x40000000;
  int min_match_length = std::max(coder->opts->min_match_length, 4);

  MermaidWriter mw;
  MermaidWriter_Init(&mw, src_size, src, is_mermaid && (Function::Level >= 0));

  uint min_match_length_table[32];
  MermaidBuildMatchLengths(min_match_length_table, min_match_length, is_mermaid ? 10 : 14);
  int initial_copy_bytes = (start_pos == 0) ? 8 : 0;
   
  // Selkie writes lits directly into the buffer
  if (!is_mermaid)
    mw.lit_start = mw.lit_cur = dst + 3 + initial_copy_bytes;

  ptrdiff_t last_offs = -8;
  typename Function::Hasher *hasher = (typename Function::Hasher*)coder->hasher;

  for (int iteration = 0; iteration < 2; iteration++) {
    const uint8 *src_cur, *block_end;

    if (iteration == 0) {
      src_cur = src + initial_copy_bytes;
      block_end = src + mw.block1_size;
      mw.cur_block_start_2 = 0; 
    } else {
      mw.tok_stream_2_offs = mw.token_cur - mw.token_start;
      if (!mw.block2_size)
        break;
      src_cur = src + mw.block1_size;
      block_end = src_cur + mw.block2_size;
      mw.cur_block_start_2 = mw.block1_size;
    }
    mw.off32_count = 0;
    Function::Run(mw, hasher, src_cur, std::min(block_end, src + src_size - 16), block_end, last_offs, dict_size, min_match_length_table, min_match_length);
    if (iteration == 0)
      mw.off32_count_1 = mw.off32_count;
    else
      mw.off32_count_2 = mw.off32_count;

  }
  return Mermaid_WriteLzTable(cost_ptr, chunk_type_ptr, 0, dst, dst_end, coder, lztemp, &mw, start_pos);
}


static LengthAndOffset Mermaid_GetLzMatch(const LengthAndOffset *lao, int recent0,
                                          const uint8 *src_cur, const uint8 *src_end,
                                          int lrl, int min_match_len,
                                          const uint *min_match_lens, const uint8 *window_base, uint dict_size) {
  int min_match_len_recent = 2;
  if (lrl >= 64) {
    min_match_len_recent = min_match_len - 1;
    min_match_len += min_match_len >> 1;
  }
  uint32 u32_at_cur = *(uint32*)src_cur;
  int recent_ml = GetMatchlengthQ(src_cur, recent0, src_end, u32_at_cur);
  if (recent_ml >= min_match_len_recent) {
    if (recent_ml >= 4) {
      LengthAndOffset m = {recent_ml, 0};
      return m;
    }
  } else {
    recent_ml = 0;
  }
  int best_ml = 0;
  int best_offs = 0;
  for (size_t midx = 0; midx != 4; midx++) {
    int ml = lao[midx].length;
    if (ml < min_match_len)
      break;
    if (ml > src_end - src_cur) {
      ml = src_end - src_cur;
      if (ml < min_match_len)
        break;
    }
    uint offs = lao[midx].offset;
    if (offs >= dict_size)
      continue;

    if (offs < 8) {
      // Expand the offset to at least 8 bytes long
      uint tt = offs;
      do offs += tt; while (offs < 8);
      if (offs > src_cur - window_base)
        continue;
      ml = GetMatchlengthQMin4(src_cur, offs, src_end, u32_at_cur);
      if (ml < min_match_len)
        continue;
    }
    if (ml >= min_match_lens[31 - BSR(offs)] && MermaidIsMatchBetter(ml, offs, best_ml, best_offs))
      best_ml = ml, best_offs = offs;
  }
  if (!MermaidIsBetterThanRecent(recent_ml, best_ml, best_offs))
    best_ml = recent_ml, best_offs = 0;
  LengthAndOffset match = { (int)best_ml, (int)best_offs };
  return match;
}

static int MermaidCollectStats(float *cost_ptr, int *chunk_type_ptr, MermaidHistos *histos,
                               uint8 *dst, uint8 *dst_end,
                               LzCoder *coder, LzTemp *lztemp,
                               const uint8 *src, int src_len, int start_pos, int initial_copy_bytes,
                               LengthAndOffset *lao, int min_match_len, int minmatch_param,
                               const uint8 *window_base, uint dict_size) {
  bool is_mermaid = coder->codec_id == kCompressorMermaid;

  uint min_match_lens[32];
  MermaidBuildMatchLengths(min_match_lens, min_match_len, minmatch_param);

  MermaidWriter mw;
  MermaidWriter_Init(&mw, src_len, src, true);
  if (!is_mermaid)
    mw.lit_start = mw.lit_cur = dst + 3 + initial_copy_bytes;

  int recent0 = 8;

  for (int iteration = 0; iteration < 2; iteration++) {
    int pos, pos_end;

    if (iteration == 0) {
      pos = initial_copy_bytes;
      pos_end = mw.block1_size;
      mw.cur_block_start_2 = 0;
    } else {
      mw.tok_stream_2_offs = mw.token_cur - mw.token_start;
      if (!mw.block2_size)
        break;
      pos = mw.block1_size;
      pos_end = pos + mw.block2_size;
      mw.cur_block_start_2 = mw.block1_size;
    }
    mw.off32_count = 0;

    const uint8 *src_end = src + std::min(src_len - 16, pos_end);
    int bytes_minus5 = src_end - src - 5;

    int lit_start = pos;
    while (pos < bytes_minus5) {
      LengthAndOffset m = Mermaid_GetLzMatch(&lao[4 * pos], recent0, src + pos, src_end, pos - lit_start, min_match_len, min_match_lens, window_base, dict_size);
      if (!m.length) {
        pos++;
        continue;
      }
      while (pos + 1 < bytes_minus5) {
        LengthAndOffset m1 = Mermaid_GetLzMatch(&lao[4 * (pos + 1)], recent0, src + pos + 1, src_end, pos + 1 - lit_start, min_match_len, min_match_lens, window_base, dict_size);
        if (m1.length >= 2 && MermaidIsLazyBetter(m1, m, 0)) {
          pos += 1;  // lazy1 is better
          m = m1;
        } else {
          if (pos + 2 >= bytes_minus5)
            break;
          LengthAndOffset m2 = Mermaid_GetLzMatch(&lao[4 * (pos + 2)], recent0, src + pos + 2, src_end, pos + 2 - lit_start, min_match_len, min_match_lens, window_base, dict_size);
          if (m2.length >= 2 && MermaidIsLazyBetter(m2, m, 1)) {
            pos += 2;  // lazy2 is better
            m = m2;
          } else {
            break;
          }
        }
      }
      int actual_offs = (m.offset == 0) ? recent0 : m.offset;

      int lrl = pos - lit_start;
      if (m.length == 16 || m.length == 31) {
        m.length--;
        if (m.offset <= 0xffff && MermaidMod7(lrl) < 7)
          pos++, lrl++;
      }

      Mermaid_WriteOffsWithLit1(mw, m.length, lrl, m.offset, -recent0, src + lit_start);

      pos += m.length;
      recent0 = actual_offs;
      lit_start = pos;
    }
    size_t remains = pos_end - lit_start;
    if (remains > 0) {
      memcpy(postadd(mw.lit_cur, remains), src + lit_start, remains);
      if (mw.litsub_cur)
        SubtractBytes(postadd(mw.litsub_cur, remains), src + lit_start, remains, -recent0);
    }
    if (iteration == 0)
      mw.off32_count_1 = mw.off32_count;
    else
      mw.off32_count_2 = mw.off32_count;
  }
  int opts_org = coder->entropy_opts;
  int level_org = coder->compression_level;
  coder->entropy_opts &= ~kEntropyOpt_MultiArray;
  coder->compression_level = 4;
  int n = Mermaid_WriteLzTable(cost_ptr, chunk_type_ptr, histos, dst, dst_end, coder, lztemp, &mw, start_pos);
  coder->compression_level = level_org;
  coder->entropy_opts = opts_org;
  return n;
}

struct MermaidToken {
  int lrl;
  int ml;
  int offs;
};

struct MermaidTokArray {
  MermaidToken *data;
  int size, capacity;
};

struct MermaidState {
  int bits, ml, lrl, offs, extracopy;
};

static int Mermaid_WriteTokens(float *cost_ptr, int *chunk_type_ptr, MermaidHistos *histos,
                               uint8 *dst, uint8 *dst_end,
                               LzCoder *coder, LzTemp *lztemp, const uint8 *src, int src_len,
                               int start_pos, MermaidTokArray *tok_array, int middle_token_count) {
  MermaidWriter mw;
  bool is_mermaid = coder->codec_id == kCompressorMermaid;
  MermaidWriter_Init(&mw, src_len, src, is_mermaid);
  int initial_copy_bytes = (start_pos == 0) ? 8 : 0;
  if (!is_mermaid)
    mw.lit_start = mw.lit_cur = dst + 3 + initial_copy_bytes;
  int recent0 = 8;
  for (int iteration = 0; iteration < 2; iteration++) {
    int pos, pos_end, tok, tok_end;
    if (iteration == 0) {
      pos = initial_copy_bytes;
      pos_end = mw.block1_size;
      mw.cur_block_start_2 = 0;
      tok = 0;
      tok_end = middle_token_count;
    } else {
      mw.tok_stream_2_offs = mw.token_cur - mw.token_start;
      if (!mw.block2_size)
        break;
      pos = mw.block1_size;
      pos_end = pos + mw.block2_size;
      mw.cur_block_start_2 = mw.block1_size;
      tok = middle_token_count;
      tok_end = tok_array->size;
    }
    mw.off32_count = 0;
    for (; tok < tok_end; tok++) {
      const MermaidToken *mt = &tok_array->data[tok];
      Mermaid_WriteOffs(mw, mt->ml, mt->lrl, mt->offs, -recent0, src + pos);
      recent0 = mt->offs ? mt->offs : recent0;
      pos += mt->ml + mt->lrl;
    }
    size_t remains = pos_end - pos;
    if (remains > 0) {
      memcpy(postadd(mw.lit_cur, remains), src + pos, remains);
      if (mw.litsub_cur)
        SubtractBytes(postadd(mw.litsub_cur, remains), src + pos, remains, -recent0);
    }
    if (iteration == 0)
      mw.off32_count_1 = mw.off32_count;
    else
      mw.off32_count_2 = mw.off32_count;
  }
  int n = Mermaid_WriteLzTable(cost_ptr, chunk_type_ptr, histos, dst, dst_end, coder, lztemp, &mw, start_pos);
  return n;
}

static int Mermaid_GetBytesForRaw(float *cost_ptr, int *chunk_type_out, MermaidHistos *mh,
                                  uint8 *dst, uint8 *dst_end,
                                  LzCoder *coder, LzTemp *lztemp,
                                  const uint8 *src, int src_len, int start_pos, int initial_copy_bytes) {
  MermaidWriter mw;
  MermaidWriter_Init(&mw, src_len, src, true);
  SubtractBytes(mw.litsub_cur, src + initial_copy_bytes, src_len - initial_copy_bytes, -8);
  mw.litsub_cur += src_len - initial_copy_bytes;
  return Mermaid_WriteLzTable(cost_ptr, chunk_type_out, mh, dst, dst_end, coder, lztemp, &mw, start_pos);
}

static void Mermaid_StateToTok(MermaidTokArray *arr, const MermaidState *S, int start_pos, int end_pos) {
  int pos = end_pos;
  int start_count = arr->size;
  while (pos > start_pos) {
    const MermaidState *st = &S[pos];
    uint32 xc = st->extracopy;
    if (xc) {
      pos -= (xc >> 8) + (uint8)xc;
      assert(arr->size < arr->capacity);
      MermaidToken *tok = &arr->data[arr->size++];
      tok->lrl = (uint8)xc;
      tok->ml = xc >> 8;
      tok->offs = 0;
    }
    pos -= st->lrl + st->ml;
    assert(arr->size < arr->capacity);
    MermaidToken *tok = &arr->data[arr->size++];
    tok->lrl = st->lrl;
    tok->ml = st->ml;
    tok->offs = (st->offs == S[pos].offs) ? 0 : st->offs;
  }
  std::reverse(arr->data + start_count, arr->data + arr->size);
}

static __forceinline void CheckBetter(MermaidState *st, int pos, int bits, int lrl, int ml, int offs,  int *max_offs, int extra = 0) {
  if (bits < st->bits) {
    st->bits = bits;
    st->lrl = lrl;
    st->ml = ml;
    st->offs = offs;
    st->extracopy = extra;
    if (max_offs)
      *max_offs = std::max(*max_offs, pos);
  }
}

static __forceinline bool GetNumLitsBeforeMatch(const uint8 *src, uint offs, uint *lits) {
  // There is yet a faster method � use hasless(v, 1), which is defined below; it works in 4
  // operations and requires no subsquent verification. It simplifies to
  // #define haszero(v) (((v) - 0x01010101UL) & ~(v) & 0x80808080UL)
  uint64 v = *(uint64*)src ^ *(uint64*)(src - offs);
  uint64 x = (v - 0x0101010101010101ull) & ~v & 0x8080808080808080ull;
  if (x == 0)
    return false;
  *lits = BSF64(x) >> 3;
  return true;
}

static __forceinline void CheckQuickMatchAfter(MermaidState *S, int cur_pos, int end_pos,
                                 int ml, int lrl, int offs, int bits_base,
                                 const uint8 *src, const uint8 *src_end) {
  uint lit_bytes;
  if (GetNumLitsBeforeMatch(&src[ml], offs, &lit_bytes)) {
    if (cur_pos + ml + lit_bytes < end_pos) {
      int n = CountMatchingBytes(&src[ml + lit_bytes], src_end, offs);
      if (n)  {
        int bits = bits_base + 0x110 + (lit_bytes << 8);
        int n_min_15 = std::min(n, 15);
        CheckBetter(&S[cur_pos + ml + n_min_15 + lit_bytes], cur_pos + ml + n_min_15 + lit_bytes,
                    bits, lrl, ml, offs, NULL, n_min_15 << 8 | lit_bytes);
      }
    }
  }
}


static void Selkie_DoOptimal(MermaidState *S, int *offs_ptr, int *final_pos_ptr,
                             LzCoder *coder, int start_pos, int end_pos,
                             const uint8 *src, int round_start, int round_end,
                             LengthAndOffset *lao, const uint8 *src_end_safe, int min_match_len,
                             int minmatch_long, const uint8 *window_base) {

  uint dict_size = coder->opts->dictionarySize > 0 && coder->opts->dictionarySize <= 0x40000000 ?
    coder->opts->dictionarySize : 0x40000000;

  uint min_match_lens[32];
  MermaidBuildMatchLengths(min_match_lens, min_match_len, minmatch_long);

  int max_offset = start_pos;
  int pos = start_pos;
  int lit_start = start_pos;

  while (pos < end_pos) {
    const uint8 *src_cur = src + pos;
    uint32 u32_at_cur = *(uint32*)src_cur;
    MermaidState *cur_st = &S[pos];

    if (pos - lit_start && cur_st->bits != INT_MAX) {
      int bits = S[lit_start].bits + (pos - lit_start) * 256;
      if (bits <= cur_st->bits) {
        if ((pos - lit_start) >= 64)
          CheckBetter(cur_st, pos, bits + 0x210, (pos - lit_start), 0, S[lit_start].offs, NULL);
      } else if (cur_st->ml > 1) {
        lit_start = pos;
      }
    }

    if (cur_st->bits != INT_MAX && pos + 7 <= end_pos) {
      int bits = cur_st->bits;
      uint offs = cur_st->offs;
      if (*(src_cur + 6) == *(src_cur + 6 - offs)) {
        CheckBetter(cur_st + 7, pos + 7, bits + 0x710, 6, 1, offs, &max_offset);
      } else {
        CheckBetter(cur_st + 7, pos + 7, bits + 0x810, 7, 0, offs, &max_offset);
        if (*(src_cur + 7) == *(src_cur + 7 - offs))
          CheckBetter(cur_st + 8, pos + 8, bits + 0x810, 7, 1, offs, &max_offset);
      }
    }

    int best_lrl = 0;
    int best_lrl_bits = cur_st->bits;
    int nback = std::min(pos - start_pos, 7);

    if (int lrl = pos - lit_start) {
      if (lrl > 7) {
        for (int i = 1; i <= nback; i++) {
          if (cur_st[-i].bits != INT_MAX && cur_st[-i].bits + 256 * i < best_lrl_bits) {
            best_lrl_bits = cur_st[-i].bits + 256 * i;
            best_lrl = i;
          }
        }
      } else {
        best_lrl = lrl;
        best_lrl_bits = S[lit_start].bits + lrl * 256;
      }
    }

    uint best_ml = 0;
    uint best_offs = 0;
    bool has_complex_token = false;
    LengthAndOffset match[5];
    size_t num_match = 0;
    LengthAndOffset *lao_cur = lao + 4 * pos;

    for (int tlrl = 0; tlrl <= nback; tlrl++) {
      MermaidState *tmp_st = cur_st - tlrl;
      if (tmp_st->bits == INT_MAX)
        continue;
      uint offs = tmp_st->offs;
      if (tlrl != best_lrl && offs == cur_st[-best_lrl].offs)
        continue;
      int tbits = tmp_st->bits + tlrl * 0x100 + 0x110;
      int ml = CountMatchingBytes(src_cur, src_end_safe, offs);
      if (!ml)
        continue;
      if (ml >= 91) {
        pos += ml;
        CheckBetter(cur_st + ml, pos, tbits, tlrl, ml, offs, &max_offset);
        lit_start = pos;
        goto next_outer_loop;
      }
      if (ml <= 15) {
        CheckBetter(cur_st + ml, pos + ml, tbits, tlrl, ml, offs, &max_offset);
        CheckQuickMatchAfter(S, pos, end_pos, ml, tlrl, offs, tbits, src_cur, src_end_safe);
        if (ml > 2)
          CheckBetter(cur_st + ml - 1, pos + ml - 1, tbits, tlrl, ml - 1, offs, &max_offset);
      } else {
        int ubits = tbits;
        int jj = ml - 15;
        for (; jj > 1; jj -= std::min(jj, 15))
          ubits += 0x110;
        CheckBetter(cur_st + ml - jj, pos + ml - jj, ubits, tlrl, ml - jj, offs, &max_offset);
        CheckBetter(cur_st + 15, pos + 15, tbits, tlrl, 15, offs, &max_offset);
        CheckBetter(cur_st + 14, pos + 14, tbits, tlrl, 14, offs, &max_offset);
      }
    }

    if (lao_cur->length >= min_match_len) {
      for (size_t lao_index = 0; lao_index < 4; lao_index++) {
        uint lao_ml = lao_cur[lao_index].length;
        uint lao_offs = lao_cur[lao_index].offset;
        if (lao_ml < min_match_len)
          break;
        lao_ml = std::min<uint>(lao_ml, src_end_safe - src_cur);
        if (lao_offs >= dict_size)
          continue;
        if (lao_offs < 8) {
          // Expand the offset to at least 8 bytes long
          uint tt = lao_offs;
          do lao_offs += tt; while (lao_offs < 8);
          // Check if it's a valid offset and still match
          if (lao_offs > src_cur - window_base)
            continue;
          lao_ml = GetMatchlengthQMin4(src_cur, lao_offs, src_end_safe, u32_at_cur);
          if (lao_ml < min_match_len)
            continue;
        }
        if (lao_ml >= min_match_lens[31-BSR(lao_offs)]) {
          match[num_match].length = lao_ml;
          match[num_match++].offset = lao_offs;
          if (lao_offs > 0xffff)
            has_complex_token = true;
          if (lao_ml > best_ml) {
            best_ml = lao_ml;
            best_offs = lao_offs;
            if (lao_ml >= 91)
              has_complex_token = true;
          }
        }
      }

      // Also always check offset 8
      int ml8 = GetMatchlengthQMin4(src_cur, 8, src_end_safe, u32_at_cur);
      if (ml8 >= min_match_len) {
        match[num_match].length = ml8;
        match[num_match++].offset = 8;
        if (ml8 > best_ml) {
          best_ml = ml8;
          best_offs = 8;
          if (ml8 >= 91)
            has_complex_token = true;
        }
      }

      if (num_match) {
        if (has_complex_token) {
          uint lrl = 0;
          int bits = cur_st->bits;
          if (best_lrl && best_lrl_bits + 0x110 < bits) {
            bits = best_lrl_bits + 0x110;
            lrl = best_lrl;
          }
          if (best_ml >= 91) {
            pos += best_ml;
            CheckBetter(cur_st + best_ml, pos, bits + 0x150, lrl, best_ml, best_offs, &max_offset);
            lit_start = pos;
            continue;
          }
          uint org_offs = cur_st[-(intptr_t)lrl].offs;
          for (size_t midx = 0; midx < num_match; midx++) {
            uint ml = match[midx].length;
            uint offs = match[midx].offset;
            if (offs <= 0xffff || offs == org_offs)
              continue;
            if (ml < minmatch_long)
              break;

            int tbits = bits + ((round_start + offs - pos < 0xc00000) ? 0x450 : 0x550);
            tbits += 0x80;
            if (offs > 0x100000)
              tbits += 0x80;
            if (offs > 0x400000)
              tbits += 0x200;
            if (ml >= 29)
              tbits += ((ml > 251 + 29 ? 3 : 1) * 256) + 24;
            CheckBetter(cur_st + ml, pos + ml, tbits, lrl, ml, offs, &max_offset);
            CheckQuickMatchAfter(S, pos, end_pos, ml, lrl, offs, tbits, src_cur, src_end_safe);
            if (ml > minmatch_long)
              CheckBetter(cur_st + ml - 1, pos + ml - 1, tbits, lrl, ml - 1, offs, &max_offset);
          }
        }

        int tbits = best_lrl_bits + 0x310;
        for (size_t midx = 0; midx < num_match; midx++) {
          uint ml = match[midx].length;
          uint offs = match[midx].offset;
          if (offs > 0xffff)
            continue;

          if (ml <= 15) {
            CheckBetter(cur_st + ml, pos + ml, tbits, best_lrl, ml, offs, &max_offset);
            CheckQuickMatchAfter(S, pos, end_pos, ml, best_lrl, offs, tbits, src_cur, src_end_safe);
            if (ml > min_match_len) {
              CheckBetter(cur_st + ml - 1, pos + ml - 1, tbits, best_lrl, ml - 1, offs, &max_offset);
              if (ml > min_match_len + 1)
                CheckBetter(cur_st + ml - 2, pos + ml - 2, tbits, best_lrl, ml - 2, offs, &max_offset);
            }
          } else {
            int tml = 15;
            int ttbits = tbits;
            for (;;) {
              CheckBetter(cur_st + tml, pos + tml, ttbits, best_lrl, tml, offs, &max_offset);
              int n = ml - tml;
              if (n >= 15) {
                n = 15;
              } else if (n <= 1) {
                if (n == 0)
                  CheckQuickMatchAfter(S, pos, end_pos, tml, best_lrl, offs, ttbits, src_cur, src_end_safe);
                break;
              }
              tml += n;
              ttbits += 0x110;
            }
          }
        } // for
      } // if (num_match)
    } 
    pos++;
next_outer_loop:;
  } // while (pos < end_pos)
  int best_final_pos = 0;
  int best_cost = INT_MAX;
  for (int final_pos = std::max(start_pos, end_pos - 16); final_pos <= round_end; final_pos++) {
    if (S[final_pos].bits != INT_MAX && S[final_pos].bits + (round_end - final_pos) * 0x100 < best_cost) {
      best_cost = S[final_pos].bits + (round_end - final_pos) * 0x100;
      best_final_pos = final_pos;
    }
  }
  if (lit_start < end_pos && S[lit_start].bits + (round_end - lit_start) * 0x100 < best_cost)
    best_final_pos = lit_start;
  *final_pos_ptr = best_final_pos;
  *offs_ptr = S[best_final_pos].offs;
}

struct Mermaid {
  static void RescaleOne(HistoU8 *h, int k) {
    for (size_t i = 0; i != 256; i++)
      h->count[i] = (h->count[i] >> 4) + k;
  }

  static void RescaleAddOne(HistoU8 *h, const HistoU8 *t, int k) {
    for (size_t i = 0; i != 256; i++)
      h->count[i] = ((h->count[i] + t->count[i]) >> 5) + k;
  }

  static void RescaleAdd(MermaidHistos *s, const MermaidHistos *t) {
    RescaleAddOne(&s->lit, &t->lit, 1);
    RescaleAddOne(&s->tok, &t->tok, 1);
    RescaleAddOne(&s->off16hi, &t->off16hi, 2);
    RescaleAddOne(&s->off16lo, &t->off16lo, 2);

  }

  static void Rescale(MermaidHistos *s) {
    RescaleOne(&s->lit, 1);
    RescaleOne(&s->tok, 1);
    RescaleOne(&s->off16hi, 2);
    RescaleOne(&s->off16lo, 2);
  }

  struct CostModel {
    uint lit[256];
    uint tok[256];
    uint off16hi[256];
    uint off16lo[256];
  };

  static void MakeCostModel(MermaidHistos *h, CostModel *cm) {
    ConvertHistoToCost(h->lit, cm->lit, 0, 240);
    ConvertHistoToCost(h->tok, cm->tok, 16, 240);
    ConvertHistoToCost(h->off16hi, cm->off16hi, 0, 240);
    ConvertHistoToCost(h->off16lo, cm->off16lo, 0, 240);
  }

  static uint BitsForLitlen(CostModel &cm, int lrl) {
    if (lrl <= 7)
      return 0;
    if (lrl < 64)
      return cm.tok[0x87] * ((lrl - 8) / 7 + 1);
    else
      return 0x58 + cm.tok[0] + (lrl > 251 + 64 ? 3 : 1) * 256;
  }

  static uint BitsForLit(CostModel &cm, const uint8 *src, int recent0, uint8 mask) {
    return cm.lit[(uint8)(*src - (mask & *(src - recent0)))];
  }


  static uint BitsForLits(CostModel &cm, const uint8 *src, size_t len, uint recent0, uint8 mask) {
    uint bits = 0;
    for (size_t i = 0; i < len; i++)
      bits += cm.lit[(uint8)(src[i] - (mask & src[i - recent0]))];
    return bits;
  }
};

static inline void CheckQuickMatchAfterMermaid(MermaidState *S, int cur_pos, int end_pos,
                                 int ml, int lrl, int offs, int bits_base,
                                 const uint8 *src_cur, const uint8 *src_end, Mermaid::CostModel &cm, uint8 chunk_type_mask, int *chunk_max) {
  uint lit_bytes;
  if (GetNumLitsBeforeMatch(&src_cur[ml], offs, &lit_bytes)) {
    if (cur_pos + ml + lit_bytes < end_pos) {
      int n = CountMatchingBytes(&src_cur[ml + lit_bytes], src_end, offs);
      if (n) {
        n = std::min(n, 15);
        int bits = bits_base + cm.tok[0x80 + lit_bytes + n * 8] + Mermaid::BitsForLits(cm, &src_cur[ml], lit_bytes, offs, chunk_type_mask);
        CheckBetter(&S[cur_pos + ml + n + lit_bytes], cur_pos + ml + n + lit_bytes, bits, lrl, ml, offs, chunk_max, n << 8 | lit_bytes);
      }
    }
  }
}

static void Mermaid_UpdateStats(int chunk_type, MermaidTokArray *arr, int old_size, int pos, MermaidHistos *stats, const uint8 *src, int recent0) {
  MermaidToken *tok = &arr->data[old_size];
  for (int p = old_size; p < arr->size; p++, tok++) {
    uint lrl = tok->lrl;
    uint ml = tok->ml;
    const uint8 *srccur = src + pos;
    pos += tok->ml + tok->lrl;
    if (chunk_type == 0) {
      for (size_t i = 0; i < lrl; i++)
        stats->lit.count[(uint8)(srccur[i] - srccur[i - recent0])] += 2;
    } else {
      for (size_t i = 0; i < lrl; i++)
        stats->lit.count[srccur[i]] += 2;
    }

    if (lrl >= 64) {
      stats->tok.count[0] += 2;
      lrl = 0;
      if (ml == 0)
        continue;
    }

    uint offs = tok->offs;
    recent0 = offs ? offs : recent0;
    if (lrl > 7) {
      int n = (lrl - 8) / 7 + 1;
      lrl -= 7 * n;
      stats->tok.count[0x87] += 2 * n;
    }

    if (offs && offs < 65536) {
      stats->off16lo.count[offs & 0xff] += 2;
      stats->off16hi.count[offs >> 8] += 2;
    }

    if (offs >= 65536 || ml >= 91) {
      if (lrl > 0)
        stats->tok.count[0x80 + lrl] += 2;
      if (recent0 >= 65536) {
        stats->tok.count[ml <= 28 ? ml - 5 : 2] += 2;
      } else {
        stats->tok.count[1] += 2;
      }
    } else if (ml | lrl) {
      uint n = std::min<uint>(ml, 15);
      stats->tok.count[(offs == 0) * 0x80 + n * 8 + lrl] += 2;
      while (ml -= n) {
        n = std::min<uint>(ml, 15);
        stats->tok.count[0x80 + n * 8] += 2;
      }
    }
  }
}

void Mermaid_DoOptimal(MermaidTokArray *arr, MermaidState *S, int *recent0_ptr,
                       LzCoder *coder, LzTemp *temp, MermaidHistos *stats,
                       int chunk_type, int pos_start_cur, int src_len_minus5,
                       const uint8 *src, int pos_start, int pos_end,
                       LengthAndOffset *lao, const uint8 *src_end_safe,
                       int min_match_len, int minmatch_long, const uint8 *window_base) {
  if (pos_start_cur < 0x10000) {
    if (coder->last_chunk_type == chunk_type) {
      Mermaid::RescaleAdd(stats, (MermaidHistos*)coder->lvsymstats_scratch.ptr);
    } else {
      Mermaid::Rescale(stats);
    }
  }

  Mermaid::CostModel cm;
  Mermaid::MakeCostModel(stats, &cm);

  if (coder->compression_level >= 7)
    min_match_len = std::max(coder->opts->min_match_length, 3);

  uint dict_size = coder->opts->dictionarySize > 0 && coder->opts->dictionarySize <= 0x40000000 ?
    coder->opts->dictionarySize : 0x40000000;

  uint min_match_lens[32];
  MermaidBuildMatchLengths(min_match_lens, min_match_len, minmatch_long);

  uint8 chunk_type_mask = (chunk_type == 1) ? 0 : 255;
  int chunk_start = pos_start_cur;
  int best_final_pos = pos_start_cur;
  while (chunk_start < src_len_minus5) {
    int lit_bits = 0;
    int lit_start = chunk_start;
    int chunk_max = std::min(chunk_start + 1024, src_len_minus5);
    int chunk_end = std::min(chunk_start + 4096, src_len_minus5);
    int pos = chunk_start;

    while (chunk_max <= chunk_end) {
next_outer_loop:
      if (pos == src_len_minus5) {
        chunk_max = pos;
        break;
      }
      const uint8 *src_cur = src + pos;
      MermaidState *cur_st = S + pos;
      uint32 u32_at_cur = *(uint32*)src_cur;

      // Accumulate lit bits since last pos 
      int lrl_cur = pos - lit_start;
      if (lrl_cur) {
        lit_bits += cm.lit[(uint8)(src_cur[-1] - (chunk_type_mask & src_cur[-1-S[lit_start].offs]))];
        if (cur_st->bits != INT_MAX) {
          if (lrl_cur >= 64) {
            int bits = lit_bits + S[lit_start].bits + cm.tok[0] + (lrl_cur > 251 + 64 ? 3 : 1) * 256;
            CheckBetter(cur_st, pos, bits, lrl_cur, 0, S[lit_start].offs, NULL);
          }
          if (cur_st->ml > 1 && S[lit_start].bits + lit_bits + Mermaid::BitsForLitlen(cm, lrl_cur) >= cur_st->bits) {
            lit_start = pos;
            lit_bits = 0;
          }
        }
      }

      // Reached end?
      if (pos + 8 >= chunk_max && cur_st->bits != INT_MAX && cur_st->ml > 1 && pos - chunk_start >= 1024) {
        int pn = pos;
        do {
          pn++;
          if (pn > chunk_max) {
            for (int k = pos + 1; k <= chunk_max; k++) {
              S[k].bits = INT_MAX;
              S[k].ml = 0;
            }
            chunk_max = pos;
            goto exit_chunk;
          }
        } while (S[pn].bits == INT_MAX || S[pn].ml <= 1);
      }

      if (cur_st->bits != INT_MAX && pos + 7 < src_len_minus5) {
        int bits = cur_st->bits;
        uint offs = cur_st->offs;
        if (*(src_cur + 6) == *(src_cur + 6 - offs)) {
          int tbits = bits + Mermaid::BitsForLits(cm, src_cur, 6, offs, chunk_type_mask);
          CheckBetter(cur_st + 7, pos + 7, tbits + cm.tok[0x8E], 6, 1, offs, &chunk_max);
          // NOTE: Bug in oodle!!
          bits = tbits;
        }
        bits += Mermaid::BitsForLits(cm, src_cur, 7, offs, chunk_type_mask);

        CheckBetter(cur_st + 7, pos + 7, bits + cm.tok[0x87], 7, 0, offs, &chunk_max);
        if (*(src_cur + 7) == *(src_cur + 7 - offs))
          CheckBetter(cur_st + 8, pos + 8, bits + cm.tok[0x8F], 7, 1, offs, &chunk_max);
      }

      uint best_ml = 0;
      uint best_offs = 0;
      bool has_complex_match = false;
      LengthAndOffset match[5];
      size_t num_match = 0;
      LengthAndOffset *lao_cur = lao + 4 * pos;

      for (size_t lao_index = 0; lao_index < 4; lao_index++) {
        uint lao_ml = lao_cur[lao_index].length;
        uint lao_offs = lao_cur[lao_index].offset;
        if (lao_ml < min_match_len)
          break;
        lao_ml = std::min<uint>(lao_ml, src_end_safe - src_cur);
        if (lao_offs >= dict_size)
          continue;
        if (lao_offs < 8) {
          // Expand the offset to at least 8 bytes long
          uint tt = lao_offs;
          do lao_offs += tt; while (lao_offs < 8);
          // Check if it's a valid offset and still match
          if (lao_offs > src_cur - window_base)
            continue;
          lao_ml = GetMatchlengthQMin4(src_cur, lao_offs, src_end_safe, u32_at_cur);
          if (lao_ml < min_match_len)
            continue;
        }
        if (lao_ml >= min_match_lens[31 - BSR(lao_offs)]) {
          match[num_match].length = lao_ml;
          match[num_match++].offset = lao_offs;
          if (lao_offs > 0xffff)
            has_complex_match = true;
          if (lao_ml > best_ml) {
            best_ml = lao_ml;
            best_offs = lao_offs;
            if (lao_ml >= 91)
              has_complex_match = true;
          }
        }
      }
      // Also always check offset 8
      int ml8 = GetMatchlengthQMin4(src_cur, 8, src_end_safe, u32_at_cur);
      if (ml8 >= min_match_len) {
        match[num_match].length = ml8;
        match[num_match++].offset = 8;
        if (ml8 > best_ml) {
          best_ml = ml8;
          best_offs = 8;
          if (ml8 >= 91)
            has_complex_match = true;
        }
      }

      int nback = std::min(pos - chunk_start, 7);

      if (has_complex_match) {
        uint lrl = 0;
        uint best_bits = cur_st->bits;
        for (int i = 1; i <= nback; i++) {
          if (cur_st[-i].bits != INT_MAX) {
            uint bits = cur_st[-i].bits + cm.tok[0x80 + i] + Mermaid::BitsForLits(cm, src_cur - i, i, cur_st[-i].offs, chunk_type_mask);
            if (bits < best_bits)
              best_bits = bits, lrl = i;
          }
        }
        best_bits += 64;
        if (best_ml >= 91) {
          pos += best_ml;
          CheckBetter(cur_st + best_ml, pos, best_bits + 0x100, lrl, best_ml, best_offs, &chunk_max);
          lit_start = pos;
          lit_bits = 0;
          if (pos >= chunk_end) {
            chunk_max = pos;
            goto exit_chunk;
          }
          goto next_outer_loop;
        }
        uint org_offs = cur_st[-(intptr_t)lrl].offs;
        for (size_t midx = 0; midx < num_match; midx++) {
          uint ml = match[midx].length;
          uint offs = match[midx].offset;
          if (offs <= 0xffff || offs == org_offs)
            continue;
          if (ml < minmatch_long)
            break;

          uint tbits = best_bits + cm.tok[ml <= 28 ? ml - 5 : 2] + ((pos_start + offs - pos < 0xc00000) ? 3 * 256 : 4 * 256) + 0x80;
          if (offs > 0x100000) {
            tbits += 0x80;
            if (offs > 0x400000)
              tbits += 0x200;
          }
          if (ml >= 29)
            tbits += ((ml > 251 + 29 ? 3 : 1) * 256) + 24;

          CheckBetter(cur_st + ml, pos + ml, tbits, lrl, ml, offs, &chunk_max);
          CheckQuickMatchAfterMermaid(S, pos, src_len_minus5, ml, lrl, offs, tbits, src_cur, src_end_safe, cm, chunk_type_mask, &chunk_max);
          CheckBetter(cur_st + ml - 1, pos + ml - 1, tbits, lrl, ml - 1, offs, &chunk_max);
        }
      }
      uint tbits_best = INT_MAX;
      for (int tlrl = 0; tlrl <= nback; tlrl++) {
        MermaidState *tmp_st = cur_st - tlrl;
        if (tmp_st->bits == INT_MAX)
          continue;
        uint offs = tmp_st->offs;
        uint tbits = tmp_st->bits + Mermaid::BitsForLits(cm, src_cur - tlrl, tlrl, offs, chunk_type_mask);
        int ml = CountMatchingBytes(src_cur, src_end_safe, offs);
        if (ml) {
          if (ml >= 91) {
            pos += ml;
            CheckBetter(cur_st + ml, pos, tbits + 256, tlrl, ml, offs, &chunk_max);
            lit_start = pos;
            lit_bits = 0;
            if (pos >= chunk_end) {
              chunk_max = pos;
              goto exit_chunk;
            }
            goto next_outer_loop;
          }
          if (ml <= 15) {
            uint ubits = tbits + cm.tok[0x80 + tlrl + ml * 8];
            CheckBetter(cur_st + ml, pos + ml, ubits, tlrl, ml, offs, &chunk_max);
            CheckQuickMatchAfterMermaid(S, pos, src_len_minus5, ml, tlrl, offs, ubits, src_cur, src_end_safe, cm, chunk_type_mask, &chunk_max);
            for(uint i = 2; i < ml; i++)
              CheckBetter(cur_st + i, pos + i, tbits + cm.tok[0x80 + i * 8 + tlrl], tlrl, i, offs, &chunk_max);
          } else {
            int ubits = tbits + cm.tok[0xf8 + tlrl];
            CheckBetter(cur_st + 15, pos + 15, ubits, tlrl, 15, offs, &chunk_max);

            int jj = ml - 15;
            while (jj > 1) {
              int n = std::min(jj, 15);
              ubits += cm.tok[0x80 + n * 8];
              jj -= n;
            }
            CheckBetter(cur_st + ml - jj, pos + ml - jj, ubits, tlrl, ml - jj, offs, &chunk_max);
          }
        }
        if (tbits >= tbits_best)
          continue;
        tbits_best = tbits;
        uint org_offs = offs;
        for (size_t midx = 0; midx < num_match; midx++) {
          uint ml = match[midx].length;
          uint offs = match[midx].offset;
          if (offs > 0xffff || offs == org_offs)
            continue;

          uint vbits = tbits + cm.off16hi[offs >> 8] + cm.off16lo[offs & 0xff];
          if (ml < 15) {
            uint ubits = vbits + cm.tok[ml * 8 + tlrl] + (offs > 256);
            CheckBetter(cur_st + ml, pos + ml, ubits, tlrl, ml, offs, &chunk_max);
            CheckQuickMatchAfterMermaid(S, pos, src_len_minus5, ml, tlrl, offs, ubits, src_cur, src_end_safe, cm, chunk_type_mask, &chunk_max);
            for (uint i = min_match_len; i < ml; i++)
              CheckBetter(cur_st + i, pos + i, vbits + cm.tok[i * 8 + tlrl] + (offs > 256), tlrl, i, offs, &chunk_max);
          } else {
            vbits += cm.tok[0x78 + tlrl] + (offs > 256);
            int tml = 15;
            for (;;) {
              CheckBetter(cur_st + tml, pos + tml, vbits, tlrl, tml, offs, &chunk_max);
              int n = ml - tml;
              if (n >= 15) {
                n = 15;
              } else if (n <= 1) {
                break;
              }
              tml += n;
              vbits += cm.tok[0x80 + tlrl + n * 8];
            }
          }
        } // for
      } // for (int tlrl = 0; tlrl <= nback; tlrl++)
      pos++;
    } // while (chunk_max <= chunk_end)

exit_chunk:
    if (chunk_max >= src_len_minus5) {
      int pos_end_adjusted = pos_end;
      while (S[pos_end_adjusted].bits == INT_MAX)
        pos_end_adjusted--;

      best_final_pos = std::max(chunk_start, pos_end_adjusted - 16);
      uint best = INT_MAX;

      for(int p = best_final_pos; p <= pos_end_adjusted; p++) {
        if (S[p].bits != INT_MAX) {
          uint bits = S[p].bits + Mermaid::BitsForLits(cm, src + p, pos_end - p, S[p].offs, chunk_type_mask);
          if (bits < best) {
            best = bits;
            best_final_pos = p;
          }
        }
      }
      if (lit_start < pos_end_adjusted - 16 &&
          S[lit_start].bits + Mermaid::BitsForLits(cm, src + lit_start, pos_end - lit_start, S[lit_start].offs, chunk_type_mask) < best)
        best_final_pos = lit_start;
      chunk_max = best_final_pos;
    }

    int old_size = arr->size;
    Mermaid_StateToTok(arr, S, chunk_start, chunk_max);
    Mermaid_UpdateStats(chunk_type, arr, old_size, chunk_start, stats, src, S[chunk_start].offs);
    Mermaid::MakeCostModel(stats, &cm);

    chunk_start = chunk_max;
    if (chunk_max == best_final_pos)
      break;
  }
  *recent0_ptr = S[best_final_pos].offs;
}


int MermaidOptimal(LzCoder *coder, LzTemp *lztemp, MatchLenStorage *mls, const uint8 *src, int src_len, uint8 *dst, uint8 *dst_end, int start_pos, int *chunk_type_ptr, float *cost_ptr) {
  *chunk_type_ptr = 0;
  *cost_ptr = kInvalidCost;
  if (src_len <= 128)
    return src_len;

  LengthAndOffset *lao = (LengthAndOffset *)lztemp->allmatch_scratch.Allocate(sizeof(LengthAndOffset) * 4 * src_len);

  const uint8 *window_base = mls->window_base;
  ExtractLaoFromMls(mls, src - window_base, src_len, lao, 4);

  MermaidTokArray lz_token_array;
  lz_token_array.capacity = src_len >> 1;
  lz_token_array.size = 0;
  lz_token_array.data = (MermaidToken*)lztemp->lztoken_scratch.Allocate(sizeof(MermaidToken) * lz_token_array.capacity);

  MermaidState *S = (MermaidState *)lztemp->kraken_states.Allocate(sizeof(MermaidState) * (src_len + 1));
  memset(S, 0, sizeof(MermaidState) * (src_len + 1));
  uint8 *tmp_dst = (uint8*)S, *tmp_dst_end = tmp_dst + lztemp->kraken_states.size;
  bool is_mermaid = coder->codec_id == kCompressorMermaid;
  int minmatch_long = is_mermaid ? 10 : 14;
  int initial_copy_bytes = (start_pos == 0) ? 8 : 0;

  uint dict_size = coder->opts->dictionarySize > 0 && coder->opts->dictionarySize <= 0x40000000 ?
      coder->opts->dictionarySize : 0x40000000;
  int min_match_length = std::max(coder->opts->min_match_length, 4);

  MermaidHistos histos = { 0 };

  int chunk_type = 0;
  float bestcost = kInvalidCost;
  int bestn = MermaidCollectStats(&bestcost, &chunk_type, &histos, dst, dst_end, coder, lztemp,
                                  src, src_len, start_pos, initial_copy_bytes, lao,
                                  min_match_length, minmatch_long, window_base, dict_size);
  if (bestn >= src_len)
    return src_len;

  *chunk_type_ptr = chunk_type;

  if (is_mermaid && coder->compression_level >= 6 && coder->opts->min_match_length < 8) {
    MermaidHistos tmp_histos = { 0 };
    float tmp_cost = kInvalidCost;
    int tmp_chunk_type = 0;
    int n = Mermaid_GetBytesForRaw(&tmp_cost, &tmp_chunk_type, &tmp_histos, tmp_dst, tmp_dst_end,
                                   coder, lztemp, src, src_len, start_pos, initial_copy_bytes);
    if (n < src_len && tmp_cost < bestcost) {
      *chunk_type_ptr = tmp_chunk_type;
      bestn = n;
      bestcost = tmp_cost;
      memcpy(dst, tmp_dst, n);
    }
  }
  int middle_token_count = 0;

  for (int i = 0; i <= src_len; i++)
    S[i].bits = INT_MAX;

  int round1_bytes = std::min(src_len, 0x10000);
  int round2_bytes = src_len - round1_bytes;
  int recent0 = 8;

  for (int round = 0; round < 2; round++) {
    int pos, round_start, round_bytes;

    if (round == 0) {
      round_start = 0;
      round_bytes = round1_bytes;
      pos = initial_copy_bytes;
    } else {
      middle_token_count = lz_token_array.size;
      pos = round_start = round1_bytes;
      round_bytes = round2_bytes;
      if (!round_bytes)
        break;
    }

    S[pos].offs = recent0;
    S[pos].bits = 0;
    S[pos].ml = 0;
    S[pos].lrl = 0;
    S[pos].extracopy = 0;

    const uint8 *src_end_safe = std::min(src + src_len - 16, src + round_start + round_bytes);
    int src_len_minus5 = src_end_safe - src - 5;

    if (is_mermaid) {
      Mermaid_DoOptimal(&lz_token_array, S, &recent0,
                        coder, lztemp, &histos, chunk_type, pos, src_len_minus5,
                        src, round_start, round_start + round_bytes,
                        lao, src_end_safe, min_match_length, minmatch_long, window_base);
    } else {
      int final_pos = pos;
      Selkie_DoOptimal(S, &recent0, &final_pos, coder, pos, src_len_minus5,
                       src, round_start, round_start+round_bytes,
                       lao, src_end_safe, min_match_length, minmatch_long, window_base);
      Mermaid_StateToTok(&lz_token_array, S, pos, final_pos);
    }
  }

  MermaidHistos *histo_ptr = NULL;
  if (is_mermaid) {
    memset(&histos, 0, sizeof(histos));
    histo_ptr = &histos;
  }
  float cost = kInvalidCost;
  chunk_type = 0;
  int n = Mermaid_WriteTokens(&cost, &chunk_type, histo_ptr,
                              tmp_dst, tmp_dst_end, coder, lztemp, src, src_len,
                              start_pos, &lz_token_array, middle_token_count);
  if (cost < bestcost) {
    bestn = n;
    *chunk_type_ptr = chunk_type;
    bestcost = cost;
    memcpy(dst, tmp_dst, n);
    if (histo_ptr) {
      MermaidHistos *dst = (MermaidHistos *)coder->lvsymstats_scratch.Allocate(sizeof(MermaidHistos));
      *dst = *histo_ptr;
      coder->last_chunk_type = chunk_type;
    }
  }
  *cost_ptr = bestcost;
  return bestn;
}

int MermaidDoCompress(LzCoder *coder, LzTemp *lztemp, MatchLenStorage *mls,
                      const uint8 *src, int src_size,
                      uint8 *dst, uint8 *dst_end,
                      int start_pos, int *chunk_type_ptr, float *cost_ptr) {
  int level = coder->compression_level;

  if (level == 1 || level == -1) {
    return MermaidCompressLoop<MermaidCompressVeryfast<1, FastMatchHasher<uint32>>>(coder, lztemp, mls, src, src_size, dst, dst_end, start_pos, chunk_type_ptr, cost_ptr);
  } else if (level == 2) {
    return MermaidCompressLoop<MermaidCompressVeryfast<2, FastMatchHasher<uint32>>>(coder, lztemp, mls, src, src_size, dst, dst_end, start_pos, chunk_type_ptr, cost_ptr);
  } else if (level == 3) {
    return MermaidCompressLoop<MermaidCompressFast<3, MatchHasher<2, false>>>(coder, lztemp, mls, src, src_size, dst, dst_end, start_pos, chunk_type_ptr, cost_ptr);
  } else if (level == 4) {
    return MermaidCompressLoop<MermaidCompressFast<4, MatchHasher2>>(coder, lztemp, mls, src, src_size, dst, dst_end, start_pos, chunk_type_ptr, cost_ptr);
  } else if (level == -2) {
    return MermaidCompressLoop<MermaidCompressVeryfast<-2, FastMatchHasher<uint16>>>(coder, lztemp, mls, src, src_size, dst, dst_end, start_pos, chunk_type_ptr, cost_ptr);
  } else if (level == -3) {
    return MermaidCompressLoop<MermaidCompressVeryfast<-3, FastMatchHasher<uint16>>>(coder, lztemp, mls, src, src_size, dst, dst_end, start_pos, chunk_type_ptr, cost_ptr);
  } else if (level >= 5) {
    return MermaidOptimal(coder, lztemp, mls, src, src_size, dst, dst_end, start_pos, chunk_type_ptr, cost_ptr);
  }

  return -1;
}


void SetupEncoder_Mermaid(LzCoder *coder, int codec_id, int src_len, int level,
                          const CompressOptions *copts,
                          const uint8 *src_base, const uint8 *src_start) {
  bool is_small = false;

  assert(src_base && src_start);
  int hash_bits = GetHashBits(src_len, std::max(level, 2), copts, 16, 20, 17, 24);

  bool is_mermaid = (codec_id == kCompressorMermaid);
  coder->codec_id = codec_id;
  coder->quantum_blocksize = 0x20000;
  coder->check_plain_huffman = is_mermaid && (level >= 4);
  coder->platforms = 0;
  coder->compression_level = level;
  coder->opts = copts;
  coder->speed_tradeoff = (copts->spaceSpeedTradeoffBytes * 0.00390625f) * (is_mermaid ? 0.050000001f : 0.14f);
  coder->max_matches_to_consider = 4;
  coder->limit_local_dictsize = (level >= 6);
  coder->compressor_file_id = 10;
  coder->encode_flags = 0;

  if (is_mermaid) {
    if (level >= 5)
      coder->entropy_opts = 0xff & ~kEntropyOpt_MultiArrayAdvanced;
    else
      coder->entropy_opts = 0xff & ~(kEntropyOpt_MultiArrayAdvanced | kEntropyOpt_tANS | kEntropyOpt_MultiArray);
    level = std::max(level, -3);
  } else {
    coder->entropy_opts = kEntropyOpt_SupportsShortMemset;
  }

  int min_match_len = 4;
  if (src_len > 0x4000 && level >= -2 && level <= 3 && !(is_small && level == 3) && IsProbablyText(src_start, src_len))
    min_match_len = 6;

  if (level >= 0 && level <= 1) {
    if (copts->hashBits <= 0)
      hash_bits = std::min(hash_bits, 17);
    CreateLzHasher<FastMatchHasher<uint32>, 0x1000000>(coder, src_base, src_start, hash_bits, min_match_len);
    coder->entropy_opts &= ~(kEntropyOpt_RLE | kEntropyOpt_RLEEntropy);
  } else if (level == 2) {
    if (copts->hashBits <= 0)
      hash_bits = std::min(hash_bits, 19);
    CreateLzHasher<FastMatchHasher<uint32>, 0x1000000>(coder, src_base, src_start, hash_bits, min_match_len);
    coder->entropy_opts &= ~(kEntropyOpt_RLE | kEntropyOpt_RLEEntropy);
  } else if (level == 3) {
    if (copts->hashBits <= 0)
      hash_bits = std::min(hash_bits, 20);
    CreateLzHasher< MatchHasher<2, false> >(coder, src_base, src_start, hash_bits, min_match_len);
  } else if (level == 4) {
    CreateLzHasher< MatchHasher2 >(coder, src_base, src_start, hash_bits, min_match_len);
  } else if (level == -1) {
    if (copts->hashBits <= 0)
      hash_bits = std::min(hash_bits, 16);
    CreateLzHasher<FastMatchHasher<uint32>, 0x1000000>(coder, src_base, src_start, hash_bits, min_match_len);
    coder->entropy_opts &= ~(kEntropyOpt_RLE | kEntropyOpt_RLEEntropy);
  } else if (level == -2) {
    if (copts->hashBits <= 0)
      hash_bits = std::min(hash_bits, 14);
    CreateLzHasher<FastMatchHasher<uint16>, 0x1000000>(coder, src_base, src_start, hash_bits, min_match_len);
    coder->entropy_opts &= ~(kEntropyOpt_RLE | kEntropyOpt_RLEEntropy);
  } else if (level == -3) {
    if (copts->hashBits <= 0)
      hash_bits = std::min(hash_bits, 13);
    CreateLzHasher<FastMatchHasher<uint16>, 0x1000000>(coder, src_base, src_start, hash_bits, min_match_len);
    coder->entropy_opts &= ~(kEntropyOpt_RLE | kEntropyOpt_RLEEntropy);
  }
}
