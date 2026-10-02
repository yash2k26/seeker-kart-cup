// A pool of light lying on the ground: what a bonfire or a lit bar does to the sand round it. Drawn additively on a
// small mesh that follows the terrain; how bright each point is rides in the mesh's vertex alpha (1 under the light,
// 0 at the pool's rim), so the shader is one multiply and the light costs none of a renderer's eight.
Shader "KartCup/Pool"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.72, 0.4, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-20" "IgnoreProjector"="True" }

        Pass
        {
            Name "Pool"
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float4 color : COLOR; };
            struct V { float4 positionCS : SV_POSITION; float2 lightFog : TEXCOORD0; };

            V vert (A i)
            {
                V o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.lightFog = float2(i.color.a, ComputeFogFactor(o.positionCS.z));
                return o;
            }

            float4 frag (V i) : SV_Target
            {
                return float4(MixFogColor(_Color.rgb * i.lightFog.x, 0, i.lightFog.y), 1);
            }
            ENDHLSL
        }
    }
}
