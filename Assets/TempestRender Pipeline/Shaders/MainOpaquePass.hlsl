#ifndef TEMPEST_MAIN_OPAQUE_PASS_INCLUDED
#define TEMPEST_MAIN_OPAQUE_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl" 

CBUFFER_START(UnityPerDraw)
	float4x4 unity_ObjectToWorld;
	float4x4 unity_WorldToObject;
	real4 unity_WorldTransformParams;
CBUFFER_END

CBUFFER_START(UnityPerMaterial)
	float4 _BaseColor;
CBUFFER_END

float4x4 unity_MatrixVP;
float4x4 unity_MatrixV;
float4x4 glstate_matrix_projection;

#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#define UNITY_MATRIX_V unity_MatrixV
#define UNITY_MATRIX_VP unity_MatrixVP
#define UNITY_MATRIX_P glstate_matrix_projection

#if defined(_SHADOW_MASK_ALWAYS) || defined(_SHADOW_MASK_DISTANCE)
#define SHADOWS_SHADOWMASK
#endif
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

struct VertexLayout
{
	float3 position : POSITION;
};

struct VertexOutput
{
	float4 position : SV_POSITION;
};

VertexOutput MainOpaqueVS(VertexLayout input)
{
	VertexOutput output;

	float3 worldPosition = TransformObjectToWorld(input.position);
	output.position = TransformWorldToHClip(worldPosition);
	return output;
}

float4 MainOpaquePS(VertexOutput input) : SV_TARGET
{
	return _BaseColor;
}

#endif