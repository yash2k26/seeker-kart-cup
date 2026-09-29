// Lit Colormap with the albedo read from vertex colour instead of a texture: the generated terrain paints its
// biomes (sand, grass, rock, snow) per vertex, so one material covers a whole map and one draw call per chunk.
// Flat shading comes from the mesh (unique verts per face), so normals are used as they arrive.
Shader "KartCup/Terrain"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _ShadowTint ("Shadow brightness", Range(0,1)) = 0.45
        _Wrap ("Light wrap", Range(0,1)) = 0.28
        _AmbientBoost ("Ambient boost", Range(0,2)) = 1
        _DetailMap ("Detail (world tiled)", 2D) = "gray" {}
        _DetailTile ("Detail tile (m)", Float) = 7
        _DetailStrength ("Detail strength", Range(0,1)) = 0
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
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_DetailMap); SAMPLER(sampler_DetailMap);

            // Every pass must declare this identically, floats only: a half here and a float there shifts the
            // offsets and the SRP Batcher hands one material's constants to everything using the shader.
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _ShadowTint;
                float _Wrap;
                float _AmbientBoost;
                float _DetailTile;
                float _DetailStrength;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 colour : COLOR; };
            struct V
            {
                float4 positionCS : SV_POSITION;
                float4 colour : COLOR;
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
                o.colour = i.colour;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float4 frag (V i) : SV_Target
            {
                float4 albedo = i.colour * _BaseColor;
                // a world-tiled detail (sand grain) over the per-face colour, so the big flat faces read up close
                float3 detail = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, i.positionWS.xz / _DetailTile).rgb;
                albedo.rgb *= lerp(1.0, detail * 2.0, _DetailStrength);
                float3 n = normalize(i.normalWS);

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light main = GetMainLight(shadowCoord);

                // wrapped lambert keeps undersides readable instead of black
                float ndl = saturate(dot(n, main.direction) * (1 - _Wrap) + _Wrap);
                float shadow = lerp(_ShadowTint, 1.0, main.shadowAttenuation);
                float3 ambient = SampleSH(n) * _AmbientBoost;
                float3 colour = albedo.rgb * (ambient + main.color * ndl * shadow);
                #ifdef _ADDITIONAL_LIGHTS
                // The lamp posts' pools on the ground beside the road. URP's loop macros cover both light paths:
                // per-object lists (the phone's Forward) and the clustered list (Forward+), which reads `inputData`.
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                uint lights = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lights)
                    Light l = GetAdditionalLight(lightIndex, i.positionWS);
                    colour += albedo.rgb * l.color * l.distanceAttenuation * saturate(dot(n, l.direction) * (1 - _Wrap) + _Wrap);
                LIGHT_LOOP_END
                #endif

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
                float4 _BaseColor;
                float _ShadowTint;
                float _Wrap;
                float _AmbientBoost;
                float _DetailTile;
                float _DetailStrength;
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
