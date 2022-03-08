Shader "PointRender"
{
	Properties
	{
		_Smoothness("Smoothness", Range(0, 1)) = 0.85
		_Metal("Metal", Range(0, 1)) = 0.25
		_Opacity("Opacity", Range(0, 10)) = 0.4
	}

	SubShader
	{

		Pass
		{
			CGPROGRAM
			#pragma enable_d3d11_debug_symbols
			// Physically based Standard lighting model
			#pragma multi_compile_instancing
			#pragma multi_compile __ CUSTOM_REFLECTION_PROBE
			#pragma instancing_options procedural:setup
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0

			#include "UnityCG.cginc"
			#include "UnityStandardBRDF.cginc"
			#include "UnityImageBasedLighting.cginc"

			struct VIn
			{
				float4 position : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct VOut
			{
				float4 position : POSITION;
				float3 raydir : TEXCOORD1;
				float2 uv : TEXCOORD0;
			};

			struct PSout
			{
				float4 color : COLOR;
				float depth : DEPTH;
			};

		    StructuredBuffer<float4> Target;

			float exposure;
			float2 Resolution;
			float3 CameraPosition;
			float4x4 ViewProjection;
			float4x4 ViewProjectionInverse;

			uint getPixelID(float2 uv)
			{
				uint2 pix= uint2(floor(0.5 * (uv + 1.0) * Resolution));
				return pix.x + pix.y * uint(Resolution.x);
			}

			VOut vert(VIn input)
			{
				VOut output;
				
				output.position = float4(2.0 * input.position.x, -2.0 * input.position.y, 0.001, 1.0);
				output.uv = 2.0*input.position.xy;

				return output;
			}

			float3 tonemap(float3 x)
			{
				float l = length(x) / sqrt(3.0);
				x =lerp(x,l,1.5*(smoothstep(0., 140.0, l) - 0.5));
				return pow(x, 0.57);
			}

			PSout frag(VOut output)
			{
				PSout fo;
				
				uint pid = getPixelID(output.uv);
				float3 col = Target[pid].xyz;
				fo.color.xyz = tanh(exposure * tonemap(col));
				//fo.color.xyz = col/256.0;
				fo.depth = 0.0;

				return fo;
			}

			ENDCG
		}
	}
}
