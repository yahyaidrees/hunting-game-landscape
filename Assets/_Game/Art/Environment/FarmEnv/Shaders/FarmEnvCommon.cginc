#ifndef FARM_ENV_COMMON_INCLUDED
#define FARM_ENV_COMMON_INCLUDED

// URP port (2026-11-20). Include after "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl".
// Globals pushed by the FarmEnvironment component.
TEXTURE2D(_FE_GroundShadow);     // R = sun visibility, G = ambient occlusion, B = cloud shadow
SAMPLER(sampler_FE_GroundShadow);
float4 _FE_GroundShadowRect;     // xy = 1 / size, zw = -origin / size (world XZ -> uv)
half _FE_ShadowStrength;         // 0 when no shadow map is assigned
half _FE_AOStrength;
float _FE_FadeScale;             // QualitySettings.lodBias, keeps grass fade in step with LOD culling

float2 FE_ShadowUV(float3 worldPos)
{
    return worldPos.xz * _FE_GroundShadowRect.xy + _FE_GroundShadowRect.zw;
}

half2 FE_ApplyShadow(half2 t)
{
    return half2(lerp(1, t.r, _FE_ShadowStrength), lerp(1, t.g, _FE_AOStrength));
}

half2 FE_SampleShadow(float2 uv)
{
    return FE_ApplyShadow(SAMPLE_TEXTURE2D(_FE_GroundShadow, sampler_FE_GroundShadow, uv).rg);
}

half2 FE_SampleShadowLod(float2 uv)
{
    return FE_ApplyShadow(SAMPLE_TEXTURE2D_LOD(_FE_GroundShadow, sampler_FE_GroundShadow, uv, 0).rg);
}

// Raw map (no strength applied), for per-instance tints sampled in the vertex shader.
half3 FE_SampleShadowRawLod(float2 uv)
{
    return SAMPLE_TEXTURE2D_LOD(_FE_GroundShadow, sampler_FE_GroundShadow, uv, 0).rgb;
}

// 1 at a 60 degree vertical FOV, larger when zoomed in through the scope.
// (abs: the projection is Y-flipped when rendering into a texture on some APIs)
float FE_ZoomFactor()
{
    return max(0.1, abs(UNITY_MATRIX_P._m11) * 0.57735);
}

// Stable 0..1 random per instance from its position (no sin, mobile-precision friendly).
float FE_Hash(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

#endif
