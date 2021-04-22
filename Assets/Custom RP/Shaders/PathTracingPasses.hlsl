#ifndef CUSTOM_CAMERA_RENDERER_PASSES_INCLUDED
#define CUSTOM_CAMERA_RENDERER_PASSES_INCLUDED

TEXTURE2D(_SourceTexture);
float g_FrameIndex;

struct Varyings
{
	float4 positionCS_SS : SV_POSITION;
	float2 screenUV : VAR_SCREEN_UV;
};

Varyings AccumulateVertex(uint vertexID : SV_VertexID)
{
	Varyings output;
	output.positionCS_SS = float4(
		vertexID <= 1 ? -1.0 : 3.0,
		vertexID == 1 ? 3.0 : -1.0,
		0.0, 1.0
	);
	output.screenUV = float2(
		vertexID <= 1 ? 0.0 : 2.0,
		vertexID == 1 ? 2.0 : 0.0
	);
	if(_ProjectionParams.x < 0.0)
	{
		output.screenUV.y = 1.0 - output.screenUV.y;
	}
	return output;
}

float4 AccumulateFragment(Varyings input) : SV_TARGET
{
	float4 currentResult = SAMPLE_TEXTURE2D_LOD(_SourceTexture, sampler_point_clamp, input.screenUV, 0);
	currentResult.a = 1.0 / (g_FrameIndex + 1.0);
	return currentResult;
}

#endif