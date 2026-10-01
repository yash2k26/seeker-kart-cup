// The halo round a street lamp in the night haze, and the haze itself: every glow on a circuit is one mesh and one
// draw. Each quad's four vertices sit AT the lamp (so the mesh may be batched or moved freely); the corner goes in
// uv, the size in metres in uv2.x, and the vertex stage opens the quad toward the eye. Additive, no depth write.
// colour.a is the glow's own strength; uv2.y its falloff power (high = a tight star, low = a wide soft haze).
Shader "KartCup/Glow"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.62, 0.28, 1)
        _Strength ("Strength", Float) = 1
        _Near ("Near fade (m)", Float) = 5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "IgnoreProjector"="True" }

        Pass
        {
            Name "Glow"
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
                float _Near;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; float4 color : COLOR; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 tint : TEXCOORD1; float2 powerFog : TEXCOORD2; };

            V vert (A i)
            {
                V o;
                float3 centreWS = TransformObjectToWorld(i.positionOS.xyz);
                float3 centreVS = TransformWorldToView(centreWS);
                // Past its reach a glow is not drawn at all (the quad collapses): the wide haze layers are the costly
                // ones on a phone, forty of them overlapping down a straight, and beyond ~100 m the fog has them anyway.
                float dist = -centreVS.z;
                float reach = i.uv2.x > 12.0 ? 110.0 : 240.0;
                float keep = saturate((reach - dist) / 30.0);
                centreVS.xy += i.uv * (i.uv2.x * 0.5) * step(0.001, keep);
                o.positionCS = TransformWViewToHClip(centreVS);
                o.uv = i.uv;
                // gone when the camera is inside it, so a lamp passed close does not white the frame out
                float near = saturate(-centreVS.z / _Near);
                o.tint = _Color.rgb * i.color.rgb * (i.color.a * _Strength * near * keep);
                o.powerFog = float2(i.uv2.y, ComputeFogFactor(o.positionCS.z));
                return o;
            }

            float4 frag (V i) : SV_Target
            {
                float r = saturate(1.0 - length(i.uv));
                // the haze swallows a far lamp like anything else: without this every lamp on the lap shows at once
                float3 c = MixFogColor(i.tint * pow(r, i.powerFog.x), 0, i.powerFog.y);
                return float4(c, 1);
            }
            ENDHLSL
        }
    }
}
