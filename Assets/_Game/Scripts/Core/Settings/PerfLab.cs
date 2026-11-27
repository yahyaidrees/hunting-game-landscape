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
    /// scale (render scale), hdr, post, density (grass), scatter, far, hud, shadows (distance, 0 = off), fps,
    /// mainshadows (real-time sun shadows supported, 0/1), miplimit (0 full textures, 1 half), fxaa (0/1),
    /// tier (0 Low, 1 Medium, 2 High, -1 Auto: the same switch as the settings panel).
    /// Heaviest moments: window (seconds per [perf] line), calm (1: the animals stop sensing), goto (metres: put the
    /// hunter that far from the wolf, facing it), aim (1: keep the view on the wolf's chest), scope (0/1),
    /// fire (any new value fires once), killcam (any new value plays the kill camera at the wolf; the wolf is not hurt). Found by type name, so Core needs no reference to the game assemblies.
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

        bool aimAtWolf;

        void Update()
        {
            if (aimAtWolf) AimAtWolf();
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

        string lastFire, lastKillCam;
        const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        static MonoBehaviour Find(string typeName)
        {
            foreach (var b in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (b.GetType().Name == typeName) return b;
            return null;
        }

        static void SetField(object target, string field, object value) => target?.GetType().GetField(field, Any)?.SetValue(target, value);

        // the hunter stands this far from the wolf, on the ground, facing it
        void GoToWolf(float distance)
        {
            var hunter = Find("FirstPersonController");
            var wolf = Find("AnimalBrain");
            if (!hunter || !wolf) return;
            Vector3 w = wolf.transform.position, h = hunter.transform.position;
            Vector3 dir = h - w; dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.back;
            Vector3 p = w + dir.normalized * distance;
            if (Physics.Raycast(p + Vector3.up * 50f, Vector3.down, out var hit, 200f, ~0, QueryTriggerInteraction.Ignore)) p = hit.point;
            var cc = hunter.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            hunter.transform.position = p + Vector3.up * 0.05f;
            if (cc) cc.enabled = true;
            AimAtWolf();
        }

        void AimAtWolf()
        {
            var hunter = Find("FirstPersonController");
            var wolf = Find("AnimalBrain");
            var cam = Camera.main;
            if (!hunter || !wolf || !cam) return;
            Vector3 to = wolf.transform.position + Vector3.up * 0.55f - cam.transform.position;   // the chest
            float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg;
            SetField(hunter, "yaw", yaw);
            SetField(hunter, "pitch", pitch);
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
                if (values.ContainsKey("mainshadows"))
                {
                    var field = typeof(UniversalRenderPipelineAsset).GetField("m_MainLightShadowsSupported",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    field?.SetValue(urp, Flag("mainshadows", true));
                }
            }
            if (values.ContainsKey("miplimit")) QualitySettings.globalTextureMipmapLimit = (int)Num("miplimit", 0);
            if (values.ContainsKey("fxaa"))
                foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                    if (c.TryGetComponent<UniversalAdditionalCameraData>(out var cd) && cd.renderType == CameraRenderType.Base)
                        cd.antialiasing = Flag("fxaa", true) ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;
            if (values.ContainsKey("tier")) GameSettings.QualityTierChoice = (int)Num("tier", GameSettings.AutoTier);
            if (values.ContainsKey("window")) PerfProbe.WindowSeconds = Mathf.Max(0.5f, Num("window", 10f));
            if (values.ContainsKey("calm"))
            {
                var senses = Find("AnimalSenses");
                if (senses) senses.enabled = !Flag("calm", false);
            }
            if (values.ContainsKey("goto")) GoToWolf(Num("goto", 30f));
            aimAtWolf = Flag("aim", false);
            if (values.ContainsKey("scope")) SetField(Find("WeaponController"), "scopeWanted", Flag("scope", false));
            if (values.TryGetValue("killcam", out var kc) && kc != lastKillCam)
            {
                lastKillCam = kc;
                var killCam = Find("KillCamera");
                var wolf = Find("AnimalBrain");
                var view = Camera.main;
                if (killCam && wolf && view)
                {
                    Vector3 chest = wolf.transform.position + Vector3.up * 0.55f;
                    var path = new List<Vector3> { view.transform.position, chest };
                    System.Action noHit = () => { };
                    var ok = killCam.GetType().GetMethod("TryPlay")?.Invoke(killCam,
                        new object[] { path, wolf.transform, Vector3.up * 0.55f, Vector3.Distance(view.transform.position, chest), noHit });
                    Debug.Log("[perf] lab kill camera started=" + ok);
                }
            }
            if (values.TryGetValue("fire", out var fire) && fire != lastFire)
            {
                lastFire = fire;
                var weapon = Find("WeaponController");
                weapon?.GetType().GetMethod("Fire")?.Invoke(weapon, null);
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
