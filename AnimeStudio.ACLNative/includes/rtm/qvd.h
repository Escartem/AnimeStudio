#pragma once

////////////////////////////////////////////////////////////////////////////////
// The MIT License (MIT)
//
// Copyright (c) 2023 Nicholas Frechette & Realtime Math contributors
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
#include "rtm/quatd.h"
#include "rtm/vector4d.h"
#include "rtm/matrix3x4d.h"
#include "rtm/version.h"
#include "rtm/impl/compiler_utils.h"
#include "rtm/impl/qv_common.h"

RTM_IMPL_FILE_PRAGMA_PUSH

namespace rtm
{
	RTM_IMPL_VERSION_NAMESPACE_BEGIN

	//////////////////////////////////////////////////////////////////////////
	// Casts a QV transform float32 variant to a float64 variant.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvd qv_cast(const qvf& input) RTM_NO_EXCEPT
	{
		return qvd{ quat_cast(input.rotation), vector_cast(input.translation) };
	}

	//////////////////////////////////////////////////////////////////////////
	// Multiplies two QV transforms.
	// Multiplication order is as follow: local_to_world = qv_mul(local_to_object, object_to_world)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline qvd qv_mul(const qvd& lhs, const qvd& rhs) RTM_NO_EXCEPT
	{
		const quatd rotation = quat_mul(lhs.rotation, rhs.rotation);
		const vector4d translation = vector_add(quat_mul_vector3(lhs.translation, rhs.rotation), rhs.translation);
		return qv_set(rotation, translation);
	}

	//////////////////////////////////////////////////////////////////////////
	// Multiplies a QV transform and a 3D point.
	// Multiplication order is as follow: world_position = qv_mul_point3(local_position, local_to_world)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline vector4d qv_mul_point3(const vector4d& point, const qvd& qv) RTM_NO_EXCEPT
	{
		return vector_add(quat_mul_vector3(point, qv.rotation), qv.translation);
	}

	//////////////////////////////////////////////////////////////////////////
	// Returns the inverse of the input QV transform.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline qvd qv_inverse(const qvd& input) RTM_NO_EXCEPT
	{
		const quatd inv_rotation = quat_conjugate(input.rotation);
		const vector4d inv_translation = vector_neg(quat_mul_vector3(input.translation, inv_rotation));
		return qv_set(inv_rotation, inv_translation);
	}

	//////////////////////////////////////////////////////////////////////////
	// Returns a QV transforms with the rotation part normalized.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvd qv_normalize(const qvd& input) RTM_NO_EXCEPT
	{
		const quatd rotation = quat_normalize(input.rotation);
		return qv_set(rotation, input.translation);
	}

	//////////////////////////////////////////////////////////////////////////
	// Per component linear interpolation of the two inputs at the specified alpha.
	// The formula used is: ((1.0 - alpha) * start) + (alpha * end).
	// Interpolation is stable and will return 'start' when alpha is 0.0 and 'end' when it is 1.0.
	// This is the same instruction count when FMA is present but it might be slightly slower
	// due to the extra multiplication compared to: start + (alpha * (end - start)).
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvd qv_lerp(const qvd& start, const qvd& end, double alpha) RTM_NO_EXCEPT
	{
		const quatd rotation = quat_lerp(start.rotation, end.rotation, alpha);
		const vector4d translation = vector_lerp(start.translation, end.translation, alpha);
		return qv_set(rotation, translation);
	}

#if defined(RTM_SSE2_INTRINSICS)
	//////////////////////////////////////////////////////////////////////////
	// Per component linear interpolation of the two inputs at the specified alpha.
	// The formula used is: ((1.0 - alpha) * start) + (alpha * end).
	// Interpolation is stable and will return 'start' when alpha is 0.0 and 'end' when it is 1.0.
	// This is the same instruction count when FMA is present but it might be slightly slower
	// due to the extra multiplication compared to: start + (alpha * (end - start)).
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvd qv_lerp(const qvd& start, const qvd& end, const scalard& alpha) RTM_NO_EXCEPT
	{
		const quatd rotation = quat_lerp(start.rotation, end.rotation, alpha);
		const vector4d translation = vector_lerp(start.translation, end.translation, alpha);
		return qv_set(rotation, translation);
	}
#endif

	//////////////////////////////////////////////////////////////////////////
	// Per component spherical interpolation of the two inputs at the specified alpha.
	// See quat_slerp(..)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvd qv_slerp(const qvd& start, const qvd& end, double alpha) RTM_NO_EXCEPT
	{
		const quatd rotation = quat_slerp(start.rotation, end.rotation, alpha);
		const vector4d translation = vector_lerp(start.translation, end.translation, alpha);
		return qv_set(rotation, translation);
	}

#if defined(RTM_SSE2_INTRINSICS)
	//////////////////////////////////////////////////////////////////////////
	// Per component spherical interpolation of the two inputs at the specified alpha.
	// See quat_slerp(..)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvd qv_slerp(const qvd& start, const qvd& end, const scalard& alpha) RTM_NO_EXCEPT
	{
		const quatd rotation = quat_slerp(start.rotation, end.rotation, alpha);
		const vector4d translation = vector_lerp(start.translation, end.translation, alpha);
		return qv_set(rotation, translation);
	}
#endif

	//////////////////////////////////////////////////////////////////////////
	// Returns true if the input QV does not contain any NaN or Inf, otherwise false.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE bool qv_is_finite(const qvd& input) RTM_NO_EXCEPT
	{
		return quat_is_finite(input.rotation) && vector_is_finite3(input.translation);
	}

	RTM_IMPL_VERSION_NAMESPACE_END
}

RTM_IMPL_FILE_PRAGMA_POP
