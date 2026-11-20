// Alpha-blended particle (texture x vertex colour) for the environment scenes, graded at night like the baked art
// (EnvNight.cginc), so falling snow does not stay bright white under the moon. By day it matches
// Mobile/Particles/Alpha Blended. No fog: the snowflakes it was made for stay within a few metres of the eye.
// URP port (2026-11-20): same look, SRP Batcher compatible.
Shader "SavannaEnv/Particle"
{
    Properties
    {
        _MainTex ("Particle Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "PreviewType"="Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off

        Pass
        {
            Name "Particle"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "EnvNight.cginc"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
            CBUFFER_END

            struct Attributes
            {
                float4 vertex : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 pos : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.pos = TransformObjectToHClip(v.vertex.xyz);
                o.color = v.color;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                c.rgb = ENV_Grade(c.rgb);
                return c;
            }
            ENDHLSL
        }
    }
}
