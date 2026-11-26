#if GAME_PERFLAB
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Core.Settings
{
    /// <summary>
    /// Test builds only (scripting define GAME_PERFLAB, set by the test build menu, never in store builds): lets the
    /// developer switch parts of the frame off and on from a PC while the game runs on a phone, to find what costs
    /// the most. Reads key=value lines from perflab.txt in the app's files folder every 2 seconds; push it with
    /// adb push perflab.txt /sdcard/Android/data/&lt;app id&gt;/files/perflab.txt. Keys:
    /// scale (render scale), hdr, post, density (grass), scatter, far, hud, shadows (distance, 0 = off), fps.
    /// Every change is logged as "[perf] lab ...", so the next [perf] line measures it.
    /// </summary>
    public sealed class PerfLab : MonoBehaviour
    {
        string path, lastText;
        float nextRead;
        readonly Dictionary<string, string> values = new Dictionary<string, string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject("PerfLab") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<PerfLab>();
        }

        void Awake() => path = Path.Combine(Application.persistentDataPath, "perflab.txt");

        void Update()
        {
            if (Time.unscaledTime < nextRead) return;
            nextRead = Time.unscaledTime + 2f;
            string text = File.Exists(path) ? File.ReadAllText(path) : "";
            if (text == lastText) return;
            lastText = text;
            values.Clear();
            foreach (var line in text.Split('\n'))
            {
                int eq = line.IndexOf('=');
                if (eq > 0) values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            Apply();
            Debug.Log("[perf] lab " + (values.Count == 0 ? "(defaults)" : text.Replace('\n', ' ').Trim()));
        }

        bool Flag(string key, bool fallback) => values.TryGetValue(key, out var v) ? v != "0" : fallback;
        float Num(string key, float fallback) => values.TryGetValue(key, out var v) && float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : fallback;

        void Apply()
        {
            var urp = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (urp)
            {
                if (values.ContainsKey("scale")) urp.renderScale = Num("scale", urp.renderScale);
                if (values.ContainsKey("hdr")) urp.supportsHDR = Flag("hdr", true);
                if (values.ContainsKey("shadows")) urp.shadowDistance = Num("shadows", urp.shadowDistance);
            }
            var cam = Camera.main;
            if (cam && cam.TryGetComponent<UniversalAdditionalCameraData>(out var data) && values.ContainsKey("post"))
                data.renderPostProcessing = Flag("post", true);
            if (values.ContainsKey("density")) QualityTiers.DetailDensity = Num("density", QualityTiers.DetailDensity);
            if (values.ContainsKey("fps")) Application.targetFrameRate = (int)Num("fps", 30);

            bool scatter = Flag("scatter", true), far = Flag("far", true), hud = Flag("hud", true);
            foreach (var b in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (b.GetType().Name == "FarmScatterRenderer") b.enabled = scatter;
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == "FarRange0" || t.name == "FarRange1") t.gameObject.SetActive(far);
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c.isRootCanvas) c.enabled = hud;
        }
    }
}
#endif
