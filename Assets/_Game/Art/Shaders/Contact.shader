// The dark under a parked kart and a standing driver on the title's street: a soft-edged patch on the road, darkest
// across its middle and gone at its rim. Drawn on a flat quad; no texture, no light, no depth write.
Shader "KartCup/Contact"
{
    Properties
    {
        _Strength ("Strength", Range(0, 1)) = 0.8
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "IgnoreProjector"="True" }

        Pass
        {
            Name "Contact"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Strength;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            V vert (A i)
            {
                V o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv * 2.0 - 1.0;
                return o;
            }

            float4 frag (V i) : SV_Target
            {
                float r = saturate(1.0 - length(i.uv));          // 1 in the middle, 0 at the rim
                return float4(0, 0, 0, _Strength * smoothstep(0.0, 0.6, r));
            }
            ENDHLSL
        }
    }
}
