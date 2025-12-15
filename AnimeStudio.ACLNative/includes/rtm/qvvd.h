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

#include "rtm/math.h"
#include "rtm/quatd.h"
#include "rtm/vector4d.h"
#include "rtm/matrix3x4d.h"
#include "rtm/version.h"
#include "rtm/impl/compiler_utils.h"
#include "rtm/impl/qvv_common.h"

RTM_IMPL_FILE_PRAGMA_PUSH

namespace rtm
{
	RTM_IMPL_VERSION_NAMESPACE_BEGIN

	//////////////////////////////////////////////////////////////////////////
	// Casts a QVV transform float32 variant to a float64 variant.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvvd RTM_SIMD_CALL qvv_cast(qvvf_arg0 input) RTM_NO_EXCEPT
	{
		return qvvd{ quat_cast(input.rotation), vector_cast(input.translation), vector_cast(input.scale) };
	}

	//////////////////////////////////////////////////////////////////////////
	// Multiplies two QVV transforms.
	// Multiplication order is as follow: local_to_world = qvv_mul(local_to_object, object_to_world)
	// NOTE: When scale is present, multiplication will not properly handle skew/shear,
	// use affine matrices if you have issues.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline qvvd qvv_mul(const qvvd& lhs, const qvvd& rhs) RTM_NO_EXCEPT
	{
		const vector4d min_scale = vector_min(lhs.scale, rhs.scale);
		const vector4d scale = vector_mul(lhs.scale, rhs.scale);

		if (vector_any_less_than3(min_scale, vector_zero()))
		{
			// If we have negative scale, we go through a matrix
			const matrix3x4d lhs_mtx = matrix_from_qvv(lhs);
			const matrix3x4d rhs_mtx = matrix_from_qvv(rhs);
			matrix3x4d result_mtx = matrix_mul(lhs_mtx, rhs_mtx);
			result_mtx = matrix_remove_scale(result_mtx);

			const vector4d sign = vector_sign(scale);
			result_mtx.x_axis = vector_mul(result_mtx.x_axis, vector_dup_x(sign));
			result_mtx.y_axis = vector_mul(result_mtx.y_axis, vector_dup_y(sign));
			result_mtx.z_axis = vector_mul(result_mtx.z_axis, vector_dup_z(sign));

			const quatd rotation = quat_from_matrix(result_mtx);
			const vector4d translation = result_mtx.w_axis;
			return qvv_set(rotation, translation, scale);
		}
		else
		{
			const quatd rotation = quat_mul(lhs.rotation, rhs.rotation);
			const vector4d translation = vector_add(quat_mul_vector3(vector_mul(lhs.translation, rhs.scale), rhs.rotation), rhs.translation);
			return qvv_set(rotation, translation, scale);
		}
	}

	//////////////////////////////////////////////////////////////////////////
	// Multiplies two QVV transforms ignoring 3D scale.
	// The resulting QVV transform will have the LHS scale.
	// Multiplication order is as follow: local_to_world = qvv_mul(local_to_object, object_to_world)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline qvvd qvv_mul_no_scale(const qvvd& lhs, const qvvd& rhs) RTM_NO_EXCEPT
	{
		const quatd rotation = quat_mul(lhs.rotation, rhs.rotation);
		const vector4d translation = vector_add(quat_mul_vector3(lhs.translation, rhs.rotation), rhs.translation);
		return qvv_set(rotation, translation, lhs.scale);
	}

	//////////////////////////////////////////////////////////////////////////
	// Multiplies a QVV transform and a 3D point.
	// Multiplication order is as follow: world_position = qvv_mul_point3(local_position, local_to_world)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline vector4d qvv_mul_point3(const vector4d& point, const qvvd& qvv) RTM_NO_EXCEPT
	{
		return vector_add(quat_mul_vector3(vector_mul(point, qvv.scale), qvv.rotation), qvv.translation);
	}

	//////////////////////////////////////////////////////////////////////////
	// Multiplies a QVV transform and a 3D point ignoring 3D scale.
	// Multiplication order is as follow: world_position = qvv_mul_point3_no_scale(local_position, local_to_world)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline vector4d qvv_mul_point3_no_scale(const vector4d& point, const qvvd& qvv) RTM_NO_EXCEPT
	{
		return vector_add(quat_mul_vector3(point, qvv.rotation), qvv.translation);
	}

	//////////////////////////////////////////////////////////////////////////
	// Returns the inverse of the input QVV transform.
	// If zero scale is contained, the result is undefined.
	// For a safe alternative, supply a fallback scale value and a threshold.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline qvvd qvv_inverse(const qvvd& input) RTM_NO_EXCEPT
	{
		const quatd inv_rotation = quat_conjugate(input.rotation);
		const vector4d inv_scale = vector_reciprocal(input.scale);
		const vector4d inv_translation = vector_neg(quat_mul_vector3(vector_mul(input.translation, inv_scale), inv_rotation));
		return qvv_set(inv_rotation, inv_translation, inv_scale);
	}

	//////////////////////////////////////////////////////////////////////////
	// Returns the inverse of the input QVV transform.
	// If the input scale has an absolute value below the supplied threshold, the
	// fallback value is used instead.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline qvvd qvv_inverse(const qvvd& input, const vector4d& fallback_scale, double threshold = 1.0E-8) RTM_NO_EXCEPT
	{
		const quatd inv_rotation = quat_conjugate(input.rotation);
		const mask4d is_scale_zero = vector_less_equal(vector_abs(input.scale), vector_set(threshold));
		const vector4d scale = vector_select(is_scale_zero, fallback_scale, input.scale);
		const vector4d inv_scale = vector_reciprocal(scale);
		const vector4d inv_translation = vector_neg(quat_mul_vector3(vector_mul(input.translation, inv_scale), inv_rotation));
		return qvv_set(inv_rotation, inv_translation, inv_scale);
	}

	//////////////////////////////////////////////////////////////////////////
	// Returns the inverse of the input QVV transform ignoring 3D scale.
	// The resulting QVV transform will have the input scale.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline qvvd qvv_inverse_no_scale(const qvvd& input) RTM_NO_EXCEPT
	{
		const quatd inv_rotation = quat_conjugate(input.rotation);
		const vector4d inv_translation = vector_neg(quat_mul_vector3(input.translation, inv_rotation));
		return qvv_set(inv_rotation, inv_translation, input.scale);
	}

	//////////////////////////////////////////////////////////////////////////
	// Returns a QVV transforms with the rotation part normalized.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvvd qvv_normalize(const qvvd& input) RTM_NO_EXCEPT
	{
		const quatd rotation = quat_normalize(input.rotation);
		return qvv_set(rotation, input.translation, input.scale);
	}

	//////////////////////////////////////////////////////////////////////////
	// Per component linear interpolation of the two inputs at the specified alpha.
	// The formula used is: ((1.0 - alpha) * start) + (alpha * end).
	// Interpolation is stable and will return 'start' when alpha is 0.0 and 'end' when it is 1.0.
	// This is the same instruction count when FMA is present but it might be slightly slower
	// due to the extra multiplication compared to: start + (alpha * (end - start)).
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvvd qvv_lerp(const qvvd& start, const qvvd& end, double alpha) RTM_NO_EXCEPT
	{
		const quatd rotation = quat_lerp(start.rotation, end.rotation, alpha);
		const vector4d translation = vector_lerp(start.translation, end.translation, alpha);
		const vector4d scale = vector_lerp(start.scale, end.scale, alpha);
		return qvv_set(rotation, translation, scale);
	}

#if defined(RTM_SSE2_INTRINSICS)
	//////////////////////////////////////////////////////////////////////////
	// Per component linear interpolation of the two inputs at the specified alpha.
	// The formula used is: ((1.0 - alpha) * start) + (alpha * end).
	// Interpolation is stable and will return 'start' when alpha is 0.0 and 'end' when it is 1.0.
	// This is the same instruction count when FMA is present but it might be slightly slower
	// due to the extra multiplication compared to: start + (alpha * (end - start)).
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvvd qvv_lerp(const qvvd& start, const qvvd& end, const scalard& alpha) RTM_NO_EXCEPT
	{
		const quatd rotation = quat_lerp(start.rotation, end.rotation, alpha);
		const vector4d translation = vector_lerp(start.translation, end.translation, alpha);
		const vector4d scale = vector_lerp(start.scale, end.scale, alpha);
		return qvv_set(rotation, translation, scale);
	}
#endif

	//////////////////////////////////////////////////////////////////////////
	// Per component linear interpolation of the two inputs at the specified alpha.
	// The formula used is: ((1.0 - alpha) * start) + (alpha * end).
	// Interpolation is stable and will return 'start' when alpha is 0.0 and 'end' when it is 1.0.
	// This is the same instruction count when FMA is present but it might be slightly slower
	// due to the extra multiplication compared to: start + (alpha * (end - start)).
	// The resulting QVV transform will have the start scale.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvvd qvv_lerp_no_scale(const qvvd& start, const qvvd& end, double alpha) RTM_NO_EXCEPT
	{
		const quatd rotation = quat_lerp(start.rotation, end.rotation, alpha);
		const vector4d translation = vector_lerp(start.translation, end.translation, alpha);
		return qvv_set(rotation, translation, start.scale);
	}

#if defined(RTM_SSE2_INTRINSICS)
	//////////////////////////////////////////////////////////////////////////
	// Per component linear interpolation of the two inputs at the specified alpha.
	// The formula used is: ((1.0 - alpha) * start) + (alpha * end).
	// Interpolation is stable and will return 'start' when alpha is 0.0 and 'end' when it is 1.0.
	// This is the same instruction count when FMA is present but it might be slightly slower
	// due to the extra multiplication compared to: start + (alpha * (end - start)).
	// The resulting QVV transform will have the start scale.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvvd qvv_lerp_no_scale(const qvvd& start, const qvvd& end, const scalard& alpha) RTM_NO_EXCEPT
	{
		const quatd rotation = quat_lerp(start.rotation, end.rotation, alpha);
		const vector4d translation = vector_lerp(start.translation, end.translation, alpha);
		return qvv_set(rotation, translation, start.scale);
	}
#endif

	//////////////////////////////////////////////////////////////////////////
	// Per component spherical interpolation of the two inputs at the specified alpha.
	// See quat_slerp(..)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvvd qvv_slerp(const qvvd& start, const qvvd& end, double alpha) RTM_NO_EXCEPT
	{
		const quatd rotation = quat_slerp(start.rotation, end.rotation, alpha);
		const vector4d translation = vector_lerp(start.translation, end.translation, alpha);
		const vector4d scale = vector_lerp(start.scale, end.scale, alpha);
		return qvv_set(rotation, translation, scale);
	}

#if defined(RTM_SSE2_INTRINSICS)
	//////////////////////////////////////////////////////////////////////////
	// Per component spherical interpolation of the two inputs at the specified alpha.
	// See quat_slerp(..)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvvd qvv_slerp(const qvvd& start, const qvvd& end, const scalard& alpha) RTM_NO_EXCEPT
	{
		const quatd rotation = quat_slerp(start.rotation, end.rotation, alpha);
		const vector4d translation = vector_lerp(start.translation, end.translation, alpha);
		const vector4d scale = vector_lerp(start.scale, end.scale, alpha);
		return qvv_set(rotation, translation, scale);
	}
#endif

	//////////////////////////////////////////////////////////////////////////
	// Per component spherical interpolation of the two inputs at the specified alpha.
	// See quat_slerp(..)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvvd qvv_slerp_no_scale(const qvvd& start, const qvvd& end, double alpha) RTM_NO_EXCEPT
	{
		const quatd rotation = quat_slerp(start.rotation, end.rotation, alpha);
		const vector4d translation = vector_lerp(start.translation, end.translation, alpha);
		return qvv_set(rotation, translation, start.scale);
	}

#if defined(RTM_SSE2_INTRINSICS)
	//////////////////////////////////////////////////////////////////////////
	// Per component spherical interpolation of the two inputs at the specified alpha.
	// See quat_slerp(..)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvvd qvv_slerp_no_scale(const qvvd& start, const qvvd& end, const scalard& alpha) RTM_NO_EXCEPT
	{
		const quatd rotation = quat_slerp(start.rotation, end.rotation, alpha);
		const vector4d translation = vector_lerp(start.translation, end.translation, alpha);
		return qvv_set(rotation, translation, start.scale);
	}
#endif

	//////////////////////////////////////////////////////////////////////////
	// Returns true if the input QVV does not contain any NaN or Inf, otherwise false.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE bool qvv_is_finite(const qvvd& input) RTM_NO_EXCEPT
	{
		return quat_is_finite(input.rotation) && vector_is_finite3(input.translation) && vector_is_finite3(input.scale);
	}

	RTM_IMPL_VERSION_NAMESPACE_END
}

RTM_IMPL_FILE_PRAGMA_POP
