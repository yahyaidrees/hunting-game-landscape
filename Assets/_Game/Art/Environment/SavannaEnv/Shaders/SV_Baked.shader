// Instanced surface whose lighting is baked into the texture (trees, rocks and plants rendered in Blender
// with the scene's sun): one texture read and no lighting maths. Vertex colour alpha = wind weight.
// The baked ground shadow map only dims it per instance: B = cloud shadow, R = cloud + object shadow.
// URP port (2026-11-20): same look; URP fog, SRP Batcher compatible, DepthOnly pass shares the wind/fade code.
Shader "SavannaEnv/Baked"
{
    Properties
    {
        _MainTex ("Baked Colour (RGB) Alpha (A)", 2D) = "white" {}
        _TintA ("Instance Tint A", Color) = (1,1,1,1)
        _TintB ("Instance Tint B", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        _ShadowTint ("Colour In Shadow", Color) = (0.62, 0.6, 0.6, 1)
        _ObjectShadow ("Darken Under Trees And Rocks", Range(0,1)) = 0
        _WindStrength ("Wind Strength", Range(0,1)) = 0
        _WindSpeed ("Wind Speed", Range(0,5)) = 1.2
        _FadeStart ("Fade Start (m, 0 = never fade)", Float) = 0
        _FadeEnd ("Fade End (m)", Float) = 0
        [Toggle(_EDGEFADE)] _EdgeFadeOn ("Fade Cards Seen Edge-On (mesh normals = card normals, 0 = never fade)", Float) = 0
        _EdgeFadeStart ("Edge Fade: Gone Below Facing", Range(0,1)) = 0.3
        _EdgeFadeEnd ("Edge Fade: Whole Above Facing", Range(0,1)) = 0.45

        [Header(Rocks lichen and moss (style sheet))]
        [Toggle(_ROCKDETAIL)] _RockDetailOn ("Lichen And Moss Detail", Float) = 0
        _DetailTex ("Detail Photo (mossy rock)", 2D) = "gray" {}
        _DetailScale ("Detail Tiles Per Metre", Float) = 0.33
        _DetailStrength ("Detail Strength", Range(0, 1.5)) = 0.9
        _DetailMean ("Detail Average Brightness", Float) = 0.35
        _MossColor ("Moss On Top", Color) = (0.42, 0.5, 0.18, 1)
        _MossAmount ("Moss On Upward Faces", Range(0, 1)) = 0.45

        [Header(Crown light for photo card trees)]
        [Toggle(_CROWNLIGHT)] _CrownLightOn ("Shade Crowns Like A Lit Ball", Float) = 0
        _CrownSunDir ("Direction To The Sun (world)", Vector) = (0.44, 0.79, 0.28, 0)
        _CrownDark ("Shaded Side", Range(0, 1.5)) = 0.55
        _CrownLit ("Lit Side", Range(0, 2)) = 1.2
        _CrownCore ("Inside Of The Crown", Range(0, 1)) = 0.72
        _CrownBase ("Under The Crown", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        LOD 100

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "../../FarmEnv/Shaders/FarmEnvCommon.cginc"
        #include "EnvNight.cginc"

        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        TEXTURE2D(_DetailTex); SAMPLER(sampler_DetailTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _TintA, _TintB, _ShadowTint;
            half _Cutoff, _ObjectShadow;
            half _WindStrength, _WindSpeed;
            float _FadeStart, _FadeEnd;
            half _EdgeFadeStart, _EdgeFadeEnd;
            half _Cull, _EdgeFadeOn;
            float4 _MossColor;
            float _DetailScale, _DetailStrength, _DetailMean, _MossAmount, _RockDetailOn;
            float4 _CrownSunDir;
            float _CrownLightOn, _CrownDark, _CrownLit, _CrownCore, _CrownBase;
        CBUFFER_END

        struct Attributes
        {
            float4 vertex : POSITION;
            float2 uv : TEXCOORD0;
            float3 crown : TEXCOORD1;    // crown-local position (centre 0, edge 1); only _CROWNLIGHT reads it
            half4 color : COLOR;
            float3 normal : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        // World position after wind and distance fade, the instance pivot and its random value.
        float3 BakedWorldPos(Attributes v, out float3 pivot, out float rand)
        {
            float4x4 m = GetObjectToWorldMatrix();
            float3 wpos = mul(m, float4(v.vertex.xyz, 1)).xyz;
            pivot = float3(m._m03, m._m13, m._m23);
            rand = FE_Hash(pivot.xz);

            if (_WindStrength > 0)
            {
                float phase = _Time.y * _WindSpeed + dot(pivot.xz, float2(0.11, 0.07)) + rand * 6.2832;
                float sway = sin(phase) + 0.4 * sin(phase * 2.31 + 1.3);
                wpos.xz += float2(0.8, 0.6) * (sway * _WindStrength * v.color.a);
            }
            if (_FadeEnd > 0)
            {
                float scale = (_FE_FadeScale > 0 ? _FE_FadeScale : 1) * FE_ZoomFactor();
                float d = distance(pivot, _WorldSpaceCameraPos);
                float f = saturate((_FadeEnd * scale - d) / max(0.001, (_FadeEnd - _FadeStart) * scale));
                wpos = lerp(pivot, wpos, f);
            }
            return wpos;
        }

        // A crossed card seen edge-on squeezes its texture into a streak while its partner faces the eye:
        // raise its alpha cutoff until it is gone. Vertices without a normal (the trunk) never fade.
        half EdgeCutoff(Attributes v, float3 wpos)
        {
            float3 n = mul((float3x3)GetObjectToWorldMatrix(), v.normal);
            float nn = dot(n, n);
            half facing = nn > 1e-6 ? abs(dot(n, normalize(_WorldSpaceCameraPos - wpos))) * rsqrt(nn) : 1;
            return lerp(1.01, _Cutoff, saturate((facing - _EdgeFadeStart) / max(0.001, _EdgeFadeEnd - _EdgeFadeStart)));
        }
        ENDHLSL

        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nolodfade nolightprobe nolightmap
            #pragma shader_feature_local _CUTOUT
            #pragma shader_feature_local _EDGEFADE
            #pragma shader_feature_local _ROCKDETAIL
            #pragma shader_feature_local _CROWNLIGHT

            struct Varyings
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 tint : TEXCOORD1;     // all varyings float: half varyings arrived as garbage on scattered pixels
                float3 wpos : TEXCOORD2;
                float cutoff : TEXCOORD3;
                #if defined(_ROCKDETAIL)
                    float3 wnrm : TEXCOORD4;
                #endif
                #if defined(_CROWNLIGHT)
                    float3 crown : TEXCOORD5;
                #endif
            };

            Varyings vert (Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                float3 pivot; float rand;
                float3 wpos = BakedWorldPos(v, pivot, rand);
                #if defined(_EDGEFADE)
                    o.cutoff = EdgeCutoff(v, wpos);
                #else
                    o.cutoff = _Cutoff;
                #endif
                o.pos = TransformWorldToHClip(wpos);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                half3 s = FE_SampleShadowRawLod(FE_ShadowUV(pivot));
                half vis = lerp(s.b, s.r, _ObjectShadow);
                o.tint = lerp(_TintA.rgb, _TintB.rgb, frac(rand * 7.31 + 0.13)) * lerp(_ShadowTint.rgb, half3(1, 1, 1), vis) * ENV_NearShade(wpos);
                o.wpos = wpos;
                #if defined(_ROCKDETAIL)
                    o.wnrm = TransformObjectToWorldNormal(v.normal);
                #endif
                #if defined(_CROWNLIGHT)
                    float3x3 m3 = (float3x3)GetObjectToWorldMatrix();
                    o.crown = mul(m3, v.crown) / max(1e-4, length(float3(m3._m00, m3._m10, m3._m20)));   // instance rotation, not scale
                #endif
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                #if defined(_CUTOUT)
                    clip(tex.a - i.cutoff);
                #endif
                float3 baked = tex.rgb;
                #if defined(_ROCKDETAIL)
                    // lichen and moss from a rock photo, projected from three sides (no UV stretching), over the baked light
                    float3 n = normalize(i.wnrm);
                    float3 bw = pow(abs(n), 4.0); bw /= dot(bw, float3(1, 1, 1));
                    float3 d = SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, i.wpos.zy * _DetailScale).rgb * bw.x
                             + SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, i.wpos.xz * _DetailScale).rgb * bw.y
                             + SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, i.wpos.xy * _DetailScale).rgb * bw.z;
                    baked *= lerp(float3(1, 1, 1), d / max(0.05, _DetailMean), _DetailStrength);
                    // moss gathers on the tops of boulders, patchy (the detail photo's own green breaks it up)
                    float moss = saturate((n.y - (1.0 - _MossAmount)) * 3.0) * saturate(d.g / max(0.05, _DetailMean) - 0.6);
                    baked = lerp(baked, _MossColor.rgb * (dot(tex.rgb, float3(0.3, 0.59, 0.11)) * 2.0), saturate(moss));
                #endif
                #if defined(_CROWNLIGHT)
                    // a photo card has no light of its own: treat the crown as a ball lit by the sun (lit side,
                    // shaded side), darker deep inside and underneath, as the firs' baked textures are
                    float3 p = i.crown;
                    float r = length(p);
                    float lit = saturate(dot(p / max(r, 1e-3), _CrownSunDir.xyz) * 0.5 + 0.5);
                    baked *= lerp(_CrownDark, _CrownLit, lit) * lerp(_CrownCore, 1.0, saturate(r)) * lerp(_CrownBase, 1.0, saturate(p.y + 1.0));
                #endif
                half3 c = ENV_Grade(baked * i.tint);
                return half4(ENV_ApplyFog(c, i.wpos), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            Cull [_Cull]
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nolodfade nolightprobe nolightmap
            #pragma shader_feature_local _CUTOUT
            #pragma shader_feature_local _EDGEFADE

            struct Varyings
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float cutoff : TEXCOORD1;
            };

            Varyings vert (Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                float3 pivot; float rand;
                float3 wpos = BakedWorldPos(v, pivot, rand);
                #if defined(_EDGEFADE)
                    o.cutoff = EdgeCutoff(v, wpos);
                #else
                    o.cutoff = _Cutoff;
                #endif
                o.pos = TransformWorldToHClip(wpos);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half frag (Varyings i) : SV_Target
            {
                #if defined(_CUTOUT)
                    clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a - i.cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }
    }
}
