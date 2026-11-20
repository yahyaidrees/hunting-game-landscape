// Far tree: one instanced quad that turns around Y toward the camera and shows the Blender render of the whole
// tree taken from the nearest of N directions (the sun is baked, so the instance itself never rotates).
// URP port (2026-11-20): same look; URP fog, SRP Batcher compatible, DepthOnly pass.
Shader "SavannaEnv/Impostor"
{
    Properties
    {
        _MainTex ("Views Atlas (RGB) Alpha (A)", 2D) = "white" {}
        _TintA ("Instance Tint A", Color) = (1,1,1,1)
        _TintB ("Instance Tint B", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.4
        _ShadowTint ("Colour In Cloud Shadow", Color) = (0.62, 0.6, 0.6, 1)
        _Cols ("Atlas Columns", Float) = 4
        _Rows ("Atlas Rows", Float) = 2
        _Views ("View Count", Float) = 8
        _WindStrength ("Wind Strength", Range(0,1)) = 0.05
        _WindSpeed ("Wind Speed", Range(0,5)) = 1.1
    }

    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        LOD 100
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "../../FarmEnv/Shaders/FarmEnvCommon.cginc"
        #include "EnvNight.cginc"

        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            half4 _TintA, _TintB, _ShadowTint;
            half _Cutoff;
            float _Cols, _Rows, _Views;
            half _WindStrength, _WindSpeed;
        CBUFFER_END

        struct Attributes
        {
            float4 vertex : POSITION;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        // Billboarded world position, atlas uv, pivot and random value.
        float3 ImpostorWorldPos(Attributes v, out float2 uv, out float3 pivot, out float rand)
        {
            float4x4 m = GetObjectToWorldMatrix();
            pivot = float3(m._m03, m._m13, m._m23);
            float scale = length(float3(m._m00, m._m10, m._m20));
            rand = FE_Hash(pivot.xz);

            float2 dir = _WorldSpaceCameraPos.xz - pivot.xz;
            dir /= max(0.001, length(dir));
            float3 right = float3(-dir.y, 0, dir.x);
            float3 wpos = pivot + (right * v.vertex.x + float3(0, v.vertex.y, 0)) * scale;

            float phase = _Time.y * _WindSpeed + dot(pivot.xz, float2(0.11, 0.07)) + rand * 6.2832;
            float sway = sin(phase) + 0.4 * sin(phase * 2.31 + 1.3);
            wpos.xz += float2(0.8, 0.6) * (sway * _WindStrength * v.uv.y * v.uv.y * scale);

            // view index: the atlas holds renders taken from azimuth i * 360 / views around the tree
            float view = floor(atan2(dir.x, dir.y) / 6.28318 * _Views + 0.5);
            view = view - _Views * floor(view / _Views);
            float row = floor(view / _Cols), col = view - row * _Cols;
            uv = float2((col + v.uv.x) / _Cols, 1 - (row + 1 - v.uv.y) / _Rows);
            return wpos;
        }
        ENDHLSL

        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nolodfade nolightprobe nolightmap

            struct Varyings
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 tint : TEXCOORD1;
                float3 wpos : TEXCOORD2;
            };

            Varyings vert (Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                float3 pivot; float rand;
                float3 wpos = ImpostorWorldPos(v, o.uv, pivot, rand);
                o.pos = TransformWorldToHClip(wpos);
                half cloud = FE_SampleShadowRawLod(FE_ShadowUV(pivot)).b;
                o.tint = lerp(_TintA.rgb, _TintB.rgb, frac(rand * 7.31 + 0.13)) * lerp(_ShadowTint.rgb, half3(1, 1, 1), cloud) * ENV_NearShade(pivot);
                o.wpos = wpos;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                clip(tex.a - _Cutoff);
                half3 c = ENV_Grade(tex.rgb * i.tint);
                return half4(ENV_ApplyFog(c, i.wpos), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nolodfade nolightprobe nolightmap

            struct Varyings { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert (Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                float3 pivot; float rand;
                o.pos = TransformWorldToHClip(ImpostorWorldPos(v, o.uv, pivot, rand));
                return o;
            }

            half frag (Varyings i) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}
