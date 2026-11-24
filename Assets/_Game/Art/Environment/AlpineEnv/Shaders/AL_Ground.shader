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

        [Header(Grass colour variation)]
        _TintWarm ("Sunny Patches", Color) = (1.08, 1.04, 0.86, 1)
        _TintCool ("Cool Patches", Color) = (0.86, 0.96, 0.92, 1)
        _TintNoise ("Variation Noise (soft, tiling)", 2D) = "gray" {}
        _TintScale ("Variation Tiles Per Metre", Float) = 0.012
        _TintStrength ("Variation Strength", Range(0, 4)) = 2

        [Header(Ground layers over the hunting area)]
        _LayerMask ("Layer Mask (R earth, G mud, B leaves, A rock)", 2D) = "black" {}
        _LayerArea ("Layer Area (x, z, 1/size x, 1/size z)", Vector) = (0, 0, 0.004, 0.004)
        _EarthTex ("Earth", 2D) = "gray" {}
        _MudTex ("Mud", 2D) = "gray" {}
        _LeavesTex ("Leaf Litter", 2D) = "gray" {}
        _RockTex ("Rock", 2D) = "gray" {}
        _LayerTiles ("Layer Tiles Per Metre (earth, mud, leaves, rock)", Vector) = (0.47, 0.77, 0.43, 0.33)
        _LayerLight ("Layer Brightness (earth, mud, leaves, rock)", Vector) = (1, 0.8, 1, 1)
        _MacroLuma ("Average Grass Brightness Of The Macro Map", Float) = 0.3
        _SlopeRock ("Rock On Slopes (start, full; 1 - normal.y)", Vector) = (0.2, 0.34, 0, 0)
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
            float4 _TintWarm, _TintCool;
            float _TintScale, _TintStrength;
            float4 _LayerArea, _LayerTiles, _LayerLight, _SlopeRock;
            float _MacroLuma;
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
            TEXTURE2D(_TintNoise); SAMPLER(sampler_TintNoise);
            TEXTURE2D(_LayerMask); SAMPLER(sampler_LayerMask);
            TEXTURE2D(_EarthTex); SAMPLER(sampler_EarthTex);
            TEXTURE2D(_MudTex); SAMPLER(sampler_MudTex);
            TEXTURE2D(_LeavesTex); SAMPLER(sampler_LeavesTex);
            TEXTURE2D(_RockTex); SAMPLER(sampler_RockTex);

            // every vertex-to-fragment value is float: half varyings arrived as garbage on scattered pixels here
            struct Varyings
            {
                float4 pos : SV_POSITION;
                float4 uvA : TEXCOORD0;      // macro, shadow
                float4 uvB : TEXCOORD1;      // grass, bare ground
                float3 uvC : TEXCOORD2;      // far, z = night near shade
                float3 wpos : TEXCOORD3;
                float3 nrm : TEXCOORD4;      // world normal: rock on steep slopes
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
                o.nrm = TransformObjectToWorldNormal(v.normal);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half4 m = SAMPLE_TEXTURE2D(_MacroMap, sampler_MacroMap, i.uvA.xy);
                half3 detail = lerp(SAMPLE_TEXTURE2D(_GrassDetail, sampler_GrassDetail, i.uvB.xy).rgb,
                                    SAMPLE_TEXTURE2D(_DirtDetail, sampler_DirtDetail, i.uvB.zw).rgb, m.a) * 2;
                detail *= 1 + (SAMPLE_TEXTURE2D(_GrassDetail, sampler_GrassDetail, i.uvC.xy).g - 0.5) * _FarStrength;     // breaks up the tile repeat

                // grass varies in tone across the meadow: sunny yellow-green patches and cooler green ones
                float vary = SAMPLE_TEXTURE2D(_TintNoise, sampler_TintNoise, i.wpos.xz * _TintScale).r;
                float3 tint = lerp(_TintCool.rgb, _TintWarm.rgb, saturate((vary - 0.5) * _TintStrength + 0.5));
                float3 col = m.rgb * detail * lerp(tint, float3(1, 1, 1), m.a);

                // ground layers: earth paths, mud, leaf litter, rock (painted mask) and rock on steep slopes.
                // Photo layers take the macro map's painted hill light so they sit in the same light as the grass.
                float4 w = SAMPLE_TEXTURE2D(_LayerMask, sampler_LayerMask, (i.wpos.xz - _LayerArea.xy) * _LayerArea.zw);
                float slope = 1 - normalize(i.nrm).y;
                w.a = max(w.a, smoothstep(_SlopeRock.x, _SlopeRock.y, slope));
                UNITY_BRANCH
                if (dot(w, float4(1, 1, 1, 1)) > 0.003)   // most of the screen skips the extra textures
                {
                    float shade = dot(m.rgb, float3(0.3, 0.59, 0.11)) / max(0.05, _MacroLuma);
                    float2 xz = i.wpos.xz;
                    col = lerp(col, SAMPLE_TEXTURE2D(_EarthTex, sampler_EarthTex, xz * _LayerTiles.x).rgb * shade * _LayerLight.x, w.r);
                    col = lerp(col, SAMPLE_TEXTURE2D(_MudTex, sampler_MudTex, xz * _LayerTiles.y).rgb * shade * _LayerLight.y, w.g);
                    col = lerp(col, SAMPLE_TEXTURE2D(_LeavesTex, sampler_LeavesTex, xz * _LayerTiles.z).rgb * shade * _LayerLight.z, w.b);
                    col = lerp(col, SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, xz * _LayerTiles.w).rgb * shade * _LayerLight.w, w.a);
                }

                half2 sh = FE_SampleShadow(i.uvA.zw);
                // real-time shadows of animals and the player join the baked ones
                half rt = MainLightShadow(TransformWorldToShadowCoord(i.wpos), i.wpos, half4(1, 1, 1, 1), _MainLightOcclusionProbes);
                col = ENV_Grade(col * lerp(_ShadowColor.rgb, half3(1, 1, 1), sh.x * rt) * (sh.y * i.uvC.z));
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
