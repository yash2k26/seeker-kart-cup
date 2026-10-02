// Polished metal for the trophies. A phone has no reflection probe worth the name here and a lit metal with nothing
// to reflect is a dull grey shape, so the reflection is painted from the surface's facing alone (a matcap, computed
// rather than sampled): a bright sky above a dark floor with a hard horizon between them, two studio lights, and the
// rim falling away. The highlights are past 1, so the bloom makes them glint.
Shader "KartCup/Polished"
{
    Properties
    {
        _Color ("Metal", Color) = (1, 0.74, 0.2, 1)
        _Shade ("Shadow side", Color) = (0.35, 0.16, 0.02, 1)
        _Glint ("Highlights", Float) = 2.2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "Polished"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _Shade;
                float _Glint;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; float3 n : TEXCOORD0; };

            V vert (A i)
            {
                V o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.n = mul((float3x3)UNITY_MATRIX_V, TransformObjectToWorldNormal(i.normalOS));   // the facing, as the camera sees it
                return o;
            }

            float4 frag (V i) : SV_Target
            {
                float3 n = normalize(i.n);
                // what a polished curve shows: floor, a hard horizon, then sky brightening toward the top
                // (a cup's bowl leans out, so most of it faces a little down: the floor has to be lit too, or the
                // whole bowl goes dark)
                float sky = smoothstep(-0.34, -0.2, n.y);
                float3 c = lerp(_Shade.rgb * (0.7 + 0.5 * saturate(-n.y)), _Color.rgb * (0.8 + 0.6 * saturate(n.y + 0.3)), sky);
                c *= 0.6 + 0.4 * saturate(n.z);                                               // the rim falls away
                float key = pow(saturate(dot(n, normalize(float3(-0.42, 0.42, 0.8)))), 16.0);
                float kick = pow(saturate(dot(n, normalize(float3(0.62, -0.1, 0.78)))), 46.0);
                c += lerp(_Color.rgb, float3(1, 1, 1), 0.7) * (_Glint * (key + 0.6 * kick));
                return float4(c, 1);
            }
            ENDHLSL
        }
        // the trophies stand in the stage's light and should ground like everything else
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}
