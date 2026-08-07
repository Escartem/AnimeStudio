// The exported surface consumed by AnimeStudio.Utility (see the HLSLDecompiler class in
// ShaderConverter.cs). Everything else in this DLL is the vendored 3Dmigoto decompiler.
//
// The decompiler reads the shader's disassembly text alongside its bytecode -- it recovers
// resource bindings, signatures and buffer layouts from the disassembly -- so this wrapper
// runs D3DDisassemble first and hands both to DecompileBinaryHLSL.

#include <windows.h>
#include <d3dcompiler.h>

#include <string>

#include "DecompileHLSL.h"

// Declared extern by log.h. This DLL keeps no log of its own, so LogInfo/LogDebug are no-ops.
FILE *LogFile = nullptr;
bool gLogDebug = false;

// Returned to the caller, which reports a non-zero value as an exception message.
enum DecompileStatus
{
	Decompile_OK = 0,
	Decompile_InvalidArgument = 1,
	Decompile_DisassembleFailed = 2,
	Decompile_DecompileFailed = 3,
	Decompile_OutOfMemory = 4,
};

extern "C" __declspec(dllexport) int __cdecl Decompile(
	const void *shaderByteCode,
	int shaderByteCodeSize,
	char **shaderText,
	int *shaderTextSize)
{
	if (shaderText == nullptr || shaderTextSize == nullptr)
		return Decompile_InvalidArgument;

	*shaderText = nullptr;
	*shaderTextSize = 0;

	if (shaderByteCode == nullptr || shaderByteCodeSize <= 0)
		return Decompile_InvalidArgument;

	ID3DBlob *disassembly = nullptr;
	HRESULT hr = D3DDisassemble(shaderByteCode, (SIZE_T)shaderByteCodeSize,
		D3D_DISASM_ENABLE_DEFAULT_VALUE_PRINTS, nullptr, &disassembly);
	if (FAILED(hr) || disassembly == nullptr)
		return Decompile_DisassembleFailed;

	// The ZRepair/BackProject/stereo fixups are 3Dmigoto's game patching features; we only
	// want a plain decompile, so every setting stays at its default.
	DecompilerSettings settings;

	ParseParameters params = {};
	params.bytecode = shaderByteCode;
	params.decompiled = (const char *)disassembly->GetBufferPointer();
	params.decompiledSize = disassembly->GetBufferSize();
	params.ZeroOutput = false;
	params.G = &settings;

	bool patched = false;
	bool errorOccurred = false;
	std::string shaderModel;

	const std::string hlsl = DecompileBinaryHLSL(params, patched, shaderModel, errorOccurred);

	disassembly->Release();

	if (errorOccurred || hlsl.empty())
		return Decompile_DecompileFailed;

	// The caller releases this with Marshal.FreeHGlobal, which is LocalFree on Windows,
	// so the buffer has to come from LocalAlloc rather than new/malloc.
	char *buffer = (char *)LocalAlloc(LMEM_FIXED, hlsl.size() + 1);
	if (buffer == nullptr)
		return Decompile_OutOfMemory;

	memcpy(buffer, hlsl.c_str(), hlsl.size() + 1);

	*shaderText = buffer;
	*shaderTextSize = (int)hlsl.size();

	return Decompile_OK;
}
