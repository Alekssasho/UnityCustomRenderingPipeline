#ifndef CUSTOM_UNLIT_INPUT_INCLUDED
#define CUSTOM_UNLIT_INPUT_INCLUDED

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
	UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
	UNITY_DEFINE_INSTANCED_PROP(float, _ZWrite)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, name)

struct InputConfig
{
	Fragment fragment;
	float4 color;
	float2 baseUV;
	float2 detailUV;
	float3 flipbookUVB;
	bool flipbookBlending;
};

InputConfig GetInputConfig(float4 positionSS, float2 baseUV, float2 detailUV = 0.0)
{
	InputConfig c;
	c.fragment = GetFragment(positionSS);
	c.color = 1.0;
	c.baseUV = baseUV;
	c.detailUV = detailUV;
	c.flipbookUVB = 0.0f;
	c.flipbookBlending = false;
	return c;
}


float2 TransformBaseUV(float2 baseUV)
{
	float4 baseST = INPUT_PROP(_BaseMap_ST);
	return baseUV * baseST.xy + baseST.zw;
}

float4 GetBase(InputConfig c)
{
	float4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, c.baseUV);
	if(c.flipbookBlending)
	{
		map = lerp(
			map, SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, c.flipbookUVB.xy),
			c.flipbookUVB.z
		);
	}
	float4 color = INPUT_PROP(_BaseColor);
	return map * color * c.color;
}

float3 GetEmission(InputConfig c)
{
	return GetBase(c).rgb;
}

float GetCutoff(InputConfig c)
{
	return INPUT_PROP(_Cutoff);
}

float GetMetallic(InputConfig c)
{
	return 0.0;
}

float GetSmoothness(InputConfig c)
{
	return 0.0;
}

float GetFresnel(InputConfig c)
{
	return 0.0;
}

float GetFinalAlpha(float alpha)
{
	return INPUT_PROP(_ZWrite) ? 1.0 : alpha;
}

#endif