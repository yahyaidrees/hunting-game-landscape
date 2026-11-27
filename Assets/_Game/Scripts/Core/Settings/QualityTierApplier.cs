using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Core.Settings
{
    /// <summary>
    /// Applies the graphics tier from GameSettings (Auto or the player's choice) and re-applies it whenever the
    /// settings change: the tier's pipeline asset (render scale, shadows, HDR), texture size, level-of-detail distance,
    /// detail density, the colour grade (post-processing) and bloom, FXAA, the light lift and the frame rate cap. One per scene for now; it moves to the Boot scene later.
    /// In the editor, everything it changed is put back when Play mode ends, so ProjectSettings stay untouched.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class QualityTierApplier : MonoBehaviour
    {
        [SerializeField] QualityTierSet tiers;

        QualityTierSet.Tier applied;
        int appliedFps = -1;

#if UNITY_EDITOR
        static bool savedOriginals;
        static RenderPipelineAsset originalPipeline;
        static int originalMipLimit, originalFps;
        static float originalLodBias;

        static void SaveEditorOriginals()
        {
            if (savedOriginals) return;
            savedOriginals = true;
            originalPipeline = QualitySettings.renderPipeline;
            originalMipLimit = QualitySettings.globalTextureMipmapLimit;
            originalLodBias = QualitySettings.lodBias;
            originalFps = Application.targetFrameRate;
            Application.quitting += RestoreEditorOriginals;   // fires when Play mode ends
        }

        static void RestoreEditorOriginals()
        {
            Application.quitting -= RestoreEditorOriginals;
            if (!savedOriginals) return;
            savedOriginals = false;
            if (QualitySettings.renderPipeline != originalPipeline) QualitySettings.renderPipeline = originalPipeline;
            QualitySettings.globalTextureMipmapLimit = originalMipLimit;
            QualitySettings.lodBias = originalLodBias;
            Application.targetFrameRate = originalFps;
            Shader.SetGlobalFloat(LowSatId, 0f);   // shader globals outlive Play mode in the editor
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => savedOriginals = false;
#endif

        void OnEnable()
        {
            GameSettings.Changed += Apply;
            QualityTiers.CinematicChanged += OnCinematic;
            Apply();
        }

        void OnDisable()
        {
            GameSettings.Changed -= Apply;
            QualityTiers.CinematicChanged -= OnCinematic;
            OnCinematic(false);   // never leave the asset at the cinematic scale (in the editor it would be saved)
        }

        // the tier's cinematic render scale while the kill camera plays, the normal one after
        UniversalRenderPipelineAsset cinematicAsset;
        float scaleBeforeCinematic = -1f;

        void OnCinematic(bool on)
        {
            if (on)
            {
                var asset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
                if (!asset || applied == null || applied.cinematicRenderScale <= 0f || scaleBeforeCinematic >= 0f) return;
                cinematicAsset = asset;
                scaleBeforeCinematic = asset.renderScale;
                asset.renderScale = Mathf.Min(asset.renderScale, applied.cinematicRenderScale);
            }
            else if (scaleBeforeCinematic >= 0f)
            {
                if (cinematicAsset) cinematicAsset.renderScale = scaleBeforeCinematic;
                scaleBeforeCinematic = -1f;
                cinematicAsset = null;
            }
        }

        /// <summary>Applies the current tier and frame rate again (after the calibrator lifted the cap to measure).</summary>
        public void Reapply()
        {
            applied = null;
            appliedFps = -1;
            Apply();
        }

        void Apply()
        {
            if (!tiers) return;
            var tier = GameSettings.Tier;
            var t = tiers.Get(tier);
            int fps = GameSettings.FrameRateChoice > 0 ? GameSettings.FrameRateChoice : t.frameRate;
            if (t == applied && fps == appliedFps) return;
#if UNITY_EDITOR
            SaveEditorOriginals();
#endif
            if (t != applied)
            {
                // each of these is costly to change (pipeline rebuild, texture re-upload), so only on a real change
                if (t.pipeline && QualitySettings.renderPipeline != t.pipeline) QualitySettings.renderPipeline = t.pipeline;
                if (QualitySettings.globalTextureMipmapLimit != t.textureMipLimit) QualitySettings.globalTextureMipmapLimit = t.textureMipLimit;
                QualitySettings.lodBias = t.lodBias;
                QualityTiers.DetailDensity = t.detailDensity;
                SetBloom(t.bloom);
                SetFxaa(t.fxaa);
                SetPostProcessing(t.postProcessing);
                SetLight(t);
                applied = t;
            }
#if !UNITY_EDITOR
            QualitySettings.vSyncCount = 0;   // phones ignore vSync; targetFrameRate is the cap
#endif
            Application.targetFrameRate = fps;
            appliedFps = fps;
            QualityTiers.Current = tier;
            QualityTiers.HasApplied = true;
            QualityTiers.RaiseApplied();
        }

        static void SetPostProcessing(bool on)
        {
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (cam.TryGetComponent<UniversalAdditionalCameraData>(out var data) && data.renderType == CameraRenderType.Base)
                    data.renderPostProcessing = on;
        }

        // the scene's own light, read once, so tiers scale it instead of compounding (Play mode undoes the changes)
        Light sun;
        bool haveSceneLight;
        float sceneSunIntensity;
        Color sceneSunColor, sceneSky, sceneEquator, sceneGround, sceneFlat;

        void SetLight(QualityTierSet.Tier t)
        {
            if (!haveSceneLight)
            {
                sun = RenderSettings.sun;
                if (!sun)
                    foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                        if (l.type == LightType.Directional) { sun = l; break; }
                if (sun) { sceneSunIntensity = sun.intensity; sceneSunColor = sun.color; }
                sceneSky = RenderSettings.ambientSkyColor; sceneEquator = RenderSettings.ambientEquatorColor;
                sceneGround = RenderSettings.ambientGroundColor; sceneFlat = RenderSettings.ambientLight;
                haveSceneLight = true;
            }
            if (sun)
            {
                sun.intensity = sceneSunIntensity * t.sunScale;
                sun.color = sceneSunColor * t.sunTint;
            }
            float a = t.ambientScale;
            RenderSettings.ambientSkyColor = Scale(sceneSky, a);
            RenderSettings.ambientEquatorColor = Scale(sceneEquator, a);
            RenderSettings.ambientGroundColor = Scale(sceneGround, a);
            RenderSettings.ambientLight = Scale(sceneFlat, a);
            Shader.SetGlobalFloat(LowSatId, t.extraSaturation);   // EnvNight.cginc: ground, sky and backdrop
        }

        static readonly int LowSatId = Shader.PropertyToID("_EnvLowSat");

        static Color Scale(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        static void SetFxaa(bool on)
        {
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (cam.TryGetComponent<UniversalAdditionalCameraData>(out var data) && data.renderType == CameraRenderType.Base)
                    data.antialiasing = on ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;
        }

        // Volume.profile makes a scene-only copy of the shared profile, so the asset on disk never changes.
        static void SetBloom(bool on)
        {
            foreach (var volume in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (!volume.sharedProfile || !volume.sharedProfile.Has<Bloom>()) continue;
                if (volume.profile.TryGet<Bloom>(out var bloom)) bloom.active = on;
            }
        }
    }
}
