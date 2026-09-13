// Two scrolling tiles → caustic bands → deep/shallow lerp, per-vertex swell. No depth texture, no opaque texture, opaque queue.
Shader "KartCup/Water Unlit"
{
    Properties
    {
        _WaterA ("Tile A", 2D) = "white" {}
        _WaterB ("Tile B", 2D) = "white" {}
        _SpeedA ("Speed A (xy)", Vector) = (0.02, 0.03, 0, 0)
        _SpeedB ("Speed B (xy)", Vector) = (-0.03, 0.01, 0, 0)
        _DeepColor ("Deep", Color) = (0.05, 0.42, 0.55, 1)
        _ShallowColor ("Shallow", Color) = (0.45, 0.85, 0.85, 1)
        _Edge ("Band edges (min,max)", Vector) = (0.2, 0.8, 0, 0)
        _WaveHeight ("Wave height", Float) = 0.05
        _WaveFreq ("Wave frequency", Float) = 0.6
        _WaveSpeed ("Wave speed", Float) = 1.2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "Water"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_WaterA); SAMPLER(sampler_WaterA);
            TEXTURE2D(_WaterB); SAMPLER(sampler_WaterB);
            CBUFFER_START(UnityPerMaterial)
                float4 _WaterA_ST, _WaterB_ST, _SpeedA, _SpeedB, _Edge;
                half4 _DeepColor, _ShallowColor;
                float _WaveHeight, _WaveFreq, _WaveSpeed;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct V { float4 positionCS : SV_POSITION; float2 uvA : TEXCOORD0; float2 uvB : TEXCOORD1; half foam : TEXCOORD2; half fog : TEXCOORD3; };

            V vert (A i)
            {
                V o;
                float3 p = i.positionOS.xyz;
                float t = _Time.y * _WaveSpeed;
                p.y += sin(p.x * _WaveFreq + t) * _WaveHeight + sin(p.z * _WaveFreq * 0.8 + t * 1.3) * _WaveHeight * 0.6;
                o.positionCS = TransformObjectToHClip(p);
                o.uvA = TRANSFORM_TEX(i.uv, _WaterA) + _SpeedA.xy * _Time.y;
                o.uvB = TRANSFORM_TEX(i.uv, _WaterB) + _SpeedB.xy * _Time.y;
                o.foam = i.color.r;                           // paint shoreline vertices white in Blender = foam, no depth needed
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag (V i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_WaterA, sampler_WaterA, i.uvA).r;
                half b = SAMPLE_TEXTURE2D(_WaterB, sampler_WaterB, i.uvB).r;
                half bands = smoothstep(_Edge.x, _Edge.y, a * b);
                half3 c = lerp(_DeepColor.rgb, _ShallowColor.rgb, bands);
                c = lerp(c, 1.0h, i.foam * step(0.6h, a * b));
                c = MixFog(c, i.fog);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
