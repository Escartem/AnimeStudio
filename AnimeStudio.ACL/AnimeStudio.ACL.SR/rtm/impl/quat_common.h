#pragma once

////////////////////////////////////////////////////////////////////////////////
// The MIT License (MIT)
//
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

#include "rtm/math.h"
#include "rtm/impl/compiler_utils.h"

RTM_IMPL_FILE_PRAGMA_PUSH

namespace rtm
{
	//////////////////////////////////////////////////////////////////////////
	// Creates a quaternion from all 4 components.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline quatf RTM_SIMD_CALL quat_set(float x, float y, float z, float w) RTM_NO_EXCEPT
	{
#if defined(RTM_SSE2_INTRINSICS)
		return _mm_set_ps(w, z, y, x);
#elif defined(RTM_NEON_INTRINSICS)
#if 1
		float32x2_t V0 = vcreate_f32(((uint64_t)*(const uint32_t*)&x) | ((uint64_t)(*(const uint32_t*)&y) << 32));
		float32x2_t V1 = vcreate_f32(((uint64_t)*(const uint32_t*)&z) | ((uint64_t)(*(const uint32_t*)&w) << 32));
		return vcombine_f32(V0, V1);
#else
		float __attribute__((aligned(16))) data[4] = { x, y, z, w };
		return vld1q_f32(data);
#endif
#else
		return quatf{ x, y, z, w };
#endif
	}

	//////////////////////////////////////////////////////////////////////////
	// Creates a quaternion from all 4 components.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline quatd RTM_SIMD_CALL quat_set(double x, double y, double z, double w) RTM_NO_EXCEPT
	{
#if defined(RTM_SSE2_INTRINSICS)
		return quatd{ _mm_set_pd(y, x), _mm_set_pd(w, z) };
#else
		return quatd{ x, y, z, w };
#endif
	}

	namespace rtm_impl
	{
		//////////////////////////////////////////////////////////////////////////
		// This is a helper struct to allow a single consistent API between
		// various quaternion types when the semantics are identical but the return
		// type differs. Implicit coercion is used to return the desired value
		// at the call site.
		//////////////////////////////////////////////////////////////////////////
		struct quat_identity_impl
		{
			RTM_DISABLE_SECURITY_COOKIE_CHECK inline RTM_SIMD_CALL operator quatd() const RTM_NO_EXCEPT
			{
				return quat_set(0.0, 0.0, 0.0, 1.0);
			}

			RTM_DISABLE_SECURITY_COOKIE_CHECK inline RTM_SIMD_CALL operator quatf() const RTM_NO_EXCEPT
			{
				return quat_set(0.0F, 0.0F, 0.0F, 1.0F);
			}
		};
	}

	//////////////////////////////////////////////////////////////////////////
	// Returns the identity quaternion.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK constexpr rtm_impl::quat_identity_impl RTM_SIMD_CALL quat_identity() RTM_NO_EXCEPT
	{
		return rtm_impl::quat_identity_impl();
	}
}

RTM_IMPL_FILE_PRAGMA_POP
