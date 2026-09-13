// Flat Kenney look: colormap × tint, optional per-vertex directional shade (no lights, no shadows, no lightmaps).
// SRP-batcher compatible (all per-material data in UnityPerMaterial). Cheapest possible fragment on Mali.
Shader "KartCup/Unlit Colormap"
{
    Properties
    {
        _BaseMap ("Colormap", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _ShadeStrength ("Vertex shade strength", Range(0,1)) = 0.35
        _ShadeDir ("Shade direction (world)", Vector) = (0.4, 0.8, 0.3, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _ShadeStrength;
                float4 _ShadeDir;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half shade : TEXCOORD1; half fog : TEXCOORD2; };

            V vert (A i)
            {
                V o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                float3 n = TransformObjectToWorldNormal(i.normalOS);
                half nl = saturate(dot(n, normalize(_ShadeDir.xyz)));
                o.shade = lerp(1.0h - _ShadeStrength, 1.0h, nl);      // per-vertex: free on a 3k-tri kart
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag (V i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                c.rgb *= i.shade;
                c.rgb = MixFog(c.rgb, i.fog);
                return c;
            }
            ENDHLSL
        }
    }
}
