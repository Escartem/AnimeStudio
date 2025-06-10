#pragma once

#define BITSUP(x) (((x)+7)>>3)
#define kInvalidCost (1073741800.0f)



template<int Dir>
struct BitWriter64 {
  uint8 *ptr_;
  uint64 bits_;
  uint pos_;
  int totalb;

  BitWriter64(uint8 *dst) : ptr_(dst), bits_(0), pos_(63), totalb(0) {}

  __forceinline void Flush() {
    uint32 t = ((63 - pos_) >> 3);
    uint64 v = bits_ << (pos_ + 1);
    pos_ += 8 * t;
    if (Dir < 0) {
      *(uint64*)(ptr_ - 8) = v;
      ptr_ -= t;
    } else {
      *(uint64*)ptr_ = _byteswap_uint64(v);
      ptr_ += t;
    }
  }

  void Write(uint32 bits, int n) {
    assert(n <= (int)pos_);
    totalb += n;
    pos_ -= n;
    bits_ = (bits_ << n) | bits;
    Flush();
  }

  void WriteNoFlush(uint32 bits, int n) {
    totalb += n;
    pos_ -= n;
    bits_ = (bits_ << n) | bits;
  }

  uint8 *GetFinalPtr() {
    return (Dir >= 0) ? ptr_ + (pos_ != 63) : ptr_ - (pos_ != 63);
  }
};

template<int Direction>
struct Bitwriter32 {
  Bitwriter32(uint8 *ptr) : bits(0), ptr(ptr), bitpos(0) {}
  void Write(uint32 b, int n) {
    bits |= (uint64)b << bitpos;
    bitpos += n;
  }
  void Push32() {
    if (bitpos >= 32) {
      if (Direction > 0)
        *(uint32*)ptr = bits, ptr += 4;
      else
        *(uint32*)(ptr -= 4) = _byteswap_ulong((uint32)bits);
      bits >>= 32, bitpos -= 32;
    }
  }
  void PushFinal() {
    while (bitpos > 0) {
      if (Direction > 0)
        *ptr++ = (uint8)bits;
      else
        *--ptr = (uint8)bits;
      bits >>= 8, bitpos -= 8;
    }
  }
  uint64 bits;
  uint8 *ptr;
  int bitpos;
};


static inline uint32 BSF(uint32 x) {
  unsigned long index;
  _BitScanForward(&index, x);
  return index;
}

static inline uint32 BSF64(uint64 x) {
  return ((uint32)x) ? BSF((uint32)x) : 32 + BSF((uint32)(x >> 32));
}

static inline uint32 BSR(uint32 x) {
  unsigned long index;
  _BitScanReverse(&index, x);
  return index;
}

static inline uint32 EncodeZigZag32(int32 t) {
  return (uint32)((t >> 31) ^ (t << 1));
}

static inline int32 DecodeZigZag32(uint32 t) {
  return -(int32)(t & 1) ^ (t >> 1);
}



float CombineCostComponents1A(int platforms, float v, float a, float b, float c, float d,
                              float x, float y, float z, float w);

float CombineCostComponents1(int platforms, float v, float a, float b, float c, float d);
float CombineCostComponents(int platforms, float a, float b, float c, float d);

int Kraken_GetBlockSize(const uint8 *src, const uint8 *src_end, int *dest_size, int dest_capacity);
bool IsProbablyText(const uint8 *p, size_t size);

template<typename T, typename U> static inline T postadd(T &x, U v) { T t = x; x += v; return t; }


struct HistoU8 {
  uint count[256];
};

extern const uint kLog2LookupTable[8193];

static __forceinline int CountMatchingBytes(const uint8 *p, const uint8 *pend, ptrdiff_t offset) {
  int len = 0;
  while (pend - p >= 4) {
    if (*(uint32*)p != *(uint32*)(p-offset))
      return len + (BSF(*(uint32*)p ^ *(uint32*)(p - offset)) >> 3);
    p += 4, len += 4;
  }
  for (; p < pend; len++, p++) {
    if (*p != p[-offset])
      break;
  }
  return len;
}

static __forceinline int GetMatchlengthQ(const uint8 *src, int offset, const uint8 *src_end, uint32 u32_at_cur) {
  uint32 u32_at_match = *(uint32*)&src[-offset];
  if (u32_at_cur == u32_at_match) {
    return 4 + CountMatchingBytes(src + 4, src_end, offset);
  } else {
    return ((uint16)(u32_at_cur ^ u32_at_match)) ? 0 : 3 - (((u32_at_cur ^ u32_at_match) & 0xFFFFFF) != 0);
  }
}

static __forceinline int GetMatchlengthMin2(const uint8 *src_ptr_cur, int offset, const uint8 *src_ptr_safe_end) {
  int len = CountMatchingBytes(src_ptr_cur, src_ptr_safe_end, offset);
  if (len == 1)
    len = 0;
  return len;
}

static __forceinline int GetMatchlengthQMin3(const uint8 *src_ptr_cur, int offset, const uint8 *src_ptr_safe_end, uint32 u32_at_cur) {
  uint32 u32_at_match = *(uint32*)&src_ptr_cur[-offset];
  if (u32_at_cur == u32_at_match) {
    return 4 + CountMatchingBytes(src_ptr_cur + 4, src_ptr_safe_end, offset);
  } else {
    return ((u32_at_cur ^ u32_at_match) & 0xFFFFFF) != 0 ? 0 : 3;
  }
}

static __forceinline int GetMatchlengthQMin4(const uint8 *src_ptr_cur, int offset, const uint8 *src_ptr_safe_end, uint32 u32_at_cur) {
  uint32 u32_at_match = *(uint32*)&src_ptr_cur[-offset];
  return (u32_at_cur == u32_at_match) ? 4 + CountMatchingBytes(src_ptr_cur + 4, src_ptr_safe_end, offset) : 0;
}

static __forceinline bool IsBetterThanRecent(int rml, int ml, int offs) {
  return (rml < 2 || (rml + 1 < ml && (rml + 2 < ml || offs < 1024) && (rml + 3 < ml || offs < 65536)));
}

static __forceinline bool IsMatchBetter(uint ml, uint offs, uint best_ml, uint best_offs) {
  if (ml < best_ml)
    return false;
  if (ml == best_ml)
    return offs < best_offs;
  if (ml == best_ml + 1)
    return (offs >> 7) <= best_offs;
  return true;
}




struct LeviathanRecentOffs {
  // The range 8..14 is the actual range, the rest is just
  // padding to do inserts easier.
  int offs[16];

  LeviathanRecentOffs() {
    offs[8] = offs[9] = offs[10] = offs[11] = offs[12] = offs[13] = offs[14] = 8;
  }

  void Insert(int offset) {
    if (offset > 0) {
      simde__m128i a0 = simde_mm_loadu_si128((const simde__m128i *)&offs[7]),
                   a1 = simde_mm_loadu_si128((const simde__m128i *)&offs[11]);
      simde_mm_storeu_si128((simde__m128i *)&offs[8], a0);
      simde_mm_storeu_si128((simde__m128i *)&offs[12], a1);
      offs[8] = offset;
    } else {
      size_t slot = -offset;
      simde__m128i a0 = simde_mm_loadu_si128((const simde__m128i *)&offs[slot]),
                   a1 = simde_mm_loadu_si128((const simde__m128i *)&offs[slot + 4]);
      int old_offset = offs[slot + 8];
      simde_mm_storeu_si128((simde__m128i *)&offs[slot + 1], a0);
      simde_mm_storeu_si128((simde__m128i *)&offs[slot + 5], a1);
      offs[8] = old_offset;
    }
  }
};


template<typename T>
static inline T clamp(T a, T min, T max) {
  if (a > max) a = max;
  if (a < min) a = min;
  return a;
}
int ilog2round(uint v);

template<typename T>
static inline T AlignPointer(void *p, size_t align) {
  return (T)(((uintptr_t)p + align - 1) & ~(align - 1));
}

static inline uint32 Rol32(uint32 v, int b) {
  return _rotl(v, b);
}
