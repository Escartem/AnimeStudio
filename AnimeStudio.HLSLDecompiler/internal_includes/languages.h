#ifndef LANGUAGES_H
#define LANGUAGES_H

#include "hlslcc.h"

static int InOutSupported(const GLLang eLang)
{
	if(eLang == LANG_ES_100 || eLang == LANG_120)
	{
		return 0;
	}
	return 1;
}

static int WriteToFragData(const GLLang eLang)
{
	if(eLang == LANG_ES_100 || eLang == LANG_120)
	{
		return 1;
	}
	return 0;
}

static int ShaderBitEncodingSupported(const GLLang eLang)
{
	if(eLang != LANG_ES_300 && eLang < LANG_330)
	{
		return 0;
	}
	return 1;
}

static int HaveOverloadedTextureFuncs(const GLLang eLang)
{
	if(eLang == LANG_ES_100 || eLang == LANG_120)
	{
		return 0;
	}
	return 1;
}

//Only enable for ES.
//Not present in 120, ignored in other desktop languages.
static int HavePrecisionQualifers(const GLLang eLang)
{
	if(eLang == LANG_ES_100 || eLang == LANG_ES_300)
	{
		return 1;
	}
	return 0;
}

//Only on vertex inputs and pixel outputs.
static int HaveLimitedInOutLocationQualifier(const GLLang eLang)
{
    if(eLang >= LANG_330 || eLang == LANG_ES_300)
    {
        return 1;
    }
    return 0;
}

static int HaveInOutLocationQualifier(const GLLang eLang)
{
    if(eLang >= LANG_410)
    {
        return 1;
    }
    return 0;
}

//layout(binding = X) uniform {uniformA; uniformB;}
//layout(location = X) uniform uniform_name;
static int HaveUniformBindingsAndLocations(const GLLang eLang)
{
#if defined(WORK_IN_PROGRESS)
    if(eLang >= LANG_430)
    {
        return 1;
    }
#endif
    return 0;
}

static int DualSourceBlendSupported(const GLLang eLang)
{
    if(eLang >= LANG_330)
    {
        return 1;
    }
    return 0;
}

static int SubroutinesSupported(const GLLang eLang)
{
    if(eLang >= LANG_400)
    {
        return 1;
    }
    return 0;
}

//Before 430, flat/smooth/centroid/noperspective must match
//between fragment and its previous stage.
//HLSL bytecode only tells us the interpolation in pixel shader.
static int PixelInterpDependency(const GLLang eLang)
{
    if(eLang < LANG_430)
    {
        return 1;
    }
    return 0;
}

static int HaveImageLoadStore(const GLLang eLang)
{
	if(eLang >= LANG_420 || eLang == LANG_ES_310)
	{
		return 1;
	}
	return 0;
}

#endif
