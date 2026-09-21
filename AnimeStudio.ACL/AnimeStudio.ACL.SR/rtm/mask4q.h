#pragma once

////////////////////////////////////////////////////////////////////////////////
// The MIT License (MIT)
//
// Copyright (c) 2019 Nicholas Frechette & Realtime Math contributors
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

#include "rtm/math.h"
#include "rtm/impl/compiler_utils.h"
#include "rtm/impl/mask_common.h"

RTM_IMPL_FILE_PRAGMA_PUSH

namespace rtm
{
	//////////////////////////////////////////////////////////////////////////
	// Returns the mask4q [x] component.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline uint64_t RTM_SIMD_CALL mask_get_x(const mask4q& input) RTM_NO_EXCEPT
	{
#if defined(RTM_SSE2_INTRINSICS)
#if defined(_M_X64)
		return _mm_cvtsi128_si64(input.xy);
#else
		// Just sign extend on 32bit systems
		return (uint64_t)_mm_cvtsi128_si32(input.xy);
#endif
#else
		return input.x;
#endif
	}

	//////////////////////////////////////////////////////////////////////////
	// Returns the mask4q [y] component.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline uint64_t RTM_SIMD_CALL mask_get_y(const mask4q& input) RTM_NO_EXCEPT
	{
#if defined(RTM_SSE2_INTRINSICS)
#if defined(_M_X64)
		return _mm_cvtsi128_si64(_mm_shuffle_epi32(input.xy, _MM_SHUFFLE(3, 2, 3, 2)));
#else
		// Just sign extend on 32bit systems
		return (uint64_t)_mm_cvtsi128_si32(_mm_shuffle_epi32(input.xy, _MM_SHUFFLE(3, 2, 3, 2)));
#endif
#else
		return input.y;
#endif
	}

	//////////////////////////////////////////////////////////////////////////
	// Returns the mask4q [z] component.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline uint64_t RTM_SIMD_CALL mask_get_z(const mask4q& input) RTM_NO_EXCEPT
	{
#if defined(RTM_SSE2_INTRINSICS)
#if defined(_M_X64)
		return _mm_cvtsi128_si64(input.zw);
#else
		// Just sign extend on 32bit systems
		return (uint64_t)_mm_cvtsi128_si32(input.zw);
#endif
#else
		return input.z;
#endif
	}

	//////////////////////////////////////////////////////////////////////////
	// Returns the mask4q [w] component.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline uint64_t RTM_SIMD_CALL mask_get_w(const mask4q& input) RTM_NO_EXCEPT
	{
#if defined(RTM_SSE2_INTRINSICS)
#if defined(_M_X64)
		return _mm_cvtsi128_si64(_mm_shuffle_epi32(input.zw, _MM_SHUFFLE(3, 2, 3, 2)));
#else
		// Just sign extend on 32bit systems
		return (uint64_t)_mm_cvtsi128_si32(_mm_shuffle_epi32(input.zw, _MM_SHUFFLE(3, 2, 3, 2)));
#endif
#else
		return input.w;
#endif
	}

	//////////////////////////////////////////////////////////////////////////
	// Returns true if all 4 components are true, otherwise false: all(input != 0)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline bool RTM_SIMD_CALL mask_all_true(const mask4q& input) RTM_NO_EXCEPT
	{
#if defined(RTM_SSE2_INTRINSICS)
		return (_mm_movemask_epi8(input.xy) & _mm_movemask_epi8(input.zw)) == 0xFFFF;
#else
		return input.x != 0 && input.y != 0 && input.z != 0 && input.w != 0;
#endif
	}

	//////////////////////////////////////////////////////////////////////////
	// Returns true if all [xy] components are true, otherwise false: all(input != 0)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline bool RTM_SIMD_CALL mask_all_true2(const mask4q& input) RTM_NO_EXCEPT
	{
#if defined(RTM_SSE2_INTRINSICS)
		return _mm_movemask_epi8(input.xy) == 0xFFFF;
#else
		return input.x != 0 && input.y != 0;
#endif
	}

	//////////////////////////////////////////////////////////////////////////
	// Returns true if all [xyz] components are true, otherwise false: all(input != 0)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline bool RTM_SIMD_CALL mask_all_true3(const mask4q& input) RTM_NO_EXCEPT
	{
#if defined(RTM_SSE2_INTRINSICS)
		return _mm_movemask_epi8(input.xy) == 0xFFFF && (_mm_movemask_epi8(input.zw) & 0x00FF) == 0x00FF;
#else
		return input.x != 0 && input.y != 0 && input.z != 0;
#endif
	}

	//////////////////////////////////////////////////////////////////////////
	// Returns true if any 4 components are true, otherwise false: any(input != 0)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline bool RTM_SIMD_CALL mask_any_true(const mask4q& input) RTM_NO_EXCEPT
	{
#if defined(RTM_SSE2_INTRINSICS)
		return (_mm_movemask_epi8(input.xy) | _mm_movemask_epi8(input.zw)) != 0;
#else
		return input.x != 0 || input.y != 0 || input.z != 0 || input.w != 0;
#endif
	}

	//////////////////////////////////////////////////////////////////////////
	// Returns true if any [xy] components are true, otherwise false: any(input != 0)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline bool RTM_SIMD_CALL mask_any_true2(const mask4q& input) RTM_NO_EXCEPT
	{
#if defined(RTM_SSE2_INTRINSICS)
		return _mm_movemask_epi8(input.xy) != 0;
#else
		return input.x != 0 || input.y != 0;
#endif
	}

	//////////////////////////////////////////////////////////////////////////
	// Returns true if any [xyz] components are true, otherwise false: any(input != 0)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline bool RTM_SIMD_CALL mask_any_true3(const mask4q& input) RTM_NO_EXCEPT
	{
#if defined(RTM_SSE2_INTRINSICS)
		return _mm_movemask_epi8(input.xy) != 0 || (_mm_movemask_epi8(input.zw) & 0x00FF) != 0;
#else
		return input.x != 0 || input.y != 0 || input.z != 0;
#endif
	}
}

RTM_IMPL_FILE_PRAGMA_POP
