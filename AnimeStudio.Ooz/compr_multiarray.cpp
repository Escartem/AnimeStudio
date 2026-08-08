// This file is not GPL. It may be used for educational purposes only.
#include "stdafx.h"
#include "compr_entropy.h"
#include "compr_util.h"
#include "qsort.h"
#include <algorithm>
#include <vector>
#include <string>

struct HistoAndCount {
  HistoU8 histo;
  int sum;
  int temp;
};

struct BitProfile {
  uint16 bits[256];
};


float GetTime_AdvMultiArray(int platforms, int a2, int a3) {
  return CombineCostComponents(
    platforms,
    a2 * 46.245f + a3 * 0.125f,
    a2 * 76.846f + a3 * 0.322f,
    a2 * 45.477f + a3 * 0.215f,
    a2 * 41.626f + a3 * 0.077f);
}

static void SubtractHisto(HistoU8 *dst, const HistoU8 &a, const HistoU8 &b) {
  for (size_t i = 0; i != 256; i++)
    dst->count[i] = a.count[i] - b.count[i];
}

// Compute bits in units of 1/256.
static void MakeHistoBitProfile(const HistoU8 &h, int histo_sum, BitProfile *histo_bits) {
  int factor = 0x40000000u / histo_sum;
  for (size_t i = 0; i != 256; i++)
    histo_bits->bits[i] = std::min(kMaxBitLength * 256u, kLog2LookupTable[factor * h.count[i] >> 17] >> 5);
}

static uint GetApproxHistoBitsFrac(const HistoU8 &h, int histo_sum) {
  int factor = 0x40000000u / histo_sum;
  uint sum = 0;
  for (size_t i = 0; i != 256; i++)
    sum += h.count[i] * std::min(kMaxBitLength * 256u, kLog2LookupTable[factor * h.count[i] >> 17] >> 5);
  return sum;
}

static uint GetApproxHistoBits(const HistoU8 &h, int histo_sum) {
  return GetApproxHistoBitsFrac(h, histo_sum) >> 8;
}

static void AddHistogram(HistoU8 *d, const HistoU8 &a, const HistoU8 &b) {
  for (size_t i = 0; i != 256; i++)
    d->count[i] = a.count[i] + b.count[i];
}

static void AddHistogram(HistoAndCount &dst, const HistoAndCount &src) {
  dst.sum += src.sum;
  AddHistogram(&dst.histo, dst.histo, src.histo);
}

static uint GetHistoBitUsageWithBitProfile(const HistoU8 &h, int histo_sum, const BitProfile *bits) {
  // optimize
  uint rv = 0;
  for (size_t i = 0; i != 256; i++)
    rv += h.count[i] * bits->bits[i];
  return rv;
}

static void ReduceNumHistograms(std::vector<HistoAndCount> *array, float A, float B, int max_count,
                                uint(*get_cost)(const HistoU8 &, int)) {
  struct Entry {
    float value;
    int cost_in_bits;
    int i_value, j_value;
    int i_count, j_count;

    bool operator<(const Entry &o) { return value < o.value; }
  };

  for (auto it = array->begin(); it != array->end(); ++it)
    it->temp = get_cost(it->histo, it->sum);

  int n = array->size();

  std::vector<Entry> ents;
  ents.resize(n * (n - 1) >> 1);

  Entry *bptr = ents.data(), *bfirst = bptr;

  HistoU8 temp_values;
  HistoAndCount *histos = array->data();

  for (int i = 0; i < n; i++) {
    for (int j = i + 1; j < n; j++) {
      const HistoAndCount &hi = histos[i];
      const HistoAndCount &hj = histos[j];
      AddHistogram(&temp_values, hi.histo, hj.histo);

      bptr->cost_in_bits = get_cost(temp_values, hi.sum + hj.sum);
      bptr->j_value = j;
      bptr->i_value = i;
      bptr->j_count = hj.sum;
      bptr->i_count = hi.sum;
      bptr->value = (hj.temp + hi.temp - bptr->cost_in_bits) * 0.125f + A;

      bptr++;
    }
  }
  MyMakeHeap(bfirst, bptr);

  while (bptr != bfirst) {
    Entry cur = *bfirst;

    MyPopHeap(bfirst, bptr--);

    HistoAndCount &hi = histos[cur.i_value];
    HistoAndCount &hj = histos[cur.j_value];

    if (hi.sum == cur.i_count && hj.sum == cur.j_count) {
      if (cur.value < B && n <= max_count)
        break;

      AddHistogram(hi, hj);
      hj.sum = 0;
      hj.temp = 0;
      hi.temp = cur.cost_in_bits;
      n--;
    } else {
      if (hi.sum && hj.sum) {
        AddHistogram(&temp_values, hi.histo, hj.histo);

        bptr->cost_in_bits = get_cost(temp_values, hi.sum + hj.sum);
        bptr->j_value = cur.j_value;
        bptr->i_value = cur.i_value;
        bptr->j_count = hj.sum;
        bptr->i_count = hi.sum;
        bptr->value = (hj.temp + hi.temp - bptr->cost_in_bits) * 0.125f + A;
        MyPushHeap(bfirst, ++bptr);
      }
    }
  }
  HistoAndCount *d = histos;
  for (auto &it : *array) {
    if (it.sum)
      *d++ = it;
  }
  array->resize(n);
}

#define LOAD__m128i(x) simde_mm_load_si128((const simde__m128i*)(x))

static void EncodeAdvMultiArray_BuildTable(uint64 *break_mask, uint8 *best_index_ptr,
                                const uint8 *ptr_cur, const uint8 *ptr_end,
                                uint16 *interval_scores, const uint16 *bits_for_sym,
                                int num_intervals_unused, int num_u16,
                                int prev_score_int, int cost_plus_4096_in) {
  simde__m128i cost_plus_4096 = simde_mm_set1_epi16(cost_plus_4096_in);
  simde__m128i prev_score = simde_mm_set1_epi16(prev_score_int), best_score;

  if (num_u16 > 8) {
    while (ptr_cur < ptr_end) {
      size_t i = 0;
      uint64 bits = 0;
      const uint16 *bsym = &bits_for_sym[num_u16 * *ptr_cur++];
      best_score = simde_mm_set1_epi16(0x7fff);
      do {
        simde__m128i scores_sub = simde_mm_sub_epi16(LOAD__m128i(&interval_scores[i]), prev_score);
        simde__m128i scores = simde_mm_add_epi16(LOAD__m128i(&bsym[i]), simde_mm_min_epi16(scores_sub, cost_plus_4096));
        // store it for temp so we can read it back in cmp below
        simde_mm_store_si128((simde__m128i*)&interval_scores[i], scores);
        best_score = simde_mm_min_epi16(best_score, scores);
        bits |= (uint64)simde_mm_movemask_epi8(simde_mm_packs_epi16(simde_mm_cmpgt_epi16(scores_sub, cost_plus_4096), simde_mm_set1_epi32(0))) << i;
      } while ((i += 8) < num_u16);
      // Duplicate the best score on all the lanes across
      best_score = simde_mm_min_epi16(best_score, simde_mm_shuffle_epi32(best_score, 0x4e));
      best_score = simde_mm_min_epi16(best_score, simde_mm_shuffle_epi32(best_score, 0xb1));
      best_score = simde_mm_min_epi16(best_score, simde_mm_shufflelo_epi16(simde_mm_shufflehi_epi16(best_score, 0xb1), 0xb1));
      // Figure out the index of the best score
      int mask;
      for (i = 0; (mask = simde_mm_movemask_epi8(simde_mm_cmpeq_epi16(LOAD__m128i(&interval_scores[i]), best_score))) == 0; i += 8) {}
      int best_index = (int)i + (BSF(mask) >> 1);
      *break_mask++ = bits;
      *best_index_ptr++ = best_index;
      prev_score = best_score;
    }
  } else {
    simde__m128i scores = LOAD__m128i(interval_scores);
    while (ptr_cur < ptr_end) {
      const uint16 *bsym = &bits_for_sym[8 * *ptr_cur++];
      simde__m128i scores_sub = simde_mm_sub_epi16(scores, prev_score);
      scores = simde_mm_add_epi16(LOAD__m128i(bsym), simde_mm_min_epi16(scores_sub, cost_plus_4096));
      uint64 bits = simde_mm_movemask_epi8(simde_mm_packs_epi16(simde_mm_cmpgt_epi16(scores_sub, cost_plus_4096), simde_mm_set1_epi32(0)));
      // Duplicate the best score on all the lanes across
      best_score = simde_mm_min_epi16(scores, simde_mm_shuffle_epi32(scores, 0x4e));
      best_score = simde_mm_min_epi16(best_score, simde_mm_shuffle_epi32(best_score, 0xb1));
      best_score = simde_mm_min_epi16(best_score, simde_mm_shufflelo_epi16(simde_mm_shufflehi_epi16(best_score, 0xb1), 0xb1));
      int best_index = BSF(simde_mm_movemask_epi8(simde_mm_cmpeq_epi16(scores, best_score))) >> 1;
      *break_mask++ = bits;
      *best_index_ptr++ = best_index;
      prev_score = best_score;
    }
  }
}

static uint32 GetMultiarrayLog2(uint32 v) {
  uint32 b = BSR(v);
  return ((b << 13) + GetLog2Interpolate(v << (32 - b))) >> 5;
}

static void HistoArr_RemoveAllWithEmptyCount(std::vector<HistoAndCount> *arr) {
  size_t n = arr->size(), m = n;
  HistoAndCount *hc = arr->data();
  while (n--) {
    if (hc[n].sum == 0)
      hc[n] = hc[--m];
  }
  arr->resize(m);
}

static void ReduceHistogramsAccurate(std::vector<HistoAndCount> *arr, float speed_tradeoff, int platforms, int max_count) {
  float value = GetTime_SingleHuffman(platforms, 0, 128) * speed_tradeoff;
  ReduceNumHistograms(arr, value, 0.0f, max_count, &GetHistoCostApprox);
}

static const uint8 *AdjustHistoWindow(HistoU8 &histo,
                                      const uint8 *data_cur, size_t size,
                                      const uint8 *data_start, const uint8 *data_end) {
  const uint8 *data_cur_end = data_cur + size;
  int move_left_score = (data_cur > data_start) ?
    histo.count[data_cur[-1]] - histo.count[data_cur_end[-1]] : -1;
  int move_right_score = (data_cur_end < data_end) ?
    histo.count[data_cur_end[0]] - histo.count[data_cur[0]] : -1;
  if (move_right_score >= move_left_score) {
    for (; data_cur_end < data_end &&
         histo.count[*data_cur_end] >= histo.count[*data_cur]; data_cur++, data_cur_end++) {
      histo.count[*data_cur]--;
      histo.count[*data_cur_end]++;
    }
  } else {
    for (; data_cur > data_start &&
         histo.count[data_cur[-1]] > histo.count[data_cur_end[-1]]; data_cur--, data_cur_end--) {
      histo.count[data_cur[-1]]++;
      histo.count[data_cur_end[-1]]--;
    }
  }
  return data_cur;
}

static int EncodeAdvMultiArray(uint8 *dst_in, uint8 *dst_end_in,
                               const uint8 **array_data, int *array_lens, size_t array_count,
                               int opts, float speed_tradeoff,
                               int platforms, float *cost_ptr, int level) {
  assert(array_count > 0);
  struct ArrRange {
    int char_idx;
    int char_count;
    int best_histo;
    ArrRange(int char_idx, int char_count, int histo) : char_idx(char_idx), char_count(char_count), best_histo(histo) {}
  };
  int total_input_bytes = 0, longest_input_array = 0;
  int max_arrs = (level == 9) ? 62 : std::max(std::min(1 << level, 32), 8);

  for (size_t i = 0; i != array_count; i++) {
    total_input_bytes += array_lens[i];
    longest_input_array = std::max<int>(longest_input_array, array_lens[i]);
  }

  if (total_input_bytes < 96 || longest_input_array < 32)
    return -1;

  int bytes_per_randregion = std::max(total_input_bytes / 100, 64);

  int num_randregions = array_count + 1 + total_input_bytes / bytes_per_randregion;

  // Compute approximate histograms for all the regions of the input
  std::vector<HistoAndCount> arr_histo;
  arr_histo.reserve(num_randregions);
  uint32 random_crap = 0xF309CC5E;  // needed to produce bit identical results
  for (size_t i = 0; i != array_count; i++) {
    int len = array_lens[i], bytes_left = len;
    const uint8 *data_start = array_data[i], *data_end = data_start + len, *data_cur = data_start;
    if (len < 16)
      continue;
    for (;;) {
      int num_bytes = (bytes_per_randregion >> 1) + ((uint64)random_crap * bytes_per_randregion >> 32);
      random_crap = 69069 * random_crap + 12345;
      num_bytes = std::min(num_bytes, (int)(data_end - data_cur));
      if (num_bytes < 16)
        break;
      arr_histo.emplace_back();
      HistoAndCount &back = arr_histo.back();

      // Make a histogram for a part of it
      int n = std::min(num_bytes, 256);
      back.sum = n;
      CountBytesHistoU8(data_cur, n, &back.histo);

      const uint8 *adjusted = AdjustHistoWindow(back.histo, data_cur, n, data_start, data_end);
      if (adjusted < data_cur)
        adjusted = data_cur;
      data_cur = adjusted + num_bytes;
    }
  }

  std::vector<BitProfile> arr_bitprofile;

  {
    std::vector<HistoAndCount> arr_histogood;
    arr_bitprofile.reserve(max_arrs + 1);

    // Create an initial dummy histogram
    arr_histogood.emplace_back();
    HistoAndCount &back = arr_histogood.back();
    for (size_t i = 0; i != 256; i++)
      back.histo.count[i] = 10;
    back.sum = 256 * 10;

    // Compute bit profile for the dummy histogram.
    arr_bitprofile.emplace_back();
    MakeHistoBitProfile(back.histo, back.sum, &arr_bitprofile.back());

    std::vector<int> arr_bit_usage(arr_histo.size());
    std::vector<int> arr_score(arr_histo.size(), 0x7fffffff);
    std::vector<int> arr_best_idx(arr_histo.size(), 0);

    for (size_t i = 0, i_end = arr_histo.size(); i != i_end; i++)
      arr_bit_usage[i] = GetApproxHistoBitsFrac(arr_histo[i].histo, arr_histo[i].sum);
    // ok here
    arr_histogood.reserve(2 * max_arrs);

    while (arr_histogood.size() < 2 * max_arrs) {
      int highest_cost_seen = 0;
      size_t worst_index = 0;

      for (size_t j = 0, j_end = arr_histo.size(); j != j_end; j++) {
        int bit_usage = GetHistoBitUsageWithBitProfile(arr_histo[j].histo, arr_histo[j].sum, &arr_bitprofile.back());
        bit_usage -= arr_bit_usage[j];
        if (bit_usage < arr_score[j]) {
          arr_score[j] = bit_usage;
          arr_best_idx[j] = arr_histogood.size() - 1;
        }
        if (arr_score[j] > highest_cost_seen) {
          highest_cost_seen = arr_score[j];
          worst_index = j;
        }
      }
      if (highest_cost_seen <= 256)
        break;
      arr_histogood.push_back(arr_histo[worst_index]);

      arr_bitprofile.emplace_back();
      MakeHistoBitProfile(arr_histogood.back().histo, arr_histogood.back().sum, &arr_bitprofile.back());

      // Remove from all the arrays
      arr_histo[worst_index] = arr_histo.back();
      arr_histo.pop_back();

      arr_bit_usage[worst_index] = arr_bit_usage.back();
      arr_bit_usage.pop_back();

      arr_score[worst_index] = arr_score.back();
      arr_score.pop_back();

      arr_best_idx[worst_index] = arr_best_idx.back();
      arr_best_idx.pop_back();
    }
    for (size_t j = 0, j_end = arr_histo.size(); j != j_end; j++)
      AddHistogram(arr_histogood[arr_best_idx[j]], arr_histo[j]);
    std::swap(arr_histo, arr_histogood);
  }

  if (!arr_histo.size())
    return -1;

  if (arr_histo.size() > max_arrs)
    ReduceNumHistograms(&arr_histo, 2.0f, 0.0f, max_arrs, &GetApproxHistoBits);

  if (arr_histo.size() <= 1)
    return -1;

  int base_cost = (int)(float)((float)((float)(speed_tradeoff * 55.0f) * 8.0f) * 256.0f);
  int cost_thres = base_cost + 4096;

  std::vector<uint64> arr_breaks;
  std::vector<uint8> arr_best_index;
  arr_breaks.reserve(longest_input_array);
  arr_best_index.reserve(longest_input_array);

  std::vector<std::vector<ArrRange> > picked_ranges;

  picked_ranges.resize(array_count);

  std::vector<uint16> arr_uint16;
  size_t bpf_size;

  for (int iteration = 0;; iteration++) {
    arr_bitprofile.resize(arr_histo.size());
    size_t k = arr_histo.size();
    while (k--) {
      MakeHistoBitProfile(arr_histo[k].histo, arr_histo[k].sum, &arr_bitprofile[k]);
      int frac_bits = GetHistoBitUsageWithBitProfile(arr_histo[k].histo, arr_histo[k].sum, &arr_bitprofile[k]);
      if (frac_bits >= 2035 * arr_histo[k].sum) {
        arr_bitprofile[k] = arr_bitprofile.back();
        arr_bitprofile.pop_back();
      }
    }

    if (arr_bitprofile.size() < 0x3f) {
      arr_bitprofile.emplace_back();
      BitProfile &bf = arr_bitprofile.back();
      for (size_t i = 0; i != 256; i++)
        bf.bits[i] = 2035;
    }

    bpf_size = arr_bitprofile.size();
    size_t bpf_size_rounded8 = (arr_bitprofile.size() + 7) & ~7;
    arr_uint16.resize(bpf_size_rounded8 * 256);

    uint16 *udat = arr_uint16.data();

    for (size_t i = 0; i != 256; i++) {
      for (size_t j = 0; j != bpf_size; j++)
        udat[i * bpf_size_rounded8 + j] = arr_bitprofile[j].bits[i];
      for (size_t j = bpf_size; j != bpf_size_rounded8; j++)
        udat[i * bpf_size_rounded8 + j] = 14 * 256;
    }
    arr_bitprofile.clear();
    arr_histo.resize(bpf_size);
    memset(arr_histo.data(), 0, sizeof(HistoAndCount) * bpf_size);
    int break_count = 0;

    for (size_t i = 0; i != array_count; i++) {
      int range_len = array_lens[i];
      const uint8 *range_ptr = array_data[i], *range_ptr_end = range_ptr + range_len;

      if (!range_len)
        continue;

      arr_breaks.resize(range_len);
      arr_best_index.resize(range_len);

      uint8 *arr_best_index_ptr = arr_best_index.data();
      uint64 *break_mask = arr_breaks.data();

      int best_index = -1;
      uint16 best_score = 0x7fff;
      uint16 interval_scores[128];

      unsigned c = *range_ptr;
      for (size_t j = 0; j != bpf_size; j++) {
        interval_scores[j] = udat[bpf_size_rounded8 * c + j];
        if (interval_scores[j] < best_score) {
          best_score = interval_scores[j];
          best_index = (int)j;
        }
      }
      for (size_t j = bpf_size; j != bpf_size_rounded8; j++)
        interval_scores[j] = 14 * 256;

      *break_mask = 0;
      *arr_best_index_ptr = best_index;

      EncodeAdvMultiArray_BuildTable(break_mask + 1, arr_best_index_ptr + 1, range_ptr + 1,
                                 range_ptr_end,
                                 interval_scores,
                                 udat,
                                 bpf_size,
                                 bpf_size_rounded8,
                                 best_score,
                                 cost_thres);

      break_count++;

      if (iteration == 2) {
        std::vector<ArrRange> &pr_cur = picked_ranges[i];

        int best_bitidx = arr_best_index_ptr[range_len - 1];
        uint64 mask = (uint64)1 << best_bitidx;
        int last_char_idx = range_len;

        for (int char_idx = range_len - 1; char_idx >= 0; char_idx--) {
          if (break_mask[char_idx] & mask) {
            pr_cur.emplace_back(char_idx, last_char_idx - char_idx, best_bitidx);
            last_char_idx = char_idx;

            best_bitidx = arr_best_index_ptr[char_idx - 1];
            mask = (uint64)1 << best_bitidx;
            break_count++;
          }
        }

        pr_cur.emplace_back(0, last_char_idx, best_bitidx);

        std::reverse(pr_cur.begin(), pr_cur.end());
      } else {
        int best_bitidx = arr_best_index_ptr[range_len - 1];
        uint64 mask = (uint64)1 << best_bitidx;
        for (int char_idx = range_len - 1; char_idx >= 0; char_idx--) {
          arr_histo[best_bitidx].histo.count[range_ptr[char_idx]]++;
          arr_histo[best_bitidx].sum++;
          if (break_mask[char_idx] & mask) {
            best_bitidx = arr_best_index_ptr[char_idx - 1];
            mask = (uint64)1 << best_bitidx;
            break_count++;
          }
        }
      }
    }
    if (iteration == 2)
      break;
    HistoArr_RemoveAllWithEmptyCount(&arr_histo);
    if (arr_histo.size() == 1)
      return -1;
    ReduceHistogramsAccurate(&arr_histo, speed_tradeoff, platforms, max_arrs);
    if (arr_histo.size() == 1)
      return -1;

    cost_thres = std::max<int>(base_cost + GetMultiarrayLog2(arr_histo.size() + 1) +
                               GetMultiarrayLog2((total_input_bytes + break_count - 1) / break_count + 1), 14 * 256);
  }

  struct IndexCount {
    int index;
    int count;
    bool operator<(const IndexCount &o) { return count > o.count; }
  };

  IndexCount index_count[63] = { 0 };
  for (size_t i = 0; i != bpf_size; i++)
    index_count[i].index = i;

  int total_indexes = 0;
  for (const auto &it : picked_ranges) {
    total_indexes += it.size();
    for (const auto &it2 : it) {
      index_count[it2.best_histo].count++;
    }
  }
  MySort(index_count, index_count + bpf_size);

  int ranks[64];
  for (size_t i = 0; i != bpf_size; i++)
    ranks[index_count[i].index] = i;

  while (bpf_size && index_count[bpf_size - 1].count == 0)
    bpf_size--;

  if (bpf_size <= 1)
    return -1;

  std::vector<uint8> temp_output;
  temp_output.resize(total_input_bytes + 32);

  uint8 *dst = temp_output.data();
  uint8 *dst_end = dst + temp_output.size();

  opts &= ~kEntropyOpt_MultiArray;

  *dst++ = (uint8)(bpf_size + 0x80);

  float cost_total = 1.0f;

  std::basic_string<uint8> tempstring;
  for (int i = 0; i != bpf_size; i++) {
    tempstring.clear();

    size_t j = 0;
    for (const auto &it : picked_ranges) {
      for (const auto &pear : it) {
        if (ranks[pear.best_histo] == i) {
          tempstring.append(array_data[j] + pear.char_idx, pear.char_count);
        }
      }
      j++;
    }

    float curcost = kInvalidCost;
    int outlen = EncodeArrayU8CompactHeader(
      dst,
      dst_end,
      tempstring.data(),
      tempstring.size(),
      opts,
      speed_tradeoff,
      platforms,
      &curcost,
      level,
      0);
    if (outlen < 0)
      return -1;
    dst += outlen;

    cost_total += curcost;
    if (cost_total >= *cost_ptr)
      return -1;
  }

  uint8 *varbits_len_ptr = dst;
  dst += 2;
  cost_total += 2.0f;

  if (6 * (array_count + total_indexes) >= 0xc000) {
    return -1;
  }

  std::vector<uint8> array_indexes_combined;
  std::vector<uint8> arr_interval_indexes;
  std::vector<uint8> arr_interval_lenlog2;
  int max_interval_lenlog2 = 0;
  array_indexes_combined.reserve(array_count + total_indexes);
  arr_interval_indexes.reserve(array_count + total_indexes);
  arr_interval_lenlog2.reserve(total_indexes);

  int total_lenlog2_bits = 0;

  for (int arri = 0; arri != array_count; arri++) {
    for (const auto &pr : picked_ranges[arri]) {
      int interval_index = ranks[pr.best_histo] + 1;
      arr_interval_indexes.push_back(interval_index);
      int interval_lenlog2 = BSR(pr.char_count);
      arr_interval_lenlog2.push_back(interval_lenlog2);
      total_lenlog2_bits += interval_lenlog2;
      max_interval_lenlog2 = std::max(max_interval_lenlog2, interval_lenlog2);
      array_indexes_combined.push_back(interval_index + interval_lenlog2 * 16);
    }
    arr_interval_indexes.push_back(0);
    array_indexes_combined.push_back(0);
  }

  uint8 *dst_base = dst;

  float curcost = kInvalidCost;
  int encsiz1 = EncodeArrayU8CompactHeader(
    dst, dst_end, arr_interval_indexes.data(), arr_interval_indexes.size(),
    opts, speed_tradeoff, platforms, &curcost,
    level, 0);
  if (encsiz1 < 0)
    return -1;
  dst += encsiz1;

  float curcost2 = kInvalidCost;
  int encsiz2 = EncodeArrayU8CompactHeader(
    dst, dst_end, arr_interval_lenlog2.data(), arr_interval_lenlog2.size(),
    opts, speed_tradeoff, platforms, &curcost2,
    level, 0);
  if (encsiz2 < 0)
    return -1;
  dst += encsiz2;

  bool uses_4bit_index = false;

  float curcost3 = curcost + curcost2;

  if (bpf_size <= 15 && max_interval_lenlog2 <= 15) {
    int encsiz3 = EncodeArrayU8CompactHeader(
      dst_base, dst_end, array_indexes_combined.data(), array_indexes_combined.size(),
      opts, speed_tradeoff, platforms, &curcost3,
      level, 0);
    if (encsiz3 >= 0) {
      dst = dst_base + encsiz3;
      uses_4bit_index = true;
    }
  }
  cost_total += curcost3;

  int length_of_varbits_in_bytes = 16 + BITSUP(total_lenlog2_bits);

  if (length_of_varbits_in_bytes + cost_total >= *cost_ptr || length_of_varbits_in_bytes > dst_end - dst)
    return -1;

  BitWriter64<1> bitsf(dst);
  BitWriter64<-1> bitsb(dst_end);

  int flag = 0;
  for (int arri = 0; arri != array_count; arri++) {
    for (const auto &pa : picked_ranges[arri]) {
      int interval_lenlog2 = BSR(pa.char_count);
      uint32 v = pa.char_count & ((1 << interval_lenlog2) - 1);
      if (!flag)
        bitsf.Write(v, interval_lenlog2);
      else
        bitsb.Write(v, interval_lenlog2);
      flag ^= 1;
    }
    if (uses_4bit_index)
      flag ^= 1;
  }

  size_t forward_bytes = bitsf.GetFinalPtr() - dst;
  size_t backward_bytes = dst_end - bitsb.GetFinalPtr();
  size_t all_bytes = forward_bytes + backward_bytes;

  memmove(dst + forward_bytes, dst_end - backward_bytes, backward_bytes);

  *(uint16*)varbits_len_ptr = (uint16)(all_bytes + uses_4bit_index * 0x8000);

  cost_total += GetTime_AdvMultiArray(platforms, total_indexes, total_input_bytes) * speed_tradeoff + all_bytes;
  if (cost_total >= *cost_ptr)
    return -1;

  size_t rv = dst - temp_output.data() + all_bytes;
  if (rv > dst_end_in - dst_in)
    return -1;

  *cost_ptr = cost_total;
  memcpy(dst_in, temp_output.data(), rv);

//  printf("Multiarray %d arrs with %d bytes became %d\n", (int)array_count, total_input_bytes, rv);
  return (int)rv;
}

static int EncodeSimpleMultiArray(uint8 *dst, uint8 *dst_end,
                                  const uint8 **array_data, int *array_lens, size_t array_count,
                                  int opts, float speed_tradeoff,
                                  int platforms, float *cost_ptr,
                                  int level) {
  float cost = 1.0f;
  uint8 *dst_org = dst;
  *dst++ = 0x80;
  for (int i = 0; i < array_count; i++) {
    float tmp_cost = kInvalidCost;
    int n = EncodeArrayU8CompactHeader(dst, dst_end, array_data[i], array_lens[i], opts, speed_tradeoff, platforms, &tmp_cost, level, NULL);
    if (n < 0)
      return -1;
    dst += n;
    cost += tmp_cost;
  }
  *cost_ptr = cost;
  return dst - dst_org;
}

int EncodeMultiArray(uint8 *dst, uint8 *dst_end, const uint8 **array_data, int *array_lens, int array_count, int opts, float speed_tradeoff, int platforms, float *cost_ptr, int level) {
  if (level < 8)
    opts &= ~kEntropyOpt_MultiArrayAdvanced;
  int n1 = EncodeSimpleMultiArray(dst, dst_end, array_data, array_lens, array_count, opts, speed_tradeoff, platforms, cost_ptr, level);
  int n2 = EncodeAdvMultiArray(dst, dst_end, array_data, array_lens, array_count, opts, speed_tradeoff, platforms, cost_ptr, level);
  return n2 >= 0 ? n2 : n1;
}

static int GetBetterHisto(const uint8 *src, int src_size, const HistoU8 &histo1, uint histo1_sum, const HistoU8 &histo2, uint histo2_sum) {
  // todo: optimize when src_size is big
  int factor1 = 0x40000000 / histo1_sum;
  int factor2 = 0x40000000 / histo2_sum;
  int sum = 0;
  for (int i = 0; i < src_size; i++) {
    sum += std::min(kMaxBitLength * 256u, kLog2LookupTable[factor1 * histo1.count[src[i]] >> 17] >> 5);
    sum -= std::min(kMaxBitLength * 256u, kLog2LookupTable[factor2 * histo2.count[src[i]] >> 17] >> 5);
  }
  return sum;
}

static void MoveBytesBetweenHistos(const uint8 *src, int src_size, HistoU8 *from, HistoU8 *to) {
  for (int i = 0; i < src_size; i++) {
    uint8 v = src[i];
    from->count[v]--;
    to->count[v]++;
  }
}

static void OptimizeSplitBoundaries(const uint8 *src, const uint8 *src_end, HistoU8 *histos, uint *sizes, uint *offsets, int num_arrs) {
  histos++;
  for (int i = 1; i < num_arrs; i++, histos++) {
    const uint8 *p = &src[offsets[i]];
    int64 score_cur = sizes[i - 1] ? (int64)sizes[i - 1] * histos->count[p[-1]] - (int64)sizes[i + 0] * histos[-1].count[p[-1]] : 0;
    int64 score_nxt = sizes[i + 0] ? (int64)sizes[i + 0] * histos[-1].count[p[0]] - (int64)sizes[i - 1] * histos->count[p[0]] : 0;
    if (score_cur > 0 && score_cur > score_nxt) {
      do {
        int v = *--p;
        histos[-1].count[v]--, histos->count[v]++;
        sizes[i - 1]--, sizes[i]++;
        offsets[i]--;
      } while (sizes[i - 1] && ((int64)sizes[i - 1] * histos->count[p[-1]] - (int64)sizes[i] * histos[-1].count[p[-1]]) > 0);
    } else if (score_nxt > 0) {
      do {
        int v = *p++;
        histos[-1].count[v]++, histos->count[v]--;
        sizes[i - 1]++, sizes[i]--;
        offsets[i]++;
      } while (sizes[i] && ((int64)sizes[i] * histos[-1].count[p[0]] - (int64)sizes[i - 1] * histos->count[p[0]]) > 0);
    }
  }
}

static int EncodeMultiArray_Short(uint8 *dst, uint8 *dst_end, const uint8 *src, int src_size, const HistoU8 &histo, int level, int opts, float speed_tradeoff, int platforms, float max_allowed_cost, float *cost_ptr) {
  float best_cost = GetCost_SingleHuffman(histo, src_size, speed_tradeoff, platforms);
  HistoU8 histol, histor;
  HistoU8 best_histo[2];
  int best_split = 0;
  int n_loops = std::max(std::min((src_size + 128) >> 8, 8), 1);
  for (int i = 0; i < n_loops; i++) {
    int splitpos = src_size * (i + 1) / (n_loops + 1);
    CountBytesHistoU8(src, splitpos, &histol);
    SubtractHisto(&histor, histo, histol);

    float cost = GetCost_SingleHuffman(histol, splitpos, speed_tradeoff, platforms) +
      GetCost_SingleHuffman(histor, src_size - splitpos, speed_tradeoff, platforms) + 6;
    if (cost < best_cost) {
      best_cost = cost;
      best_split = splitpos;
      best_histo[0] = histol;
      best_histo[1] = histor;
    }
  }
  if (!best_split)
    return -1;
  int finetune_size = 64;
  uint size_l = best_split, size_r = src_size - best_split;
  do {
    while (size_r >= 2 * finetune_size) {
      const uint8 *p = &src[size_l];
      if (GetBetterHisto(p, finetune_size, best_histo[0], size_l, best_histo[1], size_r) > 0)
        break;
      size_l += finetune_size, size_r -= finetune_size;
      MoveBytesBetweenHistos(p, finetune_size, &best_histo[1], &best_histo[0]);
    }

    while (size_l >= 2 * finetune_size) {
      const uint8 *p = &src[size_l - finetune_size];
      if (GetBetterHisto(p, finetune_size, best_histo[1], size_r, best_histo[0], size_l) > 0)
        break;
      size_l -= finetune_size, size_r += finetune_size;
      MoveBytesBetweenHistos(p, finetune_size, &best_histo[0], &best_histo[1]);
    }
    finetune_size >>= 2;
  } while (finetune_size >= 16);

  uint sizes[2] = { size_l, size_r };
  uint offsets[2] = { 0, size_l };
  OptimizeSplitBoundaries(src, src + src_size, &best_histo[0], sizes, offsets, 2);
  if (sizes[0] < 32 || sizes[1] < 32)
    return -1;

  uint8 *temp = new uint8[src_size];
  *temp = 2;
  opts &= ~kEntropyOpt_MultiArray;
  int result = -1;
  float cost1 = kInvalidCost;
  int n1 = EncodeArrayU8CompactHeader(temp + 1, &temp[src_size], src, sizes[0], opts, speed_tradeoff, platforms, &cost1, level, 0);
  if (n1 >= 0) {
    uint8 *tmp_dst = temp + 1 + n1;
    float cost2 = kInvalidCost;
    int n2 = EncodeArrayU8CompactHeader(tmp_dst, &temp[src_size], src + sizes[0], sizes[1], opts, speed_tradeoff, platforms, &cost2, level, 0);
    if (n2 >= 0) {
      float cost = cost1 + cost2 + 6;
      int total_bytes = tmp_dst + n2 - temp;
      if (cost < max_allowed_cost && total_bytes <= dst_end - dst) {
        result = total_bytes;
        *cost_ptr = cost;
        memcpy(dst, temp, total_bytes);
      }
    }
  }
  delete [] temp;

  return result;
}

struct MultiHistCandi {
  int savings;
  int idx;
  int extend_direction;
  uint64 size_and_offs;
  uint64 size_and_offs_other;

  bool operator<(const MultiHistCandi &o) { return savings < o.savings; }
};

static void MultiArrayAddCandidate(int idx, size_t array_count, HistoU8 *histo, uint *chunk_sizes, uint *chunk_offs,
                                   MultiHistCandi *mhs, int *num_mhs, const uint8 *src, int finetune_size, int direction)
{
  if (idx > 0) {
    int other_size = chunk_sizes[idx - 1];
    if (other_size >= 2 * finetune_size && direction <= 0) {
      int gain = GetBetterHisto(&src[chunk_offs[idx] - finetune_size], finetune_size, histo[idx], chunk_sizes[idx],
                                histo[idx - 1], other_size);
      if (gain < 0) {
        MultiHistCandi *m = &mhs[(*num_mhs)++];
        m->savings = -gain;
        m->extend_direction = 0;
        m->idx = idx;
        m->size_and_offs = chunk_sizes[idx] | (uint64)chunk_offs[idx] << 32;
        m->size_and_offs_other = chunk_sizes[idx - 1] | (uint64)chunk_offs[idx - 1] << 32;
      }
    }
  }
  if (idx < array_count - 1) {
    int other_size = chunk_sizes[idx + 1];
    if (other_size >= 2 * finetune_size && direction >= 0) {
      int gain = GetBetterHisto(&src[chunk_sizes[idx] + chunk_offs[idx]], finetune_size, histo[idx], chunk_sizes[idx],
                                histo[idx + 1], other_size);
      if (gain < 0) {
        MultiHistCandi *m = &mhs[(*num_mhs)++];
        m->savings = -gain;
        m->idx = idx;
        m->extend_direction = 1;
        m->size_and_offs = chunk_sizes[idx] | (uint64)chunk_offs[idx] << 32;
        m->size_and_offs_other = chunk_sizes[idx + 1] | (uint64)chunk_offs[idx + 1] << 32;
      }
    }
  }
}
bool OODLE_BUG = true;

static void MultiArrayDeleteJunk(MultiHistCandi *mhs, int *num_mhs, uint *chunk_size, uint *chunk_pos, int idx_bug) {
  for (int pos = *num_mhs; pos--; ) {
    MultiHistCandi &cur = mhs[pos];
    int idx = cur.idx, other = (OODLE_BUG ? idx_bug : idx) + (cur.extend_direction ? 1 : -1);
    if (cur.size_and_offs != (chunk_size[idx] | (uint64)chunk_pos[idx] << 32) ||
        cur.size_and_offs_other != (chunk_size[other] | (uint64)chunk_pos[other] << 32))
      cur = mhs[--*num_mhs];
  }
}

static int EncodeMultiArray_Long(uint8 *dst, uint8 *dst_end, const uint8 *src, int src_size, const HistoU8 &histo, int level, int opts, float speed_tradeoff, int platforms, float max_allowed_cost, float *cost_ptr) {
  int num_blks_base = (level == 9) ? 62 : std::max(std::min(1 << level, 32), 8);
  int num_blks = std::max(std::min(num_blks_base, (src_size + 256) >> 9), 3);
  HistoU8 *histos = new HistoU8[num_blks];

  int bytes_per_blk = src_size / num_blks;

  uint chunk_pos[64];
  uint chunk_size[64];

  for (int i = 0, pos = 0; i < num_blks; i++, pos += bytes_per_blk) {
    int size = (i == num_blks - 1) ? src_size - pos : bytes_per_blk;
    chunk_pos[i] = pos;
    chunk_size[i] = size;
    CountBytesHistoU8(src + pos, size, &histos[i]);
  }

  int mhs_capacity = num_blks * 3;
  MultiHistCandi *mhs = new MultiHistCandi[num_blks * 3];

  int tune = 64;
  do {
    int num_mhs = 0;
    for (int i = 0; i < num_blks; i++)
      MultiArrayAddCandidate(i, num_blks, histos, chunk_size, chunk_pos, mhs, &num_mhs, src, tune, 0);

    MyMakeHeap(mhs, mhs + num_mhs);

    int max_loops = num_blks * 2;

    while (num_mhs) {
      MultiHistCandi cur = *mhs;
      MyPopHeap(mhs, mhs + num_mhs--);

      int idx = cur.idx;
      uint pos = chunk_pos[idx];
      uint size = chunk_size[idx];
      int dir = cur.extend_direction ? 1 : -1;

      if (cur.size_and_offs != (size | (uint64)pos << 32) ||
          cur.size_and_offs_other != (chunk_size[idx + dir] | (uint64)chunk_pos[idx + dir] << 32))
        continue;
      if (cur.extend_direction) {
        MoveBytesBetweenHistos(&src[pos + size], tune, &histos[idx + 1], &histos[idx]);
        chunk_size[idx] += tune;
        chunk_size[idx + 1] -= tune;
        chunk_pos[idx + 1] += tune;
      } else {
        MoveBytesBetweenHistos(&src[pos - tune], tune, &histos[idx - 1], &histos[idx]);
        chunk_size[idx] += tune;
        chunk_pos[idx] -= tune;
        chunk_size[idx - 1] -= tune;
      }
      if (max_loops-- == 0)
        break;

      if (num_mhs + 4 >= mhs_capacity) {
        MultiArrayDeleteJunk(mhs, &num_mhs, chunk_size, chunk_pos, idx);
        MyMakeHeap(mhs, mhs + num_mhs);
      }
      int num_mhs_org = num_mhs;
      MultiArrayAddCandidate(idx, num_blks, histos, chunk_size, chunk_pos, mhs, &num_mhs, src, tune, 0);
      MultiArrayAddCandidate(idx + dir, num_blks, histos, chunk_size, chunk_pos, mhs, &num_mhs, src, tune, dir);
      while (num_mhs_org < num_mhs)
        MyPushHeap(mhs, mhs + ++num_mhs_org);
    }
    tune >>= 2;
  } while (tune >= 16);

  OptimizeSplitBoundaries(src, src + src_size, histos, chunk_size, chunk_pos, num_blks);

  HistoU8 tmp_hist;
  float scores[64];

  struct Entry {
    float diff;
    float combined_cost;
    int left, right;
  };

  Entry ents[63];

  Entry *ents_end = ents, *best_ent = NULL;
  float best_score = 0.0f;

  int prev_i = -1;
  for (int i = 0; i < num_blks; i++) {
    int size = chunk_size[i];
    if (!size)
      continue;

    scores[i] = GetCost_SingleHuffman(histos[i], size, speed_tradeoff, platforms);

    if (prev_i >= 0) {
      AddHistogram(&tmp_hist, histos[i], histos[prev_i]);
      float cost2 = GetCost_SingleHuffman(tmp_hist, size + chunk_size[prev_i], speed_tradeoff, platforms);
      float diff = scores[i] + scores[prev_i] - cost2;
      if (diff > 0.0f) {
        ents_end->diff = diff;
        ents_end->combined_cost = cost2;
        ents_end->left = prev_i;
        ents_end->right = i;
        if (diff > best_score) {
          best_score = diff;
          best_ent = ents_end;
        }
        ents_end++;
      }
    }
    prev_i = i;
  }

  while (ents_end != ents) {
    Entry e = *best_ent;
    *best_ent = *--ents_end;
    best_ent = NULL;
    best_score = 0;

    AddHistogram(&histos[e.left], histos[e.left], histos[e.right]);
//    printf("%d <= %d. new cost %f, diff %f\n", e.left, e.right, e.combined_cost, e.diff);
    chunk_size[e.left] += chunk_size[e.right];
    scores[e.left] = e.combined_cost;
    chunk_pos[e.right] = 0;
    chunk_size[e.right] = 0;
    scores[e.right] = 0;

    for (Entry *ep = ents_end; ep-- != ents; ) {
      if (ep->right == e.left || ep->left == e.right) {
        *ep = *--ents_end;
        if (best_ent == ents_end)
          best_ent = ep;
      } else if (ep->diff > best_score) {
        best_score = ep->diff;
        best_ent = ep;
      }
    }

    // Insert the newly made connections in the table
    int left = e.left;
    do left--; while (left >= 0 && chunk_size[left] == 0);
    if (left >= 0) {
      AddHistogram(&tmp_hist, histos[e.left], histos[left]);
      float cost2 = GetCost_SingleHuffman(tmp_hist, chunk_size[e.left] + chunk_size[left], speed_tradeoff, platforms);
      float diff = scores[e.left] + scores[left] - cost2;
      if (diff > 0.0f) {
        ents_end->diff = diff;
        ents_end->combined_cost = cost2;
        ents_end->left = left;
        ents_end->right = e.left;
        if (diff > best_score)
          best_score = diff, best_ent = ents_end;
        ents_end++;
      }
    }

    int right = e.right;
    do right++; while (right < num_blks && chunk_size[right] == 0);
    if (right < num_blks) {
      AddHistogram(&tmp_hist, histos[e.left], histos[right]);
      float cost2 = GetCost_SingleHuffman(tmp_hist, chunk_size[e.left] + chunk_size[right], speed_tradeoff, platforms);
      float diff = scores[e.left] + scores[right] - cost2;
      if (diff > 0.0f) {
        ents_end->diff = diff;
        ents_end->combined_cost = cost2;
        ents_end->left = e.left;
        ents_end->right = right;
        if (diff > best_score)
          best_score = diff, best_ent = ents_end;
        ents_end++;
      }
    }
  }

  // Remove all zero based entries
/*  int j = 0;
  for (int i = 0; i < num_blks; i++) {
    if (chunk_size[i] == 0)
      continue;
    if (i != j) {
      memcpy(&histos[j * 256], &histos[i * 256], 256 * sizeof(int));
      chunk_size[j] = chunk_size[i];
      chunk_pos[j] = chunk_pos[i];
    }
    j++;
  }*/

  int j = 0;
  for (int i = 0; i < num_blks; i++)
    j += (chunk_size[i] != 0);

  int retval = -1;
  if (j != 1) {
    uint8 *tmp_start = new uint8[src_size];
    uint8 *tmp_end = tmp_start + src_size;
    uint8 *tmp = tmp_start;

    *tmp++ = j;
    float cost = 6;
    for (int i = 0; i < num_blks; i++) {
      if (chunk_size[i]) {
        float curcost = kInvalidCost;
        int n = EncodeArrayU8CompactHeader(tmp, tmp_end, src + chunk_pos[i], chunk_size[i], opts & ~kEntropyOpt_MultiArray, speed_tradeoff, platforms, &curcost, level, 0);
        if (n < 0)
          goto getout;
        tmp += n;
        cost += curcost;
      }
    }
    if (cost < max_allowed_cost && tmp - tmp_start <= dst_end - dst) {
      *cost_ptr = cost;
      retval = tmp - tmp_start;
      memcpy(dst, tmp_start, retval);
    }
getout:
    delete[] tmp_start;
  }
  delete[] mhs;
  delete[] histos;
  return retval;
}

int EncodeArrayU8_MultiArray(uint8 *dst, uint8 *dst_end, const uint8 *src, int src_size, const HistoU8 &histo, int level, int opts, float speed_tradeoff, int platforms, float cost_thres, float *cost_ptr) {
  if (src_size < 96)
    return -1;
  int n;
  if (src_size < 1536)
    n = EncodeMultiArray_Short(dst, dst_end, src, src_size, histo, level, opts, speed_tradeoff, platforms, cost_thres, cost_ptr);
  else
    n = EncodeMultiArray_Long(dst, dst_end, src, src_size, histo, level, opts, speed_tradeoff, platforms, cost_thres, cost_ptr);
  if (opts & kEntropyOpt_MultiArrayAdvanced) {
    float cost = std::min(cost_thres, *cost_ptr) - 5;
    int nadv = EncodeAdvMultiArray(dst, dst_end, &src, &src_size, 1, opts, speed_tradeoff, platforms, &cost, level);
    if (nadv > 0) {
      *cost_ptr = cost + 5;
      return nadv;
    }
  }
  return n;
}

