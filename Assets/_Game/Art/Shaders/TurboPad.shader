// A turbo strip's chevrons: drawn in the shader, running up the strip (its uv.y is the way to drive), additive, so the
// bloom makes them light on the road. No texture and no script: the pattern moves with _Time.
Shader "KartCup/TurboPad"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.62, 0.12, 1)
        _Strength ("Strength", Float) = 2.6
        _Rows ("Chevrons along the strip", Float) = 3
        _Speed ("Chevrons a second", Float) = 1.7
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "IgnoreProjector"="True" }

        Pass
        {
            Name "TurboPad"
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Strength;
                float _Rows;
                float _Speed;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            V vert (A i)
            {
                V o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                return o;
            }

            float4 frag (V i) : SV_Target
            {
                float side = 1.0 - abs(i.uv.x * 2.0 - 1.0);                             // 0 at the strip's edges, 1 down its middle
                float band = frac(i.uv.y * _Rows - side * 0.38 - _Time.y * _Speed);    // the middle leads: an arrowhead
                float a = saturate(min(band - 0.18, 0.6 - band) / 0.07) * saturate(side / 0.12);
                a *= saturate(i.uv.y / 0.1) * saturate((1.0 - i.uv.y) / 0.1);          // in and out at the strip's ends
                return float4(_Color.rgb * (_Strength * a), 1);
            }
            ENDHLSL
        }
    }
}
