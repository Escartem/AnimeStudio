// This file is not GPL. It may be used for educational purposes only.
struct HistoU8;

enum {
  kMaxBitLength = 11,
};

enum {
  kEntropyOpt_AllowDoubleHuffman = 1,
  kEntropyOpt_tANS = 2,
  kEntropyOpt_RLE = 4,
  kEntropyOpt_RLEEntropy = 8,
  kEntropyOpt_MultiArray = 16,
  kEntropyOpt_MultiArrayAdvanced = 0x20,
  kEntropyOpt_SupportsNewHuffman = 0x40,
  kEntropyOpt_SupportsShortMemset = 0x80,
};

void CountBytesHistoU8(const uint8 *src_ptr, size_t src_size, HistoU8 *dst);
uint GetHistoSum(const uint *a, size_t n);
uint GetHistoSum(const HistoU8 &h);
uint GetHistoCostApprox(const HistoU8 &histo, int histo_sum);
uint GetHistoCostApprox(const uint *histo, size_t arrsize, int histo_sum);

int EncodeArrayU8CompactHeader(uint8 *dst, uint8 *dst_end, const uint8 *src, int src_size, int opts, float speed_tradeoff, int platforms, float *cost_ptr, int level, HistoU8 *histogram_ptr);
int EncodeArrayU8(uint8 *dst, uint8 *dst_end, const uint8 *src, int src_size, int encode_opts, float speed_tradeoff, int platforms, float *cost_ptr, int level, HistoU8 *histo_ptr);
int EncodeArrayU8_Memcpy(uint8 *dst, uint8 *dst_end, const uint8 *src, int size);
int EncodeArrayU8_MaybeConcat(uint8 *dst, uint8 *dst_end, const uint8 *src, int src_size, int opts, float speed_tradeoff, int platforms, float *cost_ptr, int level, HistoU8 *histo, int src_size_part);
int EncodeMultiArray(uint8 *dst, uint8 *dst_end, const uint8 **array_data, int *array_lens, int array_count, int opts, float speed_tradeoff, int platforms, float *cost_ptr, int level);
int EncodeArray_Huff(uint8 *dst, uint8 *dst_end, const uint8 *src, int src_size, const HistoU8 &histo, float speed_tradeoff, int platforms, float *cost_ptr, int *mode_ptr, int opts, int level);
int EncodeArrayU8WithHisto(uint8 *dst, uint8 *dst_end, const uint8 *src, int src_size, const HistoU8 &histo, int opts, float speed_tradeoff, int platforms, float *best_cost_so_far, int level);

int EncodeArrayU8_MultiArray(uint8 *dst, uint8 *dst_end, const uint8 *src, int src_size, const HistoU8 &histo, int level, int opts, float speed_tradeoff, int platforms, float cost_thres, float *cost_ptr);
int EncodeArrayU8_tANS(uint8 *dst, uint8 *dst_end, const uint8 *src, int src_size, const HistoU8 &histo, float speed_tradeoff, int platforms, float *cost_ptr);
int EncodeArray_AdvRLE(uint8 *dst, uint8 *dst_end, const uint8 *src, int src_size, float speed_tradeoff, int platforms, float *cost_ptr, int opts, int level);
int EncodeArrayU8_Memset(uint8 *dst, uint8 *dst_end, const uint8 *src, int src_size, int opts, float speed_tradeoff, int platforms, float *cost_ptr);

float GetCost_SingleHuffman(const HistoU8 &histo, int histo_sum, float speed_tradeoff, int platforms);
float GetTime_SingleHuffman(int platforms, int count, int numsyms);
float GetTime_DoubleHuffman(int platforms, int count, int numsyms);
float GetTime_Memset(int platforms, int src_size);
int GetLog2Interpolate(uint x);
void CountBytesHistoU8(const uint8 *data, size_t data_size, HistoU8 *histo);

template<int Dir> struct BitWriter64;

// For compression symranges - shared by both tans and huff
int EncodeSymRange(uint8 *rice, uint8 *bits, uint8 *bitcount, int used_syms, int *range, int numrange);
void WriteNumSymRange(BitWriter64<1> *bits, int num_symrange, int used_syms);
void WriteManyRiceCodes(BitWriter64<1> *bits, const uint8 *data, size_t num);
void WriteSymRangeLowBits(BitWriter64<1> *bits, const uint8 *data, const uint8 *bitcount, size_t num);

