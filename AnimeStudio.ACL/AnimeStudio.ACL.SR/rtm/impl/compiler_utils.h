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

//////////////////////////////////////////////////////////////////////////
// Macro to identify GCC
//////////////////////////////////////////////////////////////////////////
#if defined(__GNUG__) && !defined(__clang__)
	#define RTM_COMPILER_GCC
#endif

//////////////////////////////////////////////////////////////////////////
// Macro to identify Clang
//////////////////////////////////////////////////////////////////////////
#if defined(__clang__)
	#define RTM_COMPILER_CLANG
#endif

//////////////////////////////////////////////////////////////////////////
// Macro to identify MSVC
//////////////////////////////////////////////////////////////////////////
#if defined(_MSC_VER) && !defined(__clang__)
	#define RTM_COMPILER_MSVC
#endif

//////////////////////////////////////////////////////////////////////////
// Because this library is made entirely of headers, we have no control over the
// compilation flags used. However, in some cases, certain options must be forced.
// To do this, every header is wrapped in two macros to push and pop the necessary
// pragmas.
//////////////////////////////////////////////////////////////////////////
#if defined(RTM_COMPILER_MSVC)
	#define RTM_IMPL_FILE_PRAGMA_PUSH \
		/* Disable fast math, it can hurt precision for little to no performance gain due to the heavy usage of intrinsics. */ \
		__pragma(float_control(precise, on, push))

	#define RTM_IMPL_FILE_PRAGMA_POP \
		__pragma(float_control(pop))
#else
	#define RTM_IMPL_FILE_PRAGMA_PUSH
	#define RTM_IMPL_FILE_PRAGMA_POP
#endif

//////////////////////////////////////////////////////////////////////////
// In some cases, for performance reasons, we wish to disable stack security
// check cookies. This macro serves this purpose.
//////////////////////////////////////////////////////////////////////////
#if defined(RTM_COMPILER_MSVC)
	#define RTM_DISABLE_SECURITY_COOKIE_CHECK __declspec(safebuffers)
#else
	#define RTM_DISABLE_SECURITY_COOKIE_CHECK
#endif

//////////////////////////////////////////////////////////////////////////
// Force inline macros for when it is necessary.
//////////////////////////////////////////////////////////////////////////
#if defined(RTM_COMPILER_MSVC)
	#define RTM_FORCE_INLINE __forceinline
#elif defined(RTM_COMPILER_GCC) || defined(RTM_COMPILER_CLANG)
	#define RTM_FORCE_INLINE __attribute__((always_inline)) inline
#else
	#define RTM_FORCE_INLINE inline
#endif

//////////////////////////////////////////////////////////////////////////
// Force no-inline macros for when it is necessary.
//////////////////////////////////////////////////////////////////////////
#if defined(RTM_COMPILER_MSVC)
	#define RTM_FORCE_NOINLINE __declspec(noinline)
#elif defined(RTM_COMPILER_GCC) || defined(RTM_COMPILER_CLANG)
	#define RTM_FORCE_NOINLINE __attribute__((noinline))
#else
	#define RTM_FORCE_NOINLINE
#endif

//////////////////////////////////////////////////////////////////////////
// Joins two pre-processor tokens: RTM_JOIN_TOKENS(foo, bar) yields 'foobar'
//////////////////////////////////////////////////////////////////////////
#define RTM_JOIN_TOKENS(a, b) a ## b

//////////////////////////////////////////////////////////////////////////
// Helper macro to determine if vrndns_f32 is supported (ARM64 only)
//////////////////////////////////////////////////////////////////////////
#if defined(__aarch64__) || defined(_M_ARM64)
	// ARM documentation states __ARM_FEATURE_DIRECTED_ROUNDING must be defined
	#if defined(__ARM_FEATURE_DIRECTED_ROUNDING)
		// Only support it with clang for now
		#if defined(RTM_COMPILER_CLANG)
			// Apple redefines __clang_major__ to match the XCode version
			#if defined(__APPLE__)
				#if __clang_major__ >= 10
					// Apple clang supports it starting with XCode 10
					#define RTM_IMPL_VRNDNS_SUPPORTED
				#endif
			#else
				#if __clang_major__ >= 6
					// Ordinary clang supports it starting with clang 6
					#define RTM_IMPL_VRNDNS_SUPPORTED
				#endif
			#endif
		#endif
	#endif

	// MSVC doesn't appear to define __ARM_FEATURE_DIRECTED_ROUNDING but it supports the
	// intrinsic as of VS2019
	#if !defined(RTM_IMPL_VRNDNS_SUPPORTED) && defined(RTM_COMPILER_MSVC) && _MSC_VER >= 1920
		#define RTM_IMPL_VRNDNS_SUPPORTED
	#endif
#endif

//////////////////////////////////////////////////////////////////////////
// Helper macro to determine if the vca* (e.g vcagtq_f32) family of intrinsics are supported (ARM64 only)
//////////////////////////////////////////////////////////////////////////
#if defined(__aarch64__) || defined(_M_ARM64)
	#if defined(RTM_COMPILER_MSVC)
		#if _MSC_VER >= 1920
			// Support was introduced in VS2019
			#define RTM_IMPL_VCA_SUPPORTED
		#endif
	#else
		// Always enable with GCC and clang for now
		#define RTM_IMPL_VCA_SUPPORTED
	#endif
#endif

//////////////////////////////////////////////////////////////////////////
// Helper macro to determine if the vc*z* (e.g vceqq_f32) family of intrinsics are supported (ARM64 only)
//////////////////////////////////////////////////////////////////////////
#if defined(__aarch64__) || defined(_M_ARM64)
	#if defined(RTM_COMPILER_MSVC)
		#if _MSC_VER >= 1920
			// Support was introduced in VS2019
			#define RTM_IMPL_VCZ_SUPPORTED
		#endif
	#else
		// Always enable with GCC and clang for now
		#define RTM_IMPL_VCZ_SUPPORTED
	#endif
#endif

//////////////////////////////////////////////////////////////////////////
// Helper macro to determine if the vsqrtq_f32 intrinsic is supported (ARM64 only)
//////////////////////////////////////////////////////////////////////////
#if defined(__aarch64__) || defined(_M_ARM64)
	#if defined(RTM_COMPILER_MSVC)
		#if _MSC_VER >= 1920
			// Support was introduced in VS2019
			#define RTM_IMPL_VSQRT_SUPPORTED
		#endif
	#else
		// Always enable with GCC and clang for now
		#define RTM_IMPL_VSQRT_SUPPORTED
	#endif
#endif
