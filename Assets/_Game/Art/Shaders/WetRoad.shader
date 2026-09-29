// The generated road with a real surface: URP's own PBR (so the lamp posts, the headlights and the moon reflect in
// it), a normal-mapped asphalt tile laid in metres, and at night standing water in the low spots from a world-space
// noise. The two lanes wear drier and darker where the tyres run, so the road reads as a road and not a mirror.
// _Wet 0 is plain dry asphalt for the daytime maps.
Shader "KartCup/Wet Road"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "gray" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _RoughMap ("Roughness", 2D) = "white" {}
        _PuddleMap ("Puddle noise", 2D) = "gray" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _Tile ("Tile (m)", Float) = 3
        _PuddleTile ("Puddle tile (m)", Float) = 38
        _BumpScale ("Normal strength", Range(0,2)) = 1
        _Wet ("Wetness", Range(0,1)) = 0
        _Puddles ("Puddles", Range(0,1)) = 0.6
        _LaneOffset ("Lane centre (m)", Float) = 5
        _LaneHalf ("Lane half width (m)", Float) = 2.6
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

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_RoughMap); SAMPLER(sampler_RoughMap);
            TEXTURE2D(_PuddleMap); SAMPLER(sampler_PuddleMap);
            // One pass, but declared the way the batcher wants anyway: floats only, in one order.
            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Tile;
                float _PuddleTile;
                float _BumpScale;
                float _Wet;
                float _Puddles;
                float _LaneOffset;
                float _LaneHalf;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; };
            struct V
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;             // metres: x across the road from its centreline, y along it
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                float fog : TEXCOORD4;
            };

            V vert (A i)
            {
                V o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.tangentWS = float4(TransformObjectToWorldDir(i.tangentOS.xyz), i.tangentOS.w * GetOddNegativeScale());
                o.uv = i.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float4 frag (V i) : SV_Target
            {
                float2 uv = i.uv / _Tile;
                float3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb * _Tint.rgb;
                float3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
                float rough = SAMPLE_TEXTURE2D(_RoughMap, sampler_RoughMap, uv).r;
                float noise = SAMPLE_TEXTURE2D(_PuddleMap, sampler_PuddleMap, i.positionWS.xz / _PuddleTile).r;

                // The lanes: rubbered darker and worn drier where the tyres run; the water stands between and beside them.
                float lane = 1.0 - smoothstep(_LaneHalf * 0.6, _LaneHalf, abs(abs(i.uv.x) - _LaneOffset));
                float wet = _Wet * lerp(1.0, 0.5, lane);
                float pool = smoothstep(0.58, 0.74, noise) * _Puddles * _Wet * (1.0 - lane);
                // Wet asphalt is darker, a puddle darker still; gently, because the tile is already ~0.1 linear and the
                // lamps' pools have to read on it (at 0.04 they vanished while the pale kerbs beside them saturated).
                albedo *= lerp(1.0, 0.9, lane);
                albedo *= lerp(1.0, 0.72, wet) * lerp(1.0, 0.75, pool);
                float smoothness = lerp(1.0 - rough, 0.8, wet);
                smoothness = lerp(smoothness, 0.95, pool);
                nTS = normalize(lerp(nTS, float3(0, 0, 1), saturate(wet * 0.55 + pool)));   // water fills the grain

                float3 n = normalize(i.normalWS);
                float3 t = normalize(i.tangentWS.xyz);
                float3 b = cross(n, t) * i.tangentWS.w;
                float3 normalWS = normalize(TransformTangentToWorld(nTS, float3x3(t, b, n)));

                InputData d = (InputData)0;
                d.positionWS = i.positionWS;
                d.positionCS = i.positionCS;
                d.normalWS = normalWS;
                d.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                d.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                d.fogCoord = i.fog;
                d.bakedGI = SampleSH(normalWS);
                d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                d.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo;
                s.metallic = 0;
                s.specular = 0;
                s.smoothness = smoothness;
                s.normalTS = nTS;
                s.occlusion = 1;
                s.alpha = 1;

                float4 colour = UniversalFragmentPBR(d, s);
                colour.rgb = MixFog(colour.rgb, i.fog);
                return colour;
            }
            ENDHLSL
        }
    }
}
