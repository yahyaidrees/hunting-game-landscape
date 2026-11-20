// Transparent band wrapped around the horizon (used for the warm horizon dust).
// Eye-relative like the backdrop: drawn after the skybox at a fixed depth slot (_DepthK), so real
// geometry always covers it. Vertex alpha scales the density per direction.
// URP port (2026-11-20): same look, SRP Batcher compatible.
Shader "SavannaEnv/CloudBand"
{
    Properties
    {
        _MainTex ("Clouds (RGB lit colour, A density)", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _HazeColor ("Haze (uses fog colour when alpha = 0)", Color) = (0.7, 0.78, 0.88, 1)
        _Haze ("Haze", Range(0, 1)) = 0.15
        _DepthK ("Depth Slot", Float) = 0.0000034
    }

    SubShader
    {
        Tags { "Queue"="Transparent-20" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "CloudBand"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "EnvNight.cginc"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Tint, _HazeColor;
                half _Haze;
                float _DepthK;
            CBUFFER_END

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                half alpha : TEXCOORD1;
                float3 dir : TEXCOORD2;      // eye-relative direction
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.pos = TransformWorldToHClip(_WorldSpaceCameraPos + v.vertex.xyz);
                #if UNITY_REVERSED_Z
                    o.pos.z = o.pos.w * _DepthK;
                #else
                    o.pos.z = o.pos.w * (1 - _DepthK);
                #endif
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.alpha = v.color.a;
                o.dir = v.vertex.xyz;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half3 haze = _HazeColor.a > 0 ? _HazeColor.rgb : unity_FogColor.rgb;
                return half4(ENV_GradeBackdrop(lerp(t.rgb * _Tint.rgb, haze, _Haze), i.dir), t.a * i.alpha * _Tint.a);
            }
            ENDHLSL
        }
    }
}
