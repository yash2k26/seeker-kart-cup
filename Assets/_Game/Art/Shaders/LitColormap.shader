// Kenney palette look with real lighting: one directional light, hard shadows, SH ambient, fog.
// Much cheaper than URP/Lit (no specular, no BRDF, one light) but it gives the models form and grounds them
// with shadows — the flat unlit version read as a prototype.
Shader "KartCup/Lit Colormap"
{
    Properties
    {
        _BaseMap ("Colormap", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _ShadowTint ("Shadow brightness", Range(0,1)) = 0.45
        _Wrap ("Light wrap", Range(0,1)) = 0.28
        _AmbientBoost ("Ambient boost", Range(0,2)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            // Every pass must declare this identically, floats only: a half here and a float there shifts the
            // offsets and the SRP Batcher hands one material's constants to everything using the shader.
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _ShadowTint;
                float _Wrap;
                float _AmbientBoost;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct V
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float fog : TEXCOORD3;
            };

            V vert (A i)
            {
                V o;
                float3 positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float4 frag (V i) : SV_Target
            {
                float4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                float3 n = normalize(i.normalWS);

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light main = GetMainLight(shadowCoord);

                // wrapped lambert keeps undersides readable instead of black
                float ndl = saturate(dot(n, main.direction) * (1 - _Wrap) + _Wrap);
                float shadow = lerp(_ShadowTint, 1.0, main.shadowAttenuation);
                float3 ambient = SampleSH(n) * _AmbientBoost;
                float3 colour = albedo.rgb * (ambient + main.color * ndl * shadow);

                colour = MixFog(colour, i.fog);
                return float4(colour, albedo.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // Every pass must declare this identically, floats only: a half here and a float there shifts the
            // offsets and the SRP Batcher hands one material's constants to everything using the shader.
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _ShadowTint;
                float _Wrap;
                float _AmbientBoost;
            CBUFFER_END

            float3 _LightDirection;

            struct SA { float4 positionOS : POSITION; float3 normalOS : NORMAL; };

            float4 shadowVert (SA i) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(i.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(i.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            float4 shadowFrag () : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
