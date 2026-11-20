// Distant mountain ring. The mesh is in eye-relative space (y = 0 is the horizon) and follows
// the camera, so it behaves like part of the sky. It is drawn after all opaque geometry inside a
// thin depth band just in front of the far plane: early-z hides it behind the terrain, its own
// slopes still sort by distance, and the skybox (drawn next, at the far plane) cannot overwrite it.
// URP port (2026-11-20): main light from URP, SRP Batcher compatible.
Shader "FarmEnv/DistantMountains"
{
    Properties
    {
        _DetailTex ("Rock Detail", 2D) = "gray" {}
        _DetailStrength ("Detail Strength", Range(0,1)) = 0.45
        _HazeColor ("Haze (uses fog colour when alpha = 0)", Color) = (0.75, 0.82, 0.88, 0)
        _HazeBottom ("Haze At Base", Range(0,1)) = 0.85
        _HazeTop ("Haze At Peaks", Range(0,1)) = 0.2
        _HazeHeight ("Haze Fade Height (m)", Float) = 260
        _LightAmount ("Sun Shading", Range(0,1)) = 0.65
    }

    SubShader
    {
        Tags { "Queue"="AlphaTest+30" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off

        Pass
        {
            Name "Backdrop"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "../../SavannaEnv/Shaders/EnvNight.cginc"     // moonlit night (EnvTimeOfDay); no change by day

            TEXTURE2D(_DetailTex); SAMPLER(sampler_DetailTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _DetailTex_ST;
                half _DetailStrength;
                half4 _HazeColor;
                half _HazeBottom, _HazeTop, _HazeHeight, _LightAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;            // rgb = lit + hazed colour, a = haze amount
                float3 dir : TEXCOORD1;         // eye-relative direction; the night sky is looked up per pixel (EnvNight.cginc)
            };

            half3 HazeColor() { return _HazeColor.a > 0 ? _HazeColor.rgb : unity_FogColor.rgb; }

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 wpos = _WorldSpaceCameraPos + v.vertex.xyz;
                o.pos = TransformWorldToHClip(wpos);
                // Map 600..1100 m onto a sliver of depth at the far plane (closer = nearer the camera).
                float k = 1e-6 + 9e-6 * saturate((1100 - length(v.vertex.xz)) / 500);
                #if UNITY_REVERSED_Z
                    o.pos.z = o.pos.w * k;
                #else
                    o.pos.z = o.pos.w * (1 - k);
                #endif

                Light mainLight = GetMainLight();
                half ndl = saturate(dot(normalize(v.normal), mainLight.direction) * 0.6 + 0.4);
                half3 lit = v.color.rgb * lerp(1, ndl * ENV_SunColor(mainLight.color) * 1.15, _LightAmount);
                // vertex alpha scales the haze per ring (nearer hills are clearer); below the
                // horizon it fades fully into the fog so it meets the fogged terrain seamlessly
                half haze = lerp(_HazeBottom, _HazeTop, saturate(v.vertex.y / _HazeHeight)) * v.color.a;
                haze = max(haze, saturate(-v.vertex.y / 25));
                // at night: graded, hazed toward the night sky behind it and sunk further into it (backdrop haze)
                half3 hazeCol = HazeColor();
                half3 litG = ENV_Grade(lit);
                half3 nightHaze = ENV_NightSkyDir(v.vertex.xyz);
                hazeCol = lerp(hazeCol, nightHaze, _EnvNight);
                litG = lerp(litG, nightHaze, _EnvNight * _EnvGradeParams.z);
                o.color = half4(lerp(litG, hazeCol, haze), haze);
                o.dir = v.vertex.xyz;
                o.uv = TRANSFORM_TEX(v.uv, _DetailTex);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half3 haze = lerp(HazeColor(), ENV_NightSkyDir(i.dir), _EnvNight) * i.color.a;
                half d = SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, i.uv).g;
                half3 c = (i.color.rgb - haze) * lerp(1, d * 2, _DetailStrength) + haze;
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
