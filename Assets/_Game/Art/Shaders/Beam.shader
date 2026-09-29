// A shaft of light in the night haze: the lighthouse beam. Additive on a long thin cone, bright at the lamp and
// fading down its length, soft at the silhouette (the rim faces away from the eye) and gone when the camera is
// inside it. No depth write, so the karts and the sea show through it.
Shader "KartCup/Beam"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.9, 0.72, 1)
        _Strength ("Strength", Float) = 1
        _Falloff ("Fade (fraction of the length)", Float) = 0.16
        _Edge ("Edge softness", Float) = 1.5
        _Near ("Near fade (m)", Float) = 6
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "IgnoreProjector"="True" }

        Pass
        {
            Name "Beam"
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Strength;
                float _Falloff;
                float _Edge;
                float _Near;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct V
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;             // y: 0 at the lamp, 1 at the far end
                float3 normalWS : TEXCOORD1;
                float3 viewWS : TEXCOORD2;
                float2 fogDist : TEXCOORD3;
            };

            V vert (A i)
            {
                V o;
                float3 pw = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(pw);
                o.uv = i.uv;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.viewWS = GetWorldSpaceNormalizeViewDir(pw);
                o.fogDist = float2(ComputeFogFactor(o.positionCS.z), distance(pw, _WorldSpaceCameraPos));
                return o;
            }

            float4 frag (V i) : SV_Target
            {
                float along = exp(-i.uv.y / max(0.001, _Falloff));
                float rim = pow(abs(dot(normalize(i.normalWS), normalize(i.viewWS))), _Edge);
                float near = saturate(i.fogDist.y / _Near);
                float3 c = _Color.rgb * (along * rim * near * _Strength);
                c = MixFogColor(c, 0, i.fogDist.x);      // additive light thins out in the fog instead of turning fog-coloured
                return float4(c, 1);
            }
            ENDHLSL
        }
    }
}
