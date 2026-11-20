// Savanna ground: three photo layers (relief already baked in for the fixed sun), large-scale variation and the
// baked shadow map. Texture coordinates come from the world position, not the mesh: on Android the project's
// vertex compression stores mesh UVs as 16-bit floats, too coarse for a ground this size (blocky stretched sand).
// Splat map: R = dry grass, G = green grass, B = dirt tint, A = large-scale brightness; sand fills the rest.
// URP port (2026-11-20): main light + SH from URP, receives real-time main-light shadows (animals, player),
// DepthOnly + DepthNormals passes (needed by decals such as footprints), SRP Batcher compatible.
Shader "SavannaEnv/Ground"
{
    Properties
    {
        _SplatMap ("Splat (dry, green, dirt, macro)", 2D) = "black" {}
        _SplatArea ("Splat Area (x, z, 1/size x, 1/size z)", Vector) = (0, 0, 0.002, 0.002)
        _Layer0 ("Sand", 2D) = "white" {}
        _Layer1 ("Green Grass", 2D) = "white" {}
        _Layer2 ("Dry Grass", 2D) = "white" {}
        _Tile ("Tiles Per Metre (sand, green, dry, far)", Vector) = (0.16, 0.16, 0.14, 0.023)
        _DirtTint ("Dirt Tint", Color) = (0.72, 0.6, 0.48, 1)
        _FarStrength ("Far Detail Strength", Range(0, 3)) = 1
        _FarMean ("Far Detail Mean", Range(0, 1)) = 0.42
        _MacroDark ("Macro Dark", Color) = (0.8, 0.76, 0.7, 1)
        _MacroLight ("Macro Light", Color) = (1.12, 1.1, 1.06, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _SplatArea, _Tile;
            half4 _DirtTint, _MacroDark, _MacroLight;
            half _FarStrength, _FarMean;
        CBUFFER_END

        struct GroundAttributes
        {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
        };
        ENDHLSL

        Pass
        {
            Name "GroundForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "../../FarmEnv/Shaders/FarmEnvCommon.cginc"
            #include "EnvNight.cginc"

            TEXTURE2D(_SplatMap); SAMPLER(sampler_SplatMap);
            TEXTURE2D(_Layer0); SAMPLER(sampler_Layer0);
            TEXTURE2D(_Layer1); SAMPLER(sampler_Layer1);
            TEXTURE2D(_Layer2); SAMPLER(sampler_Layer2);

            struct Varyings
            {
                float4 pos : SV_POSITION;
                float4 uvA : TEXCOORD0;      // splat, shadow
                float4 uvB : TEXCOORD1;      // sand, green
                float4 uvC : TEXCOORD2;      // dry, far
                float3 sun : TEXCOORD3;      // float: half varyings arrived as garbage on scattered pixels
                float3 amb : TEXCOORD4;
                float3 wpos : TEXCOORD5;
            };

            Varyings vert (GroundAttributes v)
            {
                Varyings o;
                float3 wpos = TransformObjectToWorld(v.vertex.xyz);
                o.pos = TransformWorldToHClip(wpos);
                o.wpos = wpos;
                o.uvA = float4((wpos.xz - _SplatArea.xy) * _SplatArea.zw, FE_ShadowUV(wpos));
                o.uvB = float4(wpos.xz * _Tile.x, wpos.xz * _Tile.y);
                o.uvC = float4(wpos.xz * _Tile.z, wpos.xz * _Tile.w);
                float3 n = TransformObjectToWorldNormal(v.normal);
                // at night: the day lighting (the grade makes it night), dimmed near the eye
                half nearShade = ENV_NearShade(wpos);
                Light mainLight = GetMainLight();
                o.sun = ENV_SunColor(mainLight.color) * saturate(dot(n, mainLight.direction)) * nearShade;
                o.amb = ENV_Ambient(n, SampleSH(n)) * nearShade;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half4 s = SAMPLE_TEXTURE2D(_SplatMap, sampler_SplatMap, i.uvA.xy);
                half3 c = SAMPLE_TEXTURE2D(_Layer0, sampler_Layer0, i.uvB.xy).rgb * saturate(1 - s.r - s.g)
                        + SAMPLE_TEXTURE2D(_Layer1, sampler_Layer1, i.uvB.zw).rgb * s.g
                        + SAMPLE_TEXTURE2D(_Layer2, sampler_Layer2, i.uvC.xy).rgb * s.r;
                c *= lerp(half3(1, 1, 1), _DirtTint.rgb, s.b);
                c *= 1 + (SAMPLE_TEXTURE2D(_Layer0, sampler_Layer0, i.uvC.zw).g - _FarMean) * _FarStrength;     // breaks up the tile repeat
                c *= lerp(_MacroDark.rgb, _MacroLight.rgb, s.a);
                half2 sh = FE_SampleShadow(i.uvA.zw);
                // real-time shadows of animals and the player on top of the baked ones
                half rt = MainLightShadow(TransformWorldToShadowCoord(i.wpos), i.wpos, half4(1, 1, 1, 1), _MainLightOcclusionProbes);
                half3 col = ENV_Grade(c * (i.sun * sh.x * rt + i.amb * sh.y));
                return half4(ENV_ApplyFog(col, i.wpos), 1);
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
            float4 vert (GroundAttributes v) : SV_POSITION { return TransformObjectToHClip(v.vertex.xyz); }
            half frag () : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct Varyings { float4 pos : SV_POSITION; float3 normalWS : TEXCOORD0; };

            Varyings vert (GroundAttributes v)
            {
                Varyings o;
                o.pos = TransformObjectToHClip(v.vertex.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normal);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(normalize(i.normalWS));
                    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
                #else
                    return half4(NormalizeNormalPerPixel(i.normalWS), 0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
