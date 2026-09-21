#pragma once

////////////////////////////////////////////////////////////////////////////////
// The MIT License (MIT)
//
// Copyright (c) 2017 Nicholas Frechette & Animation Compression Library contributors
// Copyright (c) 2018 Nicholas Frechette & Realtime Math contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
////////////////////////////////////////////////////////////////////////////////

//////////////////////////////////////////////////////////////////////////
// Detect which intrinsics the current compilation environment supports.
//////////////////////////////////////////////////////////////////////////

#if !defined(RTM_NO_INTRINSICS)
	#if defined(__AVX2__)
		#define RTM_AVX2_INTRINSICS
		#define RTM_FMA_INTRINSICS
	#endif

	#if defined(__AVX__)
		#define RTM_AVX_INTRINSICS
		#define RTM_SSE4_INTRINSICS
		#define RTM_SSE3_INTRINSICS
		#define RTM_SSE2_INTRINSICS
	#endif

	#if defined(__SSE4_1__)
		#define RTM_SSE4_INTRINSICS
		#define RTM_SSE3_INTRINSICS
		#define RTM_SSE2_INTRINSICS
	#endif

	#if defined(__SSSE3__)
		#define RTM_SSE3_INTRINSICS
		#define RTM_SSE2_INTRINSICS
	#endif

	#if defined(__SSE2__) || defined(_M_IX86) || defined(_M_X64)
		#define RTM_SSE2_INTRINSICS
	#endif

	#if defined(__ARM_NEON) || defined(_M_ARM) || defined(_M_ARM64)
		#define RTM_NEON_INTRINSICS

		#if defined(__aarch64__) || defined(_M_ARM64)
			#define RTM_NEON64_INTRINSICS
		#endif
	#endif

	// If SSE2 and NEON aren't used, we default to the scalar implementation
	#if !defined(RTM_SSE2_INTRINSICS) && !defined(RTM_NEON_INTRINSICS)
		#define RTM_NO_INTRINSICS
	#endif
#endif

#if defined(RTM_SSE2_INTRINSICS)
	#include <xmmintrin.h>
	#include <emmintrin.h>

	// With MSVC and SSE2, we can use the __vectorcall calling convention to pass vector types and aggregates by value through registers
	// for improved code generation
	#if defined(_MSC_VER) && !defined(_M_ARM) && !defined(_MANAGED) && !defined(_M_CEE) && (!defined(_M_IX86_FP) || (_M_IX86_FP > 1)) && !defined(RTM_SIMD_CALL)
		#if ((_MSC_FULL_VER >= 170065501) && (_MSC_VER < 1800)) || (_MSC_FULL_VER >= 180020418)
			#define RTM_USE_VECTORCALL
		#endif
	#endif
#endif

#if defined(RTM_SSE3_INTRINSICS)
	#include <pmmintrin.h>
#endif

#if defined(RTM_SSE4_INTRINSICS)
	#include <smmintrin.h>
#endif

#if defined(RTM_AVX_INTRINSICS)
	#include <immintrin.h>
#endif

#if defined(RTM_NEON64_INTRINSICS) && defined(_M_ARM64)
	// MSVC specific header
	#include <arm64_neon.h>
#elif defined(RTM_NEON_INTRINSICS)
	#include <arm_neon.h>
#endif

// Specify the SIMD calling convention is we can
#if !defined(RTM_SIMD_CALL)
	#if defined(RTM_USE_VECTORCALL)
		#define RTM_SIMD_CALL __vectorcall
	#else
		#define RTM_SIMD_CALL
	#endif
#endif

// By default, we include the type definitions and error handling
#include "rtm/impl/error.h"
#include "rtm/types.h"
