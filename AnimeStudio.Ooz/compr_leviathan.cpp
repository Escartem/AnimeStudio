// This file is not GPL. It may be used for educational purposes only.
#include "stdafx.h"
#include "compr_leviathan.h"
#include "compress.h"
#include "compr_util.h"
#include "compr_entropy.h"
#include "compr_match_finder.h"
#include "match_hasher.h"
#include <algorithm>
#include <memory>

enum IsRecent {
  kOFFSET = 0,
  kRECENT = 1,
};


struct Levi {
  enum {
    kMinBytesPerRound = 384,
    kMaxBytesPerRound = 4096,
    kRecentOffsetCount = 7,
  };

  struct Token {
    int recent_offset0;
    int litoffs;
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
    int recent_offs[7];
    int matchlen;
    int litlen;
    int quick_recent_matchlen_litlen;
    int prev_state;

    void Initialize() {
      best_bit_count = 0;
      recent_offs[0] = 8;
      recent_offs[1] = 8;
      recent_offs[2] = 8;
      recent_offs[3] = 8;
      recent_offs[4] = 8;
      recent_offs[5] = 8;
      recent_offs[6] = 8;
      matchlen = 0;
      litlen = 0;
      prev_state = 0;
      quick_recent_matchlen_litlen = 0;
    }
  };


  struct LitStats {
    int total_lits;
    HistoU8 lit_histo;
    HistoU8 litsub_histo;
    HistoU8 litsub_histo_2nd;
    HistoU8 o1_histo[16];
    HistoU8 litsub3_histo[4];
    HistoU8 litsubf_histo[16];

    static void Update(LitStats *h, const uint8 *src, int offs, int num, int recent, int increment) {
      if (!num)
        return;
      const uint8 *p = src + offs;
      const uint8 *rp = src + offs - recent;

      h->total_lits += num;
      h->lit_histo.count[*p] += increment;
      h->litsub_histo.count[(uint8)(*p - *rp)] += increment;
      h->litsub3_histo[offs & 0x3].count[(uint8)(*p - *rp)] += increment;
      h->litsubf_histo[offs & 0xF].count[(uint8)(*p - *rp)] += increment;
      h->o1_histo[p[-1] >> 4].count[*p] += increment;
      while (--num) {
        p++, rp++, offs++;
        h->lit_histo.count[*p] += increment;
        h->litsub_histo_2nd.count[(uint8)(*p - *rp)] += increment;
        h->litsub3_histo[offs & 0x3].count[(uint8)(*p - *rp)] += increment;
        h->litsubf_histo[offs & 0xF].count[(uint8)(*p - *rp)] += increment;
        h->o1_histo[p[-1] >> 4].count[*p] += increment;
      }
    }

  };

  struct Stats {
    LitStats lit;
    HistoU8 xhisto;
    HistoU8 single_token_histo;
    HistoU8 multi_token_histo[8];
    char uses_multi_token_mode;
    HistoU8 matchlen_histo;
    HistoU8 litlen_histo;
    int offs_encode_type;
    HistoU8 dist_histo;
    HistoU8 distlo_histo;

    static void RescaleOne(HistoU8 *histo) {
      for (size_t i = 0; i != 256; i++)
        histo->count[i] = (histo->count[i] + 5) >> 3;
    }

    static void Rescale(Stats *h) {
      RescaleOne(&h->lit.lit_histo);
      RescaleOne(&h->lit.litsub_histo);
      RescaleOne(&h->lit.litsub_histo_2nd);
      for (size_t i = 0; i != 16; i++)
        RescaleOne(&h->lit.o1_histo[i]);
      for (size_t i = 0; i != 4; i++)
        RescaleOne(&h->lit.litsub3_histo[i]);
      for (size_t i = 0; i != 16; i++)
        RescaleOne(&h->lit.litsubf_histo[i]);
      RescaleOne(&h->xhisto);
      RescaleOne(&h->dist_histo);
      if (h->offs_encode_type > 1)
        RescaleOne(&h->distlo_histo);
      RescaleOne(&h->single_token_histo);
      for (size_t i = 0; i != 4; i++) // todo: oodle bug, why not 8
        RescaleOne(&h->multi_token_histo[i]);
      RescaleOne(&h->matchlen_histo);
      RescaleOne(&h->litlen_histo);
    }

    static void RescaleAddOne(HistoU8 *histo, const HistoU8 *histo2) {
      for (size_t i = 0; i != 256; i++)
        histo->count[i] = (histo->count[i] + histo2->count[i] + 11) >> 4;
    }

    static void RescaleAdd(Stats *h, const Stats *b) {
      RescaleAddOne(&h->lit.lit_histo, &b->lit.lit_histo);
      RescaleAddOne(&h->lit.litsub_histo, &b->lit.litsub_histo);
      RescaleAddOne(&h->lit.litsub_histo_2nd, &b->lit.litsub_histo_2nd);
      for (size_t i = 0; i != 16; i++)
        RescaleAddOne(&h->lit.o1_histo[i], &b->lit.o1_histo[i]);
      for (size_t i = 0; i != 4; i++)
        RescaleAddOne(&h->lit.litsub3_histo[i], &b->lit.litsub3_histo[i]);
      for (size_t i = 0; i != 16; i++)
        RescaleAddOne(&h->lit.litsubf_histo[i], &b->lit.litsubf_histo[i]);
      RescaleAddOne(&h->xhisto, &b->xhisto);
      RescaleAddOne(&h->single_token_histo, &b->single_token_histo);
      for (size_t i = 0; i != 4; i++) // todo: oodle bug, why not 8
        RescaleAddOne(&h->multi_token_histo[i], &b->multi_token_histo[i]);
      RescaleAddOne(&h->matchlen_histo, &b->matchlen_histo);
      RescaleAddOne(&h->litlen_histo, &b->litlen_histo);

      if (h->offs_encode_type == b->offs_encode_type) {
        if (h->offs_encode_type > 1)
          RescaleAddOne(&h->distlo_histo, &b->distlo_histo);
        RescaleAddOne(&h->dist_histo, &b->dist_histo);
      } else {
        if (h->offs_encode_type > 1)
          RescaleOne(&h->distlo_histo);
        RescaleOne(&h->dist_histo);
      }
    }

    static void ReduceIfHighOne(HistoU8 *histo) {
      if (GetHistoSum(*histo) >= 9000) {
        for (size_t i = 0; i != 256; i++)
          histo->count[i] = (histo->count[i] + 1) >> 1;
      }
    }

    static void ReduceIfHigh(Stats *h) {
      ReduceIfHighOne(&h->lit.lit_histo);
      ReduceIfHighOne(&h->lit.litsub_histo);
      ReduceIfHighOne(&h->lit.litsub_histo_2nd);
      for (size_t i = 0; i != 16; i++)
        ReduceIfHighOne(&h->lit.o1_histo[i]);
      for (size_t i = 0; i != 4; i++)
        ReduceIfHighOne(&h->lit.litsub3_histo[i]);
      for (size_t i = 0; i != 16; i++)
        ReduceIfHighOne(&h->lit.litsubf_histo[i]);
      ReduceIfHighOne(&h->xhisto);
      ReduceIfHighOne(&h->dist_histo);
      if (h->offs_encode_type > 1)
        ReduceIfHighOne(&h->distlo_histo);
      ReduceIfHighOne(&h->single_token_histo);
      for (size_t i = 0; i != 4; i++) // todo: oodle bug, why not 8
        ReduceIfHighOne(&h->multi_token_histo[i]);
      ReduceIfHighOne(&h->matchlen_histo);
      ReduceIfHighOne(&h->litlen_histo);
    }

    static void Update(Stats *h, const uint8 *src, int pos, const Levi::Token *tokens, int num_token) {
      enum {
        kIncrement = 5
      };
      for (int i = 0; i < num_token; i++) {
        const Levi::Token *t = &tokens[i];
        int litlen = t->litlen;
        int recent = t->recent_offset0;
        LitStats::Update(&h->lit, src, pos, litlen, recent, kIncrement);
        for (int j = 0; j < litlen; j++)
          h->xhisto.count[(uint8)(src[pos + j] - src[pos + j - recent])] += kIncrement;

        int token_pos = pos;
        pos += litlen + t->matchlen;

        int length_field = litlen;
        if (litlen >= 3) {
          h->litlen_histo.count[std::min(litlen - 3, 255)] += kIncrement;
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
          recent_field = 7;
          if (h->offs_encode_type == 0) {
            if (offset >= 8388360) {
              uint t = BSR(offset - 8322816) | 0xF0;
              h->dist_histo.count[t] += kIncrement;
            } else {
              uint t = ((offset - 8) & 0xF) + 16 * (BSR(offset - 8 + 256) - 8);
              h->dist_histo.count[t] += kIncrement;
            }
          } else if (h->offs_encode_type == 1) {
            uint t = BSR(offset + 8) - 3;
            uint u = 8 * t | (((offset + 8) >> t) ^ 8);
            h->dist_histo.count[u] += kIncrement;
          } else {
            uint ohi = offset / h->offs_encode_type, olo = offset % h->offs_encode_type;
            uint t = BSR(ohi + 8) - 3;
            uint u = 8 * t | (((ohi + 8) >> t) ^ 8);
            h->dist_histo.count[u] += kIncrement;
            h->distlo_histo.count[olo] += kIncrement;
          }
        }
        int matchlen_field = t->matchlen - 2;
        if (t->matchlen - 9 >= 0) {
          h->matchlen_histo.count[std::min(t->matchlen - 9, 255)] += kIncrement;
          matchlen_field = 7;
        }
        int token_value = matchlen_field + (recent_field << 5) + (length_field << 3);
        h->single_token_histo.count[token_value] += kIncrement;
        h->multi_token_histo[token_pos & 7].count[token_value] += kIncrement;
      }
    }

  };

  struct CostModel {
    int chunk_type;
    uint lit_cost[16][256];
    uint token_cost[8][256];
    int token_cost_mask;
    int offs_encode_type;
    uint dist_cost[256];
    uint distlo_cost[256];
    uint matchlen_cost[256];
    uint litlen_cost[256];
  };


  static void MakeCostModel(const Stats *h, CostModel *cm) {
    ConvertHistoToCost(h->dist_histo, cm->dist_cost, 12);
    if (h->offs_encode_type > 1)
      ConvertHistoToCost(h->distlo_histo, cm->distlo_cost, 0);
    ConvertHistoToCost(h->matchlen_histo, cm->matchlen_cost, 6);
    ConvertHistoToCost(h->litlen_histo, cm->litlen_cost, 2);
    if (h->uses_multi_token_mode) {
      cm->token_cost_mask = 7;
      for (size_t i = 0; i != 8; i++)
        ConvertHistoToCost(h->multi_token_histo[i], cm->token_cost[i], 6);
    } else {
      cm->token_cost_mask = 0;
      ConvertHistoToCost(h->single_token_histo, cm->token_cost[0], 6);
    }
    switch (cm->chunk_type) {
    case 0:
      ConvertHistoToCost(h->xhisto, cm->lit_cost[0], 0);
      break;
    case 1:
      ConvertHistoToCost(h->lit.lit_histo, cm->lit_cost[0], 0);
      break;
    case 2:
      ConvertHistoToCost(h->lit.litsub_histo, cm->lit_cost[0], 0);
      ConvertHistoToCost(h->lit.litsub_histo_2nd, cm->lit_cost[1], 0);
      break;
    case 3:
      for (size_t i = 0; i != 4; i++)
        ConvertHistoToCost(h->lit.litsub3_histo[i], cm->lit_cost[i], 0);
      break;
    case 4:
      for (size_t i = 0; i != 16; i++)
        ConvertHistoToCost(h->lit.o1_histo[i], cm->lit_cost[i], 0);
      break;
    case 5:
      for (size_t i = 0; i != 16; i++)
        ConvertHistoToCost(h->lit.litsubf_histo[i], cm->lit_cost[i], 0);
      break;
    }
  }
  struct SymStatsTwo {
    int chunk_type[2];
    Levi::Stats stats[2];
  };

  static __forceinline uint BitsForLitlen(CostModel &cost_model, int cur_litlen) {
    if (cur_litlen < 3)
      return 0;
    if (cur_litlen - 3 >= 255) {
      int v = BSR(((unsigned int)(cur_litlen - 3 - 255) >> 6) + 1);
      return cost_model.litlen_cost[255] + 32 * (2 * v + 7);
    } else {
      return cost_model.litlen_cost[cur_litlen - 3];
    }
  }

  static uint BitsForLit(const uint8 *src, int pos, int recent, const CostModel &cm, int litidx) {
    const uint8 *p = src + pos;
    switch (cm.chunk_type) {
    case 0: return cm.lit_cost[0][(uint8)(p[0] - p[-recent])];
    case 1: return cm.lit_cost[0][p[0]];
    case 2: return cm.lit_cost[(litidx != 0)][(uint8)(p[0] - p[-recent])];
    case 3: return cm.lit_cost[pos & 3][(uint8)(p[0] - p[-recent])];
    case 4: return cm.lit_cost[p[-1] >> 4][p[0]];
    case 5: return cm.lit_cost[pos & 0xf][(uint8)(p[0] - p[-recent])];
    default:return 0;
    }
  }

  static uint BitsForLits(const uint8 *src, int offs, int num, int recent, const CostModel &cm, int litidx) {
    const uint8 *p = src + offs;
    uint sum = 0;
    if (num == 0)
      return 0;
    switch (cm.chunk_type) {
    case 0:
      do sum += cm.lit_cost[0][(uint8)(p[0] - p[-recent])]; while (p++, --num);
      break;
    case 1:
      do sum += cm.lit_cost[0][p[0]]; while (p++, --num);
      break;
    case 2:
      if (litidx == 0)
        sum += cm.lit_cost[0][(uint8)(p[0] - p[-recent])], p++, num--;
      while (num)
        sum += cm.lit_cost[1][(uint8)(p[0] - p[-recent])], p++, num--;
      break;
    case 3:
      do sum += cm.lit_cost[offs++ & 3][(uint8)(p[0] - p[-recent])]; while (p++, --num);
      break;
    case 4:
      do sum += cm.lit_cost[p[-1] >> 4][p[0]]; while (p++, --num);
      break;
    case 5:
      do sum += cm.lit_cost[offs++ & 15][(uint8)(p[0] - p[-recent])]; while (p++, --num);
      break;
    }
    return sum;
  }

  static __forceinline int EnsureMatchLenShortEnough(int cur_matchlen, int offset) {
    if (offset < 8)
      cur_matchlen = std::min(cur_matchlen, offset);
    return cur_matchlen;
  }

  static __forceinline bool CheckMatchValidLength(uint ml, uint offs) {
    return (offs < 0x100000) || ((offs < 0x200000) ? (ml >= 5) : (offs < 0x400000) ? (ml >= 6) : (ml >= 8));
  }

  static __forceinline int GetMatchlengthQ(const uint8 *src, int offset, const uint8 *src_end, uint32 u32_at_cur) {
    return EnsureMatchLenShortEnough(::GetMatchlengthQ(src, offset, src_end, u32_at_cur), offset);
  }

  static __forceinline int GetMatchlengthMin2(const uint8 *src_ptr_cur, int offset, const uint8 *src_ptr_safe_end) {
    return EnsureMatchLenShortEnough(::GetMatchlengthMin2(src_ptr_cur, offset, src_ptr_safe_end), offset);
  }

  static __forceinline int BitsForToken(const CostModel &cost_model, int cur_matchlen, int cmd_offset, int recent_field, int length_field) {
    if (cur_matchlen - 9 >= 0) {
      int bits_for_matchlen;
      if (cur_matchlen - 9 >= 255) {
        int bsr = BSR(((unsigned int)(cur_matchlen - 9 - 255) >> 6) + 1);
        bits_for_matchlen = cost_model.matchlen_cost[255] + 32 * (2 * bsr + 7);
      } else {
        bits_for_matchlen = cost_model.matchlen_cost[cur_matchlen - 9];
      }
      return cost_model.token_cost[cost_model.token_cost_mask & cmd_offset][7 + (recent_field << 5) + (length_field << 3)] + bits_for_matchlen;
    } else {
      return cost_model.token_cost[cost_model.token_cost_mask & cmd_offset][(cur_matchlen - 2) + (recent_field << 5) + (length_field << 3)];
    }
  }

  static int BitsForOffset(const CostModel &cost_model, uint offs) {
    if (cost_model.offs_encode_type == 0) {
      if (offs >= 8388360) {
        unsigned t = BSR(offs - 8322816) | 0xF0;
        unsigned u = t - 224;
        return cost_model.dist_cost[t] + 32 * u + 12;
      } else {
        unsigned t = ((offs - 8) & 0xF) + 16 * (BSR(offs - 8 + 256) - 8);
        unsigned u = (t >> 4) + 4;
        return cost_model.dist_cost[t] + 32 * u;
      }
    } else if (cost_model.offs_encode_type == 1) {
      unsigned t = BSR(offs + 8) - 3;
      unsigned u = 8 * t | (((offs + 8) >> t) ^ 8);
      return cost_model.dist_cost[u] + 32 * (u >> 3);
    } else {
      unsigned ohi = offs / cost_model.offs_encode_type, olo = offs % cost_model.offs_encode_type;
      unsigned t = BSR(ohi + 8) - 3;
      unsigned u = 8 * t | (((ohi + 8) >> t) ^ 8);
      return cost_model.dist_cost[u] + 32 * (u >> 3) + cost_model.distlo_cost[olo];
    }
  }

  static __forceinline int GetRecentOffsetIndex(const int *arr, int offset) {
    return (offset == arr[0]) ? 0 :
      (offset == arr[1]) ? 1 :
      (offset == arr[2]) ? 2 :
      (offset == arr[3]) ? 3 :
      (offset == arr[4]) ? 4 :
      (offset == arr[5]) ? 5 :
      (offset == arr[6]) ? 6 : -1;
  }
};

static __forceinline bool IsMatchLongEnough2(uint ml, uint offs) {
  if (offs < 0x100000) {
    if (offs < 0x4000) {
      return ml >= 3;
    } if (offs < 0x20000) {
      return ml >= 4;
    } else {
      return ml >= 5;
    }
  } else {
    if (offs < 0x400000) {
      return ml >= 6;
    } else {
      return ml >= 8;
    }
  }
}

static __forceinline bool IsMatchLongEnough(uint ml, uint offs) {
  switch (ml) {
  case 0: return false;
  case 1: return offs < 0x80;
  case 2:
  case 3: return offs < 0x4000;
  case 4: return offs < 0x20000;
  case 5: return offs < 0x100000;
  case 6:
  case 7: return offs < 0x400000;
  default: return true;
  }
}

static float GetTime_Leviathan(int platforms, int packed_size, int num_tokens, int u8_len) {
  return CombineCostComponents(platforms,
                               200.0f + packed_size * 0.407f + num_tokens * 18.920f + u8_len * 3.716f,
                               200.0f + packed_size * 0.445f + num_tokens * 19.738f + u8_len * 7.407f,
                               200.0f + packed_size * 0.664f + num_tokens * 25.981f + u8_len * 7.029f,
                               200.0f + packed_size * 0.330f + num_tokens * 16.504f + u8_len * 3.000f);
}

static float GetCost_LeviathanOffsets(uint offs_encode_type, const uint32 *u32_offs, int offs_count, float speed_tradeoff, int platforms) {
  uint low_histo[128] = { 0 };
  HistoU8 high_histo = { 0 };

  uint bits_for_data = 0;
  for (int i = 0; i < offs_count; i++) {
    uint32 offset = u32_offs[i];
    uint ohi = offset / offs_encode_type, olo = offset % offs_encode_type;
    uint t = BSR(ohi + 8) - 3;
    uint u = 8 * t | (((ohi + 8) >> t) ^ 8);
    bits_for_data += t;
    high_histo.count[u]++;
    low_histo[olo]++;
  }
  float cost = BITSUP(GetHistoCostApprox(high_histo, offs_count)) + BITSUP(bits_for_data);
  if (offs_encode_type > 1) {
    cost += CombineCostComponents1A(platforms, offs_count, 0.595f, 1.05f, 1.179f, 0.567f,
                                    28.0f, 53.0f, 62.0f, 33.0f) * speed_tradeoff;
    cost += BITSUP(GetHistoCostApprox(low_histo, 128, offs_count));
    cost += GetTime_SingleHuffman(platforms, offs_count, 128) * speed_tradeoff;
  }
  return cost;
}

static int GetBestOffsetEncodingFast(const uint32 *u32_offs, int offs_count, float speed_tradeoff, int platforms) {
  uint arr[129];
  for (size_t i = 0; i < 129; i++)
    arr[i] = i;
  // look only at the short offsets
  for (int i = 0; i < offs_count; i++) {
    if (u32_offs[i] <= 128)
      arr[u32_offs[i]] += 256;
  }
  struct sorter {
    bool operator()(uint a, uint b) {
      return a > b;
    }
  };
  std::sort(arr, arr + 129, sorter());
  float best_cost = GetCost_LeviathanOffsets(1, u32_offs, offs_count, speed_tradeoff, platforms);
  int best_offs_encode_type = 1;

  for (size_t i = 0; i != 4; i++) {
    uint offs_encode_type = (uint8)arr[i];
    if (offs_encode_type > 1) {
      float cost = GetCost_LeviathanOffsets(offs_encode_type, u32_offs, offs_count, speed_tradeoff, platforms);
      if (cost < best_cost) {
        best_offs_encode_type = offs_encode_type;
        best_cost = cost;
      }
    }
  }
  return best_offs_encode_type;
}

static int GetBestOffsetEncodingSlow(const uint32 *u32_offs, int offs_count, float speed_tradeoff, int platforms) {
  if (offs_count < 32)
    return 1;
  int best_offs_encode_type = 0;
  float best_cost = kInvalidCost;

  for (uint offs_encode_type = 1; offs_encode_type <= 128; offs_encode_type++) {
    float cost = GetCost_LeviathanOffsets(offs_encode_type, u32_offs, offs_count, speed_tradeoff, platforms);
    if (cost < best_cost) {
      best_offs_encode_type = offs_encode_type;
      best_cost = cost;
    }
  }
  return best_offs_encode_type;
}

static void EncodeNewOffsets(uint32 *u32_offs, int offs_count, uint8 *u8_offs_hi, uint8 *u8_offs_lo, int *bits_type1_ptr, int offs_encode_type, const uint8 *u8_offs, int *bits_type0_ptr) {
  int bits_type0 = 0, bits_type1 = 0;
  if (offs_encode_type == 1) {
    for (int i = 0; i < offs_count; i++) {
      bits_type0 += (u8_offs[i] >= 0xf0) ? u8_offs[i] - 0xe0 : (u8_offs[i] >> 4) + 4;
      uint32 hi = u32_offs[i];
      int v = BSR(hi + 8) - 3;
      u8_offs_hi[i] = 8 * v | (((hi + 8) >> v) ^ 8);
      bits_type1 += v;
    }
  } else {
    for (int i = 0; i < offs_count; i++) {
      bits_type0 += (u8_offs[i] >= 0xf0) ? u8_offs[i] - 0xe0 : (u8_offs[i] >> 4) + 4;
      uint32 offs = u32_offs[i];
      uint32 lo = offs % offs_encode_type;
      uint32 hi = offs / offs_encode_type;
      int v = BSR(hi + 8) - 3;
      u8_offs_hi[i] = 8 * v | (((hi + 8) >> v) ^ 8);
      u8_offs_lo[i] = lo;
      bits_type1 += v;
    }
  }
  *bits_type0_ptr = bits_type0;
  *bits_type1_ptr = bits_type1;
}

static float GetTime_LeviathanOffset(int platforms, int offs_count, int offs_encode_type) {
  if (offs_encode_type == 0) {
    return CombineCostComponents1A(platforms, offs_count, 7.186f, 8.891f, 12.846f, 7.566f,
                                   62.07f, 43.106f, 135.580f, 62.485f);
  } else {
    float r = CombineCostComponents1A(platforms, offs_count, 8.183f, 6.598f, 14.464f, 8.403f,
                                      75.901f, 80.014f, 152.175f, 74.069f);
    if (offs_encode_type > 1)
      r += CombineCostComponents1A(platforms, offs_count, 0.595f, 1.05f, 1.179f, 0.567f,
                                   28.0f, 53.0f, 62.0f, 33.0f);
    return r;
  }
}

int EncodeLzOffsets(uint8 *dst, uint8 *dst_end, uint8 *u8_offs, uint32 *u32_offs, int offs_count,
                    int opts, float speed_tradeoff, int platforms,
                    float *cost_ptr, int min_match_len, bool use_offset_modulo_coding,
                    int *offs_encode_type_ptr, int level, HistoU8 *histo_ptr, HistoU8 *histolo_ptr) {
  int n = INT_MAX;
  HistoU8 histobuf;

  *cost_ptr = kInvalidCost;
  if (min_match_len == 8) {
    n = EncodeArrayU8(dst, dst_end, u8_offs, offs_count, opts, speed_tradeoff, platforms, cost_ptr, level, histo_ptr);
    if (n < 0)
      return -1;

    *cost_ptr += CombineCostComponents1A(platforms, offs_count, 7.186f, 8.891f, 12.846f, 7.566f,
                                         62.070f, 43.106f, 135.580f, 62.485f) * speed_tradeoff;
  }

  uint offs_encode_type = 0;
  if (use_offset_modulo_coding) {
    uint8 *temp = new uint8[offs_count * 4 + 16];
    std::unique_ptr<uint8_t[]> temp_deleter(temp);

    offs_encode_type = 1;
    if (level >= 8) {
      offs_encode_type = GetBestOffsetEncodingSlow(u32_offs, offs_count, speed_tradeoff, platforms);
    } else if (level >= 4) {
      offs_encode_type = GetBestOffsetEncodingFast(u32_offs, offs_count, speed_tradeoff, platforms);
    }

    uint8 *u8_offs_hi = temp;
    uint8 *u8_offs_lo = temp + offs_count;
    uint8 *tmp_dst_start = temp + offs_count * 2;
    uint8 *tmp_dst_end = temp + offs_count * 4 + 16;

    int bits_type1, bits_type0;
    EncodeNewOffsets(u32_offs, offs_count, u8_offs_hi, u8_offs_lo, &bits_type1, offs_encode_type, u8_offs, &bits_type0);

    uint8 *tmp_dst = tmp_dst_start;
    *tmp_dst++ = offs_encode_type + 127;

    if (histo_ptr)
      memset(&histobuf, 0, sizeof(histobuf));
    float cost = kInvalidCost;
    int n1 = EncodeArrayU8CompactHeader(tmp_dst, tmp_dst_end, u8_offs_hi, offs_count, opts, speed_tradeoff, platforms, &cost, level,
                                        histo_ptr ? &histobuf : NULL);
    if (n1 < 0)
      return -1;
    tmp_dst += n1;

    float cost_lo = 0.0f;
    if (offs_encode_type > 1) {
      cost_lo = kInvalidCost;
      n1 = EncodeArrayU8CompactHeader(tmp_dst, tmp_dst_end, u8_offs_lo, offs_count, opts, speed_tradeoff, platforms, &cost_lo, level, histolo_ptr);
      if (n1 < 0)
        return -1;
      tmp_dst += n1;
    }

    cost = cost + 1.0f + cost_lo + GetTime_LeviathanOffset(platforms, offs_count, offs_encode_type) * speed_tradeoff;
    if (BITSUP(bits_type0) + *cost_ptr <= BITSUP(bits_type1) + cost) {
      offs_encode_type = 0;
    } else {
      *cost_ptr = cost;
      n = tmp_dst - tmp_dst_start;
      memcpy(dst, tmp_dst_start, n);
      memcpy(u8_offs, u8_offs_hi, offs_count);
      if (histo_ptr)
        *histo_ptr = histobuf;
    }
  }
  *offs_encode_type_ptr = offs_encode_type;
  return n;
}

static void EncodeSubModeForBytes(uint8 *dst, const uint8 *src, size_t len, ptrdiff_t recent0) {
  for (; len >= 16; len -= 16, src += 16, dst += 16)
    simde_mm_storeu_si128((simde__m128i *)dst, simde_mm_sub_epi8(simde_mm_loadu_si128((const simde__m128i *)src), simde_mm_loadu_si128((const simde__m128i *)&src[-recent0])));
  for (; len; len--, src++, dst++)
    dst[0] = src[0] - src[-recent0];
}

static void WriteLiteralsInRawMode(const uint8 *src, int src_len, int initial_copy_bytes, uint8 *dst, Levi::TokenArray *tokens) {
  for (int i = 0; i < tokens->size; i++) {
    int lrl = tokens->data[i].litlen;
    if (lrl) {
      memcpy(dst, src + tokens->data[i].litoffs, lrl);
      dst += lrl;
    }
  }
  int pos = tokens->size ? tokens->data[tokens->size - 1].matchlen + tokens->data[tokens->size - 1].litlen +
    tokens->data[tokens->size - 1].litoffs : initial_copy_bytes;
  if (src_len - pos > 0)
    memcpy(dst, src + pos, src_len - pos);
}

static void WriteLiteralsInSubMode(const uint8 *src, int src_len, int initial_copy_bytes, uint8 *dst, Levi::TokenArray *tokens, int recent0) {
  for (int i = 0; i < tokens->size; i++) {
    int lrl = tokens->data[i].litlen;
    if (lrl) {
      EncodeSubModeForBytes(dst, src + tokens->data[i].litoffs, lrl, tokens->data[i].recent_offset0);
      dst += lrl;
    }
  }
  int pos = tokens->size ? tokens->data[tokens->size - 1].matchlen + tokens->data[tokens->size - 1].litlen +
    tokens->data[tokens->size - 1].litoffs : initial_copy_bytes;
  if (src_len - pos > 0)
    EncodeSubModeForBytes(dst, src + pos, src_len - pos, recent0);
}

static void WriteLiteralsInSub3Mode(const uint8 *src, int src_len, int initial_copy_bytes, uint8 **dst, Levi::TokenArray *tokens, int recent0) {
  for (int i = 0; i < tokens->size; i++) {
    int lrl = tokens->data[i].litlen;
    if (lrl) {
      int pos = tokens->data[i].litoffs, rec = tokens->data[i].recent_offset0;
      for (int j = 0; j < lrl; j++, pos++)
        *dst[pos & 3]++ = src[pos] - src[pos - rec];
    }
  }
  int pos = tokens->size ? tokens->data[tokens->size - 1].matchlen + tokens->data[tokens->size - 1].litlen +
    tokens->data[tokens->size - 1].litoffs : initial_copy_bytes;
  for (; pos < src_len; pos++)
    *dst[pos & 3]++ = src[pos] - src[pos - recent0];
}

static void WriteLiteralsInSubFMode(const uint8 *src, int src_len, int initial_copy_bytes, uint8 **dst, Levi::TokenArray *tokens, int recent0) {
  for (int i = 0; i < tokens->size; i++) {
    int lrl = tokens->data[i].litlen;
    if (lrl) {
      int pos = tokens->data[i].litoffs, rec = tokens->data[i].recent_offset0;
      for (int j = 0; j < lrl; j++, pos++)
        *dst[pos & 0xf]++ = src[pos] - src[pos - rec];
    }
  }
  int pos = tokens->size ? tokens->data[tokens->size - 1].matchlen + tokens->data[tokens->size - 1].litlen +
    tokens->data[tokens->size - 1].litoffs : initial_copy_bytes;
  for (; pos < src_len; pos++)
    *dst[pos & 0xf]++ = src[pos] - src[pos - recent0];
}

static void WriteLiteralsInLamsubMode(const uint8 *src, int src_len, int initial_copy_bytes, uint8 **dst, Levi::TokenArray *tokens, int recent0) {
  for (int i = 0; i < tokens->size; i++) {
    int lrl = tokens->data[i].litlen;
    if (lrl) {
      int pos = tokens->data[i].litoffs, rec = tokens->data[i].recent_offset0;
      *dst[1]++ = src[pos] - src[pos - rec];
      pos++;
      for (int j = 1; j < lrl; j++, pos++)
        *dst[0]++ = src[pos] - src[pos - rec];
    }
  }
  int pos = tokens->size ? tokens->data[tokens->size - 1].matchlen + tokens->data[tokens->size - 1].litlen +
    tokens->data[tokens->size - 1].litoffs : initial_copy_bytes;
  int num = src_len - pos;
  if (num > 0) {
    *dst[1]++ = src[pos] - src[pos - recent0];
    pos++;
    for (; --num; pos++)
      *dst[0]++ = src[pos] - src[pos - recent0];
  }
}

static void WriteLiteralsInO1Mode(const uint8 *src, int src_len, int initial_copy_bytes, uint8 **array_data, Levi::TokenArray *tokens) {
  for (int i = 0; i < tokens->size; i++) {
    int lrl = tokens->data[i].litlen;
    if (lrl) {
      const uint8 *p = src + tokens->data[i].litoffs;
      for (int j = 0; j < lrl; j++, p++)
        *array_data[p[-1] >> 4]++ = p[0];
    }
  }
  int src_cur = tokens->size ? tokens->data[tokens->size - 1].matchlen + tokens->data[tokens->size - 1].litlen +
    tokens->data[tokens->size - 1].litoffs : initial_copy_bytes;
  const uint8 *p = src + src_cur;
  for (; src_cur < src_len; src_cur++, p++)
    *array_data[p[-1] >> 4]++ = p[0];
}

int WriteLzOffsetBits(uint8 *dst, uint8 *dst_end, uint8 *u8_offs, uint32 *u32_offs, int offs_count, int offs_encode_type, uint32 *u32_len, int u32_len_count, int flag_ignore_u32_length, size_t a10) {
  if (dst_end - dst <= 16)
    return -1;

  BitWriter64<1> f(dst);
  BitWriter64<-1> b(dst_end);

  if (!flag_ignore_u32_length) {
    int nb = BSR(u32_len_count + 1);
    b.Write(1, nb + 1);
    if (nb)
      b.Write(u32_len_count + 1 - (1 << nb), nb);
  }

  if (offs_encode_type) {
    for (int i = 0; i < offs_count; i++) {
      if (b.ptr_ - f.ptr_ <= 8)
        return -1;
      uint nb = u8_offs[i] >> 3;
      uint bits = ((1 << nb) - 1) & (u32_offs[i] / offs_encode_type + 8);
      if (i & 1)
        b.Write(bits, nb);
      else
        f.Write(bits, nb);
    }
  } else {
    for (int i = 0; i < offs_count; i++) {
      if (b.ptr_ - f.ptr_ <= 8)
        return -1;
      uint nb, bits = u32_offs[i];
      if (u8_offs[i] < 0xf0) {
        nb = (u8_offs[i] >> 4) + 4;
        bits = ((bits + 248) >> 4) - (1 << nb);
      } else {
        nb = u8_offs[i] - 0xe0;
        bits = bits - (1 << nb) - 8322816;
      }
      if (i & 1)
        b.Write(bits, nb);
      else
        f.Write(bits, nb);
    }
  }

  if (!flag_ignore_u32_length) {
    for (int i = 0; i < u32_len_count; i++) {
      if (b.ptr_ - f.ptr_ <= 8)
        return -1;

      uint32 len = u32_len[i];
      int nb = BSR((len >> 6) + 1);
      if (i & 1) {
        b.Write(1, nb + 1);
        if (nb)
          b.Write((len >> 6) + 1 - (1 << nb), nb);
        b.Write(len & 0x3f, 6);
      } else {
        f.Write(1, nb + 1);
        if (nb)
          f.Write((len >> 6) + 1 - (1 << nb), nb);
        f.Write(len & 0x3f, 6);
      }
    }
  }
  uint8 *fp = f.GetFinalPtr();
  uint8 *bp = b.GetFinalPtr();

  if (bp - fp <= 8)
    return -1;
  memmove(fp, bp, dst_end - bp);
  return dst_end - bp + fp - dst;
}

static inline uint8 PackOffset(unsigned int offs) {
  if (offs >= 8388360) {
    return BSR(offs - 8322816) | 0xF0;
  } else {
    return ((offs - 8) & 0xF) | 16 * (BSR(offs + 248) - 8);
  }
}

static int Leviathan_EncodeLzArrays(LzTemp *lz_temp, float *cost_ptr, Levi::LitStats *litstats, int *chunk_type_ptr,
                                    const uint8 *src, int src_len, uint8 *dst, uint8 *dst_end,
                             int unused_start_pos, LzCoder *lzcoder, int recent_offs0, Levi::TokenArray *tokens,
                             int initial_bytes, Levi::Stats *arrhisto, int min_offset) {
  uint8 *dst_org = dst;
  int litlen_total = litstats->total_lits;
  int num_tokens = tokens->size;
  float speed_tradeoff = lzcoder->speed_tradeoff;
  int max_len = std::min(src_len / 5, 2 * num_tokens);
  int max_offs = std::min(src_len / 3, num_tokens);
  int opts = lzcoder->entropy_opts;
  int space_usage = 5 * (max_offs + max_len) + 32;
  int level = lzcoder->compression_level;
  int platforms = lzcoder->platforms;

  int scratch_size = (src_len >> 4) + src_len + 256;
  uint8 *scratchx = (uint8*)lz_temp->scratch2.Allocate(scratch_size);
  uint8 *scratchx_end = scratchx + scratch_size;

  uint8 *temp = new uint8[space_usage];
  std::unique_ptr<uint8_t[]> temp_deleter(temp);

  for (int i = 0; i < initial_bytes; i++)
    *dst++ = src[i];

  // Distribute memory
  uint8 *u8_offs = temp;
  uint8 *u8_lrl = temp + max_offs, *u8_lrl_cur = u8_lrl;
  uint8 *u8_ml = temp + max_len + max_offs, *u8_ml_cur = u8_ml;
  uint32 *u32_offs = (uint32*)((uintptr_t)(u8_ml + 3) & ~3);
  uint32 *u32_lrl = u32_offs + max_offs, *u32_lrl_cur = u32_lrl;
  uint32 *u32_ml = u32_lrl + max_len, *u32_ml_cur = u32_ml;
  int offs_count = 0;

  for (int i = 0; i < num_tokens; i++) {
    int offs = tokens->data[i].offset;
    if (offs > 0) {
      u32_offs[offs_count] = offs;
      if (min_offset != 1)
        u8_offs[offs_count] = PackOffset(offs);
      offs_count++;
    }
    int lrl = tokens->data[i].litlen - 3;
    int ml = tokens->data[i].matchlen - 9;
    if (lrl >= 0) {
      *u8_lrl_cur++ = std::min(lrl, 255);
      if (lrl >= 255)
        *u32_lrl_cur++ = lrl - 255;
    }
    if (ml >= 0) {
      *--u8_ml_cur = std::min(ml, 255);
      if (ml >= 255)
        *--u32_ml_cur = ml - 255;
    }
  }

  int u8_lrl_total = u8_lrl_cur - u8_lrl, u8_ml_total = u8_ml - u8_ml_cur;
  int u32_len_total = (u32_ml - u32_ml_cur) + (u32_lrl_cur - u32_lrl);
  int u8_len_total = u8_lrl_total + u8_ml_total;

  memcpy(u8_lrl_cur, u8_ml_cur, u8_ml_total);
  memcpy(u32_lrl_cur, u32_ml_cur, sizeof(uint32) * (u32_ml - u32_ml_cur));

  if (arrhisto) {
    CountBytesHistoU8(u8_lrl, u8_lrl_total, &arrhisto->litlen_histo);
    CountBytesHistoU8(u8_lrl_cur, u8_ml_total, &arrhisto->matchlen_histo);
  }

  int offs_encode_type = 0;
  float cost_offsets = kInvalidCost;
  int n = EncodeLzOffsets(dst, dst_end, u8_offs, u32_offs, offs_count,
                                    opts, speed_tradeoff, platforms,
                                    &cost_offsets, min_offset, true, &offs_encode_type, level,
                                    arrhisto ? &arrhisto->dist_histo : NULL,
                                    arrhisto ? &arrhisto->distlo_histo : NULL);
  if (n < 0)
    return -1;
  dst += n;

  if (arrhisto)
    arrhisto->offs_encode_type = offs_encode_type;

  float cost_u8_len = kInvalidCost;
  n = EncodeArrayU8_MaybeConcat(dst, dst_end, u8_lrl, u8_len_total, opts, speed_tradeoff,
                                platforms, &cost_u8_len, level, NULL, u8_lrl_total);
  if (n < 0)
    return -1;
  dst += n;

  cost_u8_len += CombineCostComponents(platforms,
                                       u8_len_total * 0.478f + 42.933f + u32_len_total * 21.527f,
                                       u8_len_total * 0.746f + 36.646f + u32_len_total * 32.345f,
                                       u8_len_total * 0.815f + 115.731f + u32_len_total * 36.682f,
                                       u8_len_total * 0.453f + 48.796f + u32_len_total * 20.770f) * speed_tradeoff;

  int scratch_required = u8_len_total + num_tokens + 4 * u8_len_total + 6 * offs_count + litlen_total + 32 + 4096 +
    std::max(std::max(std::max(num_tokens, litlen_total), std::max(u8_len_total, offs_count)) + 0xc000, src_len);

  int scratch_available = std::min(3 * src_len + 32 + 0xd000, 0x6C000);
  assert(scratch_required <= scratch_available);
  float adv_cost = kInvalidCost;

  if (litlen_total < 32) {
    *chunk_type_ptr = 1;
    if (litlen_total > 0)
      WriteLiteralsInRawMode(src, src_len, initial_bytes, scratchx, tokens);
    int n = EncodeArrayU8(dst, dst_end, scratchx, litlen_total, opts, speed_tradeoff, platforms, &adv_cost, level, 0);
    if (n < 0)
      return -1;
    dst += n;
  } else {
    HistoU8 xhisto;
    for (size_t i = 0; i != 256; i++)
      xhisto.count[i] = litstats->litsub_histo.count[i] + litstats->litsub_histo_2nd.count[i];

    if (arrhisto) {
      if (litstats != &arrhisto->lit)
        arrhisto->lit = *litstats;
      arrhisto->xhisto = xhisto;
    }

    float multi_cost = CombineCostComponents(platforms, 3283.811f, 5371.269f, 5615.7461f, 3112.6179f) * speed_tradeoff;

    float raw_cost = ((GetHistoCostApprox(litstats->lit_histo, GetHistoSum(litstats->lit_histo)) + 7) >> 3) + multi_cost;
    float litsub_cost = ((GetHistoCostApprox(xhisto, GetHistoSum(xhisto)) + 7) >> 3) + multi_cost;
    float lamsub_cost = ((GetHistoCostApprox(litstats->litsub_histo, GetHistoSum(litstats->litsub_histo)) + 7) >> 3) +
      ((GetHistoCostApprox(litstats->litsub_histo_2nd, GetHistoSum(litstats->litsub_histo_2nd)) + 7) >> 3) +
      multi_cost * 2;

    int litsub3_bytes = 0;
    for (size_t i = 0; i != 4; i++)
      litsub3_bytes += (GetHistoCostApprox(litstats->litsub3_histo[i], GetHistoSum(litstats->litsub3_histo[i])) + 7) >> 3;
    float litsub3_cost = litsub3_bytes + multi_cost * 4;

    int litsubf_bytes = 0;
    for (size_t i = 0; i != 16; i++)
      litsubf_bytes += (GetHistoCostApprox(litstats->litsubf_histo[i], GetHistoSum(litstats->litsubf_histo[i])) + 7) >> 3;
    float litsubf_cost = litsubf_bytes + multi_cost * 16;

    int o1_bytes = 0;
    for (size_t i = 0; i != 16; i++)
      o1_bytes += (GetHistoCostApprox(litstats->o1_histo[i], GetHistoSum(litstats->o1_histo[i])) + 7) >> 3;
    float o1_cost = o1_bytes + multi_cost * 16;

    float litsub_time_cost = CombineCostComponents1(platforms, litlen_total, 0.131f, 0.184f, 0.186f, 0.105f) * speed_tradeoff;
    float lamsub_time_cost = CombineCostComponents1(platforms, litlen_total, 0.554f, 0.874f, 1.149f, 0.606f) * speed_tradeoff;
    float litsub3_time_cost = CombineCostComponents1(platforms, litlen_total, 1.568f, 2.388f, 2.626f, 1.629f) * speed_tradeoff;
    float litsubf_time_cost = CombineCostComponents1(platforms, litlen_total, 3.946f, 5.858f, 6.167f, 3.795f) * speed_tradeoff;
    float o1_time_cost = CombineCostComponents1(platforms, litlen_total, 8.869f, 10.141f, 11.111f, 8.053f) * speed_tradeoff;

    litsub_cost += litsub_time_cost;
    lamsub_cost += lamsub_time_cost;
    litsub3_cost += litsub3_time_cost;
    litsubf_cost += litsubf_time_cost;
    o1_cost += o1_time_cost;

    float best_simple_cost = std::min(raw_cost, litsub_cost);
    float best_adv_cost = std::min(std::min(lamsub_cost, litsub3_cost), std::min(litsubf_cost, o1_cost));

    // optimize for text
    if (min_offset == 1) {
      best_adv_cost = o1_cost;
      best_simple_cost = raw_cost;
      litsub_cost = lamsub_cost = litsub3_cost = litsubf_cost = kInvalidCost;
    }

    int adv_nbytes = -1;

    int array_lens[16];
    const uint8 *array_data[16];
    uint8 *array_data_cur[16], *p = scratchx;
    float time_cost = 0.0f;

    if (best_adv_cost == o1_cost) {
      for (size_t i = 0; i != 16; i++) {
        array_lens[i] = GetHistoSum(litstats->o1_histo[i]);
        array_data[i] = array_data_cur[i] = p;
        p += array_lens[i];
      }
      *chunk_type_ptr = 4;
      WriteLiteralsInO1Mode(src, src_len, initial_bytes, array_data_cur, tokens);
      time_cost = o1_time_cost;
      adv_nbytes = EncodeMultiArray(dst, dst_end, array_data, array_lens, 16, opts, speed_tradeoff, platforms, &adv_cost, level);
    } else if (best_adv_cost == litsub3_cost) {
      for (size_t i = 0; i != 4; i++) {
        array_lens[i] = GetHistoSum(litstats->litsub3_histo[i]);
        array_data[i] = array_data_cur[i] = p;
        p += array_lens[i];
      }
      *chunk_type_ptr = 3;
      WriteLiteralsInSub3Mode(src, src_len, initial_bytes, array_data_cur, tokens, recent_offs0);
      time_cost = litsub3_time_cost;
      adv_nbytes = EncodeMultiArray(dst, dst_end, array_data, array_lens, 4, opts, speed_tradeoff, platforms, &adv_cost, level);
    } else if (best_adv_cost == litsubf_cost) {
      for (size_t i = 0; i != 16; i++) {
        array_lens[i] = GetHistoSum(litstats->litsubf_histo[i]);
        array_data[i] = array_data_cur[i] = p;
        p += array_lens[i];
      }
      *chunk_type_ptr = 5;
      WriteLiteralsInSubFMode(src, src_len, initial_bytes, array_data_cur, tokens, recent_offs0);
      time_cost = litsubf_time_cost;
      adv_nbytes = EncodeMultiArray(dst, dst_end, array_data, array_lens, 16, opts, speed_tradeoff, platforms, &adv_cost, level);
    } else if (best_adv_cost == lamsub_cost) {
      uint t = GetHistoSum(litstats->litsub_histo);
      array_lens[0] = litlen_total - t;
      array_lens[1] = t;
      array_data[0] = array_data_cur[0] = p;
      array_data[1] = array_data_cur[1] = p + (litlen_total - t);

      *chunk_type_ptr = 2;
      WriteLiteralsInLamsubMode(src, src_len, initial_bytes, array_data_cur, tokens, recent_offs0);
      time_cost = lamsub_time_cost;
      adv_nbytes = EncodeMultiArray(dst, dst_end, array_data, array_lens, 2, opts, speed_tradeoff, platforms, &adv_cost, level);
    } else {
      assert(0);
    }

    adv_cost += time_cost;

    if (best_simple_cost == litsub_cost) {
      WriteLiteralsInSubMode(src, src_len, initial_bytes, scratchx, tokens, recent_offs0);
      adv_cost -= litsub_time_cost;
      int n = EncodeArrayU8WithHisto(dst, dst_end, scratchx, litlen_total, xhisto, opts, speed_tradeoff, platforms, &adv_cost, level);
      adv_cost += litsub_time_cost;
      if (n > 0) {
        adv_nbytes = n;
        *chunk_type_ptr = 0;
      }
    } else if (best_simple_cost == raw_cost) {
      WriteLiteralsInRawMode(src, src_len, initial_bytes, scratchx, tokens);
      int n = EncodeArrayU8WithHisto(dst, dst_end, scratchx, litlen_total, litstats->lit_histo, opts, speed_tradeoff, platforms, &adv_cost, level);
      if (n > 0) {
        adv_nbytes = n;
        *chunk_type_ptr = 1;
      }
    }

    float memcpy_cost = litlen_total + 3;
    if ((adv_nbytes >= litlen_total || adv_cost >= memcpy_cost) && !(*chunk_type_ptr == 1 && adv_cost == memcpy_cost && adv_nbytes == litlen_total + 3)) {
      adv_cost = memcpy_cost;
      *chunk_type_ptr = 1;
      WriteLiteralsInRawMode(src, src_len, initial_bytes, scratchx, tokens);
      adv_nbytes = EncodeArrayU8_Memcpy(dst, dst_end, scratchx, litlen_total);
    }
    if (adv_nbytes < 0)
      return -1;
    dst += adv_nbytes;
  }

  if (num_tokens > scratchx_end - scratchx)
    return -1;

  int blkbytes = (src_len + 7) >> 3;
  uint8 *tokbuf = new uint8[blkbytes * 8];
  std::unique_ptr<uint8_t[]> tokbuf_deleter(tokbuf);

  const uint8 *cmd_ptr_org[8];
  uint8 *cmd_ptr[8], *p = tokbuf;
  for (size_t i = 0; i != 8; i++) {
    cmd_ptr_org[i] = cmd_ptr[i] = p;
    p += blkbytes;
  }

  uint8 *cmd_ptr_one = scratchx;

  for (int i = 0; i < num_tokens; i++) {
    const Levi::Token *t = &tokens->data[i];
    uint8 token_val = std::min(t->matchlen - 2, 7) + (std::min(t->litlen, 3) << 3) + ((t->offset <= 0 ? -t->offset : 7) << 5);
    *cmd_ptr_one++ = token_val;
    *cmd_ptr[t->litoffs & 7]++ = token_val;
  }
  int cmd_sizes[8];
  for (size_t i = 0; i != 8; i++) {
    cmd_sizes[i] = cmd_ptr[i] - cmd_ptr_org[i];
    if (arrhisto)
      CountBytesHistoU8(cmd_ptr_org[i], cmd_ptr[i] - cmd_ptr_org[i], &arrhisto->multi_token_histo[i]);
  }

  float token_cost = kInvalidCost;
  *dst = 0x83;
  int n1 = EncodeMultiArray(dst + 1, dst_end, cmd_ptr_org, cmd_sizes, 8, opts, speed_tradeoff, platforms, &token_cost, level);
  if (n1 >= 0) {
    n1++; // include 0x83 byte
    token_cost += CombineCostComponents1(platforms, num_tokens, 3.988f, 2.820f, 2.291f, 4.958f) * speed_tradeoff + 1.0f;
  }
  int n2 = EncodeArrayU8(dst, dst_end, scratchx, num_tokens, opts, speed_tradeoff, platforms, &token_cost, level, 0);
  if (arrhisto) {
    CountBytesHistoU8(scratchx, num_tokens, &arrhisto->single_token_histo);
    arrhisto->uses_multi_token_mode = (n2 < 0);
  }
  if (n2 < 0)
    n2 = n1;
  if (n2 < 0)
    return -1;
  dst += n2;

  n = WriteLzOffsetBits(dst, dst_end, u8_offs, u32_offs, offs_count, offs_encode_type, u32_lrl, u32_len_total, 0, 0);
  if (n < 0)
    return -1;
  dst += n;

  int nbytes = dst - dst_org;
  if (nbytes < src_len) {
    *cost_ptr = GetTime_Leviathan(platforms, src_len, num_tokens, u8_len_total) * speed_tradeoff +
      token_cost + adv_cost + cost_offsets + cost_u8_len + (n + initial_bytes);
    return nbytes;
  }
  return -1;
}

static LengthAndOffset LeviathanGetLzMatch(const LengthAndOffset *matches, LeviathanRecentOffs *recents,
                                           const uint8 *src, const uint8 *src_end,
                                           int min_match_len,
                                           const uint8 *window_base, int max_match_offset, int min_match_offset) {
  uint32 m32 = *(uint32*)src;
  int best_ml = 0, best_offs = 0, ml;
  LengthAndOffset result;

  if ((ml = Levi::GetMatchlengthQ(src, recents->offs[8], src_end, m32)) > best_ml)
    best_ml = ml, best_offs = 0;
  if ((ml = Levi::GetMatchlengthQ(src, recents->offs[9], src_end, m32)) > best_ml)
    best_ml = ml, best_offs = -1;
  if ((ml = Levi::GetMatchlengthQ(src, recents->offs[10], src_end, m32)) > best_ml)
    best_ml = ml, best_offs = -2;
  if ((ml = Levi::GetMatchlengthQ(src, recents->offs[11], src_end, m32)) > best_ml)
    best_ml = ml, best_offs = -3;
  if ((ml = Levi::GetMatchlengthQ(src, recents->offs[12], src_end, m32)) > best_ml)
    best_ml = ml, best_offs = -4;
  if ((ml = Levi::GetMatchlengthQ(src, recents->offs[13], src_end, m32)) > best_ml)
    best_ml = ml, best_offs = -5;
  if ((ml = Levi::GetMatchlengthQ(src, recents->offs[14], src_end, m32)) > best_ml)
    best_ml = ml, best_offs = -6;

  if (best_ml < 4) {
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
        if (min_match_offset == 1) {
          ml = std::min(ml, offs);
          if (ml >= min_match_len && IsMatchBetter(ml, offs, best_m_ml, best_m_offs)) {
            best_m_offs = offs;
            best_m_ml = ml;
          }
        }
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
      if (IsMatchLongEnough2(ml, offs) && IsMatchBetter(ml, offs, best_m_ml, best_m_offs)) {
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

static int RunLeviMatcherToGetStats(float *cost_ptr, int *chunk_type_ptr, Levi::Stats *arr_histo,
                           uint8 *dst_ptr, uint8 *dst_end,
                           int min_match_len, int min_offset,
                           LzCoder *lzcoder, LengthAndOffset *allmatches,
                           const uint8 *src_ptr, int src_len, int start_pos, const uint8 *window_base,
                           LzTemp *lz_temp, Levi::TokenArray *lz_token_array) {

  memset(arr_histo, 0, sizeof(Levi::Stats));
  lz_token_array->size = 0;
  LeviathanRecentOffs recent;

  int initial_copy_bytes = (start_pos == 0) ? 8 : 0;
  int pos = initial_copy_bytes, last_pos = initial_copy_bytes;
  int dict_size = lzcoder->opts->dictionarySize > 0 && lzcoder->opts->dictionarySize <= 0x40000000 ? lzcoder->opts->dictionarySize : 0x40000000;

  while (pos < src_len - 16) {
    LengthAndOffset m0 = LeviathanGetLzMatch(&allmatches[4 * pos], &recent, &src_ptr[pos], &src_ptr[src_len - 8],
                                    min_match_len, window_base, dict_size, min_offset);
    if (m0.length == 0) {
      pos++;
      continue;
    }

    while (pos + 1 < src_len - 16) {
      LengthAndOffset m1 = LeviathanGetLzMatch(&allmatches[4 * (pos + 1)], &recent, &src_ptr[pos + 1], &src_ptr[src_len - 8],
                                      min_match_len, window_base, dict_size, min_offset);

      if (m1.length && GetLazyScore(m1, m0) > 0) {
        pos++;  // lazy1 is better
        m0 = m1;
      } else {
        if (pos + 2 >= src_len - 16)
          break;
        LengthAndOffset m2 = LeviathanGetLzMatch(&allmatches[4 * (pos + 2)], &recent, &src_ptr[pos + 2], &src_ptr[src_len - 8],
                                        min_match_len, window_base, dict_size, min_offset);
        if (m2.length && GetLazyScore(m2, m0) > 3) {
          pos += 2;  // lazy2 is better
          m0 = m2;
        } else {
          break;
        }
      }
    }

    if (pos - last_pos == 0 && m0.offset == 0 && recent.offs[8] == recent.offs[9])
      m0.offset = -1;

    assert(lz_token_array->size < lz_token_array->capacity);
    Levi::Token &token = lz_token_array->data[lz_token_array->size++];
    token.recent_offset0 = recent.offs[8];
    token.matchlen = m0.length;
    token.litoffs = last_pos;
    token.litlen = pos - last_pos;
    token.offset = m0.offset;
    Levi::LitStats::Update(&arr_histo->lit, src_ptr, last_pos, pos - last_pos, recent.offs[8], 1);
    recent.Insert(m0.offset);

    pos += m0.length;
    last_pos = pos;
  }
  if (src_len - last_pos > 0)
    Levi::LitStats::Update(&arr_histo->lit, src_ptr, last_pos, src_len - last_pos, recent.offs[8], 1);
  int opts_org = lzcoder->entropy_opts;
  int level_org = lzcoder->compression_level;
  lzcoder->entropy_opts &= ~kEntropyOpt_MultiArray;
  lzcoder->compression_level = 4;
  int result = Leviathan_EncodeLzArrays(lz_temp, cost_ptr, &arr_histo->lit, chunk_type_ptr,
                                        src_ptr, src_len, dst_ptr, dst_end,
                                        start_pos, lzcoder, recent.offs[8], lz_token_array,
                                        initial_copy_bytes, arr_histo, min_offset);
  lzcoder->compression_level = level_org;
  lzcoder->entropy_opts = opts_org;
  return result;
}


template<int IsRecent>
static __forceinline bool UpdateState(int state_entry, int bits, int lrl, int ml, int recent, int prev_state, int qrm, Levi::State *state) {
  Levi::State *st = &state[state_entry];
  if (bits < st->best_bit_count) {
    st->best_bit_count = bits;
    st->litlen = lrl;
    st->matchlen = ml;
    int *recent_offs_ptr = state[prev_state].recent_offs;
    if (IsRecent) {
      int temp[16];
      temp[8] = *recent_offs_ptr;
      temp[9] = recent_offs_ptr[1];
      temp[10] = recent_offs_ptr[2];
      temp[11] = recent_offs_ptr[3];
      temp[12] = recent_offs_ptr[4];
      temp[13] = recent_offs_ptr[5];
      temp[14] = recent_offs_ptr[6];
      simde__m128i t0 = simde_mm_loadu_si128((const simde__m128i *)&temp[recent]),
        t1 = simde_mm_loadu_si128((const simde__m128i *)&temp[recent + 4]);
      st->recent_offs[0] = temp[recent + 8];
      simde_mm_storeu_si128((simde__m128i *)&temp[recent + 1], t0);
      simde_mm_storeu_si128((simde__m128i *)&temp[recent + 5], t1);
      st->recent_offs[1] = temp[9];
      st->recent_offs[2] = temp[10];
      st->recent_offs[3] = temp[11];
      st->recent_offs[4] = temp[12];
      st->recent_offs[5] = temp[13];
      st->recent_offs[6] = temp[14];
    } else {
      st->recent_offs[0] = recent;
      st->recent_offs[1] = recent_offs_ptr[0];
      st->recent_offs[2] = recent_offs_ptr[1];
      st->recent_offs[3] = recent_offs_ptr[2];
      st->recent_offs[4] = recent_offs_ptr[3];
      st->recent_offs[5] = recent_offs_ptr[4];
      st->recent_offs[6] = recent_offs_ptr[5];
    }
    st->quick_recent_matchlen_litlen = qrm;
    st->prev_state = prev_state;
    return true;
  }
  return false;
}

template<int IsRecent>
static __forceinline void UpdateStatesZ(int pos, int bits, int lrl, int ml, int recent, int prev_state, Levi::State *S, const uint8 *src_ptr, int offs, int Z, Levi::CostModel &cm, int *litindexes) {
  int after_match = pos + ml;
  UpdateState<IsRecent>(after_match * Z, bits, lrl, ml, recent, prev_state, 0, S);
  for (int jj = 1; jj < Z; jj++) {
    bits += Levi::BitsForLit(src_ptr, after_match + jj - 1, offs, cm, jj - 1);
    if (UpdateState<IsRecent>((after_match + jj) * Z + jj, bits, lrl, ml, recent, prev_state, 0, S) && jj == Z - 1)
      litindexes[after_match + jj] = jj;
  }
}

int LeviathanOptimal(LzCoder *lzcoder, LzTemp *lztemp,
                     const MatchLenStorage *mls,
                     const uint8 *src_ptr, int src_size,
                     uint8 *dst_ptr, uint8 *dst_end, int offset,
                     int *chunk_type_ptr, float *cost_ptr) {
  *chunk_type_ptr = 0;
  if (src_size <= 128)
    return -1;

  int Z = (lzcoder->compression_level >= 8) ? 2 : 1;
  int four_or_eight = (lzcoder->compression_level >= 6) ? 8 : 4;

  int offset_limit = lzcoder->opts->dictionarySize > 0 && lzcoder->opts->dictionarySize <= 0x40000000 ?
    lzcoder->opts->dictionarySize : 0x40000000;

  int initial_copy_bytes = (offset == 0) ? 8 : 0;
  const uint8 *src_end_safe = src_ptr + src_size - 8;
  int min_match_length = std::max(lzcoder->opts->min_match_length, 4);
  int min_match_offset = 8;
  int length_long_enough_thres = 1 << std::min(8, lzcoder->compression_level);

  LengthAndOffset *allmatches = (LengthAndOffset *)lztemp->allmatch_scratch.Allocate(sizeof(LengthAndOffset) * 4 * src_size);

  const uint8 *window_base = mls->window_base;
  ExtractLaoFromMls(mls, src_ptr - window_base, src_size, allmatches, 4);

  Levi::TokenArray lz_token_array;
  lz_token_array.capacity = src_size / 2 + 8;
  lz_token_array.size = 0;
  lz_token_array.data = (Levi::Token*)lztemp->lztoken2_scratch.Allocate(sizeof(Levi::Token) * lz_token_array.capacity);

  int tokens_capacity = 4096 + 8;
  Levi::Token *tokens_begin = (Levi::Token*)lztemp->lztoken_scratch.Allocate(sizeof(Levi::Token) * tokens_capacity);

  Levi::State *S = (Levi::State*)lztemp->states.Allocate(sizeof(Levi::State) * Z * (src_size + 1));
  uint8 *tmp_dst = (uint8*)S, *tmp_dst_end = tmp_dst + lztemp->states.size;

  int chunk_type = 0, tmp_chunk_type;

  Levi::CostModel cost_model;

  std::vector<int> litindexes_arr;
  int *litindexes = NULL;
  int return_value = INT_MAX;

  Levi::Stats stats, tmp_histo;

  for (int outer_loop_index = 0; outer_loop_index < 2; outer_loop_index++) {
    if (outer_loop_index == 0) {
      float cost = kInvalidCost;
      int n = RunLeviMatcherToGetStats(&cost, &chunk_type, &stats,
                                     dst_ptr, dst_end,
                                     min_match_length, min_match_offset,
                                     lzcoder, allmatches, src_ptr, src_size,
                                     offset, window_base, lztemp, &lz_token_array);
      return_value = n;
      if (n >= src_size)
        continue;
      *chunk_type_ptr = chunk_type;
      *cost_ptr = cost;

      if (lzcoder->opts->min_match_length < 8) {
        cost = kInvalidCost;
        n = RunLeviMatcherToGetStats(&cost, &tmp_chunk_type, &tmp_histo,
                                   tmp_dst, tmp_dst_end,
                                   8, min_match_offset,
                                   lzcoder, allmatches, src_ptr, src_size,
                                   offset, window_base, lztemp, &lz_token_array);
        if (cost < *cost_ptr && n < src_size) {
          *chunk_type_ptr = chunk_type = tmp_chunk_type;
          *cost_ptr = cost;
          return_value = n;
          memcpy(dst_ptr, tmp_dst, n);
          stats = tmp_histo;
        }
      }
      if ((lzcoder->compression_level >= 7 || lzcoder->codec_id == 12) && lzcoder->opts->min_match_length <= 3)
        min_match_length = 3;
    } else {
      if (lzcoder->compression_level < 9)
        break;
      min_match_offset = 1;
      min_match_length = 3;
      float cost = kInvalidCost;
      int n = RunLeviMatcherToGetStats(&cost, &chunk_type, &stats,
                                     tmp_dst, tmp_dst_end,
                                     min_match_length, min_match_offset,
                                     lzcoder, allmatches, src_ptr, src_size,
                                     offset, window_base, lztemp, &lz_token_array);
      if (n >= src_size)
        continue;
      if (cost < *cost_ptr) {
        *cost_ptr = cost;
        *chunk_type_ptr = chunk_type;
        memcpy(dst_ptr, tmp_dst, n);
        return_value = n;
      }
    }

    cost_model.chunk_type = chunk_type;

    if (Z > 1) {
      litindexes_arr.clear();
      litindexes_arr.resize(src_size + 1);
      litindexes = litindexes_arr.data();
    }

    if (lzcoder->lvsymstats_scratch.size == 0) {
      Levi::Stats::Rescale(&stats);
    } else {
      Levi::SymStatsTwo *h2 = (Levi::SymStatsTwo *)lzcoder->lvsymstats_scratch.ptr;
      Levi::Stats::RescaleAdd(&stats, &h2->stats[outer_loop_index]);
    }

    cost_model.offs_encode_type = stats.offs_encode_type;
    Levi::MakeCostModel(&stats, &cost_model);

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

      int chunk_end = chunk_start + Levi::kMaxBytesPerRound;
      if (chunk_end >= src_size - 32)
        chunk_end = src_size - 16;

      int max_offset = chunk_start + Levi::kMinBytesPerRound;
      if (max_offset >= src_size - 32)
        max_offset = src_size - 16;

      int bits_for_encoding_offset_8 = Levi::BitsForOffset(cost_model, 8);

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
              Levi::BitsForLit(src_ptr, chunk_start + i - 1, S[chunk_start * Z].recent_offs[0], cost_model, i - 1);
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
        const uint8 *src_ptr_cur = &src_ptr[pos];
        uint32 u32_at_cur = *(uint32*)src_ptr_cur;

        if (Z == 1) {
          // Accumulate lit bits since previous best position, but as soon as there exists a better
          // path to the state, reset the accumulation.
          if (pos != prev_offset) {
            lit_bits_since_prev += Levi::BitsForLit(src_ptr, pos - 1, S[prev_offset].recent_offs[0], cost_model, pos - prev_offset - 1);
            int curbits = S[pos].best_bit_count;
            if (curbits != INT_MAX) {
              int prevbits = S[prev_offset].best_bit_count + lit_bits_since_prev;
              if (curbits < prevbits + Levi::BitsForLitlen(cost_model, pos - prev_offset)) {
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
          Levi::State *cur = &S[Z * pos + Z - 1];
          if (cur->best_bit_count != 0x7FFFFFFF) {
            int bits = cur->best_bit_count + Levi::BitsForLit(src_ptr, pos, cur->recent_offs[0], cost_model, litindexes[pos]);
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
          uint lao_length = allmatches[4 * pos + lao_index].length;
          uint lao_offset = allmatches[4 * pos + lao_index].offset;
          if (lao_length < min_match_length)
            break;
          lao_length = std::min<uint>(lao_length, src_end_safe - src_ptr_cur);
          if (lao_offset >= offset_limit)
            continue;
          // Since decoder copies in 64-bit chunks we cannot support copies
          // that are longer than the offset, if offset is less than 8.
          if ((uint)lao_offset < 8) {
            if (min_match_offset == 1) {
              int curlen = std::min(lao_length, lao_offset);
              if (curlen >= min_match_length) {
                match[num_match].length = curlen;
                match[num_match].offset = lao_offset;
                match_found_offset_bits[num_match++] = Levi::BitsForOffset(cost_model, lao_offset);
              }
            }
            // Expand the offset to at least 8 bytes long
            int tt = lao_offset;
            do lao_offset += tt; while ((uint)lao_offset < 8);
            // Check if it's a valid offset and still match
            if (lao_offset > src_ptr_cur - window_base)
              continue;
            lao_length = GetMatchlengthQMin4(src_ptr_cur, lao_offset, src_end_safe, u32_at_cur);
            if (lao_length < min_match_length)
              continue;
          }
          if (Levi::CheckMatchValidLength(lao_length, lao_offset)) {
            match[num_match].length = lao_length;
            match[num_match].offset = lao_offset;
            match_found_offset_bits[num_match++] = Levi::BitsForOffset(cost_model, lao_offset);
          }
        }

        // Also always check offset 8
        if (min_match_offset == 8) {
          int length = GetMatchlengthQMin3(src_ptr_cur, 8, src_end_safe, u32_at_cur);
          if (length >= min_match_length) {
            match[num_match].length = length;
            match[num_match].offset = 8;
            match_found_offset_bits[num_match++] = bits_for_encoding_offset_8;
          }
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
                Levi::BitsForLits(src_ptr, pos - lrl, lrl,
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
              total_bits += Levi::BitsForLits(src_ptr, pos - lrl, lrl,
                                                         S[prev_state].recent_offs[0], cost_model, 0);
            }
          }
          int length_field = lrl;
          if (lrl >= 3) {
            length_field = 3;
            total_bits += Levi::BitsForLitlen(cost_model, lrl);
          }
          int recent_best_length = 0;

          // For each recent offset
          for (int ridx = 0; ridx < Levi::kRecentOffsetCount; ridx++) {
            int offs = S[prev_state].recent_offs[ridx];
            int ml = Levi::GetMatchlengthQ(src_ptr_cur, offs, src_end_safe, u32_at_cur);
            if (ml <= recent_best_length)
              continue;
            recent_best_length = ml;
            max_offset = std::max(max_offset, pos + ml);
            int full_bits = total_bits + Levi::BitsForToken(cost_model, ml, pos - lrl, ridx, length_field);
            UpdateStatesZ<kRECENT>(pos, full_bits, lrl, ml, ridx, prev_state, S, src_ptr, offs, Z, cost_model, litindexes);
            if (ml > 2 && ml < length_long_enough_thres) {
              for (int tml = 2; tml < ml; tml++)
                UpdateStatesZ<kRECENT>(pos, total_bits + Levi::BitsForToken(cost_model, tml, pos - lrl, ridx, length_field),
                                           lrl, tml, ridx, prev_state, S, src_ptr, offs, Z, cost_model, litindexes);
            }
            // check if we have another recent0 match after 1-2 lits
            if (pos + ml + 4 < src_size - 16) {
              for (int num_lits_x = 1; num_lits_x <= 2; num_lits_x++) {
                int tml = Levi::GetMatchlengthMin2(src_ptr_cur + ml + num_lits_x, offs, src_end_safe);
                if (tml) {
                  int cost = full_bits +
                    Levi::BitsForLits(src_ptr, pos + ml, num_lits_x, offs, cost_model, 0) +
                    Levi::BitsForToken(cost_model, tml, pos + ml, 0, num_lits_x);
                  max_offset = std::max(max_offset, pos + ml + tml + num_lits_x);
                  UpdateState<kRECENT>((pos + ml + tml + num_lits_x) * Z,
                                       cost, lrl, ml, ridx, prev_state, num_lits_x | (tml << 8), S);
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

            // For each match BEGIN
            for (int matchidx = 0; matchidx < num_match; ++matchidx) {
              int ml = match[matchidx].length;
              int offs = match[matchidx].offset;
              if (ml <= recent_best_length)
                break;
              best_length_so_far = std::max(best_length_so_far, ml);
              max_offset = std::max(max_offset, ml + pos);
              int bits_with_offs = total_bits + match_found_offset_bits[matchidx];

              int full_bits = bits_with_offs + Levi::BitsForToken(cost_model, ml, pos - lrl, Levi::kRecentOffsetCount, length_field);
              UpdateStatesZ<kOFFSET>(pos, full_bits, lrl, ml, offs, prev_state, S, src_ptr, offs, Z, cost_model, litindexes);
              if (ml > min_match_length && ml < length_long_enough_thres) {
                for (int tml = min_match_length; tml < ml; tml++)
                  UpdateStatesZ<kOFFSET>(pos, bits_with_offs + Levi::BitsForToken(cost_model, tml, pos - lrl, Levi::kRecentOffsetCount, length_field),
                                         lrl, tml, offs, prev_state, S, src_ptr, offs, Z, cost_model, litindexes);
              }
              // check if we have another recent0 match after 1-2 lits
              if (pos + ml + 4 < src_size - 16) {
                for (int num_lits_x = 1; num_lits_x <= 2; num_lits_x++) {
                  int tml = Levi::GetMatchlengthMin2(src_ptr_cur + ml + num_lits_x, offs, src_end_safe);
                  if (tml) {
                    int cost = full_bits +
                        Levi::BitsForLits(src_ptr, pos + ml, num_lits_x, offs, cost_model, 0) +
                        Levi::BitsForToken(cost_model, tml, pos + ml, 0, num_lits_x);
                    max_offset = std::max(max_offset, pos + ml + tml + num_lits_x);
                    UpdateState<kOFFSET>((pos + ml + tml + num_lits_x) * Z,
                                         cost, lrl, ml, offs, prev_state, num_lits_x | (tml << 8), S);
                    break;
                  }
                }
              }
            }
            // End of match loop
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
                Levi::BitsForLit(src_ptr, current_end + i - 1, recent_offs, cost_model, i - 1);
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
              bits += Levi::BitsForLits(src_ptr, final_offs, src_size - final_offs, S[final_offs].recent_offs[0], cost_model, 0);
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
                  bits += Levi::BitsForLits(src_ptr, final_offs, src_size - final_offs, S[Z * final_offs + idx].recent_offs[0], cost_model, litidx);
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
      Levi::State *state_cur = &S[last_state_index], *state_prev;
      while (outoffs != chunk_start) {
        uint32 qrm = state_cur->quick_recent_matchlen_litlen;
        if (qrm != 0) {
          outoffs = outoffs - (qrm >> 8) - (uint8)qrm;
          assert(num_tokens < tokens_capacity);
          tokens_begin[num_tokens].recent_offset0 = state_cur->recent_offs[0];
          tokens_begin[num_tokens].offset = 0;
          tokens_begin[num_tokens].matchlen = qrm >> 8;
          tokens_begin[num_tokens].litlen = (uint8)qrm;
          tokens_begin[num_tokens].litoffs = outoffs;
          num_tokens++;
        }
        outoffs = outoffs - state_cur->litlen - state_cur->matchlen;
        state_prev = &S[state_cur->prev_state];
        int recent0 = state_cur->recent_offs[0];
        int recent_index = Levi::GetRecentOffsetIndex(state_prev->recent_offs, recent0);
        assert(num_tokens < tokens_capacity);
        tokens_begin[num_tokens].recent_offset0 = state_prev->recent_offs[0];
        tokens_begin[num_tokens].litlen = state_cur->litlen;
        tokens_begin[num_tokens].matchlen = state_cur->matchlen;
        tokens_begin[num_tokens].offset = (recent_index >= 0) ? -recent_index : recent0;
        tokens_begin[num_tokens].litoffs = outoffs;
        num_tokens++;
        state_cur = state_prev;
      }

      std::reverse(tokens_begin, &tokens_begin[num_tokens]);

      memcpy(lz_token_array.data + lz_token_array.size, tokens_begin, sizeof(Levi::Token) * num_tokens);
      lz_token_array.size += num_tokens;

      if (reached_end)
        break;
      Levi::Stats::ReduceIfHigh(&stats);
      Levi::Stats::Update(&stats, src_ptr, chunk_start, tokens_begin, num_tokens);
      Levi::MakeCostModel(&stats, &cost_model);
      chunk_start = max_offset;
    }

    int num_tokens = lz_token_array.size;
    if (num_tokens == 0)
      break;

    memset(&stats, 0, sizeof(stats));

    // Account for all lz bytes
    Levi::Token *t = lz_token_array.data;
    for (int i = 0; i < num_tokens; i++, t++)
      Levi::LitStats::Update(&stats.lit, src_ptr, t->litoffs, t->litlen, t->recent_offset0, 1);

    // Account for final bytes
    if (src_size - final_lz_offset > 0)
      Levi::LitStats::Update(&stats.lit, src_ptr, final_lz_offset, src_size - final_lz_offset, last_recent0, 1);

    float cost = kInvalidCost;
    int n = Leviathan_EncodeLzArrays(lztemp, &cost, &stats.lit, &tmp_chunk_type,
                                     src_ptr, src_size, tmp_dst, tmp_dst_end,
                                     offset, lzcoder, last_recent0,
                                     &lz_token_array, initial_copy_bytes, &stats, min_match_offset);
    if (n < src_size) {
      Levi::SymStatsTwo *h2 = (Levi::SymStatsTwo *)lzcoder->lvsymstats_scratch.ptr;
      if (lzcoder->lvsymstats_scratch.size != sizeof(Levi::SymStatsTwo)) {
        h2 = (Levi::SymStatsTwo *)lzcoder->lvsymstats_scratch.Allocate(sizeof(Levi::SymStatsTwo));
        // oodle bug: does not memset this
        memset(h2, 0, sizeof(Levi::SymStatsTwo));
        h2->chunk_type[0] = -1;
        h2->chunk_type[1] = -1;
      }
      h2->stats[outer_loop_index] = stats;
      h2->chunk_type[outer_loop_index] = chunk_type;
    }
    if (cost < *cost_ptr) {
      *chunk_type_ptr = tmp_chunk_type;
      *cost_ptr = cost;
      memcpy(dst_ptr, S, n);
      return_value = n;
    }
  }

  return return_value;
}


static __forceinline void CheckRecentMatch(const uint8 *src, const uint8 *src_end, uint32 u32, LeviathanRecentOffs &r, int idx, int &best_ml, int &best_off) {
  int ml = GetMatchlengthQ(src, r.offs[8 + idx], src_end, u32);
  if (ml > best_ml)
    best_ml = ml, best_off = idx;
}

template<typename Hasher>
static inline LengthAndOffset LeviathanFast_GetMatch(const uint8 *cur_ptr, const uint8 *src_end_safe,
                                                     LeviathanRecentOffs &recent, Hasher *hasher,
                                                     int increment, int dict_size, int min_match_length) {
  uint32 hash_pos = cur_ptr - hasher->src_base_;
  uint32 u32_at_src = *(uint32*)cur_ptr;
  uint32 *hash_ptr = hasher->hashentry_ptr_next_;
  uint32 *hash2_ptr = hasher->hashentry2_ptr_next_;
  uint32 hash_hi = hasher->hashtable_high_bits_;
  uint32 hashval = hasher->MakeHashValue(hash_hi, hash_pos);
  hasher->SetHashPosPrefetch(cur_ptr + increment);

  int recent_ml = 0, recent_off = -1;
  CheckRecentMatch(cur_ptr, src_end_safe, u32_at_src, recent, 0, recent_ml, recent_off);
  CheckRecentMatch(cur_ptr, src_end_safe, u32_at_src, recent, 1, recent_ml, recent_off);
  CheckRecentMatch(cur_ptr, src_end_safe, u32_at_src, recent, 2, recent_ml, recent_off);
  CheckRecentMatch(cur_ptr, src_end_safe, u32_at_src, recent, 3, recent_ml, recent_off);
  CheckRecentMatch(cur_ptr, src_end_safe, u32_at_src, recent, 4, recent_ml, recent_off);
  CheckRecentMatch(cur_ptr, src_end_safe, u32_at_src, recent, 5, recent_ml, recent_off);
  CheckRecentMatch(cur_ptr, src_end_safe, u32_at_src, recent, 6, recent_ml, recent_off);

  int best_offs = 0, best_ml = 0;

  // If we found a recent offset at least 4 bytes long then use it.
  if (recent_ml >= 4) {
    hasher->Insert(hash_ptr, hash2_ptr, hashval);
    best_offs = -recent_off, best_ml = recent_ml;
  } else {

    uint32 *cur_hash_ptr = hash_ptr;
    for (;;) {
      for (size_t hashidx = 0; hashidx != Hasher::NumHash; hashidx++) {
        if ((cur_hash_ptr[hashidx] & 0xfc000000) == (hash_hi & 0xfc000000)) {
          int cur_offs = (hash_pos - cur_hash_ptr[hashidx]) & 0x3ffffff;
          if (cur_offs < dict_size) {
            cur_offs = std::max(cur_offs, 8);
            int cur_ml = GetMatchlengthQMin4(cur_ptr, cur_offs, src_end_safe, u32_at_src);
            if (cur_ml >= min_match_length && IsMatchLongEnough(cur_ml, cur_offs) && IsMatchBetter(cur_ml, cur_offs, best_ml, best_offs))
              best_offs = cur_offs, best_ml = cur_ml;
          }
        }
      }
      if (!Hasher::DualHash || cur_hash_ptr == hash2_ptr)
        break;
      cur_hash_ptr = hash2_ptr;
    }
    hasher->Insert(hash_ptr, hash2_ptr, hashval);
    if (!IsBetterThanRecent(recent_ml, best_ml, best_offs))
      best_offs = -recent_off, best_ml = recent_ml;
  }
  LengthAndOffset match = { best_ml, best_offs };
  return match;
}

template<int NumHash, bool DualHash, int NumLazy>
int LeviathanCompressFast(LzCoder *lzcoder, LzTemp *lztemp,
                           const uint8 *src, int src_len, uint8 *dst, uint8 *dst_end,
                           int start_pos, int *chunk_type_ptr, float *cost_ptr) {
  *chunk_type_ptr = -1;
  if (src_len <= 128)
    return src_len;
  const uint8 *src_end_safe = src + src_len - 8;

  int dict_size = lzcoder->opts->dictionarySize;
  dict_size = (dict_size <= 0) ? 0x40000000 : std::min(dict_size, 0x40000000);

  int min_match_length = std::max(lzcoder->opts->min_match_length, 4);
  int initial_copy_bytes = (start_pos == 0) ? 8 : 0;
  int cur_pos = initial_copy_bytes;

  LeviathanRecentOffs recent;
  Levi::TokenArray lz_token_array;
  lz_token_array.size = 0;
  lz_token_array.capacity = (src_len >> 1);
  lz_token_array.data = (Levi::Token*)lztemp->lztoken_scratch.Allocate(sizeof(Levi::Token) * (src_len >> 1));

  Levi::LitStats litstats = { 0 };
  int increment = 1, loops_since_match = 0;
  int lit_start = cur_pos;

  typedef MatchHasher<NumHash, DualHash> Hasher;

  Hasher *hasher = (MatchHasher<NumHash, DualHash> *)lzcoder->hasher;
  hasher->SetHashPos(src + cur_pos);

  while (cur_pos + increment < src_len - 16) {
    const uint8 *cur_ptr = src + cur_pos;

    LengthAndOffset m = LeviathanFast_GetMatch(cur_ptr, src_end_safe, recent, hasher, increment, dict_size, min_match_length);

    if (!m.length) {
      loops_since_match++;
      cur_pos += increment;
      if (NumHash == 1)
        increment = std::min((loops_since_match >> 5) + 1, 12);
      continue;
    }

    if (NumLazy >= 1) {
      while (cur_pos + 1 < src_len - 16) {
        LengthAndOffset m1 = LeviathanFast_GetMatch(cur_ptr + 1, src_end_safe, recent, hasher, 1, dict_size, min_match_length);
        if (m1.length && GetLazyScore(m1, m) > 0) {
          cur_pos++;  // lazy1 is better
          cur_ptr++;
          m = m1;
        } else {
          if (NumLazy < 2 || cur_pos + 2 >= src_len - 16 || m.length == 2)
            break;
          LengthAndOffset m2 = LeviathanFast_GetMatch(cur_ptr + 2, src_end_safe, recent, hasher, 1, dict_size, min_match_length);
          if (m2.length && GetLazyScore(m2, m) > 3) {
            cur_pos += 2;  // lazy2 is better
            cur_ptr += 2;
            m = m2;
          } else {
            break;
          }
        }
      }
    }
    int actual_offs = m.offset;
    if (m.offset <= 0) {
      // avoids coding a recent0 after a match - but that can never happen... so?
      if (m.offset == 0 && cur_pos == lit_start)
        m.offset = -1;
      actual_offs = recent.offs[-m.offset + 8];
    }
    // Reduce literal length and increase match length for bytes preceding the match.
    while (cur_pos > lit_start && cur_pos + start_pos >= actual_offs + 1 && cur_ptr[-1] == cur_ptr[-actual_offs - 1])
      cur_pos--, cur_ptr--, m.length++;

    Levi::Token &token = lz_token_array.data[lz_token_array.size++];
    token.litoffs = lit_start;
    token.offset = m.offset;
    token.recent_offset0 = recent.offs[8];
    token.litlen = cur_pos - lit_start;
    token.matchlen = m.length;

    Levi::LitStats::Update(&litstats, src, lit_start, cur_pos - lit_start, recent.offs[8], 1);
    recent.Insert(m.offset);
    hasher->InsertRange(cur_ptr, m.length);
    loops_since_match = 0;
    increment = 1;
    cur_pos += m.length;
    lit_start = cur_pos;
  }
  if (src_len - lit_start > 0)
    Levi::LitStats::Update(&litstats, src, lit_start, src_len - lit_start, recent.offs[8], 1);

  return Leviathan_EncodeLzArrays(lztemp, cost_ptr, &litstats, chunk_type_ptr,
                                src, src_len, dst, dst_end,
                                start_pos, lzcoder, recent.offs[8], &lz_token_array,
                                initial_copy_bytes, 0, 8);
}


void SetupEncoder_Leviathan(LzCoder *coder, int src_len, int level,
                            const CompressOptions *copts,
                            const uint8 *src_base, const uint8 *src_start) {
  assert(src_base && src_start);
  int hash_bits = GetHashBits(src_len, std::max(level, 2), copts, 16, 20, 17, 24);

  coder->codec_id = kCompressorLeviathan;
  coder->quantum_blocksize = 0x20000;
  coder->check_plain_huffman = true;
  coder->platforms = 0;
  coder->compression_level = level;
  coder->opts = copts;
  coder->speed_tradeoff = (copts->spaceSpeedTradeoffBytes * 0.00390625f) * 0.0024999999f;
  coder->entropy_opts = 0xff;
  coder->max_matches_to_consider = 4;
  coder->limit_local_dictsize = (level >= 6);
  coder->compressor_file_id = 12;
  if (level <= 3)
    coder->entropy_opts &= ~kEntropyOpt_MultiArrayAdvanced;
  if (level <= 2)
    coder->entropy_opts &= ~kEntropyOpt_MultiArray;
  coder->platforms = 0;

  if (level <= 1) {
    coder->entropy_opts &= ~kEntropyOpt_tANS;
    if (copts->hashBits <= 0)
      hash_bits = std::min(hash_bits, 19);
    CreateLzHasher< MatchHasher<1, false> >(coder, src_base, src_start, hash_bits);
  } else if (level == 2) {
    CreateLzHasher< MatchHasher<2, false> >(coder, src_base, src_start, hash_bits);
  } else if (level == 3) {
    CreateLzHasher< MatchHasher<4, false> >(coder, src_base, src_start, hash_bits);
  } else if (level == 4) {
    CreateLzHasher< MatchHasher<4, true> >(coder, src_base, src_start, hash_bits);
  }
}


int LeviathanDoCompress(LzCoder *coder, LzTemp *lztemp, MatchLenStorage *mls,
                        const uint8 *src, int src_size,
                        uint8 *dst, uint8 *dst_end,
                        int start_pos, int *chunk_type_ptr, float *cost_ptr) {

  int n = -1;
  if (coder->compression_level >= 5) {
    n = LeviathanOptimal(coder, lztemp, mls, src, src_size, dst, dst_end, start_pos, chunk_type_ptr, cost_ptr);
  } else if (coder->compression_level == 1) {
    n = LeviathanCompressFast<1, false, 0>(coder, lztemp, src, src_size, dst, dst_end, start_pos, chunk_type_ptr, cost_ptr);
  } else if (coder->compression_level == 2) {
    n = LeviathanCompressFast<2, false, 0>(coder, lztemp, src, src_size, dst, dst_end, start_pos, chunk_type_ptr, cost_ptr);
  } else if (coder->compression_level == 3) {
    n = LeviathanCompressFast<4, false, 1>(coder, lztemp, src, src_size, dst, dst_end, start_pos, chunk_type_ptr, cost_ptr);
  } else if (coder->compression_level == 4) {
    n = LeviathanCompressFast<4, true, 2>(coder, lztemp, src, src_size, dst, dst_end, start_pos, chunk_type_ptr, cost_ptr);
  } else {
    assert(0);
  }
  return n;
}
