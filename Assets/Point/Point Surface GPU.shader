Shader "Graph/Point Surface GPU"
{
	Properties{
		_Smoothness("Smoothness", Range(0, 1)) = 0.5
	}

		SubShader{
			CGPROGRAM
#pragma surface ConfigureSurface Standard fullforwardshadows addshadow
#pragma instancing_options assumeuniformscaling procedural:ConfigureProcedural
#pragma editor_sync_compilation
#pragma target 4.5

			struct Input {
				float3 worldPos;
			};

			float _Smoothness;

#include "PointGPU.hlsl"

			void ConfigureSurface(Input input, inout SurfaceOutputStandard surface)
			{
				surface.Albedo.rg = input.worldPos.xy * 0.5 + 0.5;
				surface.Smoothness = _Smoothness;
			}
			ENDCG
	}

		Fallback "Diffuse"
}