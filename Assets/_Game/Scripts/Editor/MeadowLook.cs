using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Editor
{
    /// <summary>
    /// The Meadow's "clear, bright morning" look (owner decision 2026-11-24), step A of the final look: light, shadows
    /// and colour. The environment's art is baked (its trees, rocks and ground carry painted light and a painted shadow
    /// map), so the look is tuned around that rather than replacing it: a warmer, stronger sun and less pale fill for
    /// everything lit in real time (wolf, hunter, props), deeper painted shadows on the ground, real-time sun shadows for
    /// the moving things, one colour grade over the whole picture, and full resolution with cheap edge smoothing.
    /// Values go into the assets once; tune them there afterwards. Run again only to restore these starting values.
    /// </summary>
    public static class MeadowLook
    {
        const string DayPreset = "Assets/_Game/Art/Environment/AlpineEnv/Lighting/AL_Day.asset";
        const string GroundMaterial = "Assets/_Game/Art/Environment/AlpineEnv/Materials/AL_Ground.mat";
        const string ProfilePath = "Assets/_Game/Art/Environment/AlpineEnv/Lighting/PP_Meadow_Morning.asset";
        const string MobileAsset = "Assets/Settings/Mobile_RPAsset.asset";
        const string VolumeName = "PostProcess_Morning";

        [MenuItem("Game/Look/Apply Clear Morning (Meadow)")]
        public static void ApplyMenu() => Debug.Log(Apply());

        public static string Apply()
        {
            var log = new StringBuilder();

            // 1. the day preset: the sun and fill light for everything lit in real time
            var day = AssetDatabase.LoadAssetAtPath<ScriptableObject>(DayPreset);
            if (day)
            {
                var so = new SerializedObject(day);
                so.FindProperty("lightIntensity").floatValue = 1.45f;
                so.FindProperty("lightColor").colorValue = new Color(1f, 0.94f, 0.83f);
                // same direction as before: the painted shadows on the ground were baked for this sun
                so.FindProperty("lightRotation").vector3Value = new Vector3(52f, 238f, 0f);
                so.FindProperty("ambientSky").colorValue = new Color(0.60f, 0.70f, 0.86f);
                so.FindProperty("ambientEquator").colorValue = new Color(0.50f, 0.56f, 0.48f);
                so.FindProperty("ambientGround").colorValue = new Color(0.32f, 0.34f, 0.22f);
                so.FindProperty("deepenShadows").floatValue = 0.15f;
                so.ApplyModifiedPropertiesWithoutUndo();
                log.AppendLine("day preset: warmer, stronger sun; less pale fill; slightly deeper darks");
            }
            else log.AppendLine("WARNING: day preset not found " + DayPreset);

            // 2. the painted shadows of trees and rocks on the ground: darker and cooler, as under a clear sky
            var ground = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterial);
            if (ground)
            {
                ground.SetColor("_ShadowColor", new Color(0.47f, 0.52f, 0.66f));
                EditorUtility.SetDirty(ground);
                log.AppendLine("ground: deeper shadow colour");
            }

            // 3. one colour grade over the whole picture
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
                log.AppendLine("created " + ProfilePath);
            }
            T Get<T>() where T : VolumeComponent
            {
                if (!profile.TryGet<T>(out var c)) { c = profile.Add<T>(true); AssetDatabase.AddObjectToAsset(c, profile); }
                c.active = true;
                return c;
            }
            Get<Tonemapping>().mode.Override(TonemappingMode.Neutral);
            var ca = Get<ColorAdjustments>();
            ca.postExposure.Override(0.15f); ca.contrast.Override(16f); ca.saturation.Override(20f);
            Get<WhiteBalance>().temperature.Override(5f);
            var bloom = Get<Bloom>();
            bloom.intensity.Override(0.2f); bloom.threshold.Override(1.15f);
            var vignette = Get<Vignette>();
            vignette.intensity.Override(0.16f); vignette.smoothness.Override(0.4f);
            EditorUtility.SetDirty(profile);

            // 4. render settings for phones: full resolution, real-time sun shadows for moving things
            var mobile = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(MobileAsset);
            if (mobile)
            {
                mobile.renderScale = 1f;
                mobile.shadowDistance = 40f;
                var mso = new SerializedObject(mobile);
                var supported = mso.FindProperty("m_MainLightShadowsSupported");
                if (supported != null) supported.boolValue = true;
                mso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(mobile);
                log.AppendLine("mobile render: full resolution, main light shadows to 40 m");
            }

            // 5. the scene: the post-process volume and the sun's shadows
            var scene = EditorSceneManager.GetActiveScene();
            GameObject volumeGo = null;
            foreach (var g in scene.GetRootGameObjects()) if (g.name == VolumeName) volumeGo = g;
            if (!volumeGo) volumeGo = new GameObject(VolumeName);
            if (!volumeGo.TryGetComponent<Volume>(out var volume)) volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                light.shadows = LightShadows.Hard;   // hard shadows: the cheap kind on phones
                light.shadowStrength = 0.8f;
                EditorUtility.SetDirty(light);
                log.AppendLine($"sun '{light.name}': real-time shadows on");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            log.AppendLine($"scene '{scene.name}': global colour grade volume");
            return log.ToString();
        }
    }
}
