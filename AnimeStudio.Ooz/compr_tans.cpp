// This file is not GPL. It may be used for educational purposes only.
#include "stdafx.h"
#include "compr_entropy.h"
#include "compr_util.h"
#include "qsort.h"
#include <algorithm>
#include <limits.h>

float GetTime_tANS(int platforms, int src_size, int used_syms, int tans_table_size) {
  return CombineCostComponents(
    platforms,
    642.078f + src_size * 3.175f + used_syms * 52.016f + tans_table_size * 1.895f,
    1073.963f + src_size * 2.963f + used_syms * 77.065f + tans_table_size * 1.695f,
    1313.768f + src_size * 3.951f + used_syms * 78.930f + tans_table_size * 4.139f,
    705.924f + src_size * 2.324f + used_syms * 49.328f + tans_table_size * 1.423f);
}


static uint GetBitsForArraysOfRice(const uint *arr, int arrsize, int k) {
  uint result = 0;
  for (int i = 0; i < arrsize; i++) {
    if (arr[i])
      result += arr[i] * (k + 1 + 2 * BSR((i >> k) + 1));
  }
  return result;
}

static void Tans_EncodeTable(BitWriter64<1> *bits_in, int L_bits, uint *lookup, int histo_size, int used_symbols) {
  BitWriter64<1> bits = *bits_in;

  if (used_symbols > 7) {
    bits.WriteNoFlush(1, 1);

    uint arr_z[128] = { 0 };
    int ranges[257], *range_cur = ranges;
    int arr_x[256];
    int arr_y[32];
    uint8 arr_w[256];
    uint8 sr_rice[256];
    uint8 sr_bits[256];
    uint8 sr_bitcount[256];

    int pos = 0;
    while (pos < histo_size && lookup[pos] == 0)
      pos++;
    *range_cur++ = pos;

    int average = 6, v, used_syms = 0, arr_x_count = 0, arr_y_count = 0;
    while (pos < histo_size) {

      int pos_start = pos;
      while (pos < histo_size && (v = lookup[pos]) != 0) {
        v--;
        int average_div4 = average >> 2;
        int limit = 2 * average_div4;
        int u = v > limit ? v : 2 * (v - average_div4) ^ ((v - average_div4) >> 31);
        arr_x[arr_x_count++] = u;
        if (u >= 0x80) {
          arr_y[arr_y_count++] = u;
        } else {
          arr_z[u]++;
        }
        if (v < limit)
          limit = v;
        pos++;
        used_syms++;
        average += limit - average_div4;
      }
      *range_cur++ = pos - pos_start;

      pos_start = pos;
      while (pos < histo_size && lookup[pos] == 0)
        pos++;
      *range_cur++ = pos - pos_start;
    }

    range_cur[-1] += 256 - pos;

    int best_score = INT_MAX;
    int Q = 0;
    for (int tq = 0; tq < 8; tq++) {
      int score = GetBitsForArraysOfRice(arr_z, 128, tq);
      for (int i = 0; i < arr_y_count; i++)
        score += tq + 2 * BSR((arr_y[i] >> tq) + 1) + 1;

      if (score < best_score) {
        best_score = score;
        Q = tq;
      }
    }

    int num_symrange = EncodeSymRange(sr_rice, sr_bits, sr_bitcount, used_syms, ranges, range_cur - ranges);
    bits.WriteNoFlush((used_syms - 1) + (Q << 8), 11);

    BitWriter64<1> bitst = bits;
    WriteNumSymRange(&bitst, num_symrange, used_syms);

    for (int i = 0; i < arr_x_count; i++) {
      uint x = arr_x[i] + (1 << Q);
      int nb = BSR(x >> Q);
      arr_w[i] = nb;
      arr_x[i] = x & ((1 << (Q + nb)) - 1);
    }

    WriteManyRiceCodes(&bitst, arr_w, arr_x_count);
    WriteManyRiceCodes(&bitst, sr_rice, num_symrange);
    WriteSymRangeLowBits(&bitst, sr_bits, sr_bitcount, num_symrange);
    bits = bitst;

    for (int i = 0; i < arr_x_count; i++) {
      if (Q + arr_w[i] != 0)
        bits.Write(arr_x[i], Q + arr_w[i]);
    }
  } else {
    bits.WriteNoFlush(0, 1);
    bits.WriteNoFlush(used_symbols - 2, 3);

    uint32 sympos[8], *sympos_end = sympos;
    for (int i = 0; i < histo_size; i++) {
      if (lookup[i])
        *sympos_end++ = i | (lookup[i] << 16);
    }
    SimpleSort(sympos, sympos_end);

    int delta_bits = 1;
    for (int i = 0, pos = 0; i < used_symbols - 1; i++) {
      int v = (sympos[i] >> 16);
      int nb = v - pos ? BSR(v - pos) + 1 : 0;
      delta_bits = std::max(delta_bits, nb);
      pos = v;
    }
    bits.WriteNoFlush(delta_bits, BSR(L_bits) + 1);

    for (int i = 0, pos = 0; i < used_symbols - 1; i++) {
      int v = (sympos[i] >> 16);
      bits.Write((v - pos) + ((uint8)sympos[i] << delta_bits), delta_bits + 8);
      pos = v;
    }
    bits.Write((uint8)sympos[used_symbols - 1], 8);
  }
  *bits_in = bits;
}

struct TansEntry {
  uint16 *next_state;
  uint16 thres;
  uint8 bits;
};

static float Tans_GetLogFactorUp(int value) {
  static const float kTansFactorUpTable[32] = {
    // ", ".join(['%.6f' % (math.log(1.0 + 1.0 / value)) for value in xrange(1, 32)])
    0.000000f, 0.693147f, 0.405465f, 0.287682f, 0.223144f, 0.182322f, 0.154151f, 0.133531f,
    0.117783f, 0.105361f, 0.095310f, 0.087011f, 0.080043f, 0.074108f, 0.068993f, 0.064539f,
    0.060625f, 0.057158f, 0.054067f, 0.051293f, 0.048790f, 0.046520f, 0.044452f, 0.042560f,
    0.040822f, 0.039221f, 0.037740f, 0.036368f, 0.035091f, 0.033902f, 0.032790f, 0.031749f
  };
  if (value >= 32)
    return (1.0f / value) - (1.0f / value) * (1.0f / value) * 0.5f;
  else
    return kTansFactorUpTable[value];
}

static float Tans_GetLogFactorDown(int value) {
  static const float kTansFactorDownTable[32] = {
    // ", ".join(['%.6f' % (math.log(1.0 - 1.0 / value)) for value in xrange(2, 32)])
    0.000000f, 0.000000f, -0.693147f, -0.405465f, -0.287682f, -0.223144f, -0.182322f, -0.154151f,
    -0.133531f, -0.117783f, -0.105361f, -0.095310f, -0.087011f, -0.080043f, -0.074108f, -0.068993f,
    -0.064539f, -0.060625f, -0.057158f, -0.054067f, -0.051293f, -0.048790f, -0.046520f, -0.044452f,
    -0.042560f, -0.040822f, -0.039221f, -0.037740f, -0.036368f, -0.035091f, -0.033902f, -0.032790f
  };
  if (value >= 32)
    return -(1.0f / value) - (1.0f / value) * (1.0f / value) * 0.5f;
  else
    return kTansFactorDownTable[value];
}

static uint DoubleToUintRoundPow2(double v) {
  uint u = (uint)v;
  return u + (v * v > (u * (u + 1)));
}

static int Tans_NormalizeCounts(uint *lookup, uint L, const HistoU8 &histo, int histo_sum, int num_syms) {
  int syms_used = 0;
  double multiplier = (double)L / (double)histo_sum;
  uint weight_sum = 0;
  for (int i = 0; i < num_syms; i++) {
    uint h = histo.count[i], u = 0;
    if (h) {
      u = DoubleToUintRoundPow2(h * multiplier);
      weight_sum += u;
      syms_used += 1;
    }
    lookup[i] = u;
  }
  if (weight_sum == L)
    return syms_used;

  struct Entry {
    int index;
    float score;

    bool operator<(const Entry &e) { return score < e.score; }
  };

  Entry heap[256], *heapcur = heap;
  int diff = L - weight_sum;

  if (diff < 0) {
    for (int i = 0; i < num_syms; i++) {
      if (lookup[i] > 1) {
        heapcur->index = i;
        heapcur->score = histo.count[i] * Tans_GetLogFactorDown(lookup[i]);
        heapcur++;
      }
    }
  } else {
    for (int i = 0; i < num_syms; i++) {
      if (histo.count[i]) {
        heapcur->index = i;
        heapcur->score = histo.count[i] * Tans_GetLogFactorUp(lookup[i]);
        heapcur++;
      }
    }
  }

  MyMakeHeap(heap, heapcur);

  if (diff < 0) {
    do {
      assert(heap != heapcur);
      uint index = heap->index;
      MyPopHeap(heap, heapcur--);
      if (--lookup[index] > 1) {
        heapcur->index = index;
        heapcur->score = histo.count[index] * Tans_GetLogFactorDown(lookup[index]);
        MyPushHeap(heap, ++heapcur);
      }
    } while (++diff);
  } else {
    do {
      assert(heap != heapcur);
      uint index = heap->index;
      MyPopHeap(heap, heapcur--);
      lookup[index]++;
      heapcur->index = index;
      heapcur->score = histo.count[index] * Tans_GetLogFactorUp(lookup[index]);
      MyPushHeap(heap, ++heapcur);
    } while (--diff);
  }
  return syms_used;
}

static void Tans_InitTable(TansEntry *te, uint16 *te_data, uint *weights, int weights_size, int L_bits) {
  uint L = 1 << L_bits;

  uint ones = 0;
  for (int i = 0; i < weights_size; i++)
    ones += weights[i] == 1;

  uint slots_left_to_alloc = L - ones;
  uint sa = slots_left_to_alloc >> 2;
  uint pointers[4];
  pointers[0] = 0;
  uint sb = sa + ((slots_left_to_alloc & 3) > 0);
  pointers[1] = sb;
  sb += sa + ((slots_left_to_alloc & 3) > 1);
  pointers[2] = sb;
  sb += sa + ((slots_left_to_alloc & 3) > 2);
  pointers[3] = sb;

  uint16 *ones_ptr = te_data + slots_left_to_alloc;
  int weights_sum = 0;

  for (int i = 0; i < weights_size; i++, te++) {
    uint w = weights[i];
    if (w) {
      if (w == 1) {
        te->bits = L_bits;
        te->thres = 2 * L;
        te->next_state = ones_ptr - 1;
        *ones_ptr = L + (ones_ptr - te_data);
        ones_ptr++;
      } else {
        int nb = BSR(w - 1) + 1;
        te->bits = L_bits - nb;
        te->thres = 2 * w << (L_bits - nb);
        uint16 *other_ptr = te_data + weights_sum;
        te->next_state = other_ptr - w;
        for (int j = 0; j < 4; j++) {
          int p = pointers[j];
          int Y = (w + ((weights_sum - j - 1) & 3)) >> 2;
          while (Y--)
            *other_ptr++ = p++ + L;
          pointers[j] = p;
        }
        weights_sum += w;
      }
    } else {
      te->next_state = NULL;
    }
  }
}

static inline void Tans_GetEncodedBitCount(TansEntry *te, const uint8 *src, int src_size, int L_bits, uint *forward_bits_ptr, uint *backward_bits_ptr) {
  uint L = 1 << L_bits;
  const uint8 *src_end = src + src_size - 5;
  uint state_0 = src_end[0] | L;
  uint state_1 = src_end[1] | L;
  uint state_2 = src_end[2] | L;
  uint state_3 = src_end[3] | L;
  uint state_4 = src_end[4] | L;
  uint forward_bits = 0, backward_bits = 0;
  uint nb;
  int rounds = (src_size - 5) / 10;
  TansEntry *t;
  src_end--;
#define TANS_COUNT_BITS(state, counter) do {   \
  t = &te[*src_end--];                    \
  nb = t->bits + (state >= t->thres);     \
  counter += nb;                          \
  state = t->next_state[state >> nb];     \
} while(0)
  switch ((src_size - 5) % 10) {
  case 9: TANS_COUNT_BITS(state_3, forward_bits);
  case 8: TANS_COUNT_BITS(state_2, forward_bits);
  case 7: TANS_COUNT_BITS(state_1, forward_bits);
  case 6: TANS_COUNT_BITS(state_0, forward_bits);
  case 5: TANS_COUNT_BITS(state_4, backward_bits);
  case 4: TANS_COUNT_BITS(state_3, backward_bits);
  case 3: TANS_COUNT_BITS(state_2, backward_bits);
  case 2: TANS_COUNT_BITS(state_1, backward_bits);
  case 1: TANS_COUNT_BITS(state_0, backward_bits);
  }
  while (rounds--) {
    TANS_COUNT_BITS(state_4, forward_bits);
    TANS_COUNT_BITS(state_3, forward_bits);
    TANS_COUNT_BITS(state_2, forward_bits);
    TANS_COUNT_BITS(state_1, forward_bits);
    TANS_COUNT_BITS(state_0, forward_bits);
    TANS_COUNT_BITS(state_4, backward_bits);
    TANS_COUNT_BITS(state_3, backward_bits);
    TANS_COUNT_BITS(state_2, backward_bits);
    TANS_COUNT_BITS(state_1, backward_bits);
    TANS_COUNT_BITS(state_0, backward_bits);
  }
#undef TANS_COUNT_BITS
  *forward_bits_ptr = forward_bits + 2 * L_bits;
  *backward_bits_ptr = backward_bits + 3 * L_bits;
}

static uint8 *Tans_EncodeBytes(uint8 *dst, uint8 *dst_end, TansEntry *te, const uint8 *src, int src_size, int L_bits, int forward_bits_pad, int backward_bits_pad) {
  BitWriter64<1> forward_bits(dst);
  BitWriter64<-1> backward_bits(dst_end);

  if (forward_bits_pad & 7)
    forward_bits.WriteNoFlush(0, 8 - (forward_bits_pad & 7));

  if (backward_bits_pad & 7)
    backward_bits.WriteNoFlush(0, 8 - (backward_bits_pad & 7));

  uint L = 1 << L_bits;
  const uint8 *src_end = src + src_size - 5;
  uint state_0 = src_end[0] | L;
  uint state_1 = src_end[1] | L;
  uint state_2 = src_end[2] | L;
  uint state_3 = src_end[3] | L;
  uint state_4 = src_end[4] | L;
  uint nb;
  int rounds = (src_size - 5) / 10;
  TansEntry *t;
  src_end--;
#define TANS_ENCODE(state, bitwr) do {                 \
  t = &te[*src_end--];                                 \
  nb = t->bits + (state >= t->thres);                  \
  bitwr.WriteNoFlush(state & ((1 << nb) - 1), nb);     \
  state = t->next_state[state >> nb];                  \
} while(0)
  switch ((src_size - 5) % 10) {
  case 9: TANS_ENCODE(state_3, forward_bits);
  case 8: TANS_ENCODE(state_2, forward_bits);
  case 7: TANS_ENCODE(state_1, forward_bits);
  case 6: TANS_ENCODE(state_0, forward_bits);
  case 5: TANS_ENCODE(state_4, backward_bits);
  case 4: TANS_ENCODE(state_3, backward_bits);
  case 3: TANS_ENCODE(state_2, backward_bits);
  case 2: TANS_ENCODE(state_1, backward_bits);
  case 1: TANS_ENCODE(state_0, backward_bits);
    backward_bits.Flush();
    forward_bits.Flush();
  }
  while (rounds--) {
    TANS_ENCODE(state_4, forward_bits);
    TANS_ENCODE(state_3, forward_bits);
    TANS_ENCODE(state_2, forward_bits);
    TANS_ENCODE(state_1, forward_bits);
    TANS_ENCODE(state_0, forward_bits);
    TANS_ENCODE(state_4, backward_bits);
    TANS_ENCODE(state_3, backward_bits);
    TANS_ENCODE(state_2, backward_bits);
    TANS_ENCODE(state_1, backward_bits);
    TANS_ENCODE(state_0, backward_bits);
    backward_bits.Flush();
    forward_bits.Flush();
  }

  backward_bits.WriteNoFlush(state_4 & (L - 1), L_bits);
  backward_bits.WriteNoFlush(state_2 & (L - 1), L_bits);
  backward_bits.WriteNoFlush(state_0 & (L - 1), L_bits);
  forward_bits.WriteNoFlush(state_3 & (L - 1), L_bits);
  forward_bits.WriteNoFlush(state_1 & (L - 1), L_bits);
  backward_bits.Flush();
  forward_bits.Flush();

#undef TANS_ENCODE
  assert(backward_bits.pos_ == 63);
  assert(forward_bits.pos_ == 63);
  // It will be decoded in the backwards direction,
  // so swap the order of the two buffers.
  // We've written it as FORWARD....BACKWARD but it needs
  // to be saved as BACKWARD....FORWARD.

  size_t forward_bytes = forward_bits.ptr_ - dst;
  size_t backward_bytes = dst_end - backward_bits.ptr_;

  uint8 *temp = new uint8[backward_bytes];
  memcpy(temp, backward_bits.ptr_, backward_bytes);
  memmove(dst + backward_bytes, dst, forward_bytes);
  memcpy(dst, temp, backward_bytes);
  delete[] temp;

  return dst + forward_bytes + backward_bytes;
}

int EncodeArrayU8_tANS(uint8 *dst, uint8 *dst_end, const uint8 *src, int src_size, const HistoU8 &histo, float speed_tradeoff, int platforms, float *cost_ptr) {
  if (src_size < 32)
    return -1;

  const uint8 *src_end = src + src_size - 5;

  HistoU8 *histo_mod = (HistoU8*)&histo;

  histo_mod->count[src_end[0]]--;
  histo_mod->count[src_end[1]]--;
  histo_mod->count[src_end[2]]--;
  histo_mod->count[src_end[3]]--;
  histo_mod->count[src_end[4]]--;

  int L_bits = std::max(std::min(ilog2round(src_size - 5) - 2, 11), 8);

  int weights_size = 256;
  uint weights[256];
  while (weights_size && histo.count[weights_size - 1] == 0)
    weights_size--;

  int used_symbols = Tans_NormalizeCounts(weights, 1 << L_bits, histo, src_size - 5, weights_size);
  histo_mod->count[src_end[0]]++;
  histo_mod->count[src_end[1]]++;
  histo_mod->count[src_end[2]]++;
  histo_mod->count[src_end[3]]++;
  histo_mod->count[src_end[4]]++;

  if (used_symbols <= 1)
    return -1;

  float cost = GetTime_tANS(platforms, src_size - 5, used_symbols, 1 << L_bits) * speed_tradeoff + 5;

  int cost_left = (*cost_ptr - cost);
  if (cost_left < 4)
    return -1;
  memset(&weights[weights_size], 0, sizeof(uint) * (256 - weights_size));

  uint8 table[512];
  BitWriter64<1> bits(table);
  bits.WriteNoFlush(L_bits - 8, 3);
  Tans_EncodeTable(&bits, L_bits, weights, weights_size, used_symbols);
  int table_size = bits.GetFinalPtr() - table;

  // Perform an inexact computation of the optimal value we may achieve to
  // see if we can fit within the limit.
  uint64 approx_bits_frac = 0;
  for (int i = 0; i < weights_size; i++) {
    if (weights[i])
      approx_bits_frac += (uint64)kLog2LookupTable[weights[i] << (13 - L_bits)] * histo.count[i];
  }
  if (table_size + (int)(approx_bits_frac >> 16) >= cost_left)
    return -1;

  int q = 0;
  for (int i = 0; i < weights_size; i++)
    q += weights[i];

  TansEntry te[256];
  uint16 te_data[1 << 11];
  Tans_InitTable(te, te_data, weights, weights_size, L_bits);

  uint forward_bits, backward_bits;
  Tans_GetEncodedBitCount(te, src, src_size, L_bits, &forward_bits, &backward_bits);

  int total_size = table_size + BITSUP(forward_bits) + BITSUP(backward_bits);
  if (total_size >= cost_left || total_size + cost >= *cost_ptr)
    return -1;
  if (total_size + 8 > dst_end - dst)
    return -1;

  *cost_ptr = cost + total_size;

  memcpy(dst, table, table_size);
  return Tans_EncodeBytes(dst + table_size, dst_end, te, src, src_size, L_bits, forward_bits, backward_bits) - dst;
}
