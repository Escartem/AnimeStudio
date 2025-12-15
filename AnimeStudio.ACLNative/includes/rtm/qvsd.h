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
#include "rtm/version.h"
#include "rtm/impl/compiler_utils.h"
#include "rtm/impl/qvs_common.h"

RTM_IMPL_FILE_PRAGMA_PUSH

namespace rtm
{
	RTM_IMPL_VERSION_NAMESPACE_BEGIN

	//////////////////////////////////////////////////////////////////////////
	// Casts a QVS transform float32 variant to a float64 variant.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvsd RTM_SIMD_CALL qvs_cast(qvsf_arg0 input) RTM_NO_EXCEPT
	{
		return qvsd{ quat_cast(input.rotation), vector_cast(input.translation_scale) };
	}

	//////////////////////////////////////////////////////////////////////////
	// Returns the rotation part of a QVS transform.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE quatd qvs_get_rotation(const qvsd& input) RTM_NO_EXCEPT
	{
		return input.rotation;
	}

	//////////////////////////////////////////////////////////////////////////
	// Returns the translation part of a QVS transform.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE vector4d qvs_get_translation(const qvsd& input) RTM_NO_EXCEPT
	{
		return input.translation_scale;
	}

	//////////////////////////////////////////////////////////////////////////
	// Returns the scale part of a QVS transform.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE constexpr rtm_impl::vector4d_vector_get_w qvs_get_scale(const qvsd& input) RTM_NO_EXCEPT
	{
		return rtm_impl::vector4d_vector_get_w{ input.translation_scale };
	}

	//////////////////////////////////////////////////////////////////////////
	// Multiplies two QVS transforms.
	// Multiplication order is as follow: local_to_world = qvs_mul(local_to_object, object_to_world)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline qvsd qvs_mul(const qvsd& lhs, const qvsd& rhs) RTM_NO_EXCEPT
	{
		const vector4d rhs_scale = vector_dup_w(rhs.translation_scale);

		const quatd rotation = quat_mul(lhs.rotation, rhs.rotation);
		const vector4d translation = vector_add(quat_mul_vector3(vector_mul(lhs.translation_scale, rhs_scale), rhs.rotation), rhs.translation_scale);
		const vector4d scale_w = vector_mul(lhs.translation_scale, rhs.translation_scale);
		const vector4d translation_scale = vector_mix<mix4::x, mix4::y, mix4::z, mix4::d>(translation, scale_w);

		return qvsd{ rotation, translation_scale };
	}

	//////////////////////////////////////////////////////////////////////////
	// Multiplies two QVS transforms ignoring scale.
	// The resulting QVS transform will have the LHS scale.
	// Multiplication order is as follow: local_to_world = qvs_mul(local_to_object, object_to_world)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline qvsd qvs_mul_no_scale(const qvsd& lhs, const qvsd& rhs) RTM_NO_EXCEPT
	{
		const quatd rotation = quat_mul(lhs.rotation, rhs.rotation);
		const vector4d translation = vector_add(quat_mul_vector3(lhs.translation_scale, rhs.rotation), rhs.translation_scale);
		const vector4d translation_scale = vector_mix<mix4::x, mix4::y, mix4::z, mix4::d>(translation, lhs.translation_scale);

		return qvsd{ rotation, translation_scale };
	}

	//////////////////////////////////////////////////////////////////////////
	// Multiplies a QVS transform and a 3D point.
	// Multiplication order is as follow: world_position = qvs_mul_point3(local_position, local_to_world)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline vector4d qvs_mul_point3(const vector4d& point, const qvsd& qvs) RTM_NO_EXCEPT
	{
		const vector4d scale = vector_dup_w(qvs.translation_scale);
		return vector_add(quat_mul_vector3(vector_mul(scale, point), qvs.rotation), qvs.translation_scale);
	}

	//////////////////////////////////////////////////////////////////////////
	// Multiplies a QVS transform and a 3D point ignoring 3D scale.
	// Multiplication order is as follow: world_position = qvs_mul_point3_no_scale(local_position, local_to_world)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline vector4d qvs_mul_point3_no_scale(const vector4d& point, const qvsd& qvs) RTM_NO_EXCEPT
	{
		return vector_add(quat_mul_vector3(point, qvs.rotation), qvs.translation_scale);
	}

	//////////////////////////////////////////////////////////////////////////
	// Returns the inverse of the input QVS transform.
	// If zero scale is contained, the result is undefined.
	// For a safe alternative, supply a fallback scale value and a threshold.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline qvsd qvs_inverse(const qvsd& input) RTM_NO_EXCEPT
	{
		const quatd inv_rotation = quat_conjugate(input.rotation);
		const vector4d inv_scale_w = vector_reciprocal(input.translation_scale);
		const vector4d inv_scale = vector_dup_w(inv_scale_w);
		const vector4d inv_translation = vector_neg(quat_mul_vector3(vector_mul(input.translation_scale, inv_scale), inv_rotation));
		const vector4d inv_translation_scale = vector_mix<mix4::x, mix4::y, mix4::z, mix4::d>(inv_translation, inv_scale_w);

		return qvsd{ inv_rotation, inv_translation_scale };
	}

	//////////////////////////////////////////////////////////////////////////
	// Returns the inverse of the input QVS transform.
	// If the input scale has an absolute value below the supplied threshold, the
	// fallback value is used instead.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline qvsd qvs_inverse(const qvsd& input, double fallback_scale, double threshold = 1.0E-8) RTM_NO_EXCEPT
	{
		const quatd inv_rotation = quat_conjugate(input.rotation);
		const mask4d is_scale_w_zero = vector_less_equal(vector_abs(input.translation_scale), vector_set(threshold));
		const vector4d scale_w = vector_select(is_scale_w_zero, vector_set(fallback_scale), input.translation_scale);
		const vector4d inv_scale_w = vector_reciprocal(scale_w);
		const vector4d inv_scale = vector_dup_w(inv_scale_w);
		const vector4d inv_translation = vector_neg(quat_mul_vector3(vector_mul(input.translation_scale, inv_scale), inv_rotation));
		const vector4d inv_translation_scale = vector_mix<mix4::x, mix4::y, mix4::z, mix4::d>(inv_translation, inv_scale_w);

		return qvsd{ inv_rotation, inv_translation_scale };
	}

	//////////////////////////////////////////////////////////////////////////
	// Returns the inverse of the input QVS transform ignoring 3D scale.
	// The resulting QVS transform will have the input scale.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK inline qvsd qvs_inverse_no_scale(const qvsd& input) RTM_NO_EXCEPT
	{
		const quatd inv_rotation = quat_conjugate(input.rotation);
		const vector4d inv_translation = vector_neg(quat_mul_vector3(input.translation_scale, inv_rotation));
		const vector4d inv_translation_scale = vector_mix<mix4::x, mix4::y, mix4::z, mix4::d>(inv_translation, input.translation_scale);

		return qvsd{ inv_rotation, inv_translation_scale };
	}

	//////////////////////////////////////////////////////////////////////////
	// Returns a QVS transforms with the rotation part normalized.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvsd qvs_normalize(const qvsd& input) RTM_NO_EXCEPT
	{
		const quatd rotation = quat_normalize(input.rotation);
		return qvsd{ rotation, input.translation_scale };
	}

	//////////////////////////////////////////////////////////////////////////
	// Per component linear interpolation of the two inputs at the specified alpha.
	// The formula used is: ((1.0 - alpha) * start) + (alpha * end).
	// Interpolation is stable and will return 'start' when alpha is 0.0 and 'end' when it is 1.0.
	// This is the same instruction count when FMA is present but it might be slightly slower
	// due to the extra multiplication compared to: start + (alpha * (end - start)).
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvsd qvs_lerp(const qvsd& start, const qvsd& end, double alpha) RTM_NO_EXCEPT
	{
		const quatd rotation = quat_lerp(start.rotation, end.rotation, alpha);
		const vector4d translation_scale = vector_lerp(start.translation_scale, end.translation_scale, alpha);
		return qvsd{ rotation, translation_scale };
	}

#if defined(RTM_SSE2_INTRINSICS)
	//////////////////////////////////////////////////////////////////////////
	// Per component linear interpolation of the two inputs at the specified alpha.
	// The formula used is: ((1.0 - alpha) * start) + (alpha * end).
	// Interpolation is stable and will return 'start' when alpha is 0.0 and 'end' when it is 1.0.
	// This is the same instruction count when FMA is present but it might be slightly slower
	// due to the extra multiplication compared to: start + (alpha * (end - start)).
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvsd qvs_lerp(const qvsd& start, const qvsd& end, const scalard& alpha) RTM_NO_EXCEPT
	{
		const quatd rotation = quat_lerp(start.rotation, end.rotation, alpha);
		const vector4d translation_scale = vector_lerp(start.translation_scale, end.translation_scale, alpha);
		return qvsd{ rotation, translation_scale };
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
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvsd qvs_lerp_no_scale(const qvsd& start, const qvsd& end, double alpha) RTM_NO_EXCEPT
	{
		const quatd rotation = quat_lerp(start.rotation, end.rotation, alpha);
		const vector4d translation_scale_lerp = vector_lerp(start.translation_scale, end.translation_scale, alpha);
		const vector4d translation_scale = vector_mix<mix4::x, mix4::y, mix4::z, mix4::d>(translation_scale_lerp, start.translation_scale);
		return qvsd{ rotation, translation_scale };
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
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvsd qvs_lerp_no_scale(const qvsd& start, const qvsd& end, const scalard& alpha) RTM_NO_EXCEPT
	{
		const quatd rotation = quat_lerp(start.rotation, end.rotation, alpha);
		const vector4d translation_scale_lerp = vector_lerp(start.translation_scale, end.translation_scale, alpha);
		const vector4d translation_scale = vector_mix<mix4::x, mix4::y, mix4::z, mix4::d>(translation_scale_lerp, start.translation_scale);
		return qvsd{ rotation, translation_scale };
	}
#endif

	//////////////////////////////////////////////////////////////////////////
	// Per component spherical interpolation of the two inputs at the specified alpha.
	// See quat_slerp(..)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvsd qvs_slerp(const qvsd& start, const qvsd& end, double alpha) RTM_NO_EXCEPT
	{
		const quatd rotation = quat_slerp(start.rotation, end.rotation, alpha);
		const vector4d translation_scale = vector_lerp(start.translation_scale, end.translation_scale, alpha);
		return qvsd{ rotation, translation_scale };
	}

#if defined(RTM_SSE2_INTRINSICS)
	//////////////////////////////////////////////////////////////////////////
	// Per component spherical interpolation of the two inputs at the specified alpha.
	// See quat_slerp(..)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvsd qvs_slerp(const qvsd& start, const qvsd& end, const scalard& alpha) RTM_NO_EXCEPT
	{
		const quatd rotation = quat_slerp(start.rotation, end.rotation, alpha);
		const vector4d translation_scale = vector_lerp(start.translation_scale, end.translation_scale, alpha);
		return qvsd{ rotation, translation_scale };
	}
#endif

	//////////////////////////////////////////////////////////////////////////
	// Per component spherical interpolation of the two inputs at the specified alpha.
	// See quat_slerp(..)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvsd qvs_slerp_no_scale(const qvsd& start, const qvsd& end, double alpha) RTM_NO_EXCEPT
	{
		const quatd rotation = quat_slerp(start.rotation, end.rotation, alpha);
		const vector4d translation_scale_lerp = vector_lerp(start.translation_scale, end.translation_scale, alpha);
		const vector4d translation_scale = vector_mix<mix4::x, mix4::y, mix4::z, mix4::d>(translation_scale_lerp, start.translation_scale);
		return qvsd{ rotation, translation_scale };
	}

#if defined(RTM_SSE2_INTRINSICS)
	//////////////////////////////////////////////////////////////////////////
	// Per component spherical interpolation of the two inputs at the specified alpha.
	// See quat_slerp(..)
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE qvsd qvs_slerp_no_scale(const qvsd& start, const qvsd& end, const scalard& alpha) RTM_NO_EXCEPT
	{
		const quatd rotation = quat_slerp(start.rotation, end.rotation, alpha);
		const vector4d translation_scale_lerp = vector_lerp(start.translation_scale, end.translation_scale, alpha);
		const vector4d translation_scale = vector_mix<mix4::x, mix4::y, mix4::z, mix4::d>(translation_scale_lerp, start.translation_scale);
		return qvsd{ rotation, translation_scale };
	}
#endif

	//////////////////////////////////////////////////////////////////////////
	// Returns true if the input QVS does not contain any NaN or Inf, otherwise false.
	//////////////////////////////////////////////////////////////////////////
	RTM_DISABLE_SECURITY_COOKIE_CHECK RTM_FORCE_INLINE bool qvs_is_finite(const qvsd& input) RTM_NO_EXCEPT
	{
		return quat_is_finite(input.rotation) && vector_is_finite(input.translation_scale);
	}

	RTM_IMPL_VERSION_NAMESPACE_END
}

RTM_IMPL_FILE_PRAGMA_POP
