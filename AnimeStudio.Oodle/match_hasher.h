// This file is not GPL. It may be used for educational purposes only.
#pragma once

#include <algorithm>

template<int _NumHash, bool _DualHash>
class MatchHasher {
public:
  MatchHasher() {}

  enum {
    NumHash = _NumHash,
    DualHash = _DualHash,
  };

  ~MatchHasher() {
    free(malloced_ptr_);
  }

  void AllocateHash(int bits, int k) {
    hash_bits_ = bits;
    hash_mask_ = (1 << bits) - NumHash;
    k = std::max(std::min(k > 0 ? k : 4, 8), 1);
    hashmult_ = 0xCF1BBCDCB7A56463ull << (8 * (8 - k));
    malloced_ptr_ = malloc(sizeof(uint32) * (1 << bits) + 64);
    hash_ptr_ = (uint32*)(((uintptr_t)malloced_ptr_ + 63) & ~63);
    memset(hash_ptr_, 0, sizeof(uint32) * (1 << bits));
  }

  struct HashPos {
    uint32 *ptr1, *ptr2;
    uint32 pos, hi;
  };

  HashPos GetHashPos(const uint8 *src) {
    HashPos hp = {hashentry_ptr_next_, hashentry2_ptr_next_, (uint32)(src - src_base_), hashtable_high_bits_};
    return hp;
  }

  void SetBaseWithoutPreload(const uint8 *src_base) {
    src_base_ = src_base;
  }

  void SetBaseAndPreload(const uint8 *src_base, const uint8 *src_start, int max_preload_len) {
    src_base_ = src_base;
    if (src_base == src_start)
      return;
    int preload_len = src_start - src_base;
    const uint8 *src = src_base;
    assert(preload_len > 0);
    if (preload_len > max_preload_len) {
      preload_len = max_preload_len;
      src = src_start - preload_len;
    }
    int step = std::max(preload_len >> 18, 2);
    int rounds_until_next_step = (preload_len >> 1) / step;
    SetHashPos(src);
    
    for (;;) {
      if (--rounds_until_next_step <= 0) {
        if (src >= src_start)
          break;
        step >>= 1;
        assert(step >= 1);
        rounds_until_next_step = (src_start - src) / step;
        if (step > 1)
          rounds_until_next_step >>= 1;
      }
      HashPos hp = GetHashPos(src);
      src += step;
      SetHashPos(src);
      Insert(hp);
    }
  }

  static uint32 MakeHashValue(uint32 cur_high, uint32 cur_pos) {
    return (cur_high & ~0x3FFFFFF) | (cur_pos & 0x3FFFFFF);
  }

  inline void InsertOne(uint32 *h, uint32 hval) {
    if (NumHash == 1) {
      h[0] = hval;
    } else if (NumHash == 2) {
      h[1] = h[0];
      h[0] = hval;
    } else if (NumHash == 4) {
      uint32 a = h[2], b = h[1], c = h[0];
      h[3] = a;
      h[2] = b;
      h[1] = c;
      h[0] = hval;
    } else if (NumHash == 16) {
      simde__m128i a0 = simde_mm_load_si128((simde__m128i*)h);
      simde__m128i a1 = simde_mm_loadu_si128((simde__m128i*)(h + 3));
      simde__m128i a2 = simde_mm_loadu_si128((simde__m128i*)(h + 7));
      simde__m128i a3 = simde_mm_loadu_si128((simde__m128i*)(h + 11));
      simde_mm_storeu_si128((simde__m128i*)(h + 1), a0);
      simde_mm_store_si128((simde__m128i*)(h + 4), a1);
      simde_mm_store_si128((simde__m128i*)(h + 8), a2);
      simde_mm_store_si128((simde__m128i*)(h + 12), a3);
      h[0] = hval;
    } else {
      assert(0);
    }
  }

  inline void Insert(uint32 *h, uint32 *h2, uint32 he) {
    InsertOne(h, he);
    if (DualHash)
      InsertOne(h2, he);
  }

  inline void Insert(const HashPos &hp) {
    Insert(hp.ptr1, hp.ptr2, MakeHashValue(hp.hi, hp.pos));
  }

  inline void SetHashPos(const uint8 *p) {
    src_cur_ = p;
    uint64 at_src = *(uint64*)p;
    uint32 hash1 = Rol32((uint32)((hashmult_ * at_src) >> 32), hash_bits_);
    hashtable_high_bits_ = hash1;
    hashentry_ptr_next_ = &hash_ptr_[hash1 & hash_mask_];
    if (DualHash) {
      uint32 hash2 = (0xCF1BBCDCB7A56463ull * at_src) >> (64 - hash_bits_);
      hashentry2_ptr_next_ = &hash_ptr_[hash2 & ~(NumHash - 1)];
    }
  }

  inline void SetHashPosPrefetch(const uint8 *p) {
    SetHashPos(p);
    simde_mm_prefetch((char*)hashentry_ptr_next_, SIMDE_MM_HINT_T0);
    if (DualHash)
      simde_mm_prefetch((char*)hashentry2_ptr_next_, SIMDE_MM_HINT_T0);
  }

  void Reset(const uint8 *p) {
    memset(hash_ptr_, 0, sizeof(uint32) * (1 << hash_bits_));
    src_base_ = p;
  }

  void InsertRange(const uint8 *p, size_t len) {
    if (src_cur_ < p + len) {

      const uint8 *src_base = src_base_;
      Insert(hashentry_ptr_next_, hashentry2_ptr_next_, MakeHashValue(hashtable_high_bits_, src_cur_ - src_base));
      for (int i = src_cur_ - p + 1; i < len; i *= 2) {
        uint32 hash = Rol32((uint32)((hashmult_ * *(uint64*)(p + i)) >> 32), hash_bits_);
        InsertOne(&hash_ptr_[hash & hash_mask_], MakeHashValue(hash, p + i - src_base));
      }
      SetHashPos(p + len);
    } else if (src_cur_ != p + len) {
      SetHashPos(p + len);
    }
  }
public:
  void *malloced_ptr_ = 0;
  uint32 *hash_ptr_ = 0;
  int hash_bits_;
  uint32 hash_mask_;
  const uint8 *src_base_ = 0;
  const uint8 *src_cur_ = 0;
  uint32 *hashentry_ptr_next_ = 0;
  uint32 *hashentry2_ptr_next_ = 0;
  uint64 hashmult_;
  uint32 hashtable_high_bits_;
};

class MatchHasher2 {
public:
  MatchHasher2() {}

  ~MatchHasher2() {
    delete[] firsthash_;
    delete[] longhash_;
    delete[] nexthash_;
  }

  void AllocateHash(int bits, int min_match_len) {
    int a_bits = bits, b_bits = bits, c_bits = 16;

    firsthash_bits_ = a_bits = std::min(a_bits, 19);
    longhash_bits_ = b_bits = std::min(b_bits, 19);

    firsthash_mask_ = (1 << a_bits) - 1;
    longhash_mask_ = (1 << b_bits) - 1;
    nexthash_mask_ = (1 << c_bits) - 1;

    firsthash_ = new uint32[1 << a_bits];
    longhash_ = new uint32[1 << b_bits];
    nexthash_ = new uint16[1 << c_bits];

    memset(firsthash_, 0, sizeof(uint32) * (1 << a_bits));
    memset(longhash_, 0, sizeof(uint32) * (1 << b_bits));
    memset(nexthash_, 0, sizeof(uint16) * (1 << c_bits));
  }

  struct HashPos {
    uint32 pos;
    uint32 hash_a, hash_b, hash_b_hi;
    const uint8 *next_p;
  };

  HashPos GetHashPos(const uint8 *src) {
    uint64 at_src = *(uint64*)src;
    uint32 hash_a = (uint32)((0xB7A5646300000000ull * at_src) >> 32);
    uint32 hash_b = (uint32)((0xCF1BBCDCB7A56463ull * at_src) >> 32);
    HashPos hp = { (uint32)(src - src_base_), hash_a >> (32 - firsthash_bits_), hash_b >> (32 - longhash_bits_), hash_b, src + 1 };
    return hp;
  }

  void SetBaseWithoutPreload(const uint8 *src_base) {
    src_base_ = src_base;
  }

  void SetBaseAndPreload(const uint8 *src_base, const uint8 *src_start, int max_preload_len) {
    src_base_ = src_base;
    if (src_base == src_start)
      return;
    int preload_len = src_start - src_base;
    const uint8 *src = src_base;
    assert(preload_len > 0);
    if (preload_len > max_preload_len) {
      preload_len = max_preload_len;
      src = src_start - preload_len;
    }
    int step = std::max(preload_len >> 18, 2);
    int rounds_until_next_step = (preload_len >> 1) / step;
    SetHashPos(src);

    for (;;) {
      if (--rounds_until_next_step <= 0) {
        if (src >= src_start)
          break;
        step >>= 1;
        assert(step >= 1);
        rounds_until_next_step = (src_start - src) / step;
        if (step > 1)
          rounds_until_next_step >>= 1;
      }
      HashPos hp = GetHashPos(src);
      src += step;
      SetHashPos(src);
      firsthash_[hp.hash_a] = hp.pos;
      longhash_[hp.hash_b] = (hp.hash_b_hi & 0x3F) | (hp.pos << 6);
    }
  }

  static uint32 MakeHashValue(uint32 cur_high, uint32 cur_pos) {
    return (cur_high & ~0x3FFFFFF) | (cur_pos & 0x3FFFFFF);
  }

  inline void Insert(const HashPos &hp) {
    nexthash_[hp.pos & nexthash_mask_] = firsthash_[hp.hash_a];
    firsthash_[hp.hash_a] = hp.pos;
    src_cur_ = hp.next_p;
  }

  inline void SetHashPos(const uint8 *p) {
    src_cur_ = p;
  }

  inline void SetHashPosPrefetch(const uint8 *p) {
    HashPos hp = GetHashPos(p);
    simde_mm_prefetch((char*)&firsthash_[hp.hash_a], SIMDE_MM_HINT_T0);
    simde_mm_prefetch((char*)&longhash_[hp.hash_b], SIMDE_MM_HINT_T0);
  }

  void InsertRange(const uint8 *p, size_t len) {
    for (int i = 0; i < len; i = 2 * i + 1) {
      uint32 hash_b = (uint32)((0xCF1BBCDCB7A56463ull * *(uint64*)(p + i)) >> 32);
      longhash_[hash_b >> (32 - longhash_bits_)] = (hash_b & 0x3F) | ((p + i - src_base_) << 6);
    }
    const uint8 *p_end = p + len;
    while (src_cur_ < p_end) {
      uint32 hash_a = (uint32)((0xB7A5646300000000ull * *(uint64*)src_cur_) >> 32) >> (32 - firsthash_bits_);
      uint32 pos = src_cur_ - src_base_;

      nexthash_[pos & nexthash_mask_] = firsthash_[hash_a];
      firsthash_[hash_a] = pos;
      src_cur_++;
    }
  }
public:
  uint32 *firsthash_ = 0;
  uint32 *longhash_ = 0;
  uint16 *nexthash_ = 0;
  const uint8 *src_base_ = 0;
  const uint8 *src_cur_ = 0;
  uint32 firsthash_mask_;
  uint32 longhash_mask_;
  uint32 nexthash_mask_;
  uint8 firsthash_bits_;
  uint8 longhash_bits_;
};


template<typename T>
class FastMatchHasher {
public:
  typedef T ElemType;
  void AllocateHash(int bits, int k) {
    hash_bits_ = bits;
    if (k == 0)
      k = 4;
    if (k >= 5 && k <= 8) {
      hashmult_ = 0xCF1BBCDCB7A56463ull << (8 * (8 - k)); 
    } else {
      hashmult_ = 0x9E3779B100000000ull;
    }
    malloced_ptr_ = malloc(sizeof(T) * (1 << bits) + 64);
    hash_ptr_ = (T*)(((uintptr_t)malloced_ptr_ + 63) & ~63);
    memset(hash_ptr_, 0, sizeof(T) * (1 << bits));
  }

  void SetBaseWithoutPreload(const uint8 *p) {
    src_base_ = p;
  }

  void SetBaseAndPreload(const uint8 *src_base, const uint8 *src_start, int max_preload_len) {
    src_base_ = src_base;
    if (src_base == src_start)
      return;
    const uint8 *src = src_base;
    int preload_len = src_start - src_base;
    assert(preload_len > 0);
    if (preload_len > max_preload_len) {
      preload_len = max_preload_len;
      src = src_start - preload_len;
    }
    int step = std::max(preload_len >> 18, 2);
    int rounds_until_next_step = (preload_len >> 1) / step;
    
    T *hash_ptr = hash_ptr_;
    uint64 hashmult = hashmult_;
    int hashshift = 64 - hash_bits_;
    for (;;) {
      if (--rounds_until_next_step <= 0) {
        if (src >= src_start)
          break;
        step >>= 1;
        assert(step >= 1);
        rounds_until_next_step = (src_start - src) / step;
        if (step > 1)
          rounds_until_next_step >>= 1;
      }
      hash_ptr[(size_t)(*(uint64*)src * hashmult >> hashshift)] = src - src_base;
      src += step;
    }
  }

  void *malloced_ptr_ = NULL;
  T *hash_ptr_ = NULL;
  const uint8 *src_base_;
  uint64 hashmult_;
  int hash_bits_;
};

