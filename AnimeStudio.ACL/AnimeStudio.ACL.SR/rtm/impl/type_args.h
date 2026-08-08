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
#include "rtm/impl/compiler_utils.h"

namespace rtm
{
	//////////////////////////////////////////////////////////////////////////
	// Register passing typedefs
	//////////////////////////////////////////////////////////////////////////



#if defined(RTM_USE_VECTORCALL)
	// On x64 with __vectorcall, the first 6x vector4f/quatf arguments can be passed by value in a register,
	// everything else afterwards is passed by const&. They can also be returned by register.
	using vector4f_arg0 = const vector4f;
	using vector4f_arg1 = const vector4f;
	using vector4f_arg2 = const vector4f;
	using vector4f_arg3 = const vector4f;
	using vector4f_arg4 = const vector4f;
	using vector4f_arg5 = const vector4f;
	using vector4f_arg6 = const vector4f&;
	using vector4f_arg7 = const vector4f&;
	using vector4f_argn = const vector4f&;

	using quatf_arg0 = const quatf;
	using quatf_arg1 = const quatf;
	using quatf_arg2 = const quatf;
	using quatf_arg3 = const quatf;
	using quatf_arg4 = const quatf;
	using quatf_arg5 = const quatf;
	using quatf_arg6 = const quatf&;
	using quatf_arg7 = const quatf&;
	using quatf_argn = const quatf&;

	using scalarf_arg0 = const scalarf;
	using scalarf_arg1 = const scalarf;
	using scalarf_arg2 = const scalarf;
	using scalarf_arg3 = const scalarf;
	using scalarf_arg4 = const scalarf;
	using scalarf_arg5 = const scalarf;
	using scalarf_arg6 = const scalarf&;
	using scalarf_arg7 = const scalarf&;
	using scalarf_argn = const scalarf&;

	using mask4f_arg0 = const mask4f;
	using mask4f_arg1 = const mask4f;
	using mask4f_arg2 = const mask4f;
	using mask4f_arg3 = const mask4f;
	using mask4f_arg4 = const mask4f;
	using mask4f_arg5 = const mask4f;
	using mask4f_arg6 = const mask4f&;
	using mask4f_arg7 = const mask4f&;
	using mask4f_argn = const mask4f&;

	using mask4i_arg0 = const mask4i;
	using mask4i_arg1 = const mask4i;
	using mask4i_arg2 = const mask4i;
	using mask4i_arg3 = const mask4i;
	using mask4i_arg4 = const mask4i;
	using mask4i_arg5 = const mask4i;
	using mask4i_arg6 = const mask4i&;
	using mask4i_arg7 = const mask4i&;
	using mask4i_argn = const mask4i&;

	// With __vectorcall, vector aggregates are also passed by register and they can use up to 4 registers.

	using qvvf_arg0 = const qvvf;
	using qvvf_arg1 = const qvvf;
	using qvvf_argn = const qvvf&;

	using matrix3x3f_arg0 = const matrix3x3f;
	using matrix3x3f_arg1 = const matrix3x3f&;
	using matrix3x3f_argn = const matrix3x3f&;

	using matrix3x4f_arg0 = const matrix3x4f;
	using matrix3x4f_arg1 = const matrix3x4f&;
	using matrix3x4f_argn = const matrix3x4f&;

	using matrix4x4f_arg0 = const matrix4x4f;
	using matrix4x4f_arg1 = const matrix4x4f&;
	using matrix4x4f_argn = const matrix4x4f&;
#elif defined(RTM_NEON64_INTRINSICS)
	// On ARM64 NEON, the first 8x vector4f/quatf arguments can be passed by value in a register,
	// everything else afterwards is passed by const&. They can also be returned by register.

	using vector4f_arg0 = const vector4f;
	using vector4f_arg1 = const vector4f;
	using vector4f_arg2 = const vector4f;
	using vector4f_arg3 = const vector4f;
	using vector4f_arg4 = const vector4f;
	using vector4f_arg5 = const vector4f;
	using vector4f_arg6 = const vector4f;
	using vector4f_arg7 = const vector4f;
	using vector4f_argn = const vector4f&;

	using quatf_arg0 = const quatf;
	using quatf_arg1 = const quatf;
	using quatf_arg2 = const quatf;
	using quatf_arg3 = const quatf;
	using quatf_arg4 = const quatf;
	using quatf_arg5 = const quatf;
	using quatf_arg6 = const quatf;
	using quatf_arg7 = const quatf;
	using quatf_argn = const quatf&;

	using scalarf_arg0 = const scalarf;
	using scalarf_arg1 = const scalarf;
	using scalarf_arg2 = const scalarf;
	using scalarf_arg3 = const scalarf;
	using scalarf_arg4 = const scalarf;
	using scalarf_arg5 = const scalarf;
	using scalarf_arg6 = const scalarf;
	using scalarf_arg7 = const scalarf;
	using scalarf_argn = const scalarf;

	using mask4f_arg0 = const mask4f;
	using mask4f_arg1 = const mask4f;
	using mask4f_arg2 = const mask4f;
	using mask4f_arg3 = const mask4f;
	using mask4f_arg4 = const mask4f;
	using mask4f_arg5 = const mask4f;
	using mask4f_arg6 = const mask4f;
	using mask4f_arg7 = const mask4f;
	using mask4f_argn = const mask4f&;

	using mask4i_arg0 = const mask4i;
	using mask4i_arg1 = const mask4i;
	using mask4i_arg2 = const mask4i;
	using mask4i_arg3 = const mask4i;
	using mask4i_arg4 = const mask4i;
	using mask4i_arg5 = const mask4i;
	using mask4i_arg6 = const mask4i;
	using mask4i_arg7 = const mask4i;
	using mask4i_argn = const mask4i&;

	// With ARM64 NEON, vector aggregates are also passed by register but the whole aggregate
	// must fit in the number of registers available (e.g. we can pass 2x qvvf but not 3x).
	// A qvvf can also be returned by register.

	using qvvf_arg0 = const qvvf;
	using qvvf_arg1 = const qvvf;
	using qvvf_argn = const qvvf&;

	using matrix3x3f_arg0 = const matrix3x3f;
	using matrix3x3f_arg1 = const matrix3x3f;
	using matrix3x3f_argn = const matrix3x3f&;

	using matrix3x4f_arg0 = const matrix3x4f;
	using matrix3x4f_arg1 = const matrix3x4f;
	using matrix3x4f_argn = const matrix3x4f&;

	using matrix4x4f_arg0 = const matrix4x4f;
	using matrix4x4f_arg1 = const matrix4x4f;
	using matrix4x4f_argn = const matrix4x4f&;
#elif defined(RTM_NEON_INTRINSICS)
	// On ARM NEON, the first 4x vector4f/quatf arguments can be passed by value in a register,
	// everything else afterwards is passed by const&. They can also be returned by register.

	using vector4f_arg0 = const vector4f;
	using vector4f_arg1 = const vector4f;
	using vector4f_arg2 = const vector4f;
	using vector4f_arg3 = const vector4f;
	using vector4f_arg4 = const vector4f&;
	using vector4f_arg5 = const vector4f&;
	using vector4f_arg6 = const vector4f&;
	using vector4f_arg7 = const vector4f&;
	using vector4f_argn = const vector4f&;

	using quatf_arg0 = const quatf;
	using quatf_arg1 = const quatf;
	using quatf_arg2 = const quatf;
	using quatf_arg3 = const quatf;
	using quatf_arg4 = const quatf&;
	using quatf_arg5 = const quatf&;
	using quatf_arg6 = const quatf&;
	using quatf_arg7 = const quatf&;
	using quatf_argn = const quatf&;

	using scalarf_arg0 = const scalarf;
	using scalarf_arg1 = const scalarf;
	using scalarf_arg2 = const scalarf;
	using scalarf_arg3 = const scalarf;
	using scalarf_arg4 = const scalarf;
	using scalarf_arg5 = const scalarf;
	using scalarf_arg6 = const scalarf;
	using scalarf_arg7 = const scalarf;
	using scalarf_argn = const scalarf;

	using mask4f_arg0 = const mask4f;
	using mask4f_arg1 = const mask4f;
	using mask4f_arg2 = const mask4f;
	using mask4f_arg3 = const mask4f;
	using mask4f_arg4 = const mask4f&;
	using mask4f_arg5 = const mask4f&;
	using mask4f_arg6 = const mask4f&;
	using mask4f_arg7 = const mask4f&;
	using mask4f_argn = const mask4f&;

	using mask4i_arg0 = const mask4i;
	using mask4i_arg1 = const mask4i;
	using mask4i_arg2 = const mask4i;
	using mask4i_arg3 = const mask4i;
	using mask4i_arg4 = const mask4i&;
	using mask4i_arg5 = const mask4i&;
	using mask4i_arg6 = const mask4i&;
	using mask4i_arg7 = const mask4i&;
	using mask4i_argn = const mask4i&;

	// ARM NEON does not support passing aggregates by register.

	using qvvf_arg0 = const qvvf&;
	using qvvf_arg1 = const qvvf&;
	using qvvf_argn = const qvvf&;

	using matrix3x3f_arg0 = const matrix3x3f&;
	using matrix3x3f_arg1 = const matrix3x3f&;
	using matrix3x3f_argn = const matrix3x3f&;

	using matrix3x4f_arg0 = const matrix3x4f&;
	using matrix3x4f_arg1 = const matrix3x4f&;
	using matrix3x4f_argn = const matrix3x4f&;

	using matrix4x4f_arg0 = const matrix4x4f&;
	using matrix4x4f_arg1 = const matrix4x4f&;
	using matrix4x4f_argn = const matrix4x4f&;
#elif defined(__x86_64__) && defined(RTM_COMPILER_GCC)
	// On x64 with gcc, the first 8x vector4f/quatf arguments can be passed by value in a register,
	// everything else afterwards is passed by const&. They can also be returned by register.

	using vector4f_arg0 = const vector4f;
	using vector4f_arg1 = const vector4f;
	using vector4f_arg2 = const vector4f;
	using vector4f_arg3 = const vector4f;
	using vector4f_arg4 = const vector4f;
	using vector4f_arg5 = const vector4f;
	using vector4f_arg6 = const vector4f;
	using vector4f_arg7 = const vector4f;
	using vector4f_argn = const vector4f&;

	using quatf_arg0 = const quatf;
	using quatf_arg1 = const quatf;
	using quatf_arg2 = const quatf;
	using quatf_arg3 = const quatf;
	using quatf_arg4 = const quatf;
	using quatf_arg5 = const quatf;
	using quatf_arg6 = const quatf;
	using quatf_arg7 = const quatf;
	using quatf_argn = const quatf&;

	using scalarf_arg0 = const scalarf;
	using scalarf_arg1 = const scalarf;
	using scalarf_arg2 = const scalarf;
	using scalarf_arg3 = const scalarf;
	using scalarf_arg4 = const scalarf;
	using scalarf_arg5 = const scalarf;
	using scalarf_arg6 = const scalarf;
	using scalarf_arg7 = const scalarf;
	using scalarf_argn = const scalarf&;

	using mask4f_arg0 = const mask4f;
	using mask4f_arg1 = const mask4f;
	using mask4f_arg2 = const mask4f;
	using mask4f_arg3 = const mask4f;
	using mask4f_arg4 = const mask4f;
	using mask4f_arg5 = const mask4f;
	using mask4f_arg6 = const mask4f;
	using mask4f_arg7 = const mask4f;
	using mask4f_argn = const mask4f&;

	using mask4i_arg0 = const mask4i;
	using mask4i_arg1 = const mask4i;
	using mask4i_arg2 = const mask4i;
	using mask4i_arg3 = const mask4i;
	using mask4i_arg4 = const mask4i;
	using mask4i_arg5 = const mask4i;
	using mask4i_arg6 = const mask4i;
	using mask4i_arg7 = const mask4i;
	using mask4i_argn = const mask4i&;

	// gcc does not appear to support passing and returning aggregates by register

	using qvvf_arg0 = const qvvf&;
	using qvvf_arg1 = const qvvf&;
	using qvvf_argn = const qvvf&;

	using matrix3x3f_arg0 = const matrix3x3f&;
	using matrix3x3f_arg1 = const matrix3x3f&;
	using matrix3x3f_argn = const matrix3x3f&;

	using matrix3x4f_arg0 = const matrix3x4f&;
	using matrix3x4f_arg1 = const matrix3x4f&;
	using matrix3x4f_argn = const matrix3x4f&;

	using matrix4x4f_arg0 = const matrix4x4f&;
	using matrix4x4f_arg1 = const matrix4x4f&;
	using matrix4x4f_argn = const matrix4x4f&;
#elif defined(__x86_64__) && defined(RTM_COMPILER_CLANG)
	// On x64 with clang, the first 8x vector4f/quatf arguments can be passed by value in a register,
	// everything else afterwards is passed by const&. They can also be returned by register.

	using vector4f_arg0 = const vector4f;
	using vector4f_arg1 = const vector4f;
	using vector4f_arg2 = const vector4f;
	using vector4f_arg3 = const vector4f;
	using vector4f_arg4 = const vector4f;
	using vector4f_arg5 = const vector4f;
	using vector4f_arg6 = const vector4f;
	using vector4f_arg7 = const vector4f;
	using vector4f_argn = const vector4f&;

	using quatf_arg0 = const quatf;
	using quatf_arg1 = const quatf;
	using quatf_arg2 = const quatf;
	using quatf_arg3 = const quatf;
	using quatf_arg4 = const quatf;
	using quatf_arg5 = const quatf;
	using quatf_arg6 = const quatf;
	using quatf_arg7 = const quatf;
	using quatf_argn = const quatf&;

	using scalarf_arg0 = const scalarf;
	using scalarf_arg1 = const scalarf;
	using scalarf_arg2 = const scalarf;
	using scalarf_arg3 = const scalarf;
	using scalarf_arg4 = const scalarf;
	using scalarf_arg5 = const scalarf;
	using scalarf_arg6 = const scalarf;
	using scalarf_arg7 = const scalarf;
	using scalarf_argn = const scalarf&;

	using mask4f_arg0 = const mask4f;
	using mask4f_arg1 = const mask4f;
	using mask4f_arg2 = const mask4f;
	using mask4f_arg3 = const mask4f;
	using mask4f_arg4 = const mask4f;
	using mask4f_arg5 = const mask4f;
	using mask4f_arg6 = const mask4f;
	using mask4f_arg7 = const mask4f;
	using mask4f_argn = const mask4f&;

	using mask4i_arg0 = const mask4i;
	using mask4i_arg1 = const mask4i;
	using mask4i_arg2 = const mask4i;
	using mask4i_arg3 = const mask4i;
	using mask4i_arg4 = const mask4i;
	using mask4i_arg5 = const mask4i;
	using mask4i_arg6 = const mask4i;
	using mask4i_arg7 = const mask4i;
	using mask4i_argn = const mask4i&;

	// We could pass up to 2 full qvvf types by register and the rotation/translation of
	// the third but aggregates are not returned by register.
	// TODO: Measure the impact of this because it could potentially degrade performance
	// if multiple qvv_mul(..) are called, forcing the return value to be written and read back
	// before the next function call can be made. It might be faster regardless as the compiler
	// might be able to insert other instructions in between.

	using qvvf_arg0 = const qvvf&;
	using qvvf_arg1 = const qvvf&;
	using qvvf_argn = const qvvf&;

	using matrix3x3f_arg0 = const matrix3x3f&;
	using matrix3x3f_arg1 = const matrix3x3f&;
	using matrix3x3f_argn = const matrix3x3f&;

	using matrix3x4f_arg0 = const matrix3x4f&;
	using matrix3x4f_arg1 = const matrix3x4f&;
	using matrix3x4f_argn = const matrix3x4f&;

	using matrix4x4f_arg0 = const matrix4x4f&;
	using matrix4x4f_arg1 = const matrix4x4f&;
	using matrix4x4f_argn = const matrix4x4f&;
#else
	// On every other platform, everything is passed by const&
	using vector4f_arg0 = const vector4f&;
	using vector4f_arg1 = const vector4f&;
	using vector4f_arg2 = const vector4f&;
	using vector4f_arg3 = const vector4f&;
	using vector4f_arg4 = const vector4f&;
	using vector4f_arg5 = const vector4f&;
	using vector4f_arg6 = const vector4f&;
	using vector4f_arg7 = const vector4f&;
	using vector4f_argn = const vector4f&;

	using quatf_arg0 = const quatf&;
	using quatf_arg1 = const quatf&;
	using quatf_arg2 = const quatf&;
	using quatf_arg3 = const quatf&;
	using quatf_arg4 = const quatf&;
	using quatf_arg5 = const quatf&;
	using quatf_arg6 = const quatf&;
	using quatf_arg7 = const quatf&;
	using quatf_argn = const quatf&;

	using scalarf_arg0 = const scalarf;
	using scalarf_arg1 = const scalarf;
	using scalarf_arg2 = const scalarf;
	using scalarf_arg3 = const scalarf;
	using scalarf_arg4 = const scalarf;
	using scalarf_arg5 = const scalarf;
	using scalarf_arg6 = const scalarf;
	using scalarf_arg7 = const scalarf;
	using scalarf_argn = const scalarf;

	using mask4f_arg0 = const mask4f&;
	using mask4f_arg1 = const mask4f&;
	using mask4f_arg2 = const mask4f&;
	using mask4f_arg3 = const mask4f&;
	using mask4f_arg4 = const mask4f&;
	using mask4f_arg5 = const mask4f&;
	using mask4f_arg6 = const mask4f&;
	using mask4f_arg7 = const mask4f&;
	using mask4f_argn = const mask4f&;

	using mask4i_arg0 = const mask4i&;
	using mask4i_arg1 = const mask4i&;
	using mask4i_arg2 = const mask4i&;
	using mask4i_arg3 = const mask4i&;
	using mask4i_arg4 = const mask4i&;
	using mask4i_arg5 = const mask4i&;
	using mask4i_arg6 = const mask4i&;
	using mask4i_arg7 = const mask4i&;
	using mask4i_argn = const mask4i&;

	using qvvf_arg0 = const qvvf&;
	using qvvf_arg1 = const qvvf&;
	using qvvf_argn = const qvvf&;

	using matrix3x3f_arg0 = const matrix3x3f&;
	using matrix3x3f_arg1 = const matrix3x3f&;
	using matrix3x3f_argn = const matrix3x3f&;

	using matrix3x4f_arg0 = const matrix3x4f&;
	using matrix3x4f_arg1 = const matrix3x4f&;
	using matrix3x4f_argn = const matrix3x4f&;

	using matrix4x4f_arg0 = const matrix4x4f&;
	using matrix4x4f_arg1 = const matrix4x4f&;
	using matrix4x4f_argn = const matrix4x4f&;
#endif
}
