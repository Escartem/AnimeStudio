#include "internal_includes/reflect.h"
#include "internal_includes/debug.h"
#include "internal_includes/decode.h"
#include <stdlib.h>

#include <string>

static void ReadStringFromTokenStream(const uint32_t* tokens, std::string &str)
{
    char* charTokens = (char*) tokens;
    char nextCharacter = *charTokens++;
	str.clear();

    //Add each individual character until
    //a terminator is found.
    while(nextCharacter != 0) {

        str.push_back(nextCharacter);

        nextCharacter = *charTokens++;
    }
}

static void ReadInputSignatures(const uint32_t* pui32Tokens,
                        ShaderInfo* psShaderInfo,
						const int extended)
{
    uint32_t i;

    InOutSignature* psSignatures;
    const uint32_t* pui32FirstSignatureToken = pui32Tokens;
    const uint32_t ui32ElementCount = *pui32Tokens++;
    const uint32_t ui32Key = *pui32Tokens++;

    psSignatures = new InOutSignature[ui32ElementCount];
    psShaderInfo->psInputSignatures = psSignatures;
    psShaderInfo->ui32NumInputSignatures = ui32ElementCount;

    for(i=0; i<ui32ElementCount; ++i)
    {
        uint32_t ui32ComponentMasks;
        InOutSignature* psCurrentSignature = psSignatures + i;
        uint32_t ui32SemanticNameOffset;

		psCurrentSignature->ui32Stream = 0;
		psCurrentSignature->eMinPrec = MIN_PRECISION_DEFAULT;

		if(extended)
			psCurrentSignature->ui32Stream = *pui32Tokens++;

		ui32SemanticNameOffset = *pui32Tokens++;
        psCurrentSignature->ui32SemanticIndex = *pui32Tokens++;
        psCurrentSignature->eSystemValueType = (SPECIAL_NAME) *pui32Tokens++;
        psCurrentSignature->eComponentType = (INOUT_COMPONENT_TYPE) *pui32Tokens++;
        psCurrentSignature->ui32Register = *pui32Tokens++;
        
        ui32ComponentMasks = *pui32Tokens++;
        psCurrentSignature->ui32Mask = ui32ComponentMasks & 0x7F;
        //Shows which components are read
        psCurrentSignature->ui32ReadWriteMask = (ui32ComponentMasks & 0x7F00) >> 8;

		if(extended)
			psCurrentSignature->eMinPrec = (MIN_PRECISION) *pui32Tokens++;

        ReadStringFromTokenStream((const uint32_t*)((const char*)pui32FirstSignatureToken+ui32SemanticNameOffset), psCurrentSignature->SemanticName);
    }
}

static void ReadOutputSignatures(const uint32_t* pui32Tokens,
                        ShaderInfo* psShaderInfo,
						const int minPrec,
						const int streams)
{
    uint32_t i;

    InOutSignature* psSignatures;
    const uint32_t* pui32FirstSignatureToken = pui32Tokens;
    const uint32_t ui32ElementCount = *pui32Tokens++;
    const uint32_t ui32Key = *pui32Tokens++;

    psSignatures = new InOutSignature[ui32ElementCount];
    psShaderInfo->psOutputSignatures = psSignatures;
    psShaderInfo->ui32NumOutputSignatures = ui32ElementCount;

    for(i=0; i<ui32ElementCount; ++i)
    {
        uint32_t ui32ComponentMasks;
        InOutSignature* psCurrentSignature = psSignatures + i;
        uint32_t ui32SemanticNameOffset;

		psCurrentSignature->ui32Stream = 0;
		psCurrentSignature->eMinPrec = MIN_PRECISION_DEFAULT;

		if(streams)
			psCurrentSignature->ui32Stream = *pui32Tokens++;

		ui32SemanticNameOffset = *pui32Tokens++;
        psCurrentSignature->ui32SemanticIndex = *pui32Tokens++;
        psCurrentSignature->eSystemValueType = (SPECIAL_NAME)*pui32Tokens++;
        psCurrentSignature->eComponentType = (INOUT_COMPONENT_TYPE) *pui32Tokens++;
        psCurrentSignature->ui32Register = *pui32Tokens++;

        ui32ComponentMasks = *pui32Tokens++;
        psCurrentSignature->ui32Mask = ui32ComponentMasks & 0x7F;
        //Shows which components are NEVER written.
        psCurrentSignature->ui32ReadWriteMask = (ui32ComponentMasks & 0x7F00) >> 8;

		if(minPrec)
			psCurrentSignature->eMinPrec = (MIN_PRECISION) *pui32Tokens++;

        ReadStringFromTokenStream((const uint32_t*)((const char*)pui32FirstSignatureToken+ui32SemanticNameOffset), psCurrentSignature->SemanticName);
    }
}

static void ReadPatchConstantSignatures(const uint32_t* pui32Tokens,
                    ShaderInfo* psShaderInfo,
					const int minPrec,
					const int streams)
{
    uint32_t i;

    InOutSignature* psSignatures;
    const uint32_t* pui32FirstSignatureToken = pui32Tokens;
    const uint32_t ui32ElementCount = *pui32Tokens++;
    const uint32_t ui32Key = *pui32Tokens++;

    psSignatures = new InOutSignature[ui32ElementCount];
    psShaderInfo->psPatchConstantSignatures = psSignatures;
    psShaderInfo->ui32NumPatchConstantSignatures = ui32ElementCount;

    for(i=0; i<ui32ElementCount; ++i)
    {
        uint32_t ui32ComponentMasks;
        InOutSignature* psCurrentSignature = psSignatures + i;
        uint32_t ui32SemanticNameOffset;

		psCurrentSignature->ui32Stream = 0;
		psCurrentSignature->eMinPrec = MIN_PRECISION_DEFAULT;

		if(streams)
			psCurrentSignature->ui32Stream = *pui32Tokens++;

		ui32SemanticNameOffset = *pui32Tokens++;
        psCurrentSignature->ui32SemanticIndex = *pui32Tokens++;
        psCurrentSignature->eSystemValueType = (SPECIAL_NAME)*pui32Tokens++;
        psCurrentSignature->eComponentType = (INOUT_COMPONENT_TYPE) *pui32Tokens++;
        psCurrentSignature->ui32Register = *pui32Tokens++;

		// Massage some special inputs/outputs to match the types of GLSL counterparts
		if (psCurrentSignature->eSystemValueType == NAME_RENDER_TARGET_ARRAY_INDEX)
		{
			psCurrentSignature->eComponentType = INOUT_COMPONENT_SINT32;
		}

        ui32ComponentMasks = *pui32Tokens++;
        psCurrentSignature->ui32Mask = ui32ComponentMasks & 0x7F;
        //Shows which components are NEVER written.
        psCurrentSignature->ui32ReadWriteMask = (ui32ComponentMasks & 0x7F00) >> 8;

		if(minPrec)
			psCurrentSignature->eMinPrec = (MIN_PRECISION) *pui32Tokens++;

        ReadStringFromTokenStream((const uint32_t*)((const char*)pui32FirstSignatureToken+ui32SemanticNameOffset), psCurrentSignature->SemanticName);
    }
}

static const uint32_t* ReadResourceBinding(const uint32_t* pui32FirstResourceToken, const uint32_t* pui32Tokens, ResourceBinding* psBinding)
{
    uint32_t ui32NameOffset = *pui32Tokens++;

    ReadStringFromTokenStream((const uint32_t*)((const char*)pui32FirstResourceToken+ui32NameOffset), psBinding->Name);

    psBinding->eType = (ResourceType) *pui32Tokens++;
    psBinding->ui32ReturnType = *pui32Tokens++;
    psBinding->eDimension = (REFLECT_RESOURCE_DIMENSION)*pui32Tokens++;
    psBinding->ui32NumSamples = *pui32Tokens++;
    psBinding->ui32BindPoint = *pui32Tokens++;
    psBinding->ui32BindCount = *pui32Tokens++;
    psBinding->ui32Flags = *pui32Tokens++;

    return pui32Tokens;
}

//Read D3D11_SHADER_TYPE_DESC
static void ReadShaderVariableType(const uint32_t ui32MajorVersion,
								   const uint32_t* pui32FirstConstBufToken,
								   const uint32_t* pui32tokens, ShaderVarType* varType)
{
    const uint16_t* pui16Tokens = (const uint16_t*) pui32tokens;
    uint16_t ui32MemberCount;
    uint32_t ui32MemberOffset;
	const uint32_t* pui32MemberTokens;
	uint32_t i;

    varType->Class = (SHADER_VARIABLE_CLASS)pui16Tokens[0];
    varType->Type = (SHADER_VARIABLE_TYPE)pui16Tokens[1];
    varType->Rows = pui16Tokens[2];
    varType->Columns = pui16Tokens[3];
    varType->Elements = pui16Tokens[4];

    varType->MemberCount = ui32MemberCount = pui16Tokens[5];
	varType->Members = 0;

	if(varType->ParentCount)
	{
		varType->FullName = varType->Parent->FullName +
			"." +
			varType->Name;
	}

	if(ui32MemberCount)
	{
		varType->Members = new ShaderVarType[ui32MemberCount];

		ui32MemberOffset = pui32tokens[3];
	
		pui32MemberTokens = (const uint32_t*)((const char*)pui32FirstConstBufToken+ui32MemberOffset);

		for(i=0; i< ui32MemberCount; ++i)
		{
			uint32_t ui32NameOffset = *pui32MemberTokens++;
			uint32_t ui32MemberTypeOffset = *pui32MemberTokens++;
			
			varType->Members[i].Parent = varType;
			varType->Members[i].ParentCount = varType->ParentCount + 1;

			varType->Members[i].Offset = *pui32MemberTokens++;

			ReadStringFromTokenStream((const uint32_t*)((const char*)pui32FirstConstBufToken+ui32NameOffset), varType->Members[i].Name);

			ReadShaderVariableType(ui32MajorVersion, pui32FirstConstBufToken, 
				(const uint32_t*)((const char*)pui32FirstConstBufToken+ui32MemberTypeOffset), &varType->Members[i]);
		}
	}
}

static const uint32_t* ReadConstantBuffer(ShaderInfo* psShaderInfo,
    const uint32_t* pui32FirstConstBufToken, const uint32_t* pui32Tokens, ConstantBuffer* psBuffer)
{
    uint32_t i;
    uint32_t ui32NameOffset = *pui32Tokens++;
    uint32_t ui32VarCount = *pui32Tokens++;
    uint32_t ui32VarOffset = *pui32Tokens++;
    const uint32_t* pui32VarToken = (const uint32_t*)((const char*)pui32FirstConstBufToken+ui32VarOffset);

    ReadStringFromTokenStream((const uint32_t*)((const char*)pui32FirstConstBufToken+ui32NameOffset), psBuffer->Name);

    psBuffer->asVars.resize(ui32VarCount);

    for(i=0; i<ui32VarCount; ++i)
    {
        //D3D11_SHADER_VARIABLE_DESC
		// DarkStarSword: Changed this to resize first, then get a pointer.
		// The way we originally did this filling out the structure then inserting
		// it with push_back(), was problematic, as the inserted struct was a copy,
		// not the original, so the Parent pointers of all members were invalid.
        ShaderVar * const psVar = &psBuffer->asVars[i];

        uint32_t ui32Flags;
        uint32_t ui32TypeOffset;
        uint32_t ui32DefaultValueOffset;

        ui32NameOffset = *pui32VarToken++;

        ReadStringFromTokenStream((const uint32_t*)((const char*)pui32FirstConstBufToken+ui32NameOffset), psVar->Name);

        psVar->ui32StartOffset = *pui32VarToken++;
        psVar->ui32Size = *pui32VarToken++;
        ui32Flags = *pui32VarToken++;
        ui32TypeOffset = *pui32VarToken++;

		psVar->sType.Name = psVar->Name;
		psVar->sType.FullName = psVar->Name;
		psVar->sType.Parent = 0;
		psVar->sType.ParentCount = 0;
		psVar->sType.Offset = 0;

        ReadShaderVariableType(psShaderInfo->ui32MajorVersion, pui32FirstConstBufToken, 
			(const uint32_t*)((const char*)pui32FirstConstBufToken+ui32TypeOffset), &psVar->sType);

        ui32DefaultValueOffset = *pui32VarToken++;


		if (psShaderInfo->ui32MajorVersion  >= 5)
		{
			uint32_t StartTexture = *pui32VarToken++;
			uint32_t TextureSize = *pui32VarToken++;
			uint32_t StartSampler = *pui32VarToken++;
			uint32_t SamplerSize = *pui32VarToken++;
		}

		psVar->haveDefaultValue = false;

        if(ui32DefaultValueOffset)
        {
			uint32_t i = 0;
			const uint32_t ui32NumDefaultValues = psVar->ui32Size / 4;
			const uint32_t* pui32DefaultValToken = (const uint32_t*)((const char*)pui32FirstConstBufToken+ui32DefaultValueOffset);

			//Always a sequence of 4-bytes at the moment.
			//bool const becomes 0 or 0xFFFFFFFF int, int & float are 4-bytes.
			ASSERT(psVar->ui32Size%4 == 0);

			psVar->haveDefaultValue = true;

			for(i=0; i<ui32NumDefaultValues;++i)
			{
				psVar->pui32DefaultValues.push_back(pui32DefaultValToken[i]);
			}
        }
    }


    {
        uint32_t ui32Flags;
        uint32_t ui32BufferType;

        psBuffer->ui32TotalSizeInBytes = *pui32Tokens++;
        ui32Flags = *pui32Tokens++;
        ui32BufferType = *pui32Tokens++;
    }

	psBuffer->iUnsized = 0;

    return pui32Tokens;
}

static void ReadResources(const uint32_t* pui32Tokens,//in
                   ShaderInfo* psShaderInfo)//out
{
    ResourceBinding* psResBindings;
    ConstantBuffer* psConstantBuffers;
    const uint32_t* pui32ConstantBuffers;
    const uint32_t* pui32ResourceBindings;
    const uint32_t* pui32FirstToken = pui32Tokens;
    uint32_t i;

	const uint32_t ui32NumConstantBuffers = *pui32Tokens++;
    const uint32_t ui32ConstantBufferOffset = *pui32Tokens++;

    uint32_t ui32NumResourceBindings = *pui32Tokens++;
    uint32_t ui32ResourceBindingOffset = *pui32Tokens++;
    uint32_t ui32ShaderModel = *pui32Tokens++;
    uint32_t ui32CompileFlags = *pui32Tokens++;//D3DCompile flags? http://msdn.microsoft.com/en-us/library/gg615083(v=vs.85).aspx

    //Resources
    pui32ResourceBindings = (const uint32_t*)((const char*)pui32FirstToken + ui32ResourceBindingOffset);

    psResBindings = new ResourceBinding[ui32NumResourceBindings];

    psShaderInfo->ui32NumResourceBindings = ui32NumResourceBindings;
    psShaderInfo->psResourceBindings = psResBindings;

    for(i=0; i < ui32NumResourceBindings; ++i)
    {
        pui32ResourceBindings = ReadResourceBinding(pui32FirstToken, pui32ResourceBindings, psResBindings+i);
		ASSERT(psResBindings[i].ui32BindPoint < MAX_RESOURCE_BINDINGS);
	}

    //Constant buffers
    pui32ConstantBuffers = (const uint32_t*)((const char*)pui32FirstToken + ui32ConstantBufferOffset);

    psConstantBuffers = new ConstantBuffer[ui32NumConstantBuffers];

    psShaderInfo->ui32NumConstantBuffers = ui32NumConstantBuffers;
    psShaderInfo->psConstantBuffers = psConstantBuffers;

    for(i=0; i < ui32NumConstantBuffers; ++i)
    {
        pui32ConstantBuffers = ReadConstantBuffer(psShaderInfo, pui32FirstToken, pui32ConstantBuffers, psConstantBuffers+i);
    }


	//Map resource bindings to constant buffers
	if(psShaderInfo->ui32NumConstantBuffers)
	{
		for(i=0; i < ui32NumResourceBindings; ++i)
		{
			ResourceGroup eRGroup;
			uint32_t cbufIndex = 0;

			eRGroup = ResourceTypeToResourceGroup(psResBindings[i].eType);

			//Find the constant buffer whose name matches the resource at the given resource binding point
			for(cbufIndex=0; cbufIndex < psShaderInfo->ui32NumConstantBuffers; cbufIndex++)
			{
				if(psConstantBuffers[cbufIndex].Name == psResBindings[i].Name)
				{
					psShaderInfo->aui32ResourceMap[eRGroup][psResBindings[i].ui32BindPoint] = cbufIndex;
				}
			}
		}
	}
}

static const uint16_t* ReadClassType(const uint32_t* pui32FirstInterfaceToken, const uint16_t* pui16Tokens, ClassType* psClassType)
{
    const uint32_t* pui32Tokens = (const uint32_t*)pui16Tokens;
    uint32_t ui32NameOffset = *pui32Tokens;
    pui16Tokens+= 2;

    psClassType->ui16ID = *pui16Tokens++;
    psClassType->ui16ConstBufStride = *pui16Tokens++;
    psClassType->ui16Texture = *pui16Tokens++;
    psClassType->ui16Sampler = *pui16Tokens++;

    ReadStringFromTokenStream((const uint32_t*)((const char*)pui32FirstInterfaceToken+ui32NameOffset), psClassType->Name);

    return pui16Tokens;
}

static const uint16_t* ReadClassInstance(const uint32_t* pui32FirstInterfaceToken, const uint16_t* pui16Tokens, ClassInstance* psClassInstance)
{
    uint32_t ui32NameOffset = *pui16Tokens++ << 16;
    ui32NameOffset |= *pui16Tokens++;

    psClassInstance->ui16ID = *pui16Tokens++;
    psClassInstance->ui16ConstBuf = *pui16Tokens++;
    psClassInstance->ui16ConstBufOffset = *pui16Tokens++;
    psClassInstance->ui16Texture = *pui16Tokens++;
    psClassInstance->ui16Sampler = *pui16Tokens++;

    ReadStringFromTokenStream((const uint32_t*)((const char*)pui32FirstInterfaceToken+ui32NameOffset), psClassInstance->Name);

    return pui16Tokens;
}


static void ReadInterfaces(const uint32_t* pui32Tokens,
                        ShaderInfo* psShaderInfo)
{
    uint32_t i;
    uint32_t ui32StartSlot;
    const uint32_t* pui32FirstInterfaceToken = pui32Tokens;
    const uint32_t ui32ClassInstanceCount = *pui32Tokens++;
    const uint32_t ui32ClassTypeCount = *pui32Tokens++;
    const uint32_t ui32InterfaceSlotRecordCount = *pui32Tokens++;
    const uint32_t ui32InterfaceSlotCount = *pui32Tokens++;
    const uint32_t ui32ClassInstanceOffset = *pui32Tokens++;
    const uint32_t ui32ClassTypeOffset = *pui32Tokens++;
    const uint32_t ui32InterfaceSlotOffset = *pui32Tokens++;

    const uint16_t* pui16ClassTypes = (const uint16_t*)((const char*)pui32FirstInterfaceToken + ui32ClassTypeOffset);
    const uint16_t* pui16ClassInstances = (const uint16_t*)((const char*)pui32FirstInterfaceToken + ui32ClassInstanceOffset);
    const uint32_t* pui32InterfaceSlots = (const uint32_t*)((const char*)pui32FirstInterfaceToken + ui32InterfaceSlotOffset);

    const uint32_t* pui32InterfaceSlotTokens = pui32InterfaceSlots;

    ClassType* psClassTypes;
    ClassInstance* psClassInstances;

    psClassTypes = new ClassType[ui32ClassTypeCount];
    for(i=0; i<ui32ClassTypeCount; ++i)
    {
        pui16ClassTypes = ReadClassType(pui32FirstInterfaceToken, pui16ClassTypes, psClassTypes+i);
        psClassTypes[i].ui16ID = (uint16_t)i;
    }

    psClassInstances = new ClassInstance[ui32ClassInstanceCount];
    for(i=0; i<ui32ClassInstanceCount; ++i)
    {
        pui16ClassInstances = ReadClassInstance(pui32FirstInterfaceToken, pui16ClassInstances, psClassInstances+i);
    }

    //Slots map function table to $ThisPointer cbuffer variable index
    ui32StartSlot = 0;
    for(i=0; i<ui32InterfaceSlotRecordCount;++i)
    {
        uint32_t k;
        
        const uint32_t ui32SlotSpan = *pui32InterfaceSlotTokens++;
        const uint32_t ui32Count = *pui32InterfaceSlotTokens++;
        const uint32_t ui32TypeIDOffset = *pui32InterfaceSlotTokens++;
        const uint32_t ui32TableIDOffset = *pui32InterfaceSlotTokens++;

        const uint16_t* pui16TypeID = (const uint16_t*)((const char*)pui32FirstInterfaceToken+ui32TypeIDOffset);
        const uint32_t* pui32TableID = (const uint32_t*)((const char*)pui32FirstInterfaceToken+ui32TableIDOffset);

        for(k=0; k < ui32Count; ++k)
        {
            psShaderInfo->aui32TableIDToTypeID[*pui32TableID++] = *pui16TypeID++;
        }

        ui32StartSlot += ui32SlotSpan;
    }

    psShaderInfo->ui32NumClassInstances = ui32ClassInstanceCount;
    psShaderInfo->psClassInstances = psClassInstances;

    psShaderInfo->ui32NumClassTypes = ui32ClassTypeCount;
    psShaderInfo->psClassTypes = psClassTypes;
}

void GetConstantBufferFromBindingPoint(const ResourceGroup eGroup, const uint32_t ui32BindPoint, const ShaderInfo* psShaderInfo, ConstantBuffer** ppsConstBuf)
{
	if(psShaderInfo->ui32MajorVersion > 3)
	{
		*ppsConstBuf = psShaderInfo->psConstantBuffers + psShaderInfo->aui32ResourceMap[eGroup].at(ui32BindPoint);
	}
	else
	{
		ASSERT(psShaderInfo->ui32NumConstantBuffers == 1);
		*ppsConstBuf = psShaderInfo->psConstantBuffers;
	}
}

int GetResourceFromBindingPoint(const ResourceGroup eGroup, uint32_t const ui32BindPoint, const ShaderInfo* psShaderInfo, ResourceBinding** ppsOutBinding)
{
    uint32_t i;
    const uint32_t ui32NumBindings = psShaderInfo->ui32NumResourceBindings;
    ResourceBinding* psBindings = psShaderInfo->psResourceBindings;

    for(i=0; i<ui32NumBindings; ++i)
    {
        if(ResourceTypeToResourceGroup(psBindings[i].eType) == eGroup)
        {
			if(ui32BindPoint >= psBindings[i].ui32BindPoint && ui32BindPoint < (psBindings[i].ui32BindPoint + psBindings[i].ui32BindCount))
			{
				*ppsOutBinding = psBindings + i;
				return 1;
			}
        }
    }
    return 0;
}

int GetInterfaceVarFromOffset(uint32_t ui32Offset, ShaderInfo* psShaderInfo, ShaderVar** ppsShaderVar)
{
    uint32_t i;
    ConstantBuffer* psThisPointerConstBuffer = psShaderInfo->psThisPointerConstBuffer;

    const uint32_t ui32NumVars = (uint32_t)psThisPointerConstBuffer->asVars.size();

    for(i=0; i<ui32NumVars; ++i)
    {
        if(ui32Offset >= psThisPointerConstBuffer->asVars[i].ui32StartOffset && 
            ui32Offset < (psThisPointerConstBuffer->asVars[i].ui32StartOffset + psThisPointerConstBuffer->asVars[i].ui32Size))
	    {
		    *ppsShaderVar = &psThisPointerConstBuffer->asVars[i];
		    return 1;
	    }
    }
    return 0;
}

// Added to calculate structure sizes rather than assume 4 bytes -DSS
uint32_t ShaderVarSize(ShaderVarType* psType, uint32_t* singularSize)
{
	uint32_t thisSize = 0;
	uint32_t m = 0;

	if(psType->Class == SVC_STRUCT)
	{
		for(m=0; m < psType->MemberCount; ++m)
			thisSize += ShaderVarSize(psType->Members + m, NULL);
	}
	else
		thisSize = psType->Columns * psType->Rows * 4;

	if(singularSize)
		*singularSize = thisSize;

	if(psType->Elements)
	{
#if 0
		// DarkStarSword: This assumption they are making is incorrect for
		// StructuredBuffers, and this can cause a variable following an array
		// to be misattributed as being part of the array. This assumption was
		// based on the alignment constraints enforced on constant buffers, so
		// upstreaming a patch for this would need to take into account the
		// kind of buffer being queried (we do not currently use this for
		// constant buffers). Our structured_buffers.hlsl test case includes a
		// couple of cases specifically for this issue in StructuredBuffers.


		// Everything smaller than vec4 in an array takes the space of vec4, except for the last one
		if (thisSize < 4 * 4)
		{
			thisSize = (4 * 4 * (psType->Elements - 1)) + thisSize;
		}
		else
		{
			thisSize *= psType->Elements;
		}
#else
		thisSize *= psType->Elements;
#endif
	}
	return thisSize;
}

// Manually added from latest James-Jones HLSLCrossCompiler for StructuredBuffer support -DSS
static int IsOffsetInType(ShaderVarType* psType,
						  uint32_t parentOffset,
						  uint32_t offsetToFind,
						  int32_t* pi32Index,
						  int32_t* pi32Rebase)
{
	uint32_t thisOffset = parentOffset + psType->Offset;
	// DarkStarSword: Changed this line to calculate arrays and nested struct sizes properly:
	uint32_t thisSize = ShaderVarSize(psType, NULL);

	if((offsetToFind >= thisOffset) &&
		offsetToFind < (thisOffset + thisSize))
	{

        if(psType->Class == SVC_MATRIX_ROWS || 
            psType->Class == SVC_MATRIX_COLUMNS)
        {
			//Matrices are treated as arrays of vectors.
			pi32Index[0] = (offsetToFind - thisOffset) / 16;
        }
		//Check for array of scalars or vectors (both take up 16 bytes per element)
		else if ((psType->Class == SVC_SCALAR || psType->Class == SVC_VECTOR) && psType->Elements > 1)
		{
			pi32Index[0] = (offsetToFind - thisOffset) / 16;
		}
		else if(psType->Class == SVC_VECTOR && psType->Columns > 1)
		{
			//Check for vector starting at a non-vec4 offset.

			// cbuffer $Globals
			// {
			//
			//   float angle;                       // Offset:    0 Size:     4
			//   float2 angle2;                     // Offset:    4 Size:     8
			//
			// }

			//cb0[0].x = angle
			//cb0[0].yzyy = angle2.xyxx

			//Rebase angle2 so that .y maps to .x, .z maps to .y

			pi32Rebase[0] = thisOffset % 16;
		}

		return 1;
	}
	return 0;
}

// Moved out of GetShaderVarFromOffset to handle nested structs -DSS
int GetShaderVarFromNestedStructOffset(ShaderVarType* psType,
									   const uint32_t parentOffset,
									   uint32_t ui32ByteOffset,
									   ShaderVarType** ppsShaderVar,
									   int32_t* pi32Index,
									   int32_t* pi32Rebase)
{
	uint32_t singularSize = 0;
	uint32_t thisSize = ShaderVarSize(psType, &singularSize);
	uint32_t thisOffset = parentOffset + psType->Offset;
	uint32_t m = 0;

	if(ui32ByteOffset < thisOffset ||
	   ui32ByteOffset >= thisOffset + thisSize)
		return 0;

	// We know we are somewhere inside the struct, but if this is an array of
	// structs and we are looking for an offset in a subsequent struct index we
	// need to adjust the offset to fit within the first struct index:
	ui32ByteOffset = (ui32ByteOffset - thisOffset) % singularSize + thisOffset;

	for(m=0; m < psType->MemberCount; ++m)
	{
		ShaderVarType* psMember = psType->Members + m;

		if(psMember->Class == SVC_STRUCT)
		{
			if(GetShaderVarFromNestedStructOffset(psMember, thisOffset, ui32ByteOffset, ppsShaderVar, pi32Index, pi32Rebase))
				return 1;
		}
		else
		{
			if(IsOffsetInType(psMember, thisOffset, ui32ByteOffset, pi32Index, pi32Rebase))
			{
				ppsShaderVar[0] = psMember;
				return 1;
			}
		}
	}
	return 0;
}

// Manually added from latest James-Jones HLSLCrossCompiler for StructuredBuffer support -DSS
int GetShaderVarFromOffset(const uint32_t ui32Vec4Offset,
						   const uint32_t* pui32Swizzle,
						   ConstantBuffer* psCBuf,
						   ShaderVarType** ppsShaderVar,
						   int32_t* pi32Index,
						   int32_t* pi32Rebase)
{
    uint32_t i;
    uint32_t ui32ByteOffset = ui32Vec4Offset * 16;

    const uint32_t ui32NumVars = (uint32_t)psCBuf->asVars.size();

	if(psCBuf->iUnsized && ui32NumVars == 1 && psCBuf->asVars[0].sType.Class != SVC_STRUCT)
	{
		ppsShaderVar[0] = &psCBuf->asVars[0].sType;
		return 1;
	}

    //Swizzle can point to another variable. In the example below
	//cbUIUpdates.g_uMaxFaces would be cb1[2].z. The scalars are combined
	//into vectors. psCBuf->ui32NumVars will be 3.

	// cbuffer cbUIUpdates
	// {
	//
	//   float g_fLifeSpan;                 // Offset:    0 Size:     4
	//   float g_fLifeSpanVar;              // Offset:    4 Size:     4 [unused]
	//   float g_fRadiusMin;                // Offset:    8 Size:     4 [unused]
	//   float g_fRadiusMax;                // Offset:   12 Size:     4 [unused]
	//   float g_fGrowTime;                 // Offset:   16 Size:     4 [unused]
	//   float g_fStepSize;                 // Offset:   20 Size:     4
	//   float g_fTurnRate;                 // Offset:   24 Size:     4
	//   float g_fTurnSpeed;                // Offset:   28 Size:     4 [unused]
	//   float g_fLeafRate;                 // Offset:   32 Size:     4
	//   float g_fShrinkTime;               // Offset:   36 Size:     4 [unused]
	//   uint g_uMaxFaces;                  // Offset:   40 Size:     4
	//
	// }

	// Name                                 Type  Format         Dim Slot Elements
	// ------------------------------ ---------- ------- ----------- ---- --------
	// cbUIUpdates                       cbuffer      NA          NA    1        1

    if(pui32Swizzle[0] == OPERAND_4_COMPONENT_Y)
    {
        ui32ByteOffset += 4;
    }
    else
    if(pui32Swizzle[0] == OPERAND_4_COMPONENT_Z)
    {
        ui32ByteOffset += 8;
    }
    else
    if(pui32Swizzle[0] == OPERAND_4_COMPONENT_W)
    {
        ui32ByteOffset += 12;
    }

    for(i=0; i<ui32NumVars; ++i)
    {
		if(psCBuf->asVars[i].sType.Class == SVC_STRUCT)
		{
			if(GetShaderVarFromNestedStructOffset(&psCBuf->asVars[i].sType, psCBuf->asVars[i].ui32StartOffset, ui32ByteOffset, ppsShaderVar, pi32Index, pi32Rebase))
				return 1;
		}
		else
		{
			if(IsOffsetInType(&psCBuf->asVars[i].sType, psCBuf->asVars[i].ui32StartOffset, ui32ByteOffset, pi32Index, pi32Rebase))
			{
				ppsShaderVar[0] = &psCBuf->asVars[i].sType;
				return 1;
			}
		}
    }
    return 0;
}

ResourceGroup ResourceTypeToResourceGroup(ResourceType eType)
{
	switch(eType)
	{
	case RTYPE_CBUFFER:
		return RGROUP_CBUFFER;

	case RTYPE_SAMPLER:
		return RGROUP_SAMPLER;

	case RTYPE_TEXTURE:
	case RTYPE_BYTEADDRESS:
	case RTYPE_STRUCTURED:
		return RGROUP_TEXTURE;

	case RTYPE_UAV_RWTYPED:
	case RTYPE_UAV_RWSTRUCTURED:
	case RTYPE_UAV_RWBYTEADDRESS:
	case RTYPE_UAV_APPEND_STRUCTURED:
	case RTYPE_UAV_CONSUME_STRUCTURED:
	case RTYPE_UAV_RWSTRUCTURED_WITH_COUNTER:
		return RGROUP_UAV;

	case RTYPE_TBUFFER:
		// DarkStarSword: Removed this assert - tbuffers appear in several of
		// our game example test cases and do indeed map to 't' registers.
		//ASSERT(0); // Need to find out which group this belongs to
		return RGROUP_TEXTURE;
	}

	ASSERT(0);
	return RGROUP_CBUFFER;
}

void LoadShaderInfo(const uint32_t ui32MajorVersion,
    const uint32_t ui32MinorVersion,
    const ReflectionChunks* psChunks,
    ShaderInfo* psInfo)
{
    const uint32_t* pui32Inputs = psChunks->pui32Inputs;
	const uint32_t* pui32Inputs11 = psChunks->pui32Inputs11;
    const uint32_t* pui32Resources = psChunks->pui32Resources;
    const uint32_t* pui32Interfaces = psChunks->pui32Interfaces;
    const uint32_t* pui32Outputs = psChunks->pui32Outputs;
	const uint32_t* pui32Outputs11 = psChunks->pui32Outputs11;
	const uint32_t* pui32OutputsWithStreams = psChunks->pui32OutputsWithStreams;
	const uint32_t* pui32PatchConstants = psChunks->pui32PatchConstants;

    psInfo->eTessOutPrim = TESSELLATOR_OUTPUT_UNDEFINED;
    psInfo->eTessPartitioning = TESSELLATOR_PARTITIONING_UNDEFINED;

    psInfo->ui32MajorVersion = ui32MajorVersion;
    psInfo->ui32MinorVersion = ui32MinorVersion;


    if(pui32Inputs)
        ReadInputSignatures(pui32Inputs, psInfo, 0);
    if(pui32Inputs11)
        ReadInputSignatures(pui32Inputs11, psInfo, 1);
    if(pui32Resources)
        ReadResources(pui32Resources, psInfo);
    if(pui32Interfaces)
        ReadInterfaces(pui32Interfaces, psInfo);
    if(pui32Outputs)
        ReadOutputSignatures(pui32Outputs, psInfo, 0, 0);
    if(pui32Outputs11)
        ReadOutputSignatures(pui32Outputs11, psInfo, 1, 1);
	if(pui32OutputsWithStreams)
		ReadOutputSignatures(pui32OutputsWithStreams, psInfo, 0, 1);
	if(pui32PatchConstants)
		ReadPatchConstantSignatures(pui32PatchConstants, psInfo, 0, 0);

    {
        uint32_t i;
        for(i=0; i<psInfo->ui32NumConstantBuffers;++i)
        {
            std::string cbufName = psInfo->psConstantBuffers[i].Name;
            std::string cbufThisPointer = "$ThisPointer";
            if(cbufName.compare(cbufThisPointer) == 0)
            {
                psInfo->psThisPointerConstBuffer = &psInfo->psConstantBuffers[i];
            }
        }
    }
}

void FreeShaderInfo(ShaderInfo* psShaderInfo)
{
    delete[] psShaderInfo->psInputSignatures; psShaderInfo->psInputSignatures = 0;
    delete[] psShaderInfo->psResourceBindings; psShaderInfo->psResourceBindings = 0;
    delete[] psShaderInfo->psConstantBuffers; psShaderInfo->psConstantBuffers = 0;
    delete[] psShaderInfo->psClassTypes; psShaderInfo->psClassTypes = 0;
    delete[] psShaderInfo->psClassInstances; psShaderInfo->psClassInstances = 0;
    delete[] psShaderInfo->psOutputSignatures; psShaderInfo->psOutputSignatures = 0;
    delete[] psShaderInfo->psPatchConstantSignatures; psShaderInfo->psPatchConstantSignatures = 0;

    psShaderInfo->ui32NumInputSignatures = 0;
    psShaderInfo->ui32NumResourceBindings = 0;
    psShaderInfo->ui32NumConstantBuffers = 0;
    psShaderInfo->ui32NumClassTypes = 0;
    psShaderInfo->ui32NumClassInstances = 0;
    psShaderInfo->ui32NumOutputSignatures = 0;
	psShaderInfo->ui32NumPatchConstantSignatures = 0;
}

struct ConstantTableD3D9
{
    uint32_t size;
    uint32_t creator;
    uint32_t version;
    uint32_t constants;
    uint32_t constantInfos;
    uint32_t flags;
    uint32_t target;
};

// These enums match those in d3dx9shader.h.
enum RegisterSet
{
    RS_BOOL,
    RS_INT4,
    RS_FLOAT4,
    RS_SAMPLER,
};

enum TypeClass
{
    CLASS_SCALAR,
    CLASS_VECTOR,
    CLASS_MATRIX_ROWS,
    CLASS_MATRIX_COLUMNS,
    CLASS_OBJECT,
    CLASS_STRUCT,
};

enum Type
{
    PT_VOID,
    PT_BOOL,
    PT_INT,
    PT_FLOAT,
    PT_STRING,
    PT_TEXTURE,
    PT_TEXTURE1D,
    PT_TEXTURE2D,
    PT_TEXTURE3D,
    PT_TEXTURECUBE,
    PT_SAMPLER,
    PT_SAMPLER1D,
    PT_SAMPLER2D,
    PT_SAMPLER3D,
    PT_SAMPLERCUBE,
    PT_PIXELSHADER,
    PT_VERTEXSHADER,
    PT_PIXELFRAGMENT,
    PT_VERTEXFRAGMENT,
    PT_UNSUPPORTED,
};
typedef struct ConstantInfoD3D9_TAG
{
    uint32_t name;
    uint16_t registerSet;
    uint16_t registerIndex;
    uint16_t registerCount;
    uint16_t reserved;
    uint32_t typeInfo;
    uint32_t defaultValue;
} ConstantInfoD3D9;

typedef struct TypeInfoD3D9_TAG
{
    uint16_t typeClass;
    uint16_t type;
    uint16_t rows;
    uint16_t columns;
    uint16_t elements;
    uint16_t structMembers;
    uint32_t structMemberInfos;
} TypeInfoD3D9;

typedef struct StructMemberInfoD3D9_TAG
{
    uint32_t name;
    uint32_t typeInfo;
} StructMemberInfoD3D9;

void LoadD3D9ConstantTable(const char* data,
    ShaderInfo* psInfo)
{
    ConstantTableD3D9* ctab;
    uint32_t constNum;
    ConstantInfoD3D9* cinfos;
    ConstantBuffer* psConstantBuffer;
    uint32_t ui32ConstantBufferSize = 0;
	uint32_t numResourceBindingsNeeded = 0;

    ctab = (ConstantTableD3D9*)data;

    cinfos = (ConstantInfoD3D9*) (data + ctab->constantInfos);

    psInfo->ui32NumConstantBuffers++;

    //Only 1 Constant Table in d3d9
    ASSERT(psInfo->ui32NumConstantBuffers==1);

    psConstantBuffer = new ConstantBuffer[1];

    psInfo->psConstantBuffers = psConstantBuffer;

    psConstantBuffer->Name = "$Globals";

	//Determine how many resource bindings to create
	for(constNum = 0; constNum < ctab->constants; ++constNum)
	{
		if(cinfos[constNum].registerSet == RS_SAMPLER)
		{
			++numResourceBindingsNeeded;
		}
	}

	psInfo->psResourceBindings = new ResourceBinding[numResourceBindingsNeeded];

    for(constNum = 0; constNum < ctab->constants; ++constNum)
    {
		TypeInfoD3D9* typeInfo = (TypeInfoD3D9*) (data + cinfos[constNum].typeInfo);
		ShaderVar var;

		if(cinfos[constNum].registerSet != RS_SAMPLER)
		{
	        var.Name = data + cinfos[constNum].name;
    	    var.ui32Size = cinfos[constNum].registerCount * 16;
       		var.ui32StartOffset = cinfos[constNum].registerIndex * 16;
			var.haveDefaultValue = false;

			if(ui32ConstantBufferSize < (var.ui32Size + var.ui32StartOffset))
			{
				ui32ConstantBufferSize = var.ui32Size + var.ui32StartOffset;
			}

			var.sType.Rows = typeInfo->rows;
			var.sType.Columns = typeInfo->columns;
			var.sType.Elements = typeInfo->elements;
			var.sType.MemberCount = typeInfo->structMembers;
			var.sType.Members = 0;
			var.sType.Offset = 0;
			var.sType.FullName = var.Name;
			var.sType.Parent = 0;
			var.sType.ParentCount = 0;

			switch(typeInfo->typeClass)
			{
				case CLASS_SCALAR:
				{
					var.sType.Class = SVC_SCALAR;
					break;
				}
				case CLASS_VECTOR:
				{
					var.sType.Class = SVC_VECTOR;
					break;
				}
				case CLASS_MATRIX_ROWS:
				{
					var.sType.Class = SVC_MATRIX_ROWS;
					break;
				}
				case CLASS_MATRIX_COLUMNS:
				{
					var.sType.Class = SVC_MATRIX_COLUMNS;
					break;
				}
				case CLASS_OBJECT:
				{
					var.sType.Class = SVC_OBJECT;
					break;
				}
				case CLASS_STRUCT:
				{
					var.sType.Class = SVC_STRUCT;
					break;
				}
			}

			switch(cinfos[constNum].registerSet)
			{
				case RS_BOOL:
				{
					var.sType.Type = SVT_BOOL;
					break;
				}
				case RS_INT4:
				{
					var.sType.Type = SVT_INT;
					break;
				}
				case RS_FLOAT4:
				{
					var.sType.Type = SVT_FLOAT;
					break;
				}
			}
			psConstantBuffer->asVars.push_back(var);
		}
		else
		{
			// Create a resource if it is sampler in order to replicate the d3d10+
			// method of separating samplers from general constants.
			uint32_t ui32ResourceIndex = psInfo->ui32NumResourceBindings++;
			ResourceBinding* res = &psInfo->psResourceBindings[ui32ResourceIndex];

			res->Name = data + cinfos[constNum].name;

			res->ui32BindPoint = cinfos[constNum].registerIndex;
			res->ui32BindCount = cinfos[constNum].registerCount;
			res->ui32Flags = 0;
			res->ui32NumSamples = 1;
			res->ui32ReturnType = 0;

			res->eType = RTYPE_TEXTURE;

			switch(typeInfo->type)
			{
			case PT_SAMPLER:
			case PT_SAMPLER1D:
				res->eDimension = REFLECT_RESOURCE_DIMENSION_TEXTURE1D;
				break;
			case PT_SAMPLER2D:
				res->eDimension = REFLECT_RESOURCE_DIMENSION_TEXTURE2D;
				break;
			case PT_SAMPLER3D:
				res->eDimension = REFLECT_RESOURCE_DIMENSION_TEXTURE2D;
				break;
			case PT_SAMPLERCUBE:
				res->eDimension = REFLECT_RESOURCE_DIMENSION_TEXTURECUBE;
				break;
			}
		}
    }
    psConstantBuffer->ui32TotalSizeInBytes = ui32ConstantBufferSize;
	psConstantBuffer->iUnsized = 0;
}
