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
#include "rtm/quatf.h"
#include "rtm/vector4f.h"
#include "rtm/version.h"
#include "rtm/impl/compiler_utils.h"
#include "rtm/impl/qvs_common.h"

RTM_IMPL_FILE_PRAGMA_PUSH

namespace rtm
{
	RTM_IMPL_VERSION_NAMESPACE_BEGIN

	//////////////////////////////////////////////////////////////////////////
	// Casts a QVS transform float64 variant to a float32 variant.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvsf RTM_SIMD_CALL qvs_cast(const qvsd& input) RTM_NO_EXCEPT
	{
		return qvsf{ quat_cast(input.rotation), vector_cast(input.translation_scale) };
	}

	//////////////////////////////////////////////////////////////////////////
	// Returns the rotation part of a QVS transform.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE quatf RTM_SIMD_CALL qvs_get_rotation(qvsf_arg0 input) RTM_NO_EXCEPT
	{
		return input.rotation;
	}

	//////////////////////////////////////////////////////////////////////////
	// Returns the translation part of a QVS transform.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE vector4f RTM_SIMD_CALL qvs_get_translation(qvsf_arg0 input) RTM_NO_EXCEPT
	{
		return input.translation_scale;
	}

	//////////////////////////////////////////////////////////////////////////
	// Returns the scale part of a QVS transform.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE constexpr rtm_impl::vector4f_vector_get_w RTM_SIMD_CALL qvs_get_scale(qvsf_arg0 input) RTM_NO_EXCEPT
	{
		return rtm_impl::vector4f_vector_get_w{ input.translation_scale };
	}

	//////////////////////////////////////////////////////////////////////////
	// Multiplies two QVS transforms.
	// Multiplication order is as follow: local_to_world = qvs_mul(local_to_object, object_to_world)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline qvsf RTM_SIMD_CALL qvs_mul(qvsf_arg0 lhs, qvsf_arg1 rhs) RTM_NO_EXCEPT
	{
		const vector4f rhs_scale = vector_dup_w(rhs.translation_scale);

		const quatf rotation = quat_mul(lhs.rotation, rhs.rotation);
		const vector4f translation = vector_add(quat_mul_vector3(vector_mul(lhs.translation_scale, rhs_scale), rhs.rotation), rhs.translation_scale);
		const vector4f scale_w = vector_mul(lhs.translation_scale, rhs.translation_scale);
		const vector4f translation_scale = vector_mix<mix4::x, mix4::y, mix4::z, mix4::d>(translation, scale_w);

		return qvsf{ rotation, translation_scale };
	}

	//////////////////////////////////////////////////////////////////////////
	// Multiplies two QVS transforms ignoring scale.
	// The resulting QVS transform will have the LHS scale.
	// Multiplication order is as follow: local_to_world = qvs_mul(local_to_object, object_to_world)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline qvsf RTM_SIMD_CALL qvs_mul_no_scale(qvsf_arg0 lhs, qvsf_arg1 rhs) RTM_NO_EXCEPT
	{
		const quatf rotation = quat_mul(lhs.rotation, rhs.rotation);
		const vector4f translation = vector_add(quat_mul_vector3(lhs.translation_scale, rhs.rotation), rhs.translation_scale);
		const vector4f translation_scale = vector_mix<mix4::x, mix4::y, mix4::z, mix4::d>(translation, lhs.translation_scale);

		return qvsf{ rotation, translation_scale };
	}

	//////////////////////////////////////////////////////////////////////////
	// Multiplies a QVS transform and a 3D point.
	// Multiplication order is as follow: world_position = qvs_mul_point3(local_position, local_to_world)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline vector4f RTM_SIMD_CALL qvs_mul_point3(vector4f_arg0 point, qvsf_arg1 qvs) RTM_NO_EXCEPT
	{
		const vector4f scale = vector_dup_w(qvs.translation_scale);
		return vector_add(quat_mul_vector3(vector_mul(scale, point), qvs.rotation), qvs.translation_scale);
	}

	//////////////////////////////////////////////////////////////////////////
	// Multiplies a QVS transform and a 3D point ignoring 3D scale.
	// Multiplication order is as follow: world_position = qvs_mul_point3_no_scale(local_position, local_to_world)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline vector4f RTM_SIMD_CALL qvs_mul_point3_no_scale(vector4f_arg0 point, qvsf_arg1 qvs) RTM_NO_EXCEPT
	{
		return vector_add(quat_mul_vector3(point, qvs.rotation), qvs.translation_scale);
	}

	//////////////////////////////////////////////////////////////////////////
	// Returns the inverse of the input QVS transform.
	// If zero scale is contained, the result is undefined.
	// For a safe alternative, supply a fallback scale value and a threshold.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline qvsf RTM_SIMD_CALL qvs_inverse(qvsf_arg0 input) RTM_NO_EXCEPT
	{
		const quatf inv_rotation = quat_conjugate(input.rotation);
		const vector4f inv_scale_w = vector_reciprocal(input.translation_scale);
		const vector4f inv_scale = vector_dup_w(inv_scale_w);
		const vector4f inv_translation = vector_neg(quat_mul_vector3(vector_mul(input.translation_scale, inv_scale), inv_rotation));
		const vector4f inv_translation_scale = vector_mix<mix4::x, mix4::y, mix4::z, mix4::d>(inv_translation, inv_scale_w);

		return qvsf{ inv_rotation, inv_translation_scale };
	}

	//////////////////////////////////////////////////////////////////////////
	// Returns the inverse of the input QVS transform.
	// If the input scale has an absolute value below the supplied threshold, the
	// fallback value is used instead.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline qvsf RTM_SIMD_CALL qvs_inverse(qvsf_arg0 input, float fallback_scale, float threshold = 1.0E-8F) RTM_NO_EXCEPT
	{
		const quatf inv_rotation = quat_conjugate(input.rotation);
		const mask4f is_scale_w_zero = vector_less_equal(vector_abs(input.translation_scale), vector_set(threshold));
		const vector4f scale_w = vector_select(is_scale_w_zero, vector_set(fallback_scale), input.translation_scale);
		const vector4f inv_scale_w = vector_reciprocal(scale_w);
		const vector4f inv_scale = vector_dup_w(inv_scale_w);
		const vector4f inv_translation = vector_neg(quat_mul_vector3(vector_mul(input.translation_scale, inv_scale), inv_rotation));
		const vector4f inv_translation_scale = vector_mix<mix4::x, mix4::y, mix4::z, mix4::d>(inv_translation, inv_scale_w);

		return qvsf{ inv_rotation, inv_translation_scale };
	}

	//////////////////////////////////////////////////////////////////////////
	// Returns the inverse of the input QVS transform ignoring 3D scale.
	// The resulting QVS transform will have the input scale.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline qvsf RTM_SIMD_CALL qvs_inverse_no_scale(qvsf_arg0 input) RTM_NO_EXCEPT
	{
		const quatf inv_rotation = quat_conjugate(input.rotation);
		const vector4f inv_translation = vector_neg(quat_mul_vector3(input.translation_scale, inv_rotation));
		const vector4f inv_translation_scale = vector_mix<mix4::x, mix4::y, mix4::z, mix4::d>(inv_translation, input.translation_scale);

		return qvsf{ inv_rotation, inv_translation_scale };
	}

	//////////////////////////////////////////////////////////////////////////
	// Returns a QVS transforms with the rotation part normalized.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvsf RTM_SIMD_CALL qvs_normalize(qvsf_arg0 input) RTM_NO_EXCEPT
	{
		const quatf rotation = quat_normalize(input.rotation);
		return qvsf{ rotation, input.translation_scale };
	}

	//////////////////////////////////////////////////////////////////////////
	// Per component linear interpolation of the two inputs at the specified alpha.
	// The formula used is: ((1.0 - alpha) * start) + (alpha * end).
	// Interpolation is stable and will return 'start' when alpha is 0.0 and 'end' when it is 1.0.
	// This is the same instruction count when FMA is present but it might be slightly slower
	// due to the extra multiplication compared to: start + (alpha * (end - start)).
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvsf RTM_SIMD_CALL qvs_lerp(qvsf_arg0 start, qvsf_arg1 end, float alpha) RTM_NO_EXCEPT
	{
		const quatf rotation = quat_lerp(start.rotation, end.rotation, alpha);
		const vector4f translation_scale = vector_lerp(start.translation_scale, end.translation_scale, alpha);
		return qvsf{ rotation, translation_scale };
	}

#if defined(RTM_SSE2_INTRINSICS)
	//////////////////////////////////////////////////////////////////////////
	// Per component linear interpolation of the two inputs at the specified alpha.
	// The formula used is: ((1.0 - alpha) * start) + (alpha * end).
	// Interpolation is stable and will return 'start' when alpha is 0.0 and 'end' when it is 1.0.
	// This is the same instruction count when FMA is present but it might be slightly slower
	// due to the extra multiplication compared to: start + (alpha * (end - start)).
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvsf RTM_SIMD_CALL qvs_lerp(qvsf_arg0 start, qvsf_arg1 end, scalarf_arg2 alpha) RTM_NO_EXCEPT
	{
		const quatf rotation = quat_lerp(start.rotation, end.rotation, alpha);
		const vector4f translation_scale = vector_lerp(start.translation_scale, end.translation_scale, alpha);
		return qvsf{ rotation, translation_scale };
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
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvsf RTM_SIMD_CALL qvs_lerp_no_scale(qvsf_arg0 start, qvsf_arg1 end, float alpha) RTM_NO_EXCEPT
	{
		const quatf rotation = quat_lerp(start.rotation, end.rotation, alpha);
		const vector4f translation_scale_lerp = vector_lerp(start.translation_scale, end.translation_scale, alpha);
		const vector4f translation_scale = vector_mix<mix4::x, mix4::y, mix4::z, mix4::d>(translation_scale_lerp, start.translation_scale);
		return qvsf{ rotation, translation_scale };
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
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvsf RTM_SIMD_CALL qvs_lerp_no_scale(qvsf_arg0 start, qvsf_arg1 end, scalarf_arg2 alpha) RTM_NO_EXCEPT
	{
		const quatf rotation = quat_lerp(start.rotation, end.rotation, alpha);
		const vector4f translation_scale_lerp = vector_lerp(start.translation_scale, end.translation_scale, alpha);
		const vector4f translation_scale = vector_mix<mix4::x, mix4::y, mix4::z, mix4::d>(translation_scale_lerp, start.translation_scale);
		return qvsf{ rotation, translation_scale };
	}
#endif

	//////////////////////////////////////////////////////////////////////////
	// Per component spherical interpolation of the two inputs at the specified alpha.
	// See quat_slerp(..)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvsf RTM_SIMD_CALL qvs_slerp(qvsf_arg0 start, qvsf_arg1 end, float alpha) RTM_NO_EXCEPT
	{
		const quatf rotation = quat_slerp(start.rotation, end.rotation, alpha);
		const vector4f translation_scale = vector_lerp(start.translation_scale, end.translation_scale, alpha);
		return qvsf{ rotation, translation_scale };
	}

#if defined(RTM_SSE2_INTRINSICS)
	//////////////////////////////////////////////////////////////////////////
	// Per component spherical interpolation of the two inputs at the specified alpha.
	// See quat_slerp(..)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvsf RTM_SIMD_CALL qvs_slerp(qvsf_arg0 start, qvsf_arg1 end, scalarf_arg2 alpha) RTM_NO_EXCEPT
	{
		const quatf rotation = quat_slerp(start.rotation, end.rotation, alpha);
		const vector4f translation_scale = vector_lerp(start.translation_scale, end.translation_scale, alpha);
		return qvsf{ rotation, translation_scale };
	}
#endif

	//////////////////////////////////////////////////////////////////////////
	// Per component spherical interpolation of the two inputs at the specified alpha.
	// See quat_slerp(..)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvsf RTM_SIMD_CALL qvs_slerp_no_scale(qvsf_arg0 start, qvsf_arg1 end, float alpha) RTM_NO_EXCEPT
	{
		const quatf rotation = quat_slerp(start.rotation, end.rotation, alpha);
		const vector4f translation_scale_lerp = vector_lerp(start.translation_scale, end.translation_scale, alpha);
		const vector4f translation_scale = vector_mix<mix4::x, mix4::y, mix4::z, mix4::d>(translation_scale_lerp, start.translation_scale);
		return qvsf{ rotation, translation_scale };
	}

#if defined(RTM_SSE2_INTRINSICS)
	//////////////////////////////////////////////////////////////////////////
	// Per component spherical interpolation of the two inputs at the specified alpha.
	// See quat_slerp(..)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvsf RTM_SIMD_CALL qvs_slerp_no_scale(qvsf_arg0 start, qvsf_arg1 end, scalarf_arg2 alpha) RTM_NO_EXCEPT
	{
		const quatf rotation = quat_slerp(start.rotation, end.rotation, alpha);
		const vector4f translation_scale_lerp = vector_lerp(start.translation_scale, end.translation_scale, alpha);
		const vector4f translation_scale = vector_mix<mix4::x, mix4::y, mix4::z, mix4::d>(translation_scale_lerp, start.translation_scale);
		return qvsf{ rotation, translation_scale };
	}
#endif

	//////////////////////////////////////////////////////////////////////////
	// Returns true if the input QVS does not contain any NaN or Inf, otherwise false.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE bool RTM_SIMD_CALL qvs_is_finite(qvsf_arg0 input) RTM_NO_EXCEPT
	{
		return quat_is_finite(input.rotation) && vector_is_finite(input.translation_scale);
	}

	RTM_IMPL_VERSION_NAMESPACE_END
}

RTM_IMPL_FILE_PRAGMA_POP
