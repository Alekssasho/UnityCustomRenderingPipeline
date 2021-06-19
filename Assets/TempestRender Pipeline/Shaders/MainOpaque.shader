Shader "Tempest RP/MainOpaque"
{
	Properties
	{
		_BaseColor("Color", Color) = (0.5, 0.5, 0.5, 1.0)
	}
	SubShader
	{
		Pass
		{
			Tags {
				"LightMode" = "TempestMainOpaque"
			}

			HLSLPROGRAM
#pragma target 3.5
//#pragma enable_d3d11_debug_symbols
#pragma vertex MainOpaqueVS
#pragma fragment MainOpaquePS
#include "MainOpaquePass.hlsl"
			ENDHLSL
		}
	}
}
