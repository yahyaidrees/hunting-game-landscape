using UnityEngine;

// Pushes the farm environment's shared shader settings (ground shadow map, grass fade distance)
// to the FarmEnv shaders. Lives on the Env root under CommomStatic.
[ExecuteAlways]
public class FarmEnvironment : MonoBehaviour
{
    [Tooltip("R = sun visibility, G = ambient occlusion. Baked by Tools > Farm Env.")]
    public Texture2D groundShadowMap;
    [Tooltip("World area covered by the shadow map: x, z, size x, size z.")]
    public Vector4 shadowMapArea = new Vector4(0, 0, 2000, 2000);
    [Range(0, 1)] public float shadowStrength = 0.75f;
    [Range(0, 1)] public float aoStrength = 0.6f;
    [Tooltip("Keep the grass fade distance on every quality level (matches FarmScatterRenderer.ignoreQualityLodBias).")]
    public bool ignoreQualityLodBias;

    static readonly int GroundShadowId = Shader.PropertyToID("_FE_GroundShadow");
    static readonly int GroundShadowRectId = Shader.PropertyToID("_FE_GroundShadowRect");
    static readonly int ShadowStrengthId = Shader.PropertyToID("_FE_ShadowStrength");
    static readonly int AOStrengthId = Shader.PropertyToID("_FE_AOStrength");
    static readonly int FadeScaleId = Shader.PropertyToID("_FE_FadeScale");

    void OnEnable()
    {
        Apply();
    }

    void OnValidate()
    {
        Apply();
    }

    public void Apply()
    {
        bool hasMap = groundShadowMap != null;
        Shader.SetGlobalTexture(GroundShadowId, hasMap ? groundShadowMap : Texture2D.whiteTexture);
        Shader.SetGlobalVector(GroundShadowRectId, new Vector4(
            1f / shadowMapArea.z, 1f / shadowMapArea.w,
            -shadowMapArea.x / shadowMapArea.z, -shadowMapArea.y / shadowMapArea.w));
        Shader.SetGlobalFloat(ShadowStrengthId, hasMap ? shadowStrength : 0f);
        Shader.SetGlobalFloat(AOStrengthId, hasMap ? aoStrength : 0f);
        // Grass fades with the same quality scale Unity applies to LOD distances.
        Shader.SetGlobalFloat(FadeScaleId, ignoreQualityLodBias ? 1f : QualitySettings.lodBias);
    }
}
