#include <utility>

#define QS_SWAP(a,b) {t = (a); (a) = (b); (b) = t; }

template<typename T>
void MySort(T *begin, T *end) {
  size_t count = end - begin, count2;
  T *right_ptr = begin + count - 1;
  T *left_ptr = begin;
  if (count <= 1)
    return;
  T t;

  struct Stack {
    T *left, *right;
    size_t count;
  };

  Stack stack[64], *sp = stack + 62;
  sp[1].count = 0;

  do {
    while (count > 1) {
      if (count == 2) {
        if (*right_ptr < *left_ptr)
          QS_SWAP(*left_ptr, *right_ptr);
        break;
      }

      T *mid_ptr = &left_ptr[count >> 1];
      if (*mid_ptr < *left_ptr) {
        if (*mid_ptr < *right_ptr) {
          if (*left_ptr < *right_ptr) {
            QS_SWAP(*mid_ptr, *left_ptr);
          } else {
            QS_SWAP(*mid_ptr, *left_ptr);
            QS_SWAP(*mid_ptr, *right_ptr);
          }
        } else {
          QS_SWAP(*left_ptr, *right_ptr);
        }
      } else {
        if (*right_ptr < *mid_ptr) {
          if (*left_ptr < *right_ptr) {
            QS_SWAP(*mid_ptr, *right_ptr);
          } else {
            QS_SWAP(*left_ptr, *mid_ptr);
            QS_SWAP(*left_ptr, *right_ptr);
          }
        }
      }

      if (count <= 4) {
        if (count == 4) {
          if (*mid_ptr < left_ptr[1]) {
            if (left_ptr[1] < right_ptr[0]) {
              QS_SWAP(left_ptr[1], *mid_ptr);
            } else {
              QS_SWAP(left_ptr[1], *mid_ptr);
              QS_SWAP(*mid_ptr, *right_ptr);
            }
          } else {
            if (left_ptr[1] < left_ptr[0]) {
              QS_SWAP(*left_ptr, left_ptr[1]);
            }
          }
        }
        break;
      }

      QS_SWAP(*left_ptr, *mid_ptr);
      T *x_ptr = left_ptr;
      T *y_ptr = right_ptr;
      for (;;) {
        do y_ptr--; while (*left_ptr < *y_ptr);
        if (y_ptr <= x_ptr)
          break;
        do x_ptr++; while (*x_ptr < *left_ptr);
        if (x_ptr >= y_ptr) {
          x_ptr--;
          break;
        }
        QS_SWAP(*x_ptr, *y_ptr);
      }

      QS_SWAP(*x_ptr, *left_ptr);

      T *z_ptr = x_ptr;
      do z_ptr++; while (z_ptr < right_ptr && !(*x_ptr < *z_ptr));
      T *w_ptr = x_ptr;
      do w_ptr--;  while (w_ptr > left_ptr && !(*w_ptr < *x_ptr));

      count = right_ptr - z_ptr + 1;
      count2 = w_ptr - left_ptr + 1;
      assert(!(count & 0x80000000));
      assert(!(count2 & 0x80000000));
      if (count >= count2) {
        sp->left = left_ptr;
        sp->right = w_ptr;
        sp->count = count2;
        left_ptr = z_ptr;
      } else {
        sp->left = z_ptr;
        sp->right = right_ptr;
        sp->count = count;
        right_ptr = w_ptr;
        count = count2;
      }
      sp--;
    }

    count = sp[1].count;
    left_ptr = sp[1].left;
    right_ptr = sp[1].right;
    sp++;
  } while (count);

}
#undef QS_SWAP



template<typename T>
void MyMakeHeap(T *begin, T *end) {
  size_t n = end - begin, t, u;
  for (size_t half = n >> 1; half--; ) {
    for (t = half; (u = 2 * t + 1) < n; t = u) {
      if (u + 1 < n && begin[u] < begin[u + 1])
        u++;
      if (begin[u] < begin[t])
        break;
      std::swap(begin[t], begin[u]);
    }
  }
}

template<typename T>
void MyPushHeap(T *begin, T *end) {
  size_t t = --end - begin, u;
  while (t) {
    u = (t - 1) >> 1;
    if (!(begin[u] < begin[t]))
      break;
    std::swap(begin[t], begin[u]);
    t = u;
  }
}

template<typename T>
void MyPopHeap(T *begin, T *end) {
  size_t n = end - begin;
  size_t t = 0, u;
  for (; (u = 2 * t + 1) < n; t = u) {
    if (u + 1 < n && begin[u] < begin[u + 1])
      u++;
    begin[t] = begin[u];
  }
  if (t < n - 1) {
    begin[t] = begin[n - 1];
    MyPushHeap(begin, begin + t + 1);
  }
}

template<typename T> void SimpleSort(T *p, T *pend) {
  if (p != pend) {
    for (T *lp = p + 1, *rp; lp != pend; lp++) {
      T t = lp[0];
      for (rp = lp; rp > p && t < rp[-1]; rp--)
        rp[0] = rp[-1];
      rp[0] = t;
    }
  }
}