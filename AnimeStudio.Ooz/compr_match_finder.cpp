// This file is not GPL. It may be used for educational purposes only.
#include "stdafx.h"
#include "compr_match_finder.h"
#include <algorithm>
#include <vector>
#include "compress.h"
#include "compr_util.h"
#include "compr_entropy.h"
#include "qsort.h"
#include "match_hasher.h"

// https://db.in.tum.de/~leis/papers/ART.pdf

struct TrieNodeBig {
  int src_offs;
  int parent;
  int follow_node;
  int length_compress;
  uint8 num_children;
};

struct TrieNodeBig_Size2 {
  TrieNodeBig hdr;
  uint8 symbol[2];
  int next[2];
};

struct TrieNodeBig_Size8 {
  TrieNodeBig hdr;
  uint8 symbol[8];
  int next[8];
};

struct TrieNodeBig_Size16 {
  TrieNodeBig hdr;
  uint8 symbol[16];
  int next[16];
};

struct TrieNodeBig_Size48 {
  TrieNodeBig hdr;
  uint8 symbol[256];
  int next[48];
};

struct TrieNodeBig_Size256 {
  TrieNodeBig hdr;
  int next[256];
};

static uint8 *VarLenWriteSpill(uint8 *dst, unsigned int value, int A) {
  unsigned shifted = 1 << A;
  unsigned thres = 256 - shifted;
  while (value >= thres) {
    value -= thres;
    *dst++ = value & (shifted - 1);
    value >>= A;
  }
  *dst++ = (uint8)(value + shifted);
  return dst;
}

static uint8 *VarLenWriteOffset(uint8 *dst, unsigned int value, int A, int B) {
  unsigned shifted = 1 << A;
  unsigned thres = 65536 - shifted;
  if (value >= thres) {
    unsigned v = (value - thres) & (shifted - 1);
    *dst++ = (uint8)(v >> 8);
    *dst++ = (uint8)(v);
    return VarLenWriteSpill(dst, (value - thres) >> A, B);
  } else {
    unsigned v = value + shifted;
    *dst++ = (uint8)(v >> 8);
    *dst++ = (uint8)(v);
    return dst;
  }
}

static uint8 *VarLenWriteLength(uint8 *dst, unsigned int value, int A, int B) {
  unsigned shifted = 1 << A;
  unsigned thres = 256 - shifted;
  if (value >= thres) {
    unsigned v = (value - thres) & (shifted - 1);
    *dst++ = (uint8)(v);
    return VarLenWriteSpill(dst, (value - thres) >> A, B);
  } else {
    unsigned v = value + shifted;
    *dst++ = (uint8)(v);
    return dst;
  }
}

MatchLenStorage *MatchLenStorage::Create(int entries, float avg_bytes) {
  MatchLenStorage *mls = new MatchLenStorage;
  mls->byte_buffer.resize((int)(entries * avg_bytes));
  mls->offset2pos.resize(entries);
  mls->byte_buffer_use = 1;
  mls->window_base = NULL;
  return mls;
}

void MatchLenStorage::Destroy(MatchLenStorage *mls) {
  delete mls;
}

void MatchLenStorage_InsertMatches(MatchLenStorage *mls, int at_offset, LengthAndOffset *lao, int num_lao) {
  if (!num_lao)
    return;
  mls->offset2pos[at_offset] = mls->byte_buffer_use;

  int needed_bytes = mls->byte_buffer_use + 16 * num_lao + 2;
  if (needed_bytes >= mls->byte_buffer.size())
    mls->byte_buffer.resize(needed_bytes);

  uint8 *cur_ptr = mls->byte_buffer.data() + mls->byte_buffer_use;

  for (int i = 0; i < num_lao && lao[i].length; i++) {
    assert(lao[i].offset != 0);
    cur_ptr = VarLenWriteLength(cur_ptr, lao[i].length, 1, 3);
    cur_ptr = VarLenWriteOffset(cur_ptr, lao[i].offset, 13, 7);
  }
  cur_ptr = VarLenWriteLength(cur_ptr, 0, 1, 3);

  mls->byte_buffer_use = cur_ptr - mls->byte_buffer.data();
  /*  if (at_offset >= old_offs) {
      printf("Literal: %.*s\n", at_offset - old_offs, text + old_offs);
      old_offs = at_offset;
    }
    for (int i = 0; i < num_lao; i++) {
      printf("Copy %d chars from %d: %.*s\n", lao[i].length, lao[i].offset, lao[i].length, text + at_offset - lao[i].offset);
    }*/
}

bool TrieFindOrInsert(TrieNodeBig **node_ptr, int cur_offs, uint8 symbol, int **offs_pptr) {
  TrieNodeBig *node = *node_ptr;
  int children = node->num_children + 1;

  if (children <= 2) {
    TrieNodeBig_Size2 *cur = (TrieNodeBig_Size2 *)node;
    for (int i = 0; i < children; i++) {
      if (cur->symbol[i] == symbol) {
        *offs_pptr = &cur->next[i];
        return true;
      }
    }
    if (children < 2) {
      cur->symbol[children] = symbol;
      cur->next[children] = -cur_offs;
      cur->hdr.num_children++;
      return false;
    }
    // It's full grow to a bigger size.
    TrieNodeBig_Size8 *nn = new TrieNodeBig_Size8;
    nn->hdr = cur->hdr;
    memcpy(nn->symbol, cur->symbol, 2 * sizeof(nn->symbol[0]));
    memcpy(nn->next, cur->next, 2 * sizeof(nn->next[0]));
    nn->symbol[2] = symbol;
    nn->next[2] = -cur_offs;
    nn->hdr.num_children++;
    *node_ptr = &nn->hdr;
    delete cur;
    return false;
  } else if (children <= 8) {
    TrieNodeBig_Size8 *cur = (TrieNodeBig_Size8 *)node;
    for (int i = 0; i < children; i++) {
      if (cur->symbol[i] == symbol) {
        *offs_pptr = &cur->next[i];
        return true;
      }
    }
    if (children < 8) {
      cur->symbol[children] = symbol;
      cur->next[children] = -cur_offs;
      cur->hdr.num_children++;
      return false;
    }
    // It's full grow to a bigger size.
    TrieNodeBig_Size16 *nn = new TrieNodeBig_Size16;
    nn->hdr = cur->hdr;
    memcpy(nn->symbol, cur->symbol, 8 * sizeof(nn->symbol[0]));
    memcpy(nn->next, cur->next, 8 * sizeof(nn->next[0]));
    nn->symbol[8] = symbol;
    nn->next[8] = -cur_offs;
    nn->hdr.num_children++;
    *node_ptr = &nn->hdr;
    delete cur;
    return false;
  } else if (children <= 16) {
    TrieNodeBig_Size16 *cur = (TrieNodeBig_Size16 *)node;
    for (int i = 0; i < children; i++) {
      if (cur->symbol[i] == symbol) {
        *offs_pptr = &cur->next[i];
        return true;
      }
    }
    if (children < 16) {
      cur->symbol[children] = symbol;
      cur->next[children] = -cur_offs;
      cur->hdr.num_children++;
      return false;
    }
    // It's full, grow to bigger size.
    TrieNodeBig_Size48 *nn = new TrieNodeBig_Size48;
    nn->hdr = cur->hdr;
    memset(nn->symbol, 0, sizeof(nn->symbol));
    nn->symbol[symbol] = 1;
    nn->next[0] = -cur_offs;
    for (size_t i = 0; i != 16; i++) {
      nn->symbol[cur->symbol[i]] = (uint8)(i + 2);
      nn->next[i + 1] = cur->next[i];
    }
    nn->hdr.num_children++;
    *node_ptr = &nn->hdr;
    delete cur;
    return false;
  } else if (children <= 48) {
    TrieNodeBig_Size48 *cur = (TrieNodeBig_Size48 *)node;
    if (cur->symbol[symbol]) {
      *offs_pptr = &cur->next[cur->symbol[symbol] - 1];
      return true;
    }
    if (children < 48) {
      cur->symbol[symbol] = children + 1;
      cur->next[children] = -cur_offs;
      cur->hdr.num_children++;
      return false;
    }
    // It's full, grow to bigger size.
    TrieNodeBig_Size256 *nn = new TrieNodeBig_Size256;
    memset(nn->next, 0, sizeof(nn->next));
    nn->hdr = cur->hdr;
    for (size_t i = 0; i != 256; i++) {
      if (cur->symbol[i])
        nn->next[i] = cur->next[cur->symbol[i] - 1];
    }
    nn->next[symbol] = -cur_offs;
    nn->hdr.num_children++;
    *node_ptr = &nn->hdr;
    delete cur;
    return false;
  } else {
    TrieNodeBig_Size256 *cur = (TrieNodeBig_Size256 *)node;
    if (cur->next[symbol]) {
      *offs_pptr = &cur->next[symbol];
      return true;
    }
    cur->next[symbol] = -cur_offs;
    cur->hdr.num_children++;
    return false;
  }
}

void FindMatchesSuffixTrie(uint8 *src_in, int src_size, MatchLenStorage *mls, int max_matches_to_consider, int src_offset_start, LRMTable *lrm) {
  LengthAndOffset *lao_arr = new LengthAndOffset[max_matches_to_consider + 1];
  TrieNodeBig **nodes_arr = new TrieNodeBig*[src_size + 2];
  memset(nodes_arr, 0, (src_size + 2) * sizeof(TrieNodeBig *));
  int *node_lut = new int[0x1000000];
  memset(node_lut, 0, 0x1000000 * sizeof(int));

  TrieNodeBig *cur_tn;

  int last_matchlen = 0;
  int cur_offs_end = src_size - 3;
  int parent_node_idx = 0;
  int lut_value = 0;

  LRMScannerEx lrmscanner;
  LengthAndOffset lao;

  LRMScannerEx_Setup(&lrmscanner, lrm, src_in + src_offset_start, src_in + src_size, INT_MAX);

  for (int cur_offs = 1; cur_offs < cur_offs_end; cur_offs++) {
    uint8 *src_ptr_cur = &src_in[cur_offs - 1], *src_ptr_match;

    int cur_last_matchlen = std::max(last_matchlen - 1, 0);
    int prev_lut_value = 0;
    int idx_a = 0, idx_b = 0;
    int *cur_lut_ptr;

    int parent_len = INT_MAX;
    if (parent_node_idx) {
      int tn = nodes_arr[parent_node_idx]->follow_node;
      parent_len = tn ? nodes_arr[tn]->length_compress : 0;
    }

    if (lut_value > 0) {
      last_matchlen = nodes_arr[lut_value]->length_compress;
    } else {
      cur_lut_ptr = &node_lut[src_ptr_cur[2] | src_ptr_cur[1] << 8 | src_ptr_cur[0] << 16];
      lut_value = *cur_lut_ptr;
      if (!lut_value) {
        last_matchlen = 0;
        *cur_lut_ptr = -cur_offs;
        if (cur_offs >= src_offset_start + 1) {
          lao.length = LRMScannerEx_FindMatch(&lrmscanner, src_ptr_cur, src_in + src_size, &lao.offset);
          if (lao.length > 0)
            MatchLenStorage_InsertMatches(mls, cur_offs - src_offset_start - 1, &lao, 1);
        }
        continue;
      }
      last_matchlen = 3;
      goto COPY_STUFF_X;
    } // if

    for (;;) {
      if (last_matchlen > parent_len) {
        TrieNodeBig *pn = nodes_arr[parent_node_idx];
        if (last_matchlen < pn->length_compress) {
          parent_len = last_matchlen;
          pn->follow_node = lut_value;
        } else {
          parent_len = INT_MAX;
        }
      }
      if (last_matchlen >= src_size - (cur_offs - 1))
        goto FINAL_STUFF;

      if (nodes_arr[lut_value]->follow_node)
        idx_a = lut_value;
      else
        idx_b = lut_value;

      uint8 cur_symbol;
      cur_symbol = src_ptr_cur[last_matchlen];
      if (!TrieFindOrInsert(&nodes_arr[lut_value], cur_offs, cur_symbol, &cur_lut_ptr)) {
        // The symbol was not found in the trie but has now been inserted.
        prev_lut_value = lut_value;
        parent_node_idx = idx_b > 0 ? idx_b : idx_a;
        src_ptr_match = src_in + nodes_arr[lut_value]->src_offs - 1;
        goto AFTER_NODE_INSERTED;
      }
      // FOUND_EXISTING_SYMBOL_SLOT
      last_matchlen++;

      prev_lut_value = lut_value;

      lut_value = *cur_lut_ptr;

COPY_STUFF_X:
      if (lut_value < 0) {
        *cur_lut_ptr = cur_offs;

        last_matchlen = std::max(cur_last_matchlen, last_matchlen);
        src_ptr_match = &src_in[-lut_value - 1];
        uint8 *pa, *pb;
        pa = &src_ptr_match[last_matchlen];
        pb = &src_ptr_cur[last_matchlen];
        for (;;) {
          if (last_matchlen == src_size - (cur_offs - 1))
            break;
          // insert a new node of size 2 to differentiate them.
          if (*pa != *pb)
            goto SPLIT_NODE;
          pa++, pb++;
          last_matchlen++;
        }
FINAL_STUFF:;
        src_ptr_match = (lut_value >= 0) ? src_in + nodes_arr[lut_value]->src_offs - 1 : src_in - lut_value - 1;
        int final_offset = src_ptr_cur - src_ptr_match;
        for (; cur_offs < src_size + 1; cur_offs += 1, src_ptr_cur += 1) {
          if (cur_offs >= src_offset_start + 1 && src_size - (cur_offs - 1) >= 2) {
            lao_arr->length = src_size - (cur_offs - 1);
            lao_arr->offset = final_offset;
            MatchLenStorage_InsertMatches(mls, cur_offs - src_offset_start - 1, lao_arr, 1);
          }
        }
        goto GETOUT;
      }

      // Trie node for this character was found. Compare 
      // the prefix up to the switch position to see if it matches.
      cur_tn = nodes_arr[lut_value];
      last_matchlen = std::max(cur_last_matchlen, last_matchlen);
      src_ptr_match = src_in + cur_tn->src_offs - 1;
      uint8 *pa, *pb;
      pa = &src_ptr_match[last_matchlen];
      pb = &src_ptr_cur[last_matchlen];
      while (last_matchlen < cur_tn->length_compress) {
        if (last_matchlen == src_size - (cur_offs - 1))
          goto FINAL_STUFF;
        if (*pa != *pb) {
          // insert a new node of size 2 to differentiate them.
          *cur_lut_ptr = cur_offs;
          cur_tn->parent = cur_offs;
          goto SPLIT_NODE;
        }
        pa++, pb++;
        last_matchlen++;
      }
      last_matchlen = cur_tn->length_compress;
    }  // for(;;)


SPLIT_NODE:
    // Insert a node
    TrieNodeBig_Size2 *tn2;
    tn2 = new TrieNodeBig_Size2;
    nodes_arr[cur_offs] = &tn2->hdr;
    tn2->hdr.src_offs = cur_offs;
    tn2->hdr.length_compress = last_matchlen;
    tn2->hdr.follow_node = 0;
    tn2->hdr.parent = prev_lut_value;
    tn2->hdr.num_children = 1;
    tn2->symbol[0] = src_ptr_cur[last_matchlen];
    tn2->symbol[1] = src_ptr_match[last_matchlen];
    tn2->next[0] = -cur_offs;
    tn2->next[1] = lut_value;

    if (last_matchlen > parent_len) {
      TrieNodeBig *tn = nodes_arr[parent_node_idx];
      if (last_matchlen < tn->length_compress)
        tn->follow_node = cur_offs;
    }
    parent_node_idx = cur_offs;

AFTER_NODE_INSERTED:;
    lut_value = idx_a ? nodes_arr[idx_a]->follow_node : 0;

    if (cur_offs >= src_offset_start + 1) {
      int num_lao = 0;
      int lrmlen = LRMScannerEx_FindMatch(&lrmscanner, src_ptr_cur, src_in + src_size, &lao_arr[0].offset);
      if (lrmlen > last_matchlen) {
        lao_arr[0].length = lrmlen;
        num_lao++;
      }

      lao_arr[num_lao].offset = src_ptr_cur - src_ptr_match;
      lao_arr[num_lao].length = last_matchlen;
      num_lao++;

      for (int numscan = 0; prev_lut_value != 0; ) {
        TrieNodeBig *tn = nodes_arr[prev_lut_value];
        if (num_lao < max_matches_to_consider) {
          if (cur_offs - tn->src_offs < lao_arr[num_lao - 1].offset) {
            if (tn->length_compress <= 2)
              break;
            lao_arr[num_lao].length = tn->length_compress;
            lao_arr[num_lao].offset = cur_offs - tn->src_offs;
            num_lao++;
          }
        }
        if (++numscan > 16)
          break;
        tn->src_offs = cur_offs;
        prev_lut_value = tn->parent;
      }
      MatchLenStorage_InsertMatches(mls, cur_offs - src_offset_start - 1, lao_arr, num_lao);
    } else {
      for (int numscan = 0; prev_lut_value != 0; ) {
        TrieNodeBig *tn = nodes_arr[prev_lut_value];
        if (++numscan > 16)
          break;
        tn->src_offs = cur_offs;
        prev_lut_value = tn->parent;
      }
    }
  }

GETOUT:

  for (size_t i = 0; i != src_size + 2; i++)
    delete nodes_arr[i];

  delete[] lao_arr;
  delete[] nodes_arr;
  delete[] node_lut;
}

static LengthAndOffset *RemoveIdentical(LengthAndOffset *p, LengthAndOffset *pend) {
  assert(pend != p);
  while (p < pend - 1 && p[0].length != p[1].length)
    p++;
  if (p < pend - 1) {
    for (LengthAndOffset *r = p + 2; r < pend; r++)
      if (p->length != r->length)
        *++p = *r;
    pend = p + 1;
  }
  return pend;
}

void FindMatchesHashBased(uint8 *src_base, int src_size, MatchLenStorage *mls, int max_num_matches, int preload_size, LRMTable *lrm_table) {
  MatchHasher<16, true> hasher;

  int bits = std::min<int>(std::max<int>(BSR(std::max(std::min(src_size, INT_MAX), 2) - 1) + 1, 18), 24);
  hasher.AllocateHash(bits, 0);
  hasher.SetBaseAndPreload(src_base, src_base + preload_size, preload_size);

  uint8 *src = src_base + preload_size;
  uint8 *src_end = src_base + src_size;
  hasher.SetHashPos(src);

  LRMScannerEx lrm;
  LRMScannerEx_Setup(&lrm, lrm_table, src, src_end, 0x40000000);

  uint8 *src_safe4 = src_base + src_size - 4;
  int src_size_safe = src_size - 8;
  for (int cur_pos = preload_size; cur_pos < src_size_safe; cur_pos++) {
    src = src_base + cur_pos;
    uint32 u32_to_scan_for = *(uint32*)src;

    uint32 *cur1 = hasher.hashentry_ptr_next_, *cur2 = hasher.hashentry2_ptr_next_;
    uint32 cur_high = hasher.hashtable_high_bits_;

    if (cur_pos + 8 < src_size_safe)
      hasher.SetHashPosPrefetch(src + 8);
    hasher.SetHashPos(src + 1);

    LengthAndOffset match[33];
    size_t num_match = 0;

    if (lrm_table) {
      int lrm_offset, lrm_length = LRMScannerEx_FindMatch(&lrm, src, src_end, &lrm_offset);
      if (lrm_length > 0)
        match[num_match++].Set(lrm_length, lrm_offset);
    }

    uint32 offsets[16];

    uint32 *hashcur = cur1;
    simde__m128i hash_high = simde_mm_set1_epi32(cur_high);
    simde__m128i max_pos = simde_mm_set1_epi32(cur_pos - 1);
    simde__m128i max_offset = simde_mm_set1_epi32(std::min<int>(src - src_base, 0x40000000));
    simde__m128i v, u, m0, m1, m2, m3;

    for (;;) {
      int best_ml = 0;

#define HASHROUND(pos)  \
      v = simde_mm_load_si128((simde__m128i *)&hashcur[pos*4]); \
      u = simde_mm_add_epi32(simde_mm_and_si128(simde_mm_sub_epi32(max_pos, v), simde_mm_set1_epi32(0x03ffffff)), simde_mm_set1_epi32(1)); \
      simde_mm_storeu_si128((simde__m128i *)&offsets[pos*4], u); \
      m##pos = simde_mm_cmpeq_epi32(simde_mm_set1_epi32(0), simde_mm_or_si128(simde_mm_cmpgt_epi32(u, max_offset), simde_mm_and_si128(simde_mm_xor_si128(v, hash_high), simde_mm_set1_epi32(0xfc000000))));
      HASHROUND(0);
      HASHROUND(1);
      HASHROUND(2);
      HASHROUND(3);
#undef HASHROUND


      uint32 matching_offsets = simde_mm_movemask_epi8(simde_mm_packs_epi16(simde_mm_packs_epi32(m0, m1), simde_mm_packs_epi32(m2, m3)));
      while (matching_offsets) {
        uint32 offset = offsets[BSF(matching_offsets)];
        matching_offsets &= matching_offsets - 1;
        if (*(uint32*)(src - offset) == u32_to_scan_for &&
          (best_ml < 4 || (src + best_ml < src_safe4 && *(src + best_ml) == *(src + best_ml - offset)))) {
          int ml = 4 + CountMatchingBytes(src + 4, src_safe4, offset);
          if (ml > best_ml) {
            best_ml = ml;
            match[num_match++].Set(ml, offset);
          }
        }
      }
      if (hashcur == cur2)
        break;
      hashcur = cur2;
    }

    hasher.Insert(cur1, cur2, hasher.MakeHashValue(cur_high, cur_pos));

    if (num_match) {
      MySort(match, match + num_match);
      num_match = RemoveIdentical(match, match + num_match) - match;

      int pos = cur_pos - preload_size;
      MatchLenStorage_InsertMatches(mls, pos, match, std::min<int>(max_num_matches, num_match));

      int best_ml = match[0].length;

      if (best_ml >= 77) {
        match[0].length = best_ml - 1;
        MatchLenStorage_InsertMatches(mls, pos + 1, match, 1);
        for (int i = 4; i < best_ml; i += 4) {
          match[0].length = best_ml - i;
          MatchLenStorage_InsertMatches(mls, pos + i, match, 1);
        }
        if (cur_pos + best_ml < src_size_safe)
          hasher.InsertRange(src, best_ml);
        cur_pos += best_ml - 1;
        if (lrm_table)
          LRMScannerEx_Setup(&lrm, lrm_table, src + best_ml, src_end, 0x40000000);
      }
    }
  }
}


void LRM_ReduceIdenticalHashes(LRMEnt *lrm) {
  HashPos *arrhashpos = lrm->arrhashpos.data();
  int arrhashpos_count = lrm->arrhashpos.size(), i;

  // find first duplicate hash
  for (i = 1; i < arrhashpos_count && arrhashpos[i].hash != arrhashpos[i - 1].hash; i++) {}
  // copy up to 16 elements with the same hash
  int i_dst = i;
  while (i < arrhashpos_count) {
    int i_end = std::min(i + 16, arrhashpos_count);
    uint32 hash = arrhashpos[i - 1].hash;
    // copy elements
    while (i < i_end && arrhashpos[i].hash == hash)
      arrhashpos[i_dst++] = arrhashpos[i++];
    // skip remaining elements
    while (i < arrhashpos_count && arrhashpos[i].hash == hash)
      i++;
    if (i >= arrhashpos_count)
      break;
    arrhashpos[i_dst++] = arrhashpos[i++];
    // copy elements until we find an identical hash again
    while (i < arrhashpos_count && arrhashpos[i].hash != arrhashpos[i_dst - 1].hash)
      arrhashpos[i_dst++] = arrhashpos[i++];
  }

  if (i_dst != arrhashpos_count) {
    lrm->arrhashpos.resize(i_dst);
    lrm->arrhashpos.shrink_to_fit();
  }
}

void LRM_CreateHashIndex(LRMEnt *lrm, int hash_lookup_bits) {
  HashPos *arrhashpos = lrm->arrhashpos.data();
  int arrhashpos_minus1 = lrm->arrhashpos.size() - 1;
  assert(hash_lookup_bits > 0);
  lrm->hash_begin_scan_pos_shift = 32 - hash_lookup_bits;
  int entries = (1 << hash_lookup_bits);
  lrm->hash_begin_scan_pos.resize(entries + 1);
  int *hash_begin_scan_pos = lrm->hash_begin_scan_pos.data();
  int current_pos = 0;
  for (int i = 0; i < entries; i++) {
    uint32 thres = i << (32 - hash_lookup_bits);
    while (current_pos < arrhashpos_minus1 && arrhashpos[current_pos].hash < thres)
      current_pos++;
    hash_begin_scan_pos[i] = current_pos;
  }
  hash_begin_scan_pos[entries] = arrhashpos_minus1;
}

uint32 LRMScanner_HashIt(const uint8 *src, int src_size) {
  if (src_size == 8) {
    return -1807498875 * src[0] + 443264249 * src[1] + 569077693 * src[2] + 1360715025 * src[3] - 480760523 * src[4] - 810880663 * src[5] + 741103597 * src[6] + src[7];
  }
  uint32 result = 0;
  for (int i = 0; i < src_size; i++)
    result = 741103597 * result + src[i];
  return result;
}

void LRM_FillMerge(LRMEnt *dst, LRMEnt *src_a, LRMEnt *src_b, int lrm_hash_lookup_bits) {
  dst->hash_length = src_a->hash_length;
  dst->cur_hashmult = src_a->cur_hashmult;
  dst->lrm_base_ptr = dst->buf = src_a->buf;
  dst->buf_size = src_b->buf - src_a->buf + src_b->buf_size;

  int position_increment = src_b->buf - src_a->buf;

  HashPos *a_data = src_a->arrhashpos.data(), *a_end = a_data + src_a->arrhashpos.size() - 1;
  HashPos *b_data = src_b->arrhashpos.data(), *b_end = b_data + src_b->arrhashpos.size() - 1;

  dst->arrhashpos.resize(src_a->arrhashpos.size() + src_b->arrhashpos.size() - 1);

  HashPos *dst_data = dst->arrhashpos.data();

  if (a_data != a_end && b_data != b_end) {
    for (;;) {
      if (a_data->hash <= b_data->hash) {
        *dst_data++ = *a_data++;
        if (a_data == a_end)
          break;
      } else {
        *dst_data++ = *b_data++;
        dst_data[-1].position += position_increment;
        if (b_data == b_end)
          break;
      }
    }
  }
  while (a_data != a_end)
    *dst_data++ = *a_data++;

  while (b_data != b_end) {
    *dst_data++ = *b_data++;
    dst_data[-1].position += position_increment;
  }

  dst->arrhashpos.resize(dst_data - dst->arrhashpos.data());

  HashPos hp = { 0xffffffff, src_a->arrhashpos.back().position };
  dst->arrhashpos.push_back(hp);
  LRM_ReduceIdenticalHashes(dst);
  LRM_CreateHashIndex(dst, lrm_hash_lookup_bits);
}

void LRM_Fill(LRMEnt *lrm, uint8 *buf, int buf_size, int step_size, int hash_lookup_bits, int hash_length) {
  assert(buf_size >= hash_length);
  assert(hash_length >= 8);
  assert(buf_size <= (1 << 30));

  lrm->buf = buf;
  lrm->buf_size = buf_size;
  lrm->lrm_base_ptr = buf;
  lrm->hash_length = hash_length;

  uint32 mult = 1;
  for (int i = lrm->hash_length - 1; i > 0; i--)
    mult *= 741103597;
  lrm->cur_hashmult = mult;

  lrm->arrhashpos.resize((buf_size - hash_length) / step_size + 2);
  HashPos *arrhashpos = lrm->arrhashpos.data();

  size_t num_hashpos = 0;
  uint32 last_hash = -1;
  for (int position = 0; position <= buf_size - hash_length; position += step_size) {
    uint32 hash = LRMScanner_HashIt(buf + position, hash_length);
    if (hash != last_hash) {
      arrhashpos[num_hashpos].hash = hash;
      arrhashpos[num_hashpos].position = position;
      num_hashpos++;
      last_hash = hash;
    }
  }

  struct sorter {
    bool operator()(const HashPos &a, const HashPos &b) {
      return a.hash < b.hash;
    }
  };

  lrm->arrhashpos.resize(num_hashpos + 1);
  MySort(lrm->arrhashpos.data(), lrm->arrhashpos.data() + num_hashpos);

  HashPos hp = { 0xffffffff, lrm->arrhashpos.back().position };
  lrm->arrhashpos.push_back(hp);

  LRM_ReduceIdenticalHashes(lrm);
  LRM_CreateHashIndex(lrm, hash_lookup_bits);
}

void LRM_FillCascade(LRMCascade *lrm, uint8 *src_ptr, int src_size, int lrm_step_size, int hash_lookup_bits_base, int hash_lookup_bits_inc, int base_bufsize, int hash_length) {
  lrm->src_ptr = src_ptr;
  lrm->src_size = src_size;
  lrm->base_bufsize = base_bufsize;

  for (int arridx = 0; arridx < 8; arridx++) {
    int lrm_bufsize = base_bufsize << arridx;
    if (src_size < lrm_bufsize)
      break;
    int lrm_hash_lookup_bits = hash_lookup_bits_base + hash_lookup_bits_inc * arridx;
    int lrm_array_cur_count = src_size / lrm_bufsize;

    std::vector<LRMEnt*> *vec = &lrm->lrms[arridx];
    vec->resize(lrm_array_cur_count);

    LRMEnt **lrments = vec->data();

    if (arridx != 0) {
      LRMEnt **lrments_prev = vec[-1].data();

      for (int inneridx = 0; inneridx < lrm_array_cur_count; inneridx++) {
        LRMEnt *ent = new LRMEnt;
        lrments[inneridx] = ent;

        LRM_FillMerge(ent, lrments_prev[inneridx * 2], lrments_prev[inneridx * 2 + 1], lrm_hash_lookup_bits);

        delete lrments_prev[inneridx * 2 + 1];
        lrments_prev[inneridx * 2 + 1] = NULL;
      }
    } else {
      for (int inneridx = 0, pos = 0; inneridx < lrm_array_cur_count; inneridx++, pos += lrm_bufsize) {
        LRMEnt *ent = new LRMEnt;
        lrments[inneridx] = ent;
        LRM_Fill(ent, src_ptr + pos, std::min(src_size - pos, lrm_bufsize), lrm_step_size, lrm_hash_lookup_bits, hash_length);
      }
    }
  }
}

LRMCascade *LRM_AllocateCascade(uint8 *src_ptr, int src_size, int lrm_step_size, int hash_lookup_bits_base, int hash_lookup_bits_inc, int base_bufsize, int hash_length) {
  LRMCascade *lrm = new LRMCascade;
  LRM_FillCascade(lrm, src_ptr, src_size, lrm_step_size, hash_lookup_bits_base, hash_lookup_bits_inc, base_bufsize, hash_length);
  return lrm;
}

void LRM_FreeCascade(LRMCascade *lrm) {
  for (size_t i = 0; i < 8; i++) {
    for (auto it : lrm->lrms[i])
      delete it;
  }
  delete lrm;
}

void LRM_CascadeGetSet(LRMCascade *lrm, LRMTable *result, uint8 *src_ptr) {
  int nbytes = src_ptr - lrm->src_ptr;
  if (nbytes <= 0)
    return;
  nbytes = std::min(nbytes, lrm->src_size);
  int nblocks = nbytes / lrm->base_bufsize;
  if (nblocks != 0) {
    assert(!lrm->lrms[0].empty());
    for (int arridx = 0; arridx < 8 && nblocks; arridx++) {
      if (nblocks & 1)
        result->vec.push_back(lrm->lrms[arridx][nblocks - 1]);
      nblocks >>= 1;
    }
    for (int i = 2 * (nblocks - 1) - 1; i >= 0; i--)
      result->vec.push_back(lrm->lrms[7][i]);
  }
}

void LRM_GetRanges(LRMCascade *lrm, LRMTable *result, uint8 *src_cur, uint8 *src_end) {
  int n = src_cur - lrm->src_ptr;
  if (n <= 0)
    return;

  int pos = lrm->base_bufsize * ((n + lrm->base_bufsize - 1) / lrm->base_bufsize);
  if (lrm->src_ptr + pos > src_end || pos > lrm->src_size)
    pos = std::min(lrm->base_bufsize * (n / lrm->base_bufsize), lrm->src_size);
  LRM_CascadeGetSet(lrm, result, lrm->src_ptr + pos);
}


void LRMScanner_Init(LRMScanner *lrm, LRMTable *lrm_table, uint8 *src_ptr_start, uint8 *src_ptr_end, int intmax) {
  memset(lrm, 0, sizeof(*lrm));
  if (lrm_table && !lrm_table->vec.empty()) {
    lrm->lrm_table = lrm_table;
    LRMEnt *first = lrm_table->vec.front();
    assert(first != NULL);
    lrm->multiplier = first->cur_hashmult;
    lrm->hash_length = first->hash_length;
    lrm->src_ptr_end = src_ptr_end - lrm->hash_length;
    lrm->intmax = intmax;
    if (src_ptr_start < lrm->src_ptr_end)
      lrm->rolling_hash = LRMScanner_HashIt(src_ptr_start, lrm->hash_length);
    else
      lrm->src_ptr_end = NULL;
  }
}

HashPos *HashPos_FindFirst(HashPos *begin, HashPos *end, uint32 scanfor) {
  size_t n = end - begin;
  while (n) {
    if (begin[n >> 1].hash >= scanfor)
      n >>= 1;
    else
      begin += (n >> 1) + 1, n -= (n >> 1) + 1;
  }
  return begin;
}

int CountMatchingCharacters(uint8 *src, uint8 *src_end, uint8 *match) {
  uint8 *src_org = src;
  if (*(uint64*)src != *(uint64*)match)
    return 0;
  src += 8;
  match += 8;
  while (src_end - src >= 4) {
    if (*(uint32*)src != *(uint32*)match)
      return (src - src_org) + (BSF(*(uint32*)src ^ *(uint32*)match) >> 3);
    src += 4, match += 4;
  }
  while (src != src_end && *src == *match)
    src++, match++;
  return src - src_org;
}

int LRMTable_Lookup(LRMTable *lrmtable, uint32 hash, uint8 *src_ptr_start, uint8 *src_ptr_end, int *found_off_ptr, int intmax) {
  LRMEnt **lrmtable_data = lrmtable->vec.data();
  int lrmtable_count = lrmtable->vec.size();
  int best_length = 0, best_offset = 0;
  for (int i = 0; i < lrmtable_count; i++) {
    int length = 0, offset = intmax;
    LRMEnt *lrm = lrmtable_data[i];
    if (src_ptr_end - src_ptr_start >= 8) {
      int *hashindex = &lrm->hash_begin_scan_pos[hash >> lrm->hash_begin_scan_pos_shift];
      HashPos *hp_start = lrm->arrhashpos.data();
      HashPos *hp = hp_start + hashindex[0];
      if (hash >= hp->hash) {
        hp = HashPos_FindFirst(hp, hp_start + hashindex[1], hash);
        for (; hp->hash == hash; hp++) {
          uint8 *match_ptr = lrm->lrm_base_ptr + hp->position;
          int cur_length = CountMatchingCharacters(src_ptr_start, src_ptr_end, match_ptr);
          // oodle bug? why compare with offset here, it means we will ignore longer matches.
          if (cur_length >= length && src_ptr_start - match_ptr < offset) {
            length = cur_length;
            offset = src_ptr_start - match_ptr;
            assert(offset != 0);
            if (length >= 256)
              break;
          }
        }
      }
    }
    if (length > best_length) {
      best_length = length;
      best_offset = offset;
    }
  }
  *found_off_ptr = best_offset;
  return best_length;
}


int LRMScanner_ScanOnePos(LRMScanner *lrm, uint8 *src_ptr, uint8 *src_ptr_end, int *found_off_ptr) {
  int match_length;

  if (src_ptr > lrm->src_ptr_end)
    return 0;
  if (src_ptr + 256 <= lrm->curr_match_end) {
    match_length = lrm->curr_match_end - src_ptr;
    *found_off_ptr = lrm->curr_match_off;
    assert(*found_off_ptr);
  } else {
    match_length = LRMTable_Lookup(lrm->lrm_table, lrm->rolling_hash, src_ptr, src_ptr_end, found_off_ptr, lrm->intmax);
    if (match_length > 0) {
      lrm->curr_match_end = &src_ptr[match_length];
      lrm->curr_match_off = *found_off_ptr;

      assert(*found_off_ptr);
    }
  }
  if (src_ptr < lrm->src_ptr_end)
    lrm->rolling_hash = src_ptr[lrm->hash_length] + 741103597 * (lrm->rolling_hash - lrm->multiplier * src_ptr[0]);
  return match_length;
}


void LRMScannerEx_Setup(LRMScannerEx *lrm, LRMTable *lrm_table, uint8 *src_ptr_start, uint8 *src_ptr_end, int intmax) {
  LRMScanner_Init(&lrm->lrm, lrm_table, src_ptr_start, src_ptr_end, intmax);

  lrm->src_ptr_cur = lrm->src_ptr_start = src_ptr_start;
  lrm->window_start = (lrm_table && !lrm_table->vec.empty()) ? lrm_table->vec.back()->buf : NULL;
  for (int i = 0; i < 32; i++) {
    lrm->offset_arr[i] = 0;
    lrm->length_arr[i] = LRMScanner_ScanOnePos(&lrm->lrm, src_ptr_start + i, src_ptr_end, &lrm->offset_arr[i]);
  }
}

int LRMScannerEx_FindMatch(LRMScannerEx *lrm, uint8 *src_ptr, uint8 *src_ptr_end, int *found_offset) {
  assert(src_ptr == lrm->src_ptr_cur);
  lrm->src_ptr_cur++;

  size_t idx = (src_ptr - lrm->src_ptr_start) & 0x1F;

  int prev_best_length = lrm->length_arr[idx];
  *found_offset = lrm->offset_arr[idx];

  // Populate the entry
  int length = LRMScanner_ScanOnePos(&lrm->lrm, src_ptr + 32, src_ptr_end, &lrm->offset_arr[idx]);
  lrm->length_arr[idx] = length;

  if (length > 0) {
    int off = lrm->offset_arr[idx];
    uint8 *match_ptr = src_ptr + 32 - off;

    // update all the other entries if the newfound match is better
    for (int j = 1; j < 32 && match_ptr - j >= lrm->window_start && *(src_ptr + 32 - j) == *(match_ptr - j); j++) {
      size_t idx2 = (idx - j) & 0x1F;
      if (length + j > lrm->length_arr[idx2]) {
        lrm->length_arr[idx2] = length + j;
        lrm->offset_arr[idx2] = off;
      }
    }
  }
  return prev_best_length;
}


static const uint8 *ExtractFromMlsInner(const uint8 *src, const uint8 *src_end, int *result, int A) {
  int sum = 0, bitpos = 0, t;
  for (;;) {
    if (src >= src_end)
      return NULL;
    t = *src++ - (1 << A);
    if (t >= 0) {
      *result = sum + (t << bitpos);
      return src;
    }
    sum += (t + 256) << bitpos;
    bitpos += A;
  }
}

static const uint8 *ExtractLengthFromMls(const uint8 *src, const uint8 *src_end, int *result, int A, int B) {
  if (src >= src_end)
    return NULL;
  int t = *src++ - (1 << A);
  if (t < 0) {
    src = ExtractFromMlsInner(src, src_end, result, B);
    *result = t + (*result << A) + 256;
  } else {
    *result = t;
  }
  return src;
}

static const uint8 *ExtractOffsetFromMls(const uint8 *src, const uint8 *src_end, int *result, int A, int B) {
  if (src_end - src < 2)
    return NULL;
  int t = (src[0] << 8 | src[1]) - (1 << A);
  src += 2;
  if (t < 0) {
    src = ExtractFromMlsInner(src, src_end, result, B);
    *result = t + (*result << A) + 65536;
  } else {
    *result = t;
  }
  return src;
}

void ExtractLaoFromMls(const MatchLenStorage *mls, int start, int src_size, LengthAndOffset *lao, int num_lao_per_offs) {
  for (; src_size--; start++) {
    int pos = mls->offset2pos[start];
    if (pos) {
      const uint8 *cur_ptr = &mls->byte_buffer[pos];
      LengthAndOffset *laocur = lao;
      for (int i = num_lao_per_offs; i--; laocur++) {
        cur_ptr = ExtractLengthFromMls(cur_ptr, cur_ptr + 32, &laocur->length, 1, 3);
        if (!cur_ptr)
          break;
        cur_ptr = ExtractOffsetFromMls(cur_ptr, cur_ptr + 32, &laocur->offset, 13, 7);
        if (!cur_ptr)
          break;
      }
    } else {
      lao->length = 0;
    }
    lao += num_lao_per_offs;
  }
}
