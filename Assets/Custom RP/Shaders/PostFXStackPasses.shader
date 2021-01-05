Shader "Hidden/Custom RP/Post FX Stack"
{
	SubShader
	{
		Cull Off
		ZTest Always
		ZWrite Off

		HLSLINCLUDE
		#include "../ShaderLibrary/Common.hlsl"
		#include "PostFXStackPasses.hlsl"
		ENDHLSL

		Pass
		{
			Name "Copy"

			HLSLPROGRAM
#pragma target 3.5
//#pragma enable_d3d11_debug_symbols
#pragma vertex DefaultPassVertex
#pragma fragment CopyPassFragment
			ENDHLSL
		}
	}
}
