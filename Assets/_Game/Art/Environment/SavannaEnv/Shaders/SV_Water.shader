// Murky savanna river: grey-green water that turns into a dull sky reflection at grazing angles, shallower and
// muddier toward the banks, and an irregular, softly fading shoreline instead of a hard ribbon edge.
// The mesh is a strip wider than the channel; vertex colour alpha = 0 at its sides .. 1 in the middle, and the
// outer part dips under the bank, so the visible edge is where the fade and the ground meet.
// World-space texture coordinates (mesh UVs are 16-bit on Android). Single pass, no depth or grab textures.
// URP port (2026-11-20): main light from URP, URP fog, SRP Batcher compatible.
Shader "SavannaEnv/Water"
{
    Properties
    {
        _DeepColor ("Deep Colour", Color) = (0.25, 0.28, 0.23, 1)
        _ShallowColor ("Shallow Colour (muddy edge)", Color) = (0.42, 0.36, 0.25, 1)
        _SkyColor ("Sky Reflection", Color) = (0.60, 0.66, 0.70, 1)
        _RippleTex ("Ripples (R)", 2D) = "gray" {}
        _RippleSize ("Ripple Size (m)", Float) = 7
        _RippleSpeed ("Ripple Speed", Float) = 0.02
        _Sparkle ("Sun Glint", Range(0, 3)) = 0.35
        _EdgeScale ("Shoreline Breakup Size (m)", Float) = 11
        _EdgeBreakup ("Shoreline Breakup", Range(0, 1)) = 0.7
        _Alpha ("Opacity", Range(0, 1)) = 0.92
    }

    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            Name "Water"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "EnvNight.cginc"

            TEXTURE2D(_RippleTex); SAMPLER(sampler_RippleTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor, _ShallowColor, _SkyColor;
                float _RippleSize, _RippleSpeed, _EdgeScale;
                half _Sparkle, _EdgeBreakup, _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 vertex : POSITION;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 pos : SV_POSITION;
                float4 uv : TEXCOORD0;
                float3 wpos : TEXCOORD1;
                float3 edgeUv : TEXCOORD2;     // xy = shoreline noise coordinates, z = 0 at the strip's sides .. 1 mid-stream
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.wpos = TransformObjectToWorld(v.vertex.xyz);
                o.pos = TransformWorldToHClip(o.wpos);
                float2 p = o.wpos.xz / _RippleSize;
                float t = _Time.y * _RippleSpeed;
                o.uv.xy = p + float2(t, t * 0.6);
                o.uv.zw = float2(p.y, -p.x) * 0.73 + float2(-t * 0.8, t * 0.5);
                o.edgeUv = float3(o.wpos.xz / _EdgeScale, v.color.a);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half r1 = SAMPLE_TEXTURE2D(_RippleTex, sampler_RippleTex, i.uv.xy).r;
                half r2 = SAMPLE_TEXTURE2D(_RippleTex, sampler_RippleTex, i.uv.zw).r;
                half breakup = SAMPLE_TEXTURE2D(_RippleTex, sampler_RippleTex, i.edgeUv.xy).r;
                // ragged shoreline: the fade line wanders with low-frequency noise
                half e = saturate((i.edgeUv.z - 0.22 - (breakup - 0.5) * _EdgeBreakup) * 2.6);
                float3 V = normalize(_WorldSpaceCameraPos - i.wpos);
                float3 n = normalize(float3((r1 - 0.5) * 0.35, 1, (r2 - 0.5) * 0.35));
                half fresnel = pow(1 - saturate(dot(V, n)), 4);
                half3 water = ENV_Grade(lerp(_ShallowColor.rgb, _DeepColor.rgb, smoothstep(0.15, 0.9, e)));
                // night sky in the calm-water reflection direction (0 by day; per pixel, see EnvNight.cginc)
                float3 toEye = _WorldSpaceCameraPos - i.wpos;
                half3 sky = lerp(_SkyColor.rgb, ENV_NightSkyDir(float3(-toEye.x, toEye.y, -toEye.z)), _EnvNight);
                half3 col = lerp(water, sky, saturate(fresnel * 0.5 + 0.03) * (0.4 + 0.6 * e));
                float3 R = reflect(-V, n);
                // at night the glint is the moon's
                Light mainLight = GetMainLight();
                float3 L = lerp(mainLight.direction, _EnvMoonDir.xyz, _EnvNight);
                half3 glint = lerp(mainLight.color, _EnvMoonColor.rgb, _EnvNight);
                col += glint * pow(saturate(dot(R, L)), 60) * _Sparkle * e;
                col = ENV_ApplyFog(col, i.wpos);
                return half4(col, _Alpha * e * lerp(0.75, 1, e));
            }
            ENDHLSL
        }
    }
}
