using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Core.Surfaces;
using Game.Player;
using Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// Builds the Phase 1 graybox: surface data, player and HUD prefabs, graybox surface decals, and the scene
    /// Scenes/Graybox/R01_Meadow_Graybox (a copy of the Alpine Meadow environment). Safe to run again: data assets
    /// keep their tuned values, prefabs and the generated scene objects are rebuilt.
    /// </summary>
    public static partial class GrayboxR01Builder
    {
        const string Data = "Assets/_Game/Data";
        const string SurfacesDir = Data + "/Surfaces";
        const string PlayerDataDir = Data + "/Player";
        const string DecalTexDir = "Assets/_Game/Art/Decals/Graybox";
        const string GrayboxMatDir = "Assets/_Game/Art/Materials/Graybox";
        const string PlayerPrefab = "Assets/_Game/Prefabs/Player/Player.prefab";
        const string HudPrefab = "Assets/_Game/Prefabs/UI/HUD_Hunt.prefab";
        const string SourceScene = "Assets/_Game/Scenes/Environments/AlpineMeadowScene.unity";
        const string ScenePath = "Assets/_Game/Scenes/Graybox/R01_Meadow_Graybox.unity";
        const string UrpDecalMat = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Decal.mat";
        const string GeneratedRoot = "[Graybox]";

        // Graybox surface patches around the spawn point: kind, offset from spawn (x right, z forward), size (m).
        static readonly (SurfaceKind kind, Vector2 offset, Vector2 size)[] Patches =
        {
            (SurfaceKind.DryLeaves,    new Vector2(-8f,  3f), new Vector2(7f, 7f)),
            (SurfaceKind.Rock,         new Vector2( 8f,  3f), new Vector2(6f, 6f)),
            (SurfaceKind.Mud,          new Vector2(-6f, -7f), new Vector2(6f, 5f)),
            (SurfaceKind.ShallowWater, new Vector2( 6f, -7f), new Vector2(6f, 4f)),
            (SurfaceKind.Rock,         new Vector2(-16f, 12f), new Vector2(5f, 5f)),   // breaks the wolf trail: no prints
        };

        [MenuItem("Game/Graybox/Build R01 Meadow Graybox")]
        public static void BuildMenu() => Debug.Log(Build());

        public static string Build()
        {
            var log = new StringBuilder();
            EnsureFolders();
            // Open the scene first: opening a scene unloads assets nothing references yet, which would turn the
            // asset handles created below into dead references (they were saved as empty fields).
            var scene = OpenOrCreateScene(log);
            var surfaces = EnsureSurfaces(log);
            var motor = EnsureAsset<PlayerMotorSettings>(PlayerDataDir + "/PlayerMotorSettings.asset", log);
            var noise = EnsureAsset<PlayerNoiseSettings>(PlayerDataDir + "/PlayerNoiseSettings.asset", log);
            var decalMats = EnsureDecalMaterials(surfaces, log);
            EnsureDecalFeature("Assets/Settings/Mobile_Renderer.asset", log);
            EnsureDecalFeature("Assets/Settings/PC_Renderer.asset", log);
            var tracking = EnsureTrackingAssets(log);
            var animals = EnsureAnimalAssets(log);
            AssetDatabase.SaveAssets();

            var player = BuildPlayerPrefab(motor, noise, log);
            var hud = BuildHudPrefab(log);
            BuildScene(scene, player, hud, surfaces, decalMats, tracking, animals, log);
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        // ---------- data ----------

        static void EnsureFolders()
        {
            foreach (var f in new[] { SurfacesDir, PlayerDataDir, DecalTexDir, GrayboxMatDir,
                                      Path.GetDirectoryName(PlayerPrefab).Replace('\\', '/'),
                                      Path.GetDirectoryName(HudPrefab).Replace('\\', '/'),
                                      Path.GetDirectoryName(ScenePath).Replace('\\', '/') })
                EnsureFolder(f);
        }

        static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        static T EnsureAsset<T>(string path, StringBuilder log) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a) return a;
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            log.AppendLine("created " + path);
            return a;
        }

        static Dictionary<SurfaceKind, SurfaceDefinition> EnsureSurfaces(StringBuilder log)
        {
            // Starting values (plan section 4 and 6): mud quiet and holds prints, dry leaves loud, rock holds none.
            var defaults = new (SurfaceKind kind, string name, float noise, float clarity, float speed, Color color)[]
            {
                (SurfaceKind.Grass,        "Grass",         0.35f, 0.5f, 1.0f, new Color(0.35f, 0.62f, 0.25f, 0.0f)),
                (SurfaceKind.DryLeaves,    "Dry leaves",    0.85f, 0.3f, 1.0f, new Color(0.80f, 0.55f, 0.18f, 0.55f)),
                (SurfaceKind.Mud,          "Mud",           0.15f, 1.0f, 0.8f, new Color(0.33f, 0.22f, 0.12f, 0.65f)),
                (SurfaceKind.Rock,         "Rock",          0.50f, 0.0f, 1.0f, new Color(0.58f, 0.58f, 0.60f, 0.60f)),
                (SurfaceKind.ShallowWater, "Shallow water", 0.65f, 0.0f, 0.7f, new Color(0.22f, 0.45f, 0.72f, 0.60f)),
                (SurfaceKind.Wood,         "Wood",          0.55f, 0.0f, 1.0f, new Color(0.55f, 0.38f, 0.22f, 0.60f)),
            };
            var map = new Dictionary<SurfaceKind, SurfaceDefinition>();
            foreach (var d in defaults)
            {
                string path = $"{SurfacesDir}/Surface_{d.kind}.asset";
                var s = AssetDatabase.LoadAssetAtPath<SurfaceDefinition>(path);
                if (!s)
                {
                    s = ScriptableObject.CreateInstance<SurfaceDefinition>();
                    s.kind = d.kind; s.displayName = d.name; s.noise = d.noise; s.trackClarity = d.clarity;
                    s.speedMultiplier = d.speed; s.debugColor = d.color;
                    AssetDatabase.CreateAsset(s, path);
                    log.AppendLine("created " + path);
                }
                map[d.kind] = s;
            }
            return map;
        }

        // ---------- decals ----------

        static Dictionary<SurfaceKind, Material> EnsureDecalMaterials(Dictionary<SurfaceKind, SurfaceDefinition> surfaces, StringBuilder log)
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(UrpDecalMat);
            var mats = new Dictionary<SurfaceKind, Material>();
            if (!source) { log.AppendLine("WARNING: URP Decal.mat not found; no surface decals"); return mats; }

            foreach (var kv in surfaces)
            {
                if (kv.Key == SurfaceKind.Grass) continue;   // the default ground needs no marker
                string texPath = $"{DecalTexDir}/GB_Surface_{kv.Key}.png";
                if (!File.Exists(texPath))
                {
                    File.WriteAllBytes(texPath, MakePatchTexture(kv.Value.debugColor, kv.Key.GetHashCode()));
                    AssetDatabase.ImportAsset(texPath);
                    var imp = (TextureImporter)AssetImporter.GetAtPath(texPath);
                    imp.alphaIsTransparency = true;
                    imp.wrapMode = TextureWrapMode.Clamp;
                    imp.SaveAndReimport();
                }
                string matPath = $"{GrayboxMatDir}/GB_Decal_{kv.Key}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (!mat)
                {
                    mat = new Material(source) { name = $"GB_Decal_{kv.Key}" };
                    AssetDatabase.CreateAsset(mat, matPath);
                    log.AppendLine("created " + matPath);
                }
                mat.SetTexture("Base_Map", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
                mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);
                mats[kv.Key] = mat;
            }
            return mats;
        }

        // A soft-edged patch with faint diagonal stripes so graybox surfaces read clearly as "marked areas".
        static byte[] MakePatchTexture(Color c, int seed)
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color[n * n];
            var rng = new System.Random(seed);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float edge = Mathf.Min(Mathf.Min(x, n - 1 - x), Mathf.Min(y, n - 1 - y)) / 14f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge));
                float stripe = ((x + y) / 8) % 2 == 0 ? 1f : 0.82f;
                float noise = 0.92f + 0.08f * (float)rng.NextDouble();
                px[y * n + x] = new Color(c.r * stripe * noise, c.g * stripe * noise, c.b * stripe * noise, c.a * a);
            }
            tex.SetPixels(px);
            tex.Apply();
            var png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            return png;
        }

        static void EnsureDecalFeature(string rendererPath, StringBuilder log)
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (!data) { log.AppendLine("WARNING: renderer not found " + rendererPath); return; }
            if (data.rendererFeatures.Any(f => f is DecalRendererFeature)) return;

            var feature = ScriptableObject.CreateInstance<DecalRendererFeature>();
            feature.name = "Decal";
            AssetDatabase.AddObjectToAsset(feature, data);
            // Screen Space: works on mobile tile GPUs and over our unlit ground shaders (DBuffer only feeds lit shaders).
            var fso = new SerializedObject(feature);
            fso.FindProperty("m_Settings.technique").enumValueIndex = 2;   // ScreenSpace
            fso.FindProperty("m_Settings.maxDrawDistance").floatValue = 300f;
            fso.ApplyModifiedPropertiesWithoutUndo();

            data.rendererFeatures.Add(feature);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
            var dso = new SerializedObject(data);
            var map = dso.FindProperty("m_RendererFeatureMap");
            map.arraySize = data.rendererFeatures.Count;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            dso.ApplyModifiedPropertiesWithoutUndo();
            data.SetDirty();
            EditorUtility.SetDirty(data);
            log.AppendLine($"added Decal feature (Screen Space) to {Path.GetFileName(rendererPath)}");
        }

        // ---------- player ----------

        static GameObject BuildPlayerPrefab(PlayerMotorSettings motor, PlayerNoiseSettings noise, StringBuilder log)
        {
            var root = new GameObject("Player");
            try
            {
                var cc = root.AddComponent<CharacterController>();
                cc.height = motor.standHeight;
                cc.radius = motor.radius;
                cc.center = new Vector3(0f, motor.standHeight * 0.5f, 0f);
                cc.slopeLimit = 50f;
                cc.stepOffset = 0.35f;
                cc.skinWidth = 0.04f;

                var pivot = new GameObject("CameraPivot");
                pivot.transform.SetParent(root.transform, false);
                pivot.transform.localPosition = new Vector3(0f, motor.standEyeHeight, 0f);
                pivot.tag = "MainCamera";
                var cam = pivot.AddComponent<Camera>();
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 1000f;
                cam.fieldOfView = 60f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.44f, 0.60f, 0.86f);
                pivot.AddComponent<AudioListener>();
                var camData = pivot.AddComponent<UniversalAdditionalCameraData>();
                camData.renderShadows = true;
                camData.renderPostProcessing = false;

                var reader = root.AddComponent<PlayerInputReader>();
                var controller = root.AddComponent<FirstPersonController>();
                SetRefs(controller, ("settings", motor), ("input", reader), ("cameraPivot", pivot.transform));
                var playerNoise = root.AddComponent<PlayerNoise>();
                SetRefs(playerNoise, ("controller", controller), ("settings", noise));
                var interactor = root.AddComponent<Game.Tracking.ClueInteractor>();
                SetRefs(interactor, ("viewCamera", cam));

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
                log.AppendLine("saved " + PlayerPrefab);
                return prefab;
            }
            finally { Object.DestroyImmediate(root); }
        }

        // ---------- HUD ----------

        static GameObject BuildHudPrefab(StringBuilder log)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            Color parchment = new Color32(0xEF, 0xE4, 0xCC, 0xFF);
            Color forest = new Color32(0x2F, 0x4A, 0x36, 0xFF);

            var root = new GameObject("HUD_Hunt", typeof(RectTransform));
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10;
                var scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 1f;   // plan section 12: scale by height
                root.AddComponent<GraphicRaycaster>();

                var safe = NewRect("SafeArea", root.transform);
                Stretch(safe);
                safe.gameObject.AddComponent<SafeAreaFitter>();

                // Right half: drag to look (behind the buttons, so it is the first child).
                var look = NewRect("LookArea", safe);
                look.anchorMin = new Vector2(0.5f, 0f);
                look.anchorMax = Vector2.one;
                look.offsetMin = look.offsetMax = Vector2.zero;
                var lookImg = look.gameObject.AddComponent<Image>();
                lookImg.color = new Color(0, 0, 0, 0);
                look.gameObject.AddComponent<TouchLookArea>();

                // Bottom left: move stick.
                var stickBg = NewRect("MoveStick", safe);
                Anchor(stickBg, new Vector2(0f, 0f), new Vector2(240f, 240f), new Vector2(270f, 270f));
                var bg = stickBg.gameObject.AddComponent<Image>();
                bg.sprite = knob; bg.color = new Color(parchment.r, parchment.g, parchment.b, 0.22f); bg.raycastTarget = false;
                var knobRect = NewRect("Knob", stickBg);
                knobRect.sizeDelta = new Vector2(120f, 120f);
                var kImg = knobRect.gameObject.AddComponent<Image>();
                kImg.sprite = knob; kImg.color = new Color(parchment.r, parchment.g, parchment.b, 0.75f);
                var stick = knobRect.gameObject.AddComponent<OnScreenStick>();
                stick.movementRange = 105f;
                stick.controlPath = "<Gamepad>/leftStick";

                // Bottom right: crouch (fire, scope and the rest arrive with shooting).
                var crouch = NewRect("CrouchButton", safe);
                Anchor(crouch, new Vector2(1f, 0f), new Vector2(-210f, 210f), new Vector2(160f, 160f));
                var cImg = crouch.gameObject.AddComponent<Image>();
                cImg.sprite = knob; cImg.color = new Color(forest.r, forest.g, forest.b, 0.8f);
                var button = crouch.gameObject.AddComponent<OnScreenButton>();
                button.controlPath = "<Gamepad>/buttonEast";
                var cLabel = NewText("Label", crouch, font, "Crouch", 30, parchment, TextAnchor.MiddleCenter);
                Stretch(cLabel.rectTransform);

                // Bottom centre: noise meter.
                var meter = NewRect("NoiseMeter", safe);
                Anchor(meter, new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(480f, 46f));
                var mBg = meter.gameObject.AddComponent<Image>();
                mBg.color = new Color(forest.r, forest.g, forest.b, 0.75f); mBg.raycastTarget = false;
                var fill = NewRect("Fill", meter);
                fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f);
                fill.offsetMin = new Vector2(4f, 4f); fill.offsetMax = new Vector2(-4f, -4f);
                var fImg = fill.gameObject.AddComponent<Image>(); fImg.raycastTarget = false;
                var mLabel = NewText("Label", meter, font, "Quiet", 28, parchment, TextAnchor.MiddleCenter);
                Stretch(mLabel.rectTransform);
                mLabel.gameObject.AddComponent<Shadow>();
                var caption = NewText("Caption", meter, font, "NOISE", 20, parchment, TextAnchor.LowerCenter);
                caption.rectTransform.anchorMin = new Vector2(0f, 1f); caption.rectTransform.anchorMax = new Vector2(1f, 1f);
                caption.rectTransform.pivot = new Vector2(0.5f, 0f);
                caption.rectTransform.anchoredPosition = new Vector2(0f, 4f); caption.rectTransform.sizeDelta = new Vector2(0f, 26f);
                var view = meter.gameObject.AddComponent<NoiseMeterView>();
                SetRefs(view, ("fill", fill), ("fillGraphic", fImg), ("label", mLabel));

                // Top left: developer readout (editor and development builds only).
                var dev = NewText("DevOverlay", safe, font, "", 24, Color.white, TextAnchor.UpperLeft);
                Anchor(dev.rectTransform, new Vector2(0f, 1f), new Vector2(290f, -275f), new Vector2(540f, 230f));
                dev.gameObject.AddComponent<Shadow>();
                var overlay = dev.gameObject.AddComponent<DevOverlay>();
                SetRefs(overlay, ("text", dev));

                AddTrackingHud(safe, font, knob, parchment, forest);
                AddWindHud(safe, font, parchment, forest);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, HudPrefab);
                log.AppendLine("saved " + HudPrefab);
                return prefab;
            }
            finally { Object.DestroyImmediate(root); }
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;   // UI
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        static void Anchor(RectTransform r, Vector2 anchor, Vector2 position, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = position;
            r.sizeDelta = size;
        }

        static Text NewText(string name, Transform parent, Font font, string value, int size, Color color, TextAnchor align)
        {
            var t = NewRect(name, parent).gameObject.AddComponent<Text>();
            t.font = font; t.text = value; t.fontSize = size; t.color = color; t.alignment = align;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        static void SetRefs(Object target, params (string field, Object value)[] refs)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in refs)
            {
                var p = so.FindProperty(field);
                if (p == null) throw new System.Exception($"{target.GetType().Name} has no serialized field '{field}'");
                p.objectReferenceValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- scene ----------

        static UnityEngine.SceneManagement.Scene OpenOrCreateScene(StringBuilder log)
        {
            if (!File.Exists(ScenePath))
            {
                if (!AssetDatabase.CopyAsset(SourceScene, ScenePath)) throw new System.Exception("could not copy " + SourceScene);
                log.AppendLine("copied " + SourceScene + " -> " + ScenePath);
            }
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        static void BuildScene(UnityEngine.SceneManagement.Scene scene, GameObject playerPrefab, GameObject hudPrefab,
                               Dictionary<SurfaceKind, SurfaceDefinition> surfaces, Dictionary<SurfaceKind, Material> decalMats,
                               TrackingAssets tracking, AnimalAssets animals, StringBuilder log)
        {
            Physics.SyncTransforms();   // ground raycasts in edit mode

            // Spawn where the old test player stood (first build) or where our player stands (rebuild).
            Vector3 spawn = Vector3.zero; float yaw = 0f;
            var marker = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "TestPlayer" || g.name == "Player");
            if (marker) { spawn = marker.transform.position; yaw = marker.transform.eulerAngles.y; }

            // Remove the old player first: its CharacterController is a collider the ground ray would hit.
            foreach (var g in scene.GetRootGameObjects())
                if (g.name is "TestPlayer" or "Player" or "HUD_Hunt" or "EventSystem" or GeneratedRoot)
                    Object.DestroyImmediate(g);
            Physics.SyncTransforms();
            spawn = GroundPoint(spawn);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.transform.SetPositionAndRotation(spawn + Vector3.up * 0.05f, Quaternion.Euler(0f, yaw, 0f));
            PrefabUtility.InstantiatePrefab(hudPrefab, scene);

            var es = new GameObject("EventSystem", typeof(EventSystem));
            var module = es.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();

            var root = new GameObject(GeneratedRoot);
            root.AddComponent<SceneSurfaceDefault>().surface = surfaces[SurfaceKind.Grass];
            var rot = Quaternion.Euler(0f, yaw, 0f);
            foreach (var p in Patches)
            {
                Vector3 center = GroundPoint(spawn + rot * new Vector3(p.offset.x, 0f, p.offset.y));
                var zoneGo = new GameObject("Zone_" + p.kind);
                zoneGo.transform.SetParent(root.transform, false);
                zoneGo.transform.SetPositionAndRotation(center, rot);
                var zone = zoneGo.AddComponent<SurfaceZone>();
                zone.surface = surfaces[p.kind];
                zone.size = new Vector3(p.size.x, 6f, p.size.y);

                if (decalMats.TryGetValue(p.kind, out var mat))
                {
                    var decalGo = new GameObject("Decal");
                    decalGo.transform.SetParent(zoneGo.transform, false);
                    decalGo.transform.localPosition = new Vector3(0f, 2f, 0f);
                    decalGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // project straight down
                    var projector = decalGo.AddComponent<DecalProjector>();
                    projector.material = mat;
                    projector.size = new Vector3(p.size.x, p.size.y, 4f);
                    projector.pivot = new Vector3(0f, 0f, 2f);
                    projector.drawDistance = 200f;
                }
            }

            Physics.SyncTransforms();
            BuildTrackingScene(root.transform, spawn, rot, tracking, log);
            BuildAnimalScene(root.transform, spawn, rot, animals, log);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine($"saved {ScenePath} (spawn {spawn}, yaw {yaw:0})");

            var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        static Vector3 GroundPoint(Vector3 p)
        {
            var origin = new Vector3(p.x, p.y + 200f, p.z);
            return Physics.Raycast(origin, Vector3.down, out var hit, 500f, ~0, QueryTriggerInteraction.Ignore) ? hit.point : p;
        }
    }
}
