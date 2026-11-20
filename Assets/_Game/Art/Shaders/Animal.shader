// Animal skin and fur for URP (plan section 13: animals carry the highest budget, they are seen through the scope).
// Opaque skin: albedo + normal map. Fur (_FUR): two-sided cards whose colour comes from the body texture while a
// strand mask cuts out the hair, with darker roots, and their own normal map. Soft wrapped diffuse, a little
// sheen, ambient from the light probes, real-time main-light shadows (received and cast) and URP fog.
// Every vertex-to-fragment value is float: half varyings arrived as garbage on scattered pixels in this project.
Shader "Game/Animal"
{
    Properties
    {
        _BaseMap ("Albedo (body texture)", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        [Normal] _BumpMap ("Body Normal", 2D) = "bump" {}
        _BumpScale ("Body Normal Strength", Range(0, 2)) = 1
        _Smoothness ("Sheen", Range(0, 1)) = 0.15
        _Wrap ("Soft Light Wrap", Range(0, 1)) = 0.35

        [Toggle(_FUR)] _Fur ("Fur Cards", Float) = 0
        _FurMap ("Fur Strands (R alpha, A root to tip)", 2D) = "white" {}
        [Normal] _FurNormal ("Fur Normal", 2D) = "bump" {}
        _FurNormalScale ("Fur Normal Strength", Range(0, 2)) = 0.5
        _Cutoff ("Strand Cutoff", Range(0, 1)) = 0.5
        _RootTint ("Root Tint", Color) = (0.37, 0.25, 0.25, 1)
        [Toggle] _ColorFromUV1 ("Colour From Second UV (fur map on first)", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_FurMap); SAMPLER(sampler_FurMap);
        TEXTURE2D(_FurNormal); SAMPLER(sampler_FurNormal);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float _BumpScale, _Smoothness, _Wrap;
            float _Fur, _FurNormalScale, _Cutoff, _ColorFromUV1, _Cull;
            float4 _RootTint;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv0 : TEXCOORD0;
            float2 uv1 : TEXCOORD1;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        // colour UV and fur-mask UV for this material
        void AnimalUVs(float2 uv0, float2 uv1, out float2 colourUV, out float2 furUV)
        {
            colourUV = _ColorFromUV1 > 0.5 ? uv1 : uv0;
            furUV = _ColorFromUV1 > 0.5 ? uv0 : uv1;
        }

        float FurAlpha(float2 furUV)
        {
            return SAMPLE_TEXTURE2D(_FurMap, sampler_FurMap, furUV).r;
        }
        ENDHLSL

        Pass
        {
            Name "AnimalForward"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _FUR
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;
                float4 uv : TEXCOORD3;          // xy colour UV, zw fur UV
            };

            Varyings vert (Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = n.normalWS;
                o.tangentWS = float4(n.tangentWS, v.tangentOS.w * GetOddNegativeScale());
                float2 cuv, fuv;
                AnimalUVs(v.uv0, v.uv1, cuv, fuv);
                o.uv = float4(TRANSFORM_TEX(cuv, _BaseMap), fuv);
                return o;
            }

            float4 frag (Varyings i, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                float3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv.xy).rgb * _BaseColor.rgb;
                float3 normalTS;
            #if defined(_FUR)
                float4 strands = SAMPLE_TEXTURE2D(_FurMap, sampler_FurMap, i.uv.zw);
                clip(strands.r - _Cutoff);
                albedo *= lerp(_RootTint.rgb, float3(1, 1, 1), strands.a);
                normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_FurNormal, sampler_FurNormal, i.uv.zw), _FurNormalScale);
            #else
                normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv.xy), _BumpScale);
            #endif
                float3 n = normalize(i.normalWS);
                float3 t = normalize(i.tangentWS.xyz);
                float3 b = cross(n, t) * i.tangentWS.w;
                float3 N = normalize(mul(normalTS, float3x3(t, b, n)));
                if (!frontFace) N = -N;   // two-sided fur cards

                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                float ndl = dot(N, light.direction);
                float wrap = saturate((ndl + _Wrap) / (1.0 + _Wrap));
                float atten = light.shadowAttenuation * light.distanceAttenuation;
                float3 H = normalize(light.direction + V);
                float sheen = pow(saturate(dot(N, H)), 24.0) * _Smoothness;
                float3 colour = albedo * (light.color * wrap * atten + SampleSH(N))
                              + light.color * sheen * atten;
                colour = MixFog(colour, InitializeInputDataFog(float4(i.positionWS, 1), 0));
                return float4(colour, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _FUR
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Varyings { float4 positionCS : SV_POSITION; float2 furUV : TEXCOORD0; };

            Varyings vert (Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
            #if UNITY_REVERSED_Z
                o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                float2 cuv;
                AnimalUVs(v.uv0, v.uv1, cuv, o.furUV);
                return o;
            }

            float4 frag (Varyings i) : SV_Target
            {
            #if defined(_FUR)
                clip(FurAlpha(i.furUV) - _Cutoff);
            #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _FUR
            #pragma multi_compile_instancing

            struct Varyings { float4 positionCS : SV_POSITION; float2 furUV : TEXCOORD0; };

            Varyings vert (Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                float2 cuv;
                AnimalUVs(v.uv0, v.uv1, cuv, o.furUV);
                return o;
            }

            float frag (Varyings i) : SV_Target
            {
            #if defined(_FUR)
                clip(FurAlpha(i.furUV) - _Cutoff);
            #endif
                return 0;
            }
            ENDHLSL
        }
    }
}
