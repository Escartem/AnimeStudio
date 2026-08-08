#pragma once

#include <string>
#include <vector>

// The goal with this file is too keep it as much C++ as possible.
// So we avoid adding Windows specific stuff if at all possible.

// Let's use these constants instead of hard coded numbers, so that we can use safe versions
// of sprintf_s, sscanf_s, strcpy_s

const int opcodeSize = 128;
const int stringSize = 256;

struct DecompilerSettings
{
	int StereoParamsReg;
	int IniParamsReg;

	bool fixSvPosition;
	bool recompileVs;
	char ZRepair_DepthTextureReg1, ZRepair_DepthTextureReg2;
	std::string ZRepair_DepthTexture1, ZRepair_DepthTexture2;
	std::vector<std::string> ZRepair_Dependencies1, ZRepair_Dependencies2;
	std::string ZRepair_ZPosCalc1, ZRepair_ZPosCalc2;
	std::string ZRepair_PositionTexture;
	bool ZRepair_DepthBuffer;
	std::vector<std::string> InvTransforms;
	std::string ZRepair_WorldPosCalc;
	std::string BackProject_Vector1, BackProject_Vector2;
	std::string ObjectPos_ID1, ObjectPos_ID2, ObjectPos_MUL1, ObjectPos_MUL2;
	std::string MatrixPos_ID1, MatrixPos_MUL1;

	DecompilerSettings() :
		StereoParamsReg(-1),
		IniParamsReg(-1),
		fixSvPosition(false),
		recompileVs(false),
		ZRepair_DepthTextureReg1('\0'),
		ZRepair_DepthTextureReg2('\0'),
		ZRepair_DepthBuffer(false)
	{}
};

struct ParseParameters
{
	const void *bytecode;
	const char *decompiled;
	size_t decompiledSize;
	bool ZeroOutput;

	//dx9
	int StereoParamsVertexReg;
	int StereoParamsPixelReg;
	//dx9

	DecompilerSettings *G;
};

const std::string DecompileBinaryHLSL(ParseParameters &params, bool &patched, std::string &shaderModel, bool &errorOccurred);
