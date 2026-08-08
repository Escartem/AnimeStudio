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

#include "rtm/types.h"

namespace rtm
{
	//////////////////////////////////////////////////////////////////////////
	// Returns the proper types for a floating point type.
	//////////////////////////////////////////////////////////////////////////
	template<typename float_type>
	struct float_traits {};

	template<>
	struct float_traits<float>
	{
		using mask4 = mask4f;

		using scalar = scalarf;
		using vector4 = vector4f;
		using quat = quatf;
		using qvv = qvvf;

		using matrix3x3 = matrix3x3f;
		using matrix3x4 = matrix3x4f;
		using matrix4x4 = matrix4x4f;

		using float2 = float2f;
		using float3 = float3f;
		using float4 = float4f;
	};

	template<>
	struct float_traits<double>
	{
		using mask4 = mask4d;

		using scalar = scalard;
		using vector4 = vector4d;
		using quat = quatd;
		using qvv = qvvd;

		using matrix3x3 = matrix3x3d;
		using matrix3x4 = matrix3x4d;
		using matrix4x4 = matrix4x4d;

		using float2 = float2d;
		using float3 = float3d;
		using float4 = float4d;
	};
}
