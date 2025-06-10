// This file is not GPL. It may be used for educational purposes only.
#pragma once
#include <vector>
#include "compress.h"


struct LRMEnt {
  int a = 0;
  int b = 0;
  std::vector<HashPos> arrhashpos;
  int hash_length = 0;
  uint32 cur_hashmult = 0;
  uint8 *buf = 0;
  uint8 *lrm_base_ptr = 0;
  int buf_size = 0;
  std::vector<int> hash_begin_scan_pos;
  int hash_begin_scan_pos_shift = 0;
};

struct LRMTable {
  std::vector<LRMEnt*> vec;
};

struct LRMCascade {
  std::vector<LRMEnt*> lrms[8];
  int base_bufsize;
  uint8 *src_ptr;
  int src_size;
};

struct LRMScanner {
  uint8 *curr_match_end;
  int rolling_hash;
  int multiplier;
  int curr_match_off;
  int hash_length;
  uint8 *src_ptr_end;
  LRMTable *lrm_table;
  int intmax;
};

struct LRMScannerEx {
  LRMScanner lrm;
  uint8 *src_ptr_start;
  uint8 *src_ptr_cur;
  uint8 *window_start;
  int length_arr[32];
  int offset_arr[32];
};


struct MatchLenStorage {
  std::vector<uint8_t> byte_buffer;
  int byte_buffer_use = 0;
  std::vector<int> offset2pos;
  uint8 *window_base;

  static MatchLenStorage *Create(int entries, float avg_bytes);
  static void Destroy(MatchLenStorage *mls);
};

void FindMatchesSuffixTrie(uint8 *src_in, int src_size, MatchLenStorage *mls, int max_matches_to_consider, int src_offset_start, LRMTable *lrm);
void FindMatchesHashBased(uint8 *src_base, int src_size, MatchLenStorage *mls, int max_num_matches, int preload_size, LRMTable *lrm_table);

struct LRMCascade;
void LRM_FreeCascade(LRMCascade *lrm);
LRMCascade *LRM_AllocateCascade(uint8 *src_ptr, int src_size, int lrm_step_size, int hash_lookup_bits_base, int hash_lookup_bits_inc, int base_bufsize, int hash_length);
void LRM_GetRanges(LRMCascade *lrm, LRMTable *result, uint8 *src_cur, uint8 *src_end);

void LRMScannerEx_Setup(LRMScannerEx *lrm, LRMTable *lrm_table, uint8 *src_ptr_start, uint8 *src_ptr_next, int intmax);
int LRMScannerEx_FindMatch(LRMScannerEx *lrm, uint8 *src_ptr, uint8 *src_ptr_end, int *found_offset);

void ExtractLaoFromMls(const MatchLenStorage *mls, int start, int src_size, LengthAndOffset *lao, int num_lao_per_offs);
