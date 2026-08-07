// This file is not GPL. It may be used for educational purposes only.
#include "stdafx.h"
#include "compr_kraken.h"
#include "compress.h"
#include "compr_util.h"
#include "compr_entropy.h"
#include <algorithm>
#include <memory>
#include "match_hasher.h"
#include "compr_match_finder.h"

struct KrakenRecentOffs {
  KrakenRecentOffs() {
    offs[4] = offs[5] = offs[6] = 8;
  }
  int offs[8];
};

struct KrakEncLz {
  uint8 *lits_start;
  uint8 *lits;
  uint8 *sub_lits_start;
  uint8 *sub_lits;
  uint8 *tokens_start;
  uint8 *tokens;
  uint8 *u8_offs_start;
  uint8 *u8_offs;
  uint32 *u32_offs_start;
  uint32 *u32_offs;
  uint8 *lrl8_start;
  uint8 *lrl8;
  uint32 *len32_start;
  uint32 *len32;
  int src_len;
  const uint8 *src_ptr;
  int recent0;
  int encode_flags;
};

static bool KrakIsMatchLongEnough(uint ml, uint offs) {
  switch (ml) {
  case 0:
  case 1:
  case 2: return false;
  case 3: return offs < 0x4000;
  case 4: return offs < 0x20000;
  case 5: return offs < 0x100000;
  case 6:
  case 7: return offs < 0x400000;
  default: return true;
  }
}

enum IsRecent {
  kOFFSET = 0,
  kRECENT = 1,
};

struct Krak {
  enum {
    kMinBytesPerRound = 256,
    kMaxBytesPerRound = 4096,
    kRecentOffsetCount = 3,
  };
  struct Token {
    int recent_offset0;
    int litlen;
    int matchlen;
    int offset;
  };
  struct TokenArray {
    Token *data;
    int size, capacity;
  };
  struct State {
    int best_bit_count;
    int recent_offs[3];
    int matchlen;
    int litlen;
    int quick_recent_matchlen_litlen;
    int prev_state;

    void Initialize() {
      best_bit_count = 0;
      recent_offs[0] = 8;
      recent_offs[1] = 8;
      recent_offs[2] = 8;
      matchlen = 0;
      litlen = 0;
      prev_state = 0;
      quick_recent_matchlen_litlen = 0;
    }
  };
  struct CostModel {
    int chunk_type;
    int sub_or_copy_mask;
    uint lit_cost[256];
    uint token_cost[256];
    int offs_encode_type;
    uint offs_cost[256];
    uint offs_lo_cost[256];
    uint matchlen_cost[256];
  };

  struct Stats {
    HistoU8 lit_raw;
    HistoU8 lit_sub;
    HistoU8 token;
    HistoU8 matchlen;
    int offs_encode_type;
    HistoU8 offs;
    HistoU8 offs_lo;

    static void RescaleOne(HistoU8 *h) {
      for (size_t i = 0; i != 256; i++)
        h->count[i] = (h->count[i] >> 4) + 1;
    }

    static void Rescale(Stats *s) {
      RescaleOne(&s->lit_raw);
      RescaleOne(&s->lit_sub);
      RescaleOne(&s->offs);
      if (s->offs_encode_type > 1)
        RescaleOne(&s->offs_lo);
      RescaleOne(&s->token);
      RescaleOne(&s->matchlen);
    }

    static void RescaleAddOne(HistoU8 *h, const HistoU8 *t) {
      for (size_t i = 0; i != 256; i++)
        h->count[i] = ((h->count[i] + t->count[i]) >> 5) + 1;
    }

    static void RescaleAdd(Stats *s, const Stats *t, bool chunk_type_same) {
      if (chunk_type_same) {
        RescaleAddOne(&s->lit_raw, &t->lit_raw);
        RescaleAddOne(&s->lit_sub, &t->lit_sub);
      } else {
        RescaleOne(&s->lit_raw);
        RescaleOne(&s->lit_sub);
      }
      RescaleAddOne(&s->token, &t->token);
      RescaleAddOne(&s->matchlen, &t->matchlen);
      if (s->offs_encode_type == t->offs_encode_type) {
        RescaleAddOne(&s->offs, &t->offs);
        if (s->offs_encode_type > 1)
          RescaleAddOne(&s->offs_lo, &t->offs_lo);
      } else {
        s->offs = t->offs;
        s->offs_lo = t->offs_lo;
        s->offs_encode_type = t->offs_encode_type;
        RescaleOne(&s->offs);
        if (s->offs_encode_type > 1)
          RescaleOne(&s->offs_lo);
      }
    }

    static void ReduceIfHigh(Stats *s) {
      // empty
    }

    static void Update(Stats *h, const uint8 *src, int pos, const Token *tokens, int num_token) {
      enum {
        kIncrement = 2
      };
      for (int i = 0; i < num_token; i++) {
        const Token *t = &tokens[i];
        int litlen = t->litlen;
        int recent = t->recent_offset0;
        for (int j = 0; j < litlen; j++) {
          uint b = src[pos + j];
          h->lit_raw.count[b] += kIncrement;
          h->lit_sub.count[(uint8)(b - src[pos + j - recent])] += kIncrement;
        }

        pos += litlen + t->matchlen;

        int length_field = litlen;
        if (litlen >= 3) {
          h->matchlen.count[std::min(litlen - 3, 255)] += kIncrement;
          length_field = 3;
        }

        if (t->matchlen < 2) {
          assert(t->matchlen >= 2);
          continue;
        }
        uint offset = t->offset;
        int recent_field;
        if (t->offset <= 0) {
          recent_field = -(int)offset;
        } else {
          recent_field = 3;
          if (h->offs_encode_type == 0) {
            if (offset >= 8388360) {
              uint t = BSR(offset - 8322816) | 0xF0;
              h->offs.count[t] += kIncrement;
            } else {
              uint t = ((offset - 8) & 0xF) + 16 * (BSR(offset - 8 + 256) - 8);
              h->offs.count[t] += kIncrement;
            }
          } else if (h->offs_encode_type == 1) {
            uint t = BSR(offset + 8) - 3;
            uint u = 8 * t | (((offset + 8) >> t) ^ 8);
            h->offs.count[u] += kIncrement;
          } else {
            uint ohi = offset / h->offs_encode_type, olo = offset % h->offs_encode_type;
            uint t = BSR(ohi + 8) - 3;
            uint u = 8 * t | (((ohi + 8) >> t) ^ 8);
            h->offs.count[u] += kIncrement;
            h->offs_lo.count[olo] += kIncrement;
          }
        }
        int matchlen_field = t->matchlen - 2;
        if (t->matchlen - 17 >= 0) {
          h->matchlen.count[std::min(t->matchlen - 17, 255)] += kIncrement;
          matchlen_field = 15;
        }
        int token_value = (matchlen_field << 2) + (recent_field << 6) + (length_field);
        h->token.count[token_value] += kIncrement;
      }
    }
  };

  static void MakeCostModel(const Stats *h, CostModel *cm) {
    ConvertHistoToCost(h->offs, cm->offs_cost, 36);
    if (h->offs_encode_type > 1)
      ConvertHistoToCost(h->offs_lo, cm->offs_lo_cost, 0);
    ConvertHistoToCost(h->token, cm->token_cost, 18);
    ConvertHistoToCost(h->matchlen, cm->matchlen_cost, 12);
    if (cm->chunk_type == 1)
      ConvertHistoToCost(h->lit_raw, cm->lit_cost, 0);
    else
      ConvertHistoToCost(h->lit_sub, cm->lit_cost, 0);
  }

  static __forceinline uint BitsForLitlen(CostModel &cost_model, int cur_litlen) {
    if (cur_litlen < 3)
      return 0;
    if (cur_litlen - 3 >= 255) {
      int v = BSR(((unsigned int)(cur_litlen - 3 - 255) >> 6) + 1);
      return cost_model.matchlen_cost[255] + 32 * (2 * v + 7);
    } else {
      return cost_model.matchlen_cost[cur_litlen - 3];
    }
  }

  static __forceinline uint BitsForLit(const uint8 *src, int pos, int recent, const CostModel &cm, int litidx) {
    const uint8 *p = src + pos;
    return cm.lit_cost[(uint8)(p[0] - (p[-recent] & cm.sub_or_copy_mask))];
  }

  static __forceinline uint BitsForLits(const uint8 *src, int pos, int num, int recent, const CostModel &cm, int litidx) {
    const uint8 *p = src + pos;
    uint sum = 0;
    for (int i = 0; i < num; i++, p++)
      sum += cm.lit_cost[(uint8)(p[0] - (p[-recent] & cm.sub_or_copy_mask))];
    return sum;
  }

  static __forceinline bool CheckMatchValidLength(uint ml, uint offs) {
    return (offs < 0x100000) || ((offs < 0x200000) ? (ml >= 5) : (offs < 0x400000) ? (ml >= 6) : (ml >= 8));
  }


  static __forceinline int GetMatchlengthQ(const uint8 *src, int offset, const uint8 *src_end, uint32 u32_at_cur) {
    return ::GetMatchlengthQ(src, offset, src_end, u32_at_cur);
  }

  static __forceinline int GetMatchlengthMin2(const uint8 *src_ptr_cur, int offset, const uint8 *src_ptr_safe_end) {
    return ::GetMatchlengthMin2(src_ptr_cur, offset, src_ptr_safe_end);
  }
   
  static __forceinline int BitsForToken(const CostModel &cost_model, int cur_matchlen, int cmd_offset, int recent_field, int length_field) {
    if (cur_matchlen - 17 >= 0) {
      int bits_for_matchlen;
      if (cur_matchlen - 17 >= 255) {
        int bsr = BSR(((unsigned int)(cur_matchlen - 17 - 255) >> 6) + 1);
        bits_for_matchlen = cost_model.matchlen_cost[255] + 32 * (2 * bsr + 7);
      } else {
        bits_for_matchlen = cost_model.matchlen_cost[cur_matchlen - 17];
      }
      return cost_model.token_cost[(15 << 2) + (recent_field << 6) + length_field] + bits_for_matchlen;
    } else {
      return cost_model.token_cost[((cur_matchlen - 2) << 2) + (recent_field << 6) + length_field];
    }
  }

  static uint BitsForOffset(const CostModel &cost_model, uint offs) {
    if (cost_model.offs_encode_type == 0) {
      if (offs >= 8388360) {
        unsigned t = BSR(offs - 8322816) | 0xF0;
        unsigned u = t - 224;
        return cost_model.offs_cost[t] + 32 * u + 12;
      } else {
        unsigned t = ((offs - 8) & 0xF) + 16 * (BSR(offs - 8 + 256) - 8);
        unsigned u = (t >> 4) + 4;
        return cost_model.offs_cost[t] + 32 * u;
      }
    } else if (cost_model.offs_encode_type == 1) {
      unsigned t = BSR(offs + 8) - 3;
      unsigned u = 8 * t | (((offs + 8) >> t) ^ 8);
      return cost_model.offs_cost[u] + 32 * (u >> 3);
    } else {
      unsigned ohi = offs / cost_model.offs_encode_type, olo = offs % cost_model.offs_encode_type;
      unsigned t = BSR(ohi + 8) - 3;
      unsigned u = 8 * t | (((ohi + 8) >> t) ^ 8);
      return cost_model.offs_cost[u] + 32 * (u >> 3) + cost_model.offs_lo_cost[olo];
    }
  }

  static __forceinline int GetRecentOffsetIndex(const int *arr, int offset) {
    return (offset == arr[0]) ? 0 :
           (offset == arr[1]) ? 1 :
           (offset == arr[2]) ? 2 : -1;
  }
};



static float GetTime_KrakenWriteBits(int platforms, int src_len, int tokens, int len8) {
  return CombineCostComponents(platforms,
      200.0f + src_len * 0.405f + tokens * 15.213f + len8 * 4.017f,
      200.0f + src_len * 0.419f + tokens * 19.861f + len8 * 10.898f,
      200.0f + src_len * 0.647f + tokens * 24.886f + len8 * 9.685f,
      200.0f + src_len * 0.305f + tokens * 13.591f + len8 * 4.394f);
}

static double GetTime_KrakenLengths(int platforms, int len8, int len32) {
  return CombineCostComponents(platforms,
       42.933f + len8 * 0.478f + len32 * 21.527f,
       36.646f + len8 * 0.746f + len32 * 32.345f,
      115.731f + len8 * 0.815f + len32 * 36.682f,
       48.796f + len8 * 0.453f + len32 * 20.770f);
}


static int Kraken_EncodeLzArrays(float *cost_ptr, int *chunk_type_ptr, Krak::Stats *stats,
                                 uint8 *dst, uint8 *dst_end,
                                 LzCoder *lzcoder, LzTemp *lztemp,
                                 KrakEncLz *kl, int start_pos) {
  uint8 *dst_org = dst;

  if (stats)
    memset(stats, 0, sizeof(*stats));

  const uint8 *src_ptr = kl->src_ptr;
  int src_len = kl->src_len;

  int initial_bytes = start_pos == 0 ? 8 : 0;
  for (int i = 0; i < initial_bytes; i++)
    *dst++ = src_ptr[i];

  int level = lzcoder->compression_level;

  int flag_ignore_u32_length = 0;
  int extra_size = 0;
  assert((lzcoder->encode_flags & 1) == 0);

  int num_lits = kl->lits - kl->lits_start;
  float lit_cost = kInvalidCost;
  float memcpy_cost = num_lits + 3;
  if (num_lits < 32 || level <= -4) {
    *chunk_type_ptr = 1;
    int n = EncodeArrayU8_Memcpy(dst, dst_end, kl->lits_start, num_lits);
    if (n < 0)
      return src_len;
    dst += n;
    lit_cost = memcpy_cost;
  } else {
    HistoU8 lits_histo, litsub_histo;
    bool has_litsub = (kl->sub_lits != kl->sub_lits_start);

    CountBytesHistoU8(kl->lits_start, num_lits, &lits_histo);
    if (has_litsub)
      CountBytesHistoU8(kl->sub_lits_start, num_lits, &litsub_histo);

    if (stats) {
      stats->lit_raw = lits_histo;
      if (has_litsub)
        stats->lit_sub = litsub_histo;
    }

    int lit_n = -1;
    bool skip_normal_lit = false;
    if (has_litsub) {
      float litsub_extra_cost = CombineCostComponents1(lzcoder->platforms, num_lits,
                                                       0.14399999f, 0.292f, 0.322f, 0.129f) * lzcoder->speed_tradeoff;
      bool skip_litsub = (level < 6) && (GetHistoCostApprox(lits_histo, num_lits) * 0.125f <= 
                                         GetHistoCostApprox(litsub_histo, num_lits) * 0.125f + litsub_extra_cost);
      if (!skip_litsub) {
        *chunk_type_ptr = 0;
        float litsub_cost = kInvalidCost;
        lit_n = EncodeArrayU8WithHisto(dst, dst_end, kl->sub_lits_start, num_lits, litsub_histo,
                                       lzcoder->entropy_opts, lzcoder->speed_tradeoff, lzcoder->platforms,
                                       &litsub_cost, level);
        litsub_cost += litsub_extra_cost;
        if (lit_n > 0 && lit_n < num_lits && litsub_cost <= memcpy_cost) {
          lit_cost = litsub_cost;
          if (level < 6)
            skip_normal_lit = true;
        }
      }
    }
    if (!skip_normal_lit) {
      int n = EncodeArrayU8WithHisto(dst, dst_end, kl->lits_start, num_lits, lits_histo,
                                     lzcoder->entropy_opts, lzcoder->speed_tradeoff, lzcoder->platforms,
                                     &lit_cost, level);
      if (n > 0) {
        lit_n = n;
        *chunk_type_ptr = 1;
      }
    }
    if (lit_n < 0)
      return src_len;
    dst += lit_n;
  }

  float token_cost = kInvalidCost;
  int n = EncodeArrayU8(dst, dst_end, kl->tokens_start, kl->tokens - kl->tokens_start,
                        lzcoder->entropy_opts, lzcoder->speed_tradeoff, lzcoder->platforms,
                        &token_cost, level, stats ? &stats->token : NULL);
  if (n < 0)
    return src_len;
  dst += n;

  int offs_encode_type = 0;
  float offs_cost = kInvalidCost;
  n = EncodeLzOffsets(dst, dst_end, kl->u8_offs_start, kl->u32_offs_start, kl->u8_offs - kl->u8_offs_start,
                                lzcoder->entropy_opts, lzcoder->speed_tradeoff, lzcoder->platforms,
                                &offs_cost, 8, (lzcoder->encode_flags & 4), &offs_encode_type, level,
                                stats ? &stats->offs : NULL, stats ? &stats->offs_lo : NULL);
  if (n < 0)
    return src_len;
  dst += n;
  if (stats)
    stats->offs_encode_type = offs_encode_type;


  float lrl8_cost = kInvalidCost;
  n = EncodeArrayU8(dst, dst_end, kl->lrl8_start, kl->lrl8 - kl->lrl8_start,
                    lzcoder->entropy_opts, lzcoder->speed_tradeoff, lzcoder->platforms,
                    &lrl8_cost, level, stats ? &stats->matchlen : NULL);
  if (n < 0)
    return src_len;
  dst += n;

  int lrl_num = kl->lrl8 - kl->lrl8_start;
  int offs_num = kl->u8_offs - kl->u8_offs_start;
  int tok_num = kl->tokens - kl->tokens_start;
  int required = std::max(lrl_num, offs_num) + lrl_num + num_lits + 4 * lrl_num + 6 * offs_num + tok_num + 16;
  required = std::max(std::max(required, 2 * num_lits), num_lits + 2 * tok_num) + 0xd000;
  int scratch_available = std::min(3 * src_len + 32 + 0xd000, 0x6C000);
  if (required > scratch_available) {
    assert(required <= scratch_available);
    return src_len;
  }

  n = WriteLzOffsetBits(dst, dst_end, kl->u8_offs_start, kl->u32_offs_start, offs_num, offs_encode_type,
                                   kl->len32_start, kl->len32 - kl->len32_start,
                                   flag_ignore_u32_length, extra_size);
  if (n < 0)
    return src_len;
  dst += n;

  if (dst - dst_org >= src_len)
    return src_len;

  float bits_cost = GetTime_KrakenWriteBits(lzcoder->platforms, src_len, tok_num, lrl_num) * 
      lzcoder->speed_tradeoff + (initial_bytes + flag_ignore_u32_length + n);
  float cost = token_cost + lit_cost + offs_cost + lrl8_cost + bits_cost;
  *cost_ptr = GetTime_KrakenLengths(lzcoder->platforms, lrl_num, kl->len32 - kl->len32_start) * 
      lzcoder->speed_tradeoff + cost;
  return dst - dst_org;
}

static void KrakEncLz_Init(KrakEncLz *kl, LzTemp *lztemp, int src_len, const uint8 *src_base, int encode_flags) {
  kl->src_ptr = src_base;
  kl->src_len = src_len;

  size_t nlits = src_len + 8;
  size_t ntokens = src_len / 2 + 8;
  size_t nu8offs = src_len / 3;
  size_t nu32offs = src_len / 3;
  size_t nlen8 = src_len / 5;
  size_t nlen32 = src_len / 256;

  uint8 *p = (uint8*)lztemp->scratch0.Allocate(nlits * 2 + ntokens + nu8offs + nu32offs * 4 + nlen8 + nlen32 * 4 + 256);
  kl->lits_start = kl->lits = p, p += nlits;
  kl->sub_lits_start = kl->sub_lits = p, p += nlits;
  kl->tokens_start = kl->tokens = p, p += ntokens;
  kl->u8_offs = kl->u8_offs_start = p, p += nu8offs;
  p = AlignPointer<uint8*>(p, 4);
  kl->u32_offs = kl->u32_offs_start = (uint32*)p, p += nu32offs * 4;
  kl->lrl8 = kl->lrl8_start = p, p += nlen8;
  p = AlignPointer<uint8*>(p, 4);
  kl->len32 = kl->len32_start = (uint32*)p;
  kl->recent0 = 8;
  kl->encode_flags = encode_flags;
}

static inline int KrakEnk_WriteMatchLen(KrakEncLz &kl, int ml) {
  int ml_token = ml - 2;
  if (ml_token >= 15) {
    ml_token = 15;
    if (ml >= 255 + 17) {
      *kl.lrl8++ = 255;
      *kl.len32++ = ml - 255 - 17;
    } else {
      *kl.lrl8++ = ml - 17;
    }
  }
  return ml_token << 2;
}



template<bool DoSubtract>
static void KrakEnc_WriteLitsLong(KrakEncLz &kl, const uint8 *p, size_t len) {
  if (DoSubtract) {
    SubtractBytesUnsafe(kl.sub_lits, p, len, -kl.recent0);
    kl.sub_lits += len;
  }
  uint8 *d = kl.lits, *d_end = d + len;
  do {
    *(uint32*)d = *(uint32*)p;
    d += 4, p += 4;
  } while (d < d_end);
  kl.lits = d_end;

  if (len >= 258) {
    *kl.lrl8++ = 255;
    *kl.len32++ = len - 258;
  } else {
    *kl.lrl8++ = (uint8)(len - 3);
  }
}

template<bool DoSubtract>
static inline int KrakEnc_WriteLits(KrakEncLz &kl, const uint8 *p, size_t len) {
  if (len == 0)
    return 0;
  if (len > 8) {
    KrakEnc_WriteLitsLong<DoSubtract>(kl, p, len);
    return 3;
  }
  uint8 *lrl8 = kl.lrl8;
  *lrl8 = (uint8)(len - 3);
  kl.lrl8 = (len >= 3) ? lrl8 + 1 : lrl8;

  uint8 *ll = kl.lits;
  *(uint64*)ll = *(uint64*)p;
  kl.lits = ll + len;

  if (DoSubtract) {
    uint8 *sl = kl.sub_lits;
    simde_mm_storel_epi64((simde__m128i *)sl,
                          simde_mm_sub_epi8(simde_mm_loadl_epi64((const simde__m128i *)p),
                                            simde_mm_loadl_epi64((const simde__m128i *)&p[-kl.recent0])));
    kl.sub_lits = sl + len;
  }
  return std::min<int>(len, 3);
}

static inline void KrakEnc_WriteFarOffs(KrakEncLz &kl, uint32 offs) {
  int bsr = BSR(offs - 8322816);
  *kl.u8_offs++ = bsr | 0xf0;
  *kl.u32_offs++ = offs;
}

static inline void KrakEnc_WriteNearOffs(KrakEncLz &kl, uint32 offs) {
  int bsr = BSR(offs + 248);
  *kl.u8_offs++ = ((offs - 8) & 0xF) | 16 * (bsr - 8);
  *kl.u32_offs++ = offs;
}

static inline void KrakEnc_WriteOffs(KrakEncLz &kl, uint32 offs) {
  if (offs >= 8388360)
    KrakEnc_WriteFarOffs(kl, offs);
  else
    KrakEnc_WriteNearOffs(kl, offs);
}

template<bool DoRecent, bool DoSubtract>
static inline void KrakEnc_AddToken(KrakEncLz &kl, KrakenRecentOffs &recent, const uint8 *litstart, size_t litlen, int matchlen, int offs_or_recent) {
  int token = KrakEnc_WriteLits<DoSubtract>(kl, litstart, litlen);
  token += KrakEnk_WriteMatchLen(kl, matchlen);
  if (offs_or_recent > 0) {
    token += 3 << 6;
    if (DoRecent) {
      recent.offs[6] = recent.offs[5];
      recent.offs[5] = recent.offs[4];
      recent.offs[4] = offs_or_recent;
    }
    kl.recent0 = offs_or_recent;
    KrakEnc_WriteOffs(kl, offs_or_recent);
  } else {
    if (DoRecent) {
      size_t idx = -offs_or_recent;
      assert(idx >= 0 && idx <= 2);
      token += idx << 6;
      int v = recent.offs[idx + 4];
      recent.offs[idx + 4] = recent.offs[idx + 3];
      recent.offs[idx + 3] = recent.offs[idx + 2];
      recent.offs[4] = v;
      kl.recent0 = v;
    }
  }
  *kl.tokens++ = token;
}

template<bool DoSubtract>
static inline void KrakEnc_AddFinal(KrakEncLz &kl, const uint8 *p, const uint8 *pend) {
  size_t len = pend - p;
  if (len) {
    memcpy(kl.lits, p, len);
    kl.lits += len;
    if (DoSubtract) {
      SubtractBytes(kl.sub_lits, p, len, -kl.recent0);
      kl.sub_lits += len;
    }
  }
}

template<int Level, typename Hasher>
int KrakenCompressVeryfast(LzCoder *coder, LzTemp *lztemp, MatchLenStorage *mls_unused,
                         const uint8 *src, int src_size,
                         uint8 *dst, uint8 *dst_end,
                         int start_pos, int *chunk_type_ptr, float *cost_ptr) {
  enum {
    SkipFactor = (Level >= 1) ? 5 : (Level >= -2) ? 4 : 3,
    DoRecent = Level >= 1,
    DoSubtract = Level >= -1,
  };
  *chunk_type_ptr = -1;
  if (src_size <= 128)
    return -1;
  
  uint dict_size = coder->opts->dictionarySize > 0 && coder->opts->dictionarySize <= 0x40000000 ?
      coder->opts->dictionarySize : 0x40000000;

  KrakEncLz kl;

  KrakEncLz_Init(&kl, lztemp, src_size, src, coder->encode_flags);
  int initial_copy_bytes = (start_pos == 0) ? 8 : 0;
  const uint8 *src_end_minus8 = src + src_size - 8;
  const uint8 *src_end_minus16 = src + src_size - 16;

  int skip = 1 << SkipFactor;

  const uint8 *src_cur = src + initial_copy_bytes;
  const uint8 *src_end = src + src_size;
  const uint8 *litstart = src_cur;
  Hasher *hasher = (Hasher*)coder->hasher;

  KrakenRecentOffs recent;

  int last_offs = -8;

  const uint8 *src_base = hasher->src_base_;
  uint64 hashmult = hasher->hashmult_;
  typename Hasher::ElemType *hash_ptr = hasher->hash_ptr_, *hash;
  int hashshift = 64 - hasher->hash_bits_;
  const uint8 *src_cur_next;
  while ((skip >> SkipFactor) < src_end_minus16 - src_cur) {
    src_cur_next = src_cur + (skip >> SkipFactor);

    if (Level >= 0) {
      hash = &hash_ptr[(size_t)(*(uint64*)src_cur_next * hashmult >> hashshift)];
      simde_mm_prefetch((char*)hash, SIMDE_MM_HINT_T0);
    }
    hash = &hash_ptr[(size_t)(*(uint64*)src_cur * hashmult >> hashshift)];
    uint32 u32_at_cur = *(uint32*)src_cur;

    uint32 hashval = *hash;
    *hash = src_cur - src_base;
    
    int offs, offs_or_recent;
    const uint8 *matchstart;

    if (Level >= 1 && (uint16)u32_at_cur == *(uint16*)&src_cur[-recent.offs[5]]) {
      offs = -recent.offs[5];
      offs_or_recent = -1;
      matchstart = src_cur;
      src_cur += 2;
    } else if (Level >= 1 && (uint16)u32_at_cur == *(uint16*)&src_cur[-recent.offs[6]]) {
      offs = -recent.offs[6];
      offs_or_recent = -2;
      matchstart = src_cur;
      src_cur += 2;
    } else if (!((u32_at_cur ^ *(uint32*)&src_cur[last_offs]) & 0xffffff00)) {
      src_cur += 1;
      offs = last_offs;
      offs_or_recent = 0;
      matchstart = src_cur;
      hash_ptr[(size_t)(*(uint64*)src_cur * hashmult >> hashshift)] = src_cur - src_base;
      src_cur += 3;
    } else {
      offs_or_recent = (typename Hasher::ElemType)(src_cur - src_base - hashval);
      if (u32_at_cur != *(uint32*)&src_cur[-offs_or_recent])
        goto no_match;
      if ((uint)(offs_or_recent - 8) < (uint)(dict_size - 8)) {
        offs = -offs_or_recent;
      } else if (u32_at_cur == *(uint32*)(src_cur - 8)) {
        offs = -8;
        offs_or_recent = 8;
      } else {
no_match:
        if (Level >= -2)
          skip++;
        else
          skip = std::min<int>(skip + ((src_cur - litstart) >> 1), 296);
        src_cur = src_cur_next;
        continue;
      }
      // Reduce literal length and increase match length for bytes preceding the match.
      if (Level >= -2) {
        while (src_cur > litstart && (src_base - src_cur) < offs &&
               src_cur[-1] == src_cur[offs - 1])
          src_cur--;
      }
      matchstart = src_cur;
      src_cur += 4;
    }
    // compute length of it
    for (; src_cur < src_end - 8; src_cur += 4) {
      uint32 v = *(uint32*)src_cur ^ *(uint32*)(src_cur + offs);
      if (v != 0) {
        src_cur += (BSF(v) >> 3);
        break;
      }
    }
    src_cur = std::min(src_cur, src_end - 8);

    assert(matchstart >= litstart);

    int ml = src_cur - matchstart;

    KrakEnc_AddToken<DoRecent, DoSubtract>(kl, recent, litstart, matchstart - litstart, ml, offs_or_recent);

    litstart = src_cur;
    last_offs = offs;

    if (src_cur >= src_end - 16)
      break;

    if (Level >= 0) {
      for (int i = 1; i < ml; i *= 2)
        hash_ptr[(size_t)(*(uint64*)(i + matchstart) * hashmult >> hashshift)] = i + matchstart - src_base;
    }
    skip = 1 << SkipFactor;
  }
  KrakEnc_AddFinal<DoSubtract>(kl, litstart, src_end);
  return Kraken_EncodeLzArrays(cost_ptr, chunk_type_ptr, NULL, dst, dst_end, coder, lztemp, &kl, start_pos);
}

static __forceinline void KrakCheckRecentMatch(const uint8 *src, const uint8 *src_end, uint32 u32, KrakenRecentOffs &recent, int idx, int &best_ml, int &best_off) {
  int ml = GetMatchlengthQ(src, recent.offs[4 + idx], src_end, u32);
  if (ml > best_ml)
    best_ml = ml, best_off = idx;
}

template<typename Hasher>
static __forceinline LengthAndOffset KrakenFast_GetMatch(const uint8 *cur_ptr, const uint8 *src_end_safe, const uint8 *lit_start,
                                                  KrakenRecentOffs &recent, Hasher *hasher,
                                                  const uint8 *next_cur_ptr, int dict_size, int min_match_length) {
  typename Hasher::HashPos hp = hasher->GetHashPos(cur_ptr);
  uint32 u32_at_src = *(uint32*)cur_ptr;
  hasher->SetHashPosPrefetch(next_cur_ptr);

  int recent_ml = 0, recent_off = -1;
  KrakCheckRecentMatch(cur_ptr, src_end_safe, u32_at_src, recent, 0, recent_ml, recent_off);
  KrakCheckRecentMatch(cur_ptr, src_end_safe, u32_at_src, recent, 1, recent_ml, recent_off);
  KrakCheckRecentMatch(cur_ptr, src_end_safe, u32_at_src, recent, 2, recent_ml, recent_off);

  int best_offs = 0, best_ml = 0;

  // If we found a recent offset at least 4 bytes long then use it.
  if (recent_ml >= 4) {
    hasher->Insert(hp);
    best_offs = -recent_off, best_ml = recent_ml;
  } else {

    // Make decompression faster by skipping short recent matches
    if (cur_ptr - lit_start >= 56) {
      if (recent_ml <= 2)
        recent_ml = 0;
      min_match_length = std::max(min_match_length, 5);
    }

    int max_ml = 0;
    uint32 *cur_hash_ptr = hp.ptr1;
    for (;;) {
      for (size_t hashidx = 0; hashidx != Hasher::NumHash; hashidx++) {
        if ((cur_hash_ptr[hashidx] & 0xfc000000) == (hp.hi & 0xfc000000)) {
          int cur_offs = (hp.pos - cur_hash_ptr[hashidx]) & 0x3ffffff;
          if (cur_offs < dict_size) {
            cur_offs = std::max(cur_offs, 8);
            if (*(uint32*)(cur_ptr - cur_offs) == u32_at_src &&
                (max_ml < 4 || (cur_ptr + max_ml < src_end_safe && *(cur_ptr + max_ml) == *(cur_ptr + max_ml - cur_offs)))) {
              int cur_ml = 4 + CountMatchingBytes(cur_ptr + 4, src_end_safe, cur_offs);
              if (cur_ml > max_ml && cur_ml >= min_match_length) {
                max_ml = cur_ml;
                if (KrakIsMatchLongEnough(cur_ml, cur_offs) && IsMatchBetter(cur_ml, cur_offs, best_ml, best_offs))
                  best_offs = cur_offs, best_ml = cur_ml;
              }
            }
          }
        }
      }
      if (!Hasher::DualHash || cur_hash_ptr == hp.ptr2)
        break;
      cur_hash_ptr = hp.ptr2;
    }
    hasher->Insert(hp);
    if (!IsBetterThanRecent(recent_ml, best_ml, best_offs))
      best_offs = -recent_off, best_ml = recent_ml;
  }
  LengthAndOffset match = { best_ml, best_offs };
  return match;
}

template<int NumHash, bool DualHash, int NumLazy>
int KrakenCompressFast(LzCoder *lzcoder, LzTemp *lztemp,
                        const uint8 *src, int src_len, uint8 *dst, uint8 *dst_end,
                        int start_pos, int *chunk_type_ptr, float *cost_ptr) {
  const uint8 *src_end = src + src_len;
  KrakEncLz kl;
  KrakenRecentOffs recent;
  *chunk_type_ptr = -1;
  if (src_len <= 128)
    return src_len;
  int dict_size = lzcoder->opts->dictionarySize;
  dict_size = (dict_size <= 0) ? 0x40000000 : std::min(dict_size, 0x40000000);
  int min_match_length = std::max(lzcoder->opts->min_match_length, 4);
  int initial_copy_bytes = (start_pos == 0) ? 8 : 0;

  KrakEncLz_Init(&kl, lztemp, src_len, src, lzcoder->encode_flags);
  int recent0 = 8;

  const uint8 *cur_ptr = src + initial_copy_bytes, *litstart = cur_ptr;

  typedef MatchHasher<NumHash, DualHash> Hasher;
  Hasher *hasher = (Hasher *)lzcoder->hasher;
  hasher->SetHashPos(cur_ptr);
  uint skip = 256;
  LengthAndOffset m;

  for(;;) {
    if ((NumHash == 1) && *(uint32*)(cur_ptr + 1) == *(uint32*)(cur_ptr + 1 - recent0)) {
      int len = CountMatchingBytes(cur_ptr + 5, src_end - 8, recent0) + 4;
      typename Hasher::HashPos hp = hasher->GetHashPos(cur_ptr);
      hasher->SetHashPosPrefetch(cur_ptr + 2);
      hasher->Insert(hp);
      cur_ptr += 1;
      m.offset = 0;
      m.length = len;
    } else {
      uint increment = (NumHash == 1) ? skip >> 8 : 1;
      const uint8 *next_ptr = cur_ptr + increment;
      if (src_end - 16 - cur_ptr <= increment)
        break;
      m = KrakenFast_GetMatch(cur_ptr, src_end - 8, litstart, recent, hasher, next_ptr, dict_size, min_match_length);
      if (m.length < 2) {
        cur_ptr = next_ptr;
        skip++;
        continue;
      }

      if (NumLazy >= 1) {
        while (cur_ptr + 1 < src_end - 16) {
          LengthAndOffset m1 = KrakenFast_GetMatch(cur_ptr + 1, src_end - 8, litstart, recent, hasher, cur_ptr + 2, dict_size, min_match_length);
          if (m1.length >= 2 && GetLazyScore(m1, m) > 0) {
            cur_ptr += 1;  // lazy1 is better
            m = m1;
          } else {
            if (NumLazy < 2 || cur_ptr + 2 >= src_end - 16 || m.length == 2)
              break;
            LengthAndOffset m2 = KrakenFast_GetMatch(cur_ptr + 2, src_end - 8, litstart, recent, hasher, cur_ptr + 3, dict_size, min_match_length);
            if (m2.length >= 2 && GetLazyScore(m2, m) > 3) {
              cur_ptr += 2;  // lazy2 is better
              m = m2;
            } else {
              break;
            }
          }
        }
      }

    }
    assert(m.offset >= -2);
    ptrdiff_t actual_offs = (m.offset <= 0) ? recent.offs[-m.offset + 4] : m.offset;
    // Reduce literal length and increase match length for bytes preceding the match.
    while (cur_ptr > litstart && cur_ptr - hasher->src_base_ > actual_offs && cur_ptr[-1] == cur_ptr[-actual_offs - 1])
      cur_ptr--, m.length++;
    if (m.offset == 0 && cur_ptr == litstart)
      m.offset = -1;
    KrakEnc_AddToken<true,true>(kl, recent, litstart, cur_ptr - litstart, m.length, m.offset);
    cur_ptr += m.length;
    litstart = cur_ptr;
    if (cur_ptr >= src_end - 16)
      break;
    recent0 = kl.recent0;
    hasher->InsertRange(cur_ptr - m.length, m.length);
    skip = 256;
  }
  KrakEnc_AddFinal<true>(kl, litstart, src_end);
  return Kraken_EncodeLzArrays(cost_ptr, chunk_type_ptr, NULL, dst, dst_end, lzcoder, lztemp, &kl, start_pos);
}

static LengthAndOffset KrakenGetLzMatch(const LengthAndOffset *matches, KrakenRecentOffs *recents,
                                        const uint8 *src, const uint8 *src_end,
                                        int min_match_len, int arg_14,
                                        const uint8 *window_base, int max_match_offset) {
  uint32 m32 = *(uint32*)src;
  int best_ml = 0, best_offs = 0, ml;
  LengthAndOffset result;

  if ((ml = Krak::GetMatchlengthQ(src, recents->offs[4], src_end, m32)) > best_ml)
    best_ml = ml, best_offs = 0;
  if ((ml = Krak::GetMatchlengthQ(src, recents->offs[5], src_end, m32)) > best_ml)
    best_ml = ml, best_offs = -1;
  if ((ml = Krak::GetMatchlengthQ(src, recents->offs[6], src_end, m32)) > best_ml)
    best_ml = ml, best_offs = -2;

  if (best_ml < 4) {
    if (arg_14 >= 56) {
      if (best_ml <= 2)
        best_ml = 0;
      min_match_len++;
    }
    uint best_m_ml = 0, best_m_offs = 0;
    for (size_t i = 0; i != 4; i++) {
      uint ml = matches[i].length;
      if (ml < min_match_len)
        break;
      if (ml > src_end - src && (ml = src_end - src) < min_match_len)
        break;

      uint offs = matches[i].offset;
      if (offs >= max_match_offset)
        continue;
      // Since decoder copies in 64-bit chunks we cannot support copies
      // that are longer than the offset, if offset is less than 8.
      if (offs < 8) {
        // Expand the offset to at least 8 bytes long
        uint tt = offs;
        do offs += tt; while (offs < 8);
        // Check if it's a valid offset and still match
        if (offs > src - window_base)
          continue;
        ml = GetMatchlengthQMin3(src, offs, src_end, m32);
        if (ml < min_match_len)
          continue;
      }
      if (KrakIsMatchLongEnough(ml, offs) && IsMatchBetter(ml, offs, best_m_ml, best_m_offs)) {
        best_m_offs = offs;
        best_m_ml = ml;
      }
    }
    if (IsBetterThanRecent(best_ml, best_m_ml, best_m_offs)) {
      best_ml = best_m_ml;
      best_offs = best_m_offs;
    }
  }
  result.length = best_ml;
  result.offset = best_offs;
  return result;
}

static int RunKrakenMatcherToGetStats(float *cost_ptr, int *chunk_type_ptr, Krak::Stats *stats,
                                      uint8 *dst_ptr, uint8 *dst_end,
                                      int min_match_len,
                                      LzCoder *lzcoder, LengthAndOffset *lao,
                                      const uint8 *src_ptr, int src_len, int start_pos, const uint8 *window_base,
                                      LzTemp *lztemp) {
  memset(stats, 0, sizeof(*stats));

  KrakenRecentOffs recent;
  KrakEncLz kl;

  KrakEncLz_Init(&kl, lztemp, src_len, src_ptr, lzcoder->encode_flags);

  int initial_copy_bytes = (start_pos == 0) ? 8 : 0;
  int pos = initial_copy_bytes, last_pos = initial_copy_bytes;
  int dict_size = lzcoder->opts->dictionarySize > 0 && lzcoder->opts->dictionarySize <= 0x40000000 ? lzcoder->opts->dictionarySize : 0x40000000;
  
  while (pos < src_len - 16) {
    LengthAndOffset m0 = KrakenGetLzMatch(&lao[4 * pos], &recent, &src_ptr[pos], &src_ptr[src_len - 8],
                                          min_match_len, pos - last_pos, window_base, dict_size);
    if (m0.length == 0) {
      pos++;
      continue;
    }

    while (pos + 1 < src_len - 16) {
      LengthAndOffset m1 = KrakenGetLzMatch(&lao[4 * (pos + 1)], &recent, &src_ptr[pos + 1], &src_ptr[src_len - 8],
                                            min_match_len, pos + 1 - last_pos, window_base, dict_size);

      if (m1.length && GetLazyScore(m1, m0) > 0) {
        pos++;  // lazy1 is better
        m0 = m1;
      } else {
        if (pos + 2 >= src_len - 16)
          break;
        LengthAndOffset m2 = KrakenGetLzMatch(&lao[4 * (pos + 2)], &recent, &src_ptr[pos + 2], &src_ptr[src_len - 8],
                                              min_match_len, pos + 2 - last_pos, window_base, dict_size);
        if (m2.length && GetLazyScore(m2, m0) > 3) {
          pos += 2;  // lazy2 is better
          m0 = m2;
        } else {
          break;
        }
      }
    }

    if (pos - last_pos == 0 && m0.offset == 0 && recent.offs[4] == recent.offs[5])
      m0.offset = -1;

    KrakEnc_AddToken<true, true>(kl, recent, src_ptr + last_pos, pos - last_pos, m0.length, m0.offset);
    pos += m0.length;
    last_pos = pos;
  }
  KrakEnc_AddFinal<true>(kl, src_ptr + last_pos, src_ptr + src_len);

  int opts_org = lzcoder->entropy_opts;
  int level_org = lzcoder->compression_level;
  lzcoder->entropy_opts &= ~kEntropyOpt_MultiArray;
  lzcoder->compression_level = 4;
  int result = Kraken_EncodeLzArrays(cost_ptr, chunk_type_ptr, stats, dst_ptr, dst_end, lzcoder, lztemp, &kl, start_pos);
  lzcoder->compression_level = level_org;
  lzcoder->entropy_opts = opts_org;
  return result;
}

static int Krak_EncodeTokens(LzTemp *lztemp, float *cost_ptr, int *chunk_type_ptr,
                             const uint8 *src, int src_len,
                             uint8 *dst, uint8 *dst_end, int start_pos,
                             LzCoder *lzcoder, Krak::TokenArray *tokarray, Krak::Stats *stat) {
  *cost_ptr = kInvalidCost;
  const uint8 *src_end = src + src_len;
  int tok_count = tokarray->size;
  if (tok_count == 0)
    return src_len;

  KrakenRecentOffs recent;
  KrakEncLz kl;
  KrakEncLz_Init(&kl, lztemp, src_len, src, lzcoder->encode_flags);

  src += (start_pos == 0) ? 8 : 0;

  for (int i = 0; i < tok_count; i++) {
    Krak::Token *tok = tokarray->data + i;
    KrakEnc_AddToken<true, true>(kl, recent, src, tok->litlen, tok->matchlen, tok->offset);
    src += tok->litlen + tok->matchlen;
  }
  KrakEnc_AddFinal<true>(kl, src, src_end);
  return Kraken_EncodeLzArrays(cost_ptr, chunk_type_ptr, stat, dst, dst_end, lzcoder, lztemp, &kl, start_pos);
}

template<int IsRecent>
static __forceinline bool UpdateState(int state_idx, int bits, int lrl, int ml, int recent, int prev_state, int qrm, Krak::State *S) {
  Krak::State *st = &S[state_idx];
  if (bits < st->best_bit_count) {
    st->best_bit_count = bits;
    st->litlen = lrl;
    st->matchlen = ml;
    int *recent_offs_ptr = S[prev_state].recent_offs;
    if (IsRecent) {
      assert(recent >= 0 && recent <= 2);
      if (recent == 0) {
        st->recent_offs[0] = recent_offs_ptr[0];
        st->recent_offs[1] = recent_offs_ptr[1];
        st->recent_offs[2] = recent_offs_ptr[2];
      } else if (recent == 1) {
        st->recent_offs[0] = recent_offs_ptr[1];
        st->recent_offs[1] = recent_offs_ptr[0];
        st->recent_offs[2] = recent_offs_ptr[2];
      } else {
        st->recent_offs[0] = recent_offs_ptr[2];
        st->recent_offs[1] = recent_offs_ptr[0];
        st->recent_offs[2] = recent_offs_ptr[1];
      }
    } else {
      st->recent_offs[0] = recent;
      st->recent_offs[1] = recent_offs_ptr[0];
      st->recent_offs[2] = recent_offs_ptr[1];
    }
    st->quick_recent_matchlen_litlen = qrm;
    st->prev_state = prev_state;
    return true;
  }
  return false;
}

template<int IsRecent>
static __forceinline void UpdateStatesZ(int pos, int bits, int lrl, int ml, int recent, int prev_state, Krak::State *S, const uint8 *src_ptr, int offs, int Z, Krak::CostModel &cm, int *litindexes) {
  int after_match = pos + ml;
  UpdateState<IsRecent>(after_match * Z, bits, lrl, ml, recent, prev_state, 0, S);
  for (int jj = 1; jj < Z; jj++) {
    bits += Krak::BitsForLit(src_ptr, after_match + jj - 1, offs, cm, jj - 1);
    if (UpdateState<IsRecent>((after_match + jj) * Z + jj, bits, lrl, ml, recent, prev_state, 0, S) && jj == Z - 1)
      litindexes[after_match + jj] = jj;
  }
}

int KrakenOptimal(LzCoder *lzcoder, LzTemp *lztemp,
                  const MatchLenStorage *mls,
                  const uint8 *src_ptr, int src_size,
                  uint8 *dst_ptr, uint8 *dst_end, int start_pos,
                  int *chunk_type_ptr, float *cost_ptr) {
  *chunk_type_ptr = 0;
  if (src_size <= 128)
    return -1;

  int Z = (lzcoder->compression_level >= 8) ? 2 : 1;
  int four_or_eight = (lzcoder->compression_level >= 6) ? 8 : 4;

  int dict_size = lzcoder->opts->dictionarySize > 0 && lzcoder->opts->dictionarySize <= 0x40000000 ?
    lzcoder->opts->dictionarySize : 0x40000000;

  int initial_copy_bytes = (start_pos == 0) ? 8 : 0;
  const uint8 *src_end_safe = src_ptr + src_size - 8;
  int min_match_length = std::max(lzcoder->opts->min_match_length, 4);
  int length_long_enough_thres = 1 << std::min(8, lzcoder->compression_level);

  LengthAndOffset *lao = (LengthAndOffset *)lztemp->allmatch_scratch.Allocate(sizeof(LengthAndOffset) * 4 * src_size);

  const uint8 *window_base = mls->window_base;
  ExtractLaoFromMls(mls, src_ptr - window_base, src_size, lao, 4);

  Krak::TokenArray lz_token_array;
  lz_token_array.capacity = src_size / 2 + 8;
  lz_token_array.size = 0;
  lz_token_array.data = (Krak::Token*)lztemp->lztoken2_scratch.Allocate(sizeof(Krak::Token) * lz_token_array.capacity);

  int tokens_capacity = 4096 + 8;
  Krak::Token *tokens_begin = (Krak::Token*)lztemp->lztoken_scratch.Allocate(sizeof(Krak::Token) * tokens_capacity);

  Krak::State *S = (Krak::State*)lztemp->states.Allocate(sizeof(Krak::State) * Z * (src_size + 1));
  uint8 *tmp_dst = (uint8*)S, *tmp_dst_end = tmp_dst + lztemp->states.size;

  int chunk_type = 0, tmp_chunk_type;

  Krak::CostModel cost_model;

  std::vector<int> litindexes_arr;
  int *litindexes = NULL;

  Krak::Stats stats, tmp_stats;

  float cost = kInvalidCost;
  int n_first = RunKrakenMatcherToGetStats(&cost, chunk_type_ptr, &stats,
                                           dst_ptr, dst_end,
                                           min_match_length, 
                                           lzcoder, lao, src_ptr, src_size,
                                           start_pos, window_base, lztemp);
  if (n_first >= src_size)
    return -1;

  float best_cost = cost;
  int best_length = n_first;

  if ((lzcoder->compression_level >= 7 || lzcoder->codec_id == 12) && lzcoder->opts->min_match_length <= 3) {
    cost = kInvalidCost;
    int tmp_chunk_type;
    int n = RunKrakenMatcherToGetStats(&cost, &tmp_chunk_type, &tmp_stats,
                                       tmp_dst, tmp_dst_end,
                                       3, lzcoder, lao, src_ptr, src_size,
                                       start_pos, window_base, lztemp);
    if (cost < best_cost && n < src_size) {
      *chunk_type_ptr = tmp_chunk_type;
      memcpy(dst_ptr, tmp_dst, n);
      best_cost = cost;
      best_length = n;
      min_match_length = 3;
      stats = tmp_stats;
    }
  }

  if (lzcoder->opts->min_match_length < 8) {
    cost = kInvalidCost;
    int tmp_chunk_type;
    int n = RunKrakenMatcherToGetStats(&cost, &tmp_chunk_type, &tmp_stats,
                                       tmp_dst, tmp_dst_end,
                                       8, lzcoder, lao, src_ptr, src_size,
                                       start_pos, window_base, lztemp);
    if (cost < best_cost && n < src_size) {
      *chunk_type_ptr = tmp_chunk_type;
      memcpy(dst_ptr, tmp_dst, n);
      best_cost = cost;
      best_length = n;
      stats = tmp_stats;
    }
  }

  if (lzcoder->compression_level >= 7 || lzcoder->codec_id == 12)
    min_match_length = std::max(lzcoder->opts->min_match_length, 3);
  
  if (Z > 1) {
    litindexes_arr.clear();
    litindexes_arr.resize(src_size + 1);
    litindexes = litindexes_arr.data();
  }

  int outer_loop_index = 0;

  for (;;) {
    cost_model.chunk_type = *chunk_type_ptr;
    cost_model.sub_or_copy_mask = (*chunk_type_ptr != 1) ? -1 : 0;

    if (lzcoder->last_chunk_type < 0) {
      Krak::Stats::Rescale(&stats);
    } else {
      Krak::Stats *h2 = (Krak::Stats*)lzcoder->lvsymstats_scratch.ptr;
      Krak::Stats::RescaleAdd(&stats, h2, lzcoder->last_chunk_type == *chunk_type_ptr);
    }
    cost_model.offs_encode_type = stats.offs_encode_type;
    Krak::MakeCostModel(&stats, &cost_model);

    for (int i = 0; i <= Z * src_size; i++)
      S[i].best_bit_count = INT_MAX;

    int final_lz_offset = -1;
    int last_recent0 = 0;
    lz_token_array.size = 0;
    int chunk_start = initial_copy_bytes;

    // Initial state
    S[Z * chunk_start].Initialize();

    while (chunk_start < src_size - 16) {
      int lit_bits_since_prev = 0;
      int prev_offset = chunk_start;

      int chunk_end = chunk_start + Krak::kMaxBytesPerRound;
      if (chunk_end >= src_size - 32)
        chunk_end = src_size - 16;

      int max_offset = chunk_start + Krak::kMinBytesPerRound;
      if (max_offset >= src_size - 32)
        max_offset = src_size - 16;

      int bits_for_encoding_offset_8 = Krak::BitsForOffset(cost_model, 8);

      if (Z > 1) {
        for (int i = 1; i < Z; i++)
          for (int j = 0; j < Z; j++)
            S[Z * (chunk_start + i) + j].best_bit_count = INT_MAX;

        for (int j = 1; j < Z; j++)
          S[Z * chunk_start + j].best_bit_count = INT_MAX;

        if (max_offset - chunk_start > Z) {
          for (int i = 1; i < Z; i++) {
            S[(chunk_start + i) * Z + i] = S[(chunk_start + i - 1) * Z + i - 1];
            S[(chunk_start + i) * Z + i].best_bit_count +=
              Krak::BitsForLit(src_ptr, chunk_start + i - 1, S[chunk_start * Z].recent_offs[0], cost_model, i - 1);
          }
          litindexes[chunk_start + Z - 1] = Z - 1;
        } else {
          // prevent loop from running
          chunk_start = src_size - 16;
        }
      }

      for (int pos = chunk_start; max_offset <= chunk_end; pos++) {
        if (pos == src_size - 16) {
          max_offset = pos;
          break;
        }

        const uint8 *src_cur = &src_ptr[pos];
        uint32 u32_at_cur = *(uint32*)src_cur;

        if (Z == 1) {
          // Accumulate lit bits since previous best position, but as soon as there exists a better
          // path to the state, reset the accumulation.
          if (pos != prev_offset) {
            lit_bits_since_prev += Krak::BitsForLit(src_ptr, pos - 1, S[prev_offset].recent_offs[0], cost_model, pos - prev_offset - 1);
            int curbits = S[pos].best_bit_count;
            if (curbits != INT_MAX) {
              int prevbits = S[prev_offset].best_bit_count + lit_bits_since_prev;
              if (curbits < prevbits + Krak::BitsForLitlen(cost_model, pos - prev_offset)) {
                prev_offset = pos;
                lit_bits_since_prev = 0;
                if (pos >= max_offset) {
                  max_offset = pos;
                  break;
                }
              }
            }
          }
        } else if (Z > 1) {
          if (pos >= max_offset) {
            int tmp_cur_offset = 0, best_bits = 0x7FFFFFFF;
            for (int i = 0; i < Z; i++) {
              if (S[Z * pos + i].best_bit_count < best_bits) {
                best_bits = S[Z * pos + i].best_bit_count;
                tmp_cur_offset = pos - ((i != Z - 1) ? i : litindexes[pos]);
              }
            }
            if (tmp_cur_offset >= max_offset) {
              max_offset = tmp_cur_offset;
              break;
            }
          }
          // Update the alternate state with the lit cost.
          Krak::State *cur = &S[Z * pos + Z - 1];
          if (cur->best_bit_count != 0x7FFFFFFF) {
            int bits = cur->best_bit_count + Krak::BitsForLit(src_ptr, pos, cur->recent_offs[0], cost_model, litindexes[pos]);
            if (bits < cur[Z].best_bit_count) {
              cur[Z] = *cur;
              cur[Z].best_bit_count = bits;
              litindexes[pos + 1] = litindexes[pos] + 1;
            }
          }
        }

        // Extract the possible matches from the match table
        LengthAndOffset match[8];
        int match_found_offset_bits[8];
        int num_match = 0;
        for (int lao_index = 0; lao_index < 4; lao_index++) {
          uint lao_ml = lao[4 * pos + lao_index].length;
          uint lao_offs = lao[4 * pos + lao_index].offset;
          if (lao_ml < min_match_length)
            break;
          lao_ml = std::min<uint>(lao_ml, src_end_safe - src_cur);
          if (lao_offs >= dict_size)
            continue;
          if ((uint)lao_offs < 8) {
            // Expand the offset to at least 8 bytes long
            int tt = lao_offs;
            do lao_offs += tt; while ((uint)lao_offs < 8);
            // Check if it's a valid offset and still match
            if (lao_offs > src_cur - window_base)
              continue;
            lao_ml = GetMatchlengthQMin4(src_cur, lao_offs, src_end_safe, u32_at_cur);
            if (lao_ml < min_match_length)
              continue;
          }
          if (Krak::CheckMatchValidLength(lao_ml, lao_offs)) {
            match[num_match].length = lao_ml;
            match[num_match].offset = lao_offs;
            match_found_offset_bits[num_match++] = Krak::BitsForOffset(cost_model, lao_offs);
          }
        }

        // Also always check offset 8
        int length = GetMatchlengthQMin3(src_cur, 8, src_end_safe, u32_at_cur);
        if (length >= min_match_length) {
          match[num_match].length = length;
          match[num_match].offset = 8;
          match_found_offset_bits[num_match++] = bits_for_encoding_offset_8;
        }

        int best_length_so_far = 0;
        int lits_since_prev = pos - prev_offset;
        int best_bits_y = 0x7FFFFFFF;

        // For each few literal lengths right before the current position, check if it improves things
        // by encoding the literals explicitly.
        for (int lazy = 0; lazy <= four_or_eight; lazy++) {
          int lrl, total_bits, prev_state;

          if (Z == 1) {
            lrl = (lazy == four_or_eight && lits_since_prev > four_or_eight) ? lits_since_prev : lazy;
            if (pos - lrl < chunk_start)
              break;
            prev_state = pos - lrl;
            total_bits = S[prev_state].best_bit_count;
            if (total_bits == INT_MAX)
              continue;
            total_bits += (lrl == lits_since_prev) ? lit_bits_since_prev :
              Krak::BitsForLits(src_ptr, pos - lrl, lrl,
                                           S[prev_state].recent_offs[0], cost_model, 0);
          } else {
            if (lazy < Z) {
              prev_state = Z * pos + lazy;
              total_bits = S[prev_state].best_bit_count;
              if (total_bits == INT_MAX)
                continue;
              lrl = (lazy == Z - 1) ? litindexes[pos] : lazy;
            } else {
              lrl = (lazy - 1);
              if (pos - lrl < chunk_start)
                break;
              prev_state = Z * (pos - lrl);
              total_bits = S[prev_state].best_bit_count;
              if (total_bits == INT_MAX)
                continue;
              total_bits += Krak::BitsForLits(src_ptr, pos - lrl, lrl,
                                                         S[prev_state].recent_offs[0], cost_model, 0);
            }
          }
          int length_field = lrl;
          if (lrl >= 3) {
            length_field = 3;
            total_bits += Krak::BitsForLitlen(cost_model, lrl);
          }
          int recent_best_length = 0;

          // For each recent offset
          for (int ridx = 0; ridx < Krak::kRecentOffsetCount; ridx++) {
            int offs = S[prev_state].recent_offs[ridx];
            int ml = Krak::GetMatchlengthQ(src_cur, offs, src_end_safe, u32_at_cur);
            if (ml <= recent_best_length)
              continue;
            recent_best_length = ml;
            max_offset = std::max(max_offset, pos + ml);
            int full_bits = total_bits + Krak::BitsForToken(cost_model, ml, pos - lrl, ridx, length_field);
            UpdateStatesZ<kRECENT>(pos, full_bits, lrl, ml, ridx, prev_state, S, src_ptr, offs, Z, cost_model, litindexes);
            if (ml > 2 && ml < length_long_enough_thres) {
              for (int tml = 2; tml < ml; tml++)
                UpdateStatesZ<kRECENT>(pos, total_bits + Krak::BitsForToken(cost_model, tml, pos - lrl, ridx, length_field),
                                       lrl, tml, ridx, prev_state, S, src_ptr, offs, Z, cost_model, litindexes);
            }
            // check if we have another recent0 match after 1-2 lits
            if (pos + ml + 4 < src_size - 16) {
              for (int num_lazy = 1; num_lazy <= 2; num_lazy++) {
                int tml = Krak::GetMatchlengthMin2(src_cur + ml + num_lazy, offs, src_end_safe);
                if (tml) {
                  int cost = full_bits +
                    Krak::BitsForLits(src_ptr, pos + ml, num_lazy, offs, cost_model, 0) +
                    Krak::BitsForToken(cost_model, tml, pos + ml, 0, num_lazy);
                  max_offset = std::max(max_offset, pos + ml + tml + num_lazy);
                  UpdateState<kRECENT>((pos + ml + tml + num_lazy) * Z,
                                       cost, lrl, ml, ridx, prev_state, num_lazy | (tml << 8), S);
                  break;
                }
              }
            }
          }
          // For each recent offset - END
          best_length_so_far = std::max(best_length_so_far, recent_best_length);

          if (best_length_so_far >= length_long_enough_thres)
            break;

          if (total_bits < best_bits_y) {
            best_bits_y = total_bits;

            // For each match entry BEGIN
            for (int matchidx = 0; matchidx < num_match; ++matchidx) {
              int ml = match[matchidx].length;
              int offs = match[matchidx].offset;
              if (ml <= recent_best_length)
                break;
              int after_match = pos + ml;
              best_length_so_far = std::max(best_length_so_far, ml);
              max_offset = std::max(max_offset, after_match);
              int bits_with_offlen = total_bits + match_found_offset_bits[matchidx];
              int full_bits = bits_with_offlen + Krak::BitsForToken(cost_model, ml, pos - lrl, Krak::kRecentOffsetCount, length_field);

              UpdateStatesZ<kOFFSET>(pos, full_bits, lrl, ml, offs, prev_state, S, src_ptr, offs, Z, cost_model, litindexes);
              if (ml > min_match_length && ml < length_long_enough_thres) {
                for (int tml = min_match_length; tml < ml; tml++)
                  UpdateStatesZ<kOFFSET>(pos, bits_with_offlen + Krak::BitsForToken(cost_model, tml, pos - lrl, Krak::kRecentOffsetCount, length_field),
                                         lrl, tml, offs, prev_state, S, src_ptr, offs, Z, cost_model, litindexes);
              }
              // check if we have another recent0 match after 1-2 lits
              if (after_match + 4 < src_size - 16) {
                for (int num_lazy = 1; num_lazy <= 2; num_lazy++) {
                  int tml = Krak::GetMatchlengthMin2(src_cur + ml + num_lazy, offs, src_end_safe);
                  if (tml) {
                    int cost = full_bits +
                      Krak::BitsForLits(src_ptr, after_match, num_lazy, offs, cost_model, 0) +
                      Krak::BitsForToken(cost_model, tml, after_match, 0, num_lazy);
                    max_offset = std::max(max_offset, after_match + tml + num_lazy);
                    UpdateState<kOFFSET>((after_match + tml + num_lazy) * Z,
                                         cost, lrl, ml, offs, prev_state, num_lazy | (tml << 8), S);
                    break;
                  }
                }
              }
            }
            // End of match Loop
          }
        }

        // Length is long enough to directly skip over some input
        if (best_length_so_far >= length_long_enough_thres) {
          int current_end = best_length_so_far + pos;
          if (max_offset == current_end) {
            max_offset = prev_offset = current_end;
            break;
          }
          if (Z == 1) {
            lit_bits_since_prev = 0;
            prev_offset = current_end;
          } else {
            int recent_offs = S[current_end * Z].recent_offs[0];
            for (int i = 1; i < Z; i++) {
              S[(current_end + i) * Z + i] = S[(current_end + i - 1) * Z + i - 1];
              S[(current_end + i) * Z + i].best_bit_count +=
                Krak::BitsForLit(src_ptr, current_end + i - 1, recent_offs, cost_model, i - 1);
            }
            litindexes[current_end + Z - 1] = Z - 1;
          }
          pos = current_end - 1;
        }
      } // for (max_offset <= chunk_end)

      int last_state_index = Z * max_offset;
      bool reached_end = (max_offset >= src_size - 18);
      if (reached_end) {
        int best_bits = INT_MAX;
        if (Z == 1) {
          for (int final_offs = std::max(chunk_start, prev_offset - 8); final_offs < src_size; final_offs++) {
            int bits = S[final_offs].best_bit_count;
            if (bits != INT_MAX) {
              bits += Krak::BitsForLits(src_ptr, final_offs, src_size - final_offs, S[final_offs].recent_offs[0], cost_model, 0);
              if (bits < best_bits) {
                best_bits = bits;
                final_lz_offset = final_offs;
                last_state_index = final_offs;
              }
            }
          }
        } else {
          for (int final_offs = std::max(chunk_start, max_offset - 8); final_offs < src_size; final_offs++) {
            for (int idx = 0; idx < Z; idx++) {
              int bits = S[Z * final_offs + idx].best_bit_count;
              if (bits != INT_MAX) {
                int litidx = ((idx == Z - 1) ? litindexes[final_offs] : idx);
                int offs = final_offs - litidx;
                if (offs >= chunk_start) {
                  bits += Krak::BitsForLits(src_ptr, final_offs, src_size - final_offs, S[Z * final_offs + idx].recent_offs[0], cost_model, litidx);
                  if (bits < best_bits) {
                    best_bits = bits;
                    final_lz_offset = offs;
                    last_state_index = Z * final_offs + idx;
                  }
                }
              }
            }
          }
        }
        max_offset = final_lz_offset;
        last_recent0 = S[last_state_index].recent_offs[0];
      }

      int outoffs = max_offset;
      int num_tokens = 0;
      Krak::State *state_cur = &S[last_state_index], *state_prev;
      while (outoffs != chunk_start) {
        uint32 qrm = state_cur->quick_recent_matchlen_litlen;
        if (qrm != 0) {
          outoffs = outoffs - (qrm >> 8) - (uint8)qrm;
          assert(num_tokens < tokens_capacity);
          tokens_begin[num_tokens].recent_offset0 = state_cur->recent_offs[0];
          tokens_begin[num_tokens].offset = 0;
          tokens_begin[num_tokens].matchlen = qrm >> 8;
          tokens_begin[num_tokens].litlen = (uint8)qrm;
          num_tokens++;
        }
        outoffs = outoffs - state_cur->litlen - state_cur->matchlen;
        state_prev = &S[state_cur->prev_state];
        int recent0 = state_cur->recent_offs[0];
        int recent_index = Krak::GetRecentOffsetIndex(state_prev->recent_offs, recent0);
        assert(num_tokens < tokens_capacity);
        tokens_begin[num_tokens].recent_offset0 = state_prev->recent_offs[0];
        tokens_begin[num_tokens].litlen = state_cur->litlen;
        tokens_begin[num_tokens].matchlen = state_cur->matchlen;
        tokens_begin[num_tokens].offset = (recent_index >= 0) ? -recent_index : recent0;
        num_tokens++;
        state_cur = state_prev;
      }

      std::reverse(tokens_begin, &tokens_begin[num_tokens]);

      memcpy(lz_token_array.data + lz_token_array.size, tokens_begin, sizeof(Krak::Token) * num_tokens);
      lz_token_array.size += num_tokens;

      if (reached_end)
        break;
      Krak::Stats::ReduceIfHigh(&stats);
      Krak::Stats::Update(&stats, src_ptr, chunk_start, tokens_begin, num_tokens);
      Krak::MakeCostModel(&stats, &cost_model);
      chunk_start = max_offset;
    }

    float cost = kInvalidCost;
    int n = Krak_EncodeTokens(lztemp, &cost, &tmp_chunk_type,
                              src_ptr, src_size, tmp_dst, tmp_dst_end,
                              start_pos, lzcoder, &lz_token_array, &stats);
    if (cost >= best_cost)
      break;

    *chunk_type_ptr = tmp_chunk_type;
    best_cost = cost;
    best_length = n;
    memcpy(dst_ptr, tmp_dst, n);

    if (lzcoder->compression_level < 8 || outer_loop_index || cost_model.chunk_type == tmp_chunk_type) {
      Krak::Stats *ks = (Krak::Stats *)lzcoder->lvsymstats_scratch.Allocate(sizeof(Krak::Stats));
      *ks = stats;
      lzcoder->last_chunk_type = tmp_chunk_type;
      break;
    }
    lzcoder->last_chunk_type = -1;
    outer_loop_index = 1;
  }
  *cost_ptr = best_cost;
  return best_length;
}


int KrakenDoCompress(LzCoder *coder, LzTemp *lztemp, MatchLenStorage *mls,
                         const uint8 *src, int src_size,
                         uint8 *dst, uint8 *dst_end,
                         int start_pos, int *chunk_type_ptr, float *cost_ptr) {
  if (coder->compression_level == 1)
    return KrakenCompressVeryfast<1, FastMatchHasher<uint32>>(coder, lztemp, mls, src, src_size, dst, dst_end, start_pos, chunk_type_ptr, cost_ptr);
  if (coder->compression_level == -1)
    return KrakenCompressVeryfast<-1, FastMatchHasher<uint32>>(coder, lztemp, mls, src, src_size, dst, dst_end, start_pos, chunk_type_ptr, cost_ptr);
  if (coder->compression_level == -2)
    return KrakenCompressVeryfast<-2, FastMatchHasher<uint16>>(coder, lztemp, mls, src, src_size, dst, dst_end, start_pos, chunk_type_ptr, cost_ptr);
  if (coder->compression_level == -3)
    return KrakenCompressVeryfast<-3, FastMatchHasher<uint16>>(coder, lztemp, mls, src, src_size, dst, dst_end, start_pos, chunk_type_ptr, cost_ptr);

  if (coder->compression_level == 2)
    return KrakenCompressFast<2, false, 0>(coder, lztemp, src, src_size, dst, dst_end, start_pos, chunk_type_ptr, cost_ptr);
  if (coder->compression_level == 3)
    return KrakenCompressFast<4, false, 1>(coder, lztemp, src, src_size, dst, dst_end, start_pos, chunk_type_ptr, cost_ptr);
  if (coder->compression_level == 4)
    return KrakenCompressFast<4, true, 2>(coder, lztemp, src, src_size, dst, dst_end, start_pos, chunk_type_ptr, cost_ptr);
  if (coder->compression_level >= 5)
    return KrakenOptimal(coder, lztemp, mls, src, src_size, dst, dst_end, start_pos, chunk_type_ptr, cost_ptr);

  assert(0);
  return -1;
}

void SetupEncoder_Kraken(LzCoder *coder, int src_len, int level,
                         const CompressOptions *copts,
                         const uint8 *src_base, const uint8 *src_start) {
  assert(src_base && src_start);
  int hash_bits = GetHashBits(src_len, std::max(level, 2), copts, 16, 20, 17, 24);

  coder->codec_id = kCompressorKraken;
  coder->quantum_blocksize = 0x20000;
  coder->check_plain_huffman = (level >= 3);
  coder->platforms = 0;
  coder->compression_level = level;
  coder->opts = copts;
  coder->speed_tradeoff = (copts->spaceSpeedTradeoffBytes * 0.00390625f) * 0.0099999998f;
  coder->max_matches_to_consider = 4;
  coder->limit_local_dictsize = (level >= 6);
  coder->compressor_file_id = 6;
  coder->encode_flags = 0;
  coder->entropy_opts = 0xff & ~kEntropyOpt_MultiArrayAdvanced;
  if (level >= 7)
    coder->entropy_opts |= kEntropyOpt_MultiArrayAdvanced;

  if (level >= 5)
    coder->encode_flags = 4;

  int min_match_len = 4;
  if (src_len > 0x10000 && level >= -2 && level <= 3 && IsProbablyText(src_start, src_len))
    min_match_len = 6;

  if (level >= 0 && level <= 1) {
    if (copts->hashBits <= 0)
      hash_bits = std::min(hash_bits, 19);
    CreateLzHasher<FastMatchHasher<uint32>, 0x1000000>(coder, src_base, src_start, hash_bits, min_match_len);
    coder->entropy_opts &= ~(kEntropyOpt_tANS | kEntropyOpt_MultiArray | kEntropyOpt_RLE);
  } else if (level == -1) {
    if (copts->hashBits <= 0)
      hash_bits = std::min(hash_bits, 16);
    CreateLzHasher<FastMatchHasher<uint32>, 0x1000000>(coder, src_base, src_start, hash_bits, min_match_len);
    coder->entropy_opts &= ~(kEntropyOpt_tANS | kEntropyOpt_MultiArray | kEntropyOpt_RLE);
  } else if (level == -2) {
    if (copts->hashBits <= 0)
      hash_bits = std::min(hash_bits, 14);
    CreateLzHasher<FastMatchHasher<uint16>, 0x1000000>(coder, src_base, src_start, hash_bits, min_match_len);
    coder->entropy_opts &= ~(kEntropyOpt_tANS | kEntropyOpt_MultiArray | kEntropyOpt_RLE);
  } else if (level == -3) {
    if (copts->hashBits <= 0)
      hash_bits = std::min(hash_bits, 12);
    CreateLzHasher<FastMatchHasher<uint16>, 0x1000000>(coder, src_base, src_start, hash_bits, min_match_len);
    coder->entropy_opts &= ~(kEntropyOpt_tANS | kEntropyOpt_MultiArray | kEntropyOpt_RLE);
  } else if (level == 2) {
    CreateLzHasher< MatchHasher<2, false> >(coder, src_base, src_start, hash_bits, min_match_len);
    coder->entropy_opts &= ~(kEntropyOpt_tANS | kEntropyOpt_MultiArray);
  } else if (level == 3) {
    CreateLzHasher< MatchHasher<4, false> >(coder, src_base, src_start, hash_bits, min_match_len);
    coder->entropy_opts &= ~(kEntropyOpt_tANS | kEntropyOpt_MultiArray);
  } else if (level == 4) {
    CreateLzHasher< MatchHasher<4, true> >(coder, src_base, src_start, hash_bits, 0);
    coder->entropy_opts &= ~(kEntropyOpt_tANS | kEntropyOpt_MultiArrayAdvanced);
  }
}
