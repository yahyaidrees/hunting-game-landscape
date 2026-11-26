// Transparent band wrapped around the horizon (used for the warm horizon dust).
// Eye-relative like the backdrop: drawn after the skybox at a fixed depth slot (_DepthK), so real
// geometry always covers it. Vertex alpha scales the density per direction.
// URP port (2026-11-20): same look, SRP Batcher compatible.
// Alpha split (2026-11-26, phones): a layer can be drawn as two materials. The solid one (_SplitSide 1,
// opaque blend, depth write, drawn before the sky) keeps only pixels at or above _AlphaSplit, so the sky and
// layers behind are never shaded under them; the edge one (_SplitSide -1, blended as before) keeps the rest.
// Off by default (keyword _ALPHASPLIT), so materials that do not use it look and cost the same as before.
Shader "SavannaEnv/CloudBand"
{
    Properties
    {
        _MainTex ("Clouds (RGB lit colour, A density)", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _HazeColor ("Haze (uses fog colour when alpha = 0)", Color) = (0.7, 0.78, 0.88, 1)
        _Haze ("Haze", Range(0, 1)) = 0.15
        _DepthK ("Depth Slot", Float) = 0.0000034
        [Toggle(_ALPHASPLIT)] _AlphaSplitOn ("Alpha Split", Float) = 0
        _AlphaSplit ("Split Alpha", Range(0.5, 1)) = 0.98
        _SplitSide ("Split Side (1 solid, -1 edge)", Float) = -1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
        [Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent-20" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]
        Cull Off

        Pass
        {
            Name "CloudBand"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ALPHASPLIT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "EnvNight.cginc"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Tint, _HazeColor;
                half _Haze;
                float _DepthK;
                half _AlphaSplit, _SplitSide;
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
                float alpha : TEXCOORD1;     // float: half varyings arrived as garbage on scattered pixels (black specks with HDR)
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
                half a = t.a * i.alpha * _Tint.a;
                #if defined(_ALPHASPLIT)
                    // solid keeps a >= split (and writes it opaque); edge keeps a < split
                    clip(_SplitSide > 0 ? a - _AlphaSplit : _AlphaSplit - a - 0.0001);
                    if (_SplitSide > 0) a = 1;
                #endif
                return half4(ENV_GradeBackdrop(lerp(t.rgb * _Tint.rgb, haze, _Haze), i.dir), a);
            }
            ENDHLSL
        }
    }
}
