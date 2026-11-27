// Photo sky on an eye-relative dome: one texture read with UVs from the vertices, instead of the per-pixel
// atan2/acos of Skybox/Panoramic. Drawn after the opaque scene at the very back of the depth range.
// At night (EnvTimeOfDay) it fades to a procedural moonlit sky: no extra texture.
// URP port (2026-11-20): same look, SRP Batcher compatible.
Shader "SavannaEnv/SkyDome"
{
    Properties
    {
        _MainTex ("Sky (upper hemisphere, equirect)", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue"="AlphaTest+40" "RenderType"="Background" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            Name "Sky"
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

            struct Attributes { float4 vertex : POSITION; float2 uv : TEXCOORD0; };

            struct Varyings
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 dir : TEXCOORD1;      // eye-relative point on the dome = the view direction
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.pos = TransformWorldToHClip(_WorldSpaceCameraPos + v.vertex.xyz);
                #if UNITY_REVERSED_Z
                    o.pos.z = o.pos.w * 0.0000005;
                #else
                    o.pos.z = o.pos.w * 0.9999995;
                #endif
                o.uv = v.uv;
                o.dir = v.vertex.xyz;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half3 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb;
                UNITY_BRANCH
                if (_EnvNight > 0)
                    c = lerp(c, ENV_NightSky(normalize(i.dir)), _EnvNight);
                return half4(ENV_LowTierGrade(c), 1);
            }
            ENDHLSL
        }
    }
}
