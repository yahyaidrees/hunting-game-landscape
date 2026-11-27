#ifndef ENV_NIGHT_INCLUDED
#define ENV_NIGHT_INCLUDED

// URP port (2026-11-20). Include after "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl".
//
// Moonlit night for the environment shaders, driven by the EnvTimeOfDay component (SavannaEnv/Scripts).
// The art has the sun baked into its textures, so night is a colour grade of the baked colour plus a procedural sky,
// not a change of lights. Shader globals that no script has set read as zero: _EnvNight = 0 leaves every colour as it
// was, so scenes without the component keep their day look.

half _EnvNight;              // 0 = day, untouched .. 1 = full night
half4 _EnvGrade;             // rgb = colour multiplier at night
half4 _EnvGradeParams;       // x = desaturation, y = shadow deepening (blend toward c * c), z = backdrop haze, w = fog takes the sky colour
float4 _EnvNear;             // x = brightness at the eye, y = 1 / distance (m) where it is back to full
half _EnvLowSat;             // extra saturation on tiers drawn without the colour grade (Low): 0 = untouched, 0.2 = +20%

// The graphics tiers that skip post-processing (Low) lose the grade's saturation: the ground, sky and backdrop take
// it back here instead (QualityTierApplier sets _EnvLowSat). A uniform branch, skipped on the other tiers.
half3 ENV_LowTierGrade(half3 c)
{
    UNITY_BRANCH
    if (_EnvLowSat > 0)
    {
        half l = dot(c, half3(0.2126, 0.7152, 0.0722));
        c = max(l + (c - l) * (1 + _EnvLowSat), 0);
    }
    return c;
}

// Night sky: a dark gradient from the horizon up, the moon disc, a tight halo and a wide glow spread sideways.
float4 _EnvMoonDir;          // xyz = unit direction to the moon, w = disc radius (radians)
half4 _EnvMoonColor;
half4 _EnvSkyZenith;
half4 _EnvSkyHorizon;
half4 _EnvHalo;              // halo colour at the moon (falls off fast)
half4 _EnvGlow;              // glow colour at the moon (falls off slowly)
float4 _EnvSkyParams;        // x = 1 / halo width, y = 1 / glow width (radians), z = glow squash (vertical), w = 1 / sin(zenith elevation)

// A baked surface colour as it looks at night: desaturated, darks pushed down more than lights, then tinted.
half3 ENV_Grade(half3 c)
{
    half3 g = lerp(c, dot(c, half3(0.3, 0.59, 0.11)).xxx, _EnvGradeParams.x);
    g = lerp(g, g * g, _EnvGradeParams.y) * _EnvGrade.rgb;
    return lerp(c, g, _EnvNight);
}

// Per vertex, multiplies the colour before the grade: at night the ground and props close to the eye go darker and the
// open meadow further out stays the brightest part of the view (the moon's sheen on grass seen at a low angle).
half ENV_NearShade(float3 wpos)
{
    half f = saturate(distance(wpos, _WorldSpaceCameraPos) * _EnvNear.y);
    return 1 - _EnvNight * (1 - lerp(_EnvNear.x, 1, f));
}

// The night sky without the disc. dir: unit view direction. Direction maths in float: the moon is about a degree
// wide, finer than half precision resolves near 1.
half3 ENV_NightSkyGlow(float3 dir)
{
    half3 sky = lerp(_EnvSkyHorizon.rgb, _EnvSkyZenith.rgb, saturate(dir.y * _EnvSkyParams.w));
    float3 dv = dir - _EnvMoonDir.xyz;
    float r = sqrt(dot(dv, dv) + _EnvSkyParams.z * dv.y * dv.y);     // about the angle from the moon, stretched sideways
    return sky + _EnvHalo.rgb * exp(-r * _EnvSkyParams.x) + _EnvGlow.rgb * exp(-r * _EnvSkyParams.y);
}

half3 ENV_NightSky(float3 dir)
{
    half3 sky = ENV_NightSkyGlow(dir);
    float rd = length(dir - _EnvMoonDir.xyz) / _EnvMoonDir.w;       // 0 at the disc centre, 1 at its edge
    half disc = saturate((1 - rd) * 5 + 0.5);
    return lerp(sky, _EnvMoonColor.rgb * (1 - 0.18 * rd * rd), disc);
}

// At night the fog takes the night sky's colour in each direction, so far hills and trees fade into the sky right behind
// them and the mist under the moon carries its glow. The fog amount stays URP's (RenderSettings fog).
//   v2f:  float3 wpos : TEXCOORDn;
//   frag: col.rgb = ENV_ApplyFog(col.rgb, i.wpos);
// Fog factor and night fog are both computed per pixel. Under URP 17 (D3D11, editor) half-precision values written in
// the vertex stage behind a run-time branch (URP's dynamic fog keywords, the _EnvNight branch) reached the fragment
// stage as garbage on scattered pixels: black and white specks over the whole ground. Per pixel they are cheap: the
// fog is one multiply-add and the night branch is uniform and skipped by day.
half3 ENV_NightFog(float3 wpos)
{
    half3 sky = 0;
    UNITY_BRANCH
    if (_EnvNight > 0)
        sky = ENV_NightSkyGlow(normalize(wpos - _WorldSpaceCameraPos));
    return sky;
}

half ENV_FogFactor(float3 wpos)
{
    return InitializeInputDataFog(float4(wpos, 1), 0);
}

half3 ENV_MixFog(half3 col, half fogFactor, half3 nightFog)
{
    return MixFogColor(col, lerp(unity_FogColor.rgb, nightFog, _EnvNight * _EnvGradeParams.w), fogFactor);
}

// Per pixel: URP fog toward the fog colour by day, toward the night sky in that direction at night.
half3 ENV_ApplyFog(half3 col, float3 wpos)
{
    return ENV_LowTierGrade(ENV_MixFog(col, ENV_FogFactor(wpos), ENV_NightFog(wpos)));
}

// The night sky in a direction (any length), 0 by day. For reflections and backdrop haze, per vertex.
half3 ENV_NightSkyDir(float3 dir)
{
    half3 sky = 0;
    UNITY_BRANCH
    if (_EnvNight > 0)
        sky = ENV_NightSkyGlow(normalize(dir));
    return sky;
}

// The few environment shaders lit in real time (savanna ground, far ridges) keep the day lighting at night, like the
// art that has it baked in, and the grade alone makes them night: the dimmed moonlight would darken them twice.
// Pass the main light colour / SampleSH(n); by day both come back unchanged.
half4 _EnvDayLight;          // rgb = the day light colour x intensity
half4 _EnvDayAmbSky, _EnvDayAmbEquator, _EnvDayAmbGround;

half3 ENV_SunColor(half3 lightColor)
{
    return lerp(lightColor, _EnvDayLight.rgb, _EnvNight);
}

half3 ENV_Ambient(half3 n, half3 sh)
{
    half3 day = n.y > 0 ? lerp(_EnvDayAmbEquator.rgb, _EnvDayAmbSky.rgb, n.y) : lerp(_EnvDayAmbEquator.rgb, _EnvDayAmbGround.rgb, -n.y);
    return lerp(sh, day, _EnvNight);
}

// Backdrop layers drawn at infinity (mountain panorama, horizon bands) have no fog: at night they sink into the sky
// colour in their direction here. dir: eye-relative direction (any length).
half3 ENV_GradeBackdrop(half3 c, float3 dir)
{
    half3 g = ENV_Grade(c);
    UNITY_BRANCH
    if (_EnvNight > 0)
        g = lerp(g, ENV_NightSkyGlow(normalize(dir)), _EnvNight * _EnvGradeParams.z);
    return ENV_LowTierGrade(g);
}

#endif
