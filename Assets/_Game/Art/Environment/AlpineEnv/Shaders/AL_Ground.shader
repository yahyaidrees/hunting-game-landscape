// Alpine meadow ground: one baked colour map holds everything large-scale (grass colour, trails, hill light and shade),
// two neutral photo tiles add the close-up detail, and the baked shadow map adds the shadows of trees, rocks and clouds.
// Texture coordinates come from the world position, not the mesh: on Android the project's vertex compression stores
// mesh UVs as 16-bit floats, too coarse for a ground this size.
// URP port (2026-11-20): receives real-time main-light shadows (animals, player) as the same shadow colour as the
// baked ones; DepthOnly + DepthNormals passes (needed by decals such as footprints); SRP Batcher compatible.
Shader "AlpineEnv/Ground"
{
    Properties
    {
        _MacroMap ("Macro (RGB lit colour, A bare ground)", 2D) = "gray" {}
        _MacroArea ("Macro Area (x, z, 1/size x, 1/size z)", Vector) = (0, 0, 0.001, 0.001)
        _GrassDetail ("Grass Detail (mean 0.5)", 2D) = "gray" {}
        _DirtDetail ("Bare Ground Detail (mean 0.5)", 2D) = "gray" {}
        _Tile ("Tiles Per Metre (grass, bare ground, far)", Vector) = (0.16, 0.14, 0.023, 0)
        _FarStrength ("Far Detail Strength", Range(0, 3)) = 1
        _ShadowColor ("Colour In Shadow", Color) = (0.63, 0.65, 0.77, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _MacroArea, _Tile;
            half4 _ShadowColor;
            half _FarStrength;
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
            #include "../../SavannaEnv/Shaders/EnvNight.cginc"

            TEXTURE2D(_MacroMap); SAMPLER(sampler_MacroMap);
            TEXTURE2D(_GrassDetail); SAMPLER(sampler_GrassDetail);
            TEXTURE2D(_DirtDetail); SAMPLER(sampler_DirtDetail);

            struct Varyings
            {
                float4 pos : SV_POSITION;
                float4 uvA : TEXCOORD0;      // macro, shadow
                float4 uvB : TEXCOORD1;      // grass, bare ground
                float3 uvC : TEXCOORD2;      // far, z = night near shade
                float3 wpos : TEXCOORD3;
            };

            Varyings vert (GroundAttributes v)
            {
                Varyings o;
                float3 wpos = TransformObjectToWorld(v.vertex.xyz);
                o.pos = TransformWorldToHClip(wpos);
                o.wpos = wpos;
                o.uvA = float4((wpos.xz - _MacroArea.xy) * _MacroArea.zw, FE_ShadowUV(wpos));
                o.uvB = float4(wpos.xz * _Tile.x, wpos.xz * _Tile.y);
                o.uvC = float3(wpos.xz * _Tile.z, ENV_NearShade(wpos));
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half4 m = SAMPLE_TEXTURE2D(_MacroMap, sampler_MacroMap, i.uvA.xy);
                half3 detail = lerp(SAMPLE_TEXTURE2D(_GrassDetail, sampler_GrassDetail, i.uvB.xy).rgb,
                                    SAMPLE_TEXTURE2D(_DirtDetail, sampler_DirtDetail, i.uvB.zw).rgb, m.a) * 2;
                detail *= 1 + (SAMPLE_TEXTURE2D(_GrassDetail, sampler_GrassDetail, i.uvC.xy).g - 0.5) * _FarStrength;     // breaks up the tile repeat
                half2 sh = FE_SampleShadow(i.uvA.zw);
                // real-time shadows of animals and the player join the baked ones
                half rt = MainLightShadow(TransformWorldToShadowCoord(i.wpos), i.wpos, half4(1, 1, 1, 1), _MainLightOcclusionProbes);
                half3 col = ENV_Grade(m.rgb * detail * lerp(_ShadowColor.rgb, half3(1, 1, 1), sh.x * rt) * (sh.y * i.uvC.z));
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
