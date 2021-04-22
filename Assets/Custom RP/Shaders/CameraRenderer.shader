Shader "Hidden/Custom RP/Camera Renderer"
{
	SubShader
	{
		Cull Off
		ZTest Always
		ZWrite Off

		Pass
		{
			Name "Copy"

			Blend [_CameraSrcBlend] [_CameraDstBlend]

			HLSLPROGRAM
			#include "../ShaderLibrary/Common.hlsl"
			#include "CameraRendererPasses.hlsl"
#pragma target 3.5
//#pragma enable_d3d11_debug_symbols
#pragma vertex DefaultPassVertex
#pragma fragment CopyPassFragment
			ENDHLSL
		}

		Pass
		{
			Name "Copy Depth"

			ColorMask 0
			ZWrite On

			HLSLPROGRAM
			#include "../ShaderLibrary/Common.hlsl"
			#include "CameraRendererPasses.hlsl"
#pragma target 3.5
//#pragma enable_d3d11_debug_symbols
#pragma vertex DefaultPassVertex
#pragma fragment CopyDepthPassFragment
			ENDHLSL
		}

		Pass
		{
			Name "Accumulate Path Tracing"

			Blend SrcAlpha OneMinusSrcAlpha

			HLSLPROGRAM
			#include "../ShaderLibrary/Common.hlsl"
			#include "PathTracingPasses.hlsl"
#pragma target 3.5
//#pragma enable_d3d11_debug_symbols
#pragma vertex AccumulateVertex
#pragma fragment AccumulateFragment
			ENDHLSL
		}
	}
}
