using System.IO;
using System.Linq;
using System.Text;
using Game.Animals;
using Game.Core.Combat;
using Game.Player;
using Game.UI;
using Game.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>Weapons part of the graybox builder: rifle data, first-person rifle on an overlay camera, wolf hitboxes, HUD.</summary>
    public static partial class GrayboxR01Builder
    {
        const string WeaponsDataDir = Data + "/Weapons";
        const string BloodDir = "Assets/_Game/Art/Decals/Blood";
        const string UiSpriteDir = "Assets/_Game/Art/UI/Sprites";
        const string WeaponLayerName = "FirstPersonWeapon";

        public sealed class WeaponAssets
        {
            public WeaponDefinition rifle;
            public Material blood, wood, metal;
            public Sprite scopeMask;
            public int layer;
        }

        static WeaponAssets EnsureWeaponAssets(StringBuilder log)
        {
            foreach (var f in new[] { WeaponsDataDir, BloodDir, UiSpriteDir }) EnsureFolder(f);
            var w = new WeaponAssets
            {
                rifle = EnsureAsset<WeaponDefinition>(WeaponsDataDir + "/Weapon_HuntingRifle.asset", log),
                wood = LitMaterial("GB_RifleWood", new Color(0.36f, 0.22f, 0.12f), log),
                metal = LitMaterial("GB_RifleMetal", new Color(0.16f, 0.16f, 0.17f), log),
                layer = EnsureLayer(WeaponLayerName, log),
            };
            var source = AssetDatabase.LoadAssetAtPath<Material>(UrpDecalMat);
            string bloodTex = BloodDir + "/Blood_Drops.png";
            if (!File.Exists(bloodTex)) { File.WriteAllBytes(bloodTex, MakeBloodDrops()); ImportDecalTexture(bloodTex); }
            w.blood = DecalMaterialFromTexture("M_BloodDrops", bloodTex, source, 4, log);

            string maskPath = UiSpriteDir + "/UI_ScopeMask.png";
            if (!File.Exists(maskPath))
            {
                File.WriteAllBytes(maskPath, MakeScopeMask());
                AssetDatabase.ImportAsset(maskPath);
                var imp = (TextureImporter)AssetImporter.GetAtPath(maskPath);
                imp.textureType = TextureImporterType.Sprite;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.SaveAndReimport();
            }
            w.scopeMask = AssetDatabase.LoadAssetAtPath<Sprite>(maskPath);
            return w;
        }

        static int EnsureLayer(string name, StringBuilder log)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 8; i < 32; i++)
            {
                var p = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(p.stringValue)) continue;
                p.stringValue = name;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                log.AppendLine($"added layer {i} '{name}'");
                return i;
            }
            throw new System.Exception("no free layer for " + name);
        }

        static void ImportDecalTexture(string path)
        {
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
        }

        static Material DecalMaterialFromTexture(string name, string texPath, Material source, int drawOrder, StringBuilder log)
        {
            string matPath = $"{TrackingMatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (!mat) { mat = new Material(source) { name = name }; AssetDatabase.CreateAsset(mat, matPath); log.AppendLine("created " + matPath); }
            mat.SetTexture("Base_Map", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            mat.SetFloat("_DrawOrder", drawOrder);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // A few dark drops and a smear, kept small and muted (plan: light blood, never gore).
        static byte[] MakeBloodDrops()
        {
            const int n = 128;
            var rng = new System.Random(11);
            var drops = Enumerable.Range(0, 7).Select(_ => (x: 0.25f + (float)rng.NextDouble() * 0.5f, y: 0.25f + (float)rng.NextDouble() * 0.5f, r: 0.04f + (float)rng.NextDouble() * 0.07f)).ToArray();
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n, a = 0f;
                foreach (var d in drops)
                {
                    float dist = Mathf.Sqrt((u - d.x) * (u - d.x) + (v - d.y) * (v - d.y)) / d.r;
                    a = Mathf.Max(a, Mathf.SmoothStep(1f, 0.7f, dist));
                }
                px[y * n + x] = new Color(0.26f, 0.05f, 0.04f, a * 0.85f);
            }
            return Png(px, n);
        }

        // Duplex reticle (thick posts, fine centre cross) inside a round field of view with a dark surround.
        static byte[] MakeScopeMask()
        {
            const int n = 512;
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n - 0.5f, v = (y + 0.5f) / n - 0.5f;
                float r = Mathf.Sqrt(u * u + v * v);
                float outside = Mathf.SmoothStep(0.47f, 0.49f, r);
                float ax = Mathf.Abs(u), ay = Mathf.Abs(v);
                float thick = 3.5f / n, thin = 0.9f / n;
                bool postH = ay < thick && ax > 0.17f, postV = ax < thick && ay > 0.17f;
                bool fineH = ay < thin && ax <= 0.17f, fineV = ax < thin && ay <= 0.17f;
                float line = (postH || postV) ? 1f : (fineH || fineV) ? 0.9f : 0f;
                float edgeShade = Mathf.SmoothStep(0.38f, 0.47f, r) * 0.35f;   // soft darkening toward the rim
                px[y * n + x] = new Color(0f, 0f, 0f, Mathf.Clamp01(Mathf.Max(outside, Mathf.Max(line, edgeShade))));
            }
            return Png(px, n);
        }

        // ---------- player: rifle on its own overlay camera ----------

        static void AddWeaponToPlayer(GameObject root, GameObject pivot, Camera cam, WeaponAssets w)
        {
            int layerMask = 1 << w.layer;
            cam.cullingMask &= ~layerMask;

            var overlayGo = new GameObject("WeaponCamera");
            overlayGo.transform.SetParent(pivot.transform, false);
            var overlay = overlayGo.AddComponent<Camera>();
            overlay.cullingMask = layerMask;
            overlay.nearClipPlane = 0.01f;
            overlay.farClipPlane = 5f;
            overlay.fieldOfView = 55f;
            overlay.clearFlags = CameraClearFlags.Depth;
            var overlayData = overlayGo.AddComponent<UniversalAdditionalCameraData>();
            overlayData.renderType = CameraRenderType.Overlay;
            overlayData.renderShadows = false;
            cam.GetUniversalAdditionalCameraData().cameraStack.Add(overlay);

            // placeholder rifle: stock, receiver, barrel, scope (graybox until the real model)
            var rifle = new GameObject("Rifle").transform;
            rifle.SetParent(pivot.transform, false);
            rifle.localPosition = new Vector3(0.22f, -0.2f, 0.45f);
            Primitive(PrimitiveType.Cube, rifle, "Stock", new Vector3(0f, -0.03f, -0.28f), Quaternion.Euler(8f, 0f, 0f), new Vector3(0.05f, 0.09f, 0.32f), w.wood);
            Primitive(PrimitiveType.Cube, rifle, "Receiver", new Vector3(0f, 0f, 0f), Quaternion.identity, new Vector3(0.045f, 0.06f, 0.26f), w.metal);
            Primitive(PrimitiveType.Cube, rifle, "Forestock", new Vector3(0f, -0.025f, 0.2f), Quaternion.identity, new Vector3(0.05f, 0.05f, 0.24f), w.wood);
            Primitive(PrimitiveType.Cylinder, rifle, "Barrel", new Vector3(0f, 0.005f, 0.42f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.022f, 0.24f, 0.022f), w.metal);
            Primitive(PrimitiveType.Cylinder, rifle, "Scope", new Vector3(0f, 0.065f, 0.02f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.04f, 0.14f, 0.04f), w.metal);
            foreach (var t in rifle.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = w.layer;
                if (t.TryGetComponent(out MeshRenderer mr))
                {
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    // the overlay camera renders no shadows of its own; sampling the main camera's shadow map speckled the rifle
                    mr.receiveShadows = false;
                }
            }

            var weapon = root.AddComponent<WeaponController>();
            SetRefs(weapon, ("weapon", w.rifle), ("controller", root.GetComponent<FirstPersonController>()),
                ("input", root.GetComponent<PlayerInputReader>()), ("viewCamera", cam), ("weaponModel", rifle));
        }

        // ---------- wolf: hit zones, health, blood ----------

        static void AddWolfCombat(GameObject root, AnimalBehaviourSettings behaviour, Transform visual, Material blood)
        {
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;   // moved by the NavMeshAgent; lets its trigger hitboxes move cheaply
            var zones = new GameObject("Hitboxes").transform;
            zones.SetParent(root.transform, false);
            void Box(string name, HitZone zone, System.Action<GameObject> shape)
            {
                var go = new GameObject(name);
                go.transform.SetParent(zones, false);
                shape(go);
                var col = go.GetComponent<Collider>();
                col.isTrigger = true;
                go.AddComponent<Hitbox>().zone = zone;
            }
            Box("Body", HitZone.Body, g => { var c = g.AddComponent<CapsuleCollider>(); c.direction = 2; c.radius = 0.2f; c.height = 1.05f; c.center = new Vector3(0f, 0.58f, 0f); });
            Box("HeartLungs", HitZone.Vital, g => { var c = g.AddComponent<SphereCollider>(); c.radius = 0.14f; c.center = new Vector3(0f, 0.56f, 0.28f); });
            Box("Head", HitZone.Head, g => { var c = g.AddComponent<SphereCollider>(); c.radius = 0.15f; c.center = new Vector3(0f, 0.78f, 0.64f); });
            foreach (var (x, z) in new[] { (-0.12f, 0.35f), (0.12f, 0.35f), (-0.12f, -0.35f), (0.12f, -0.35f) })
                Box("Leg", HitZone.Leg, g => { var c = g.AddComponent<CapsuleCollider>(); c.direction = 1; c.radius = 0.06f; c.height = 0.42f; c.center = new Vector3(x, 0.2f, z); });

            var health = root.AddComponent<AnimalHealth>();
            SetRefs(health, ("settings", behaviour));
            var trail = root.AddComponent<BloodTrailEmitter>();
            SetRefs(trail, ("health", health), ("dropMaterial", blood));
            SetRefs(root.GetComponent<AnimalBrain>(), ("health", health), ("visual", visual));
        }

        // ---------- HUD ----------

        static void AddWeaponHud(RectTransform hudRoot, RectTransform safe, Font font, Sprite knob, Sprite scopeMask, Color parchment, Color forest)
        {
            Color amber = new Color32(0xE9, 0xA2, 0x3B, 0xFF);
            Color ink = new Color32(0x26, 0x1E, 0x17, 0xFF);

            // Scope picture over the whole screen, behind the controls; never blocks touches.
            var scope = NewRect("ScopeOverlay", hudRoot);
            scope.SetSiblingIndex(0);
            Stretch(scope);
            var group = scope.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false;
            var mask = NewRect("Mask", scope);
            Anchor(mask, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080f, 1080f));
            var maskImg = mask.gameObject.AddComponent<Image>(); maskImg.sprite = scopeMask; maskImg.raycastTarget = false;
            var left = NewRect("Left", scope);
            left.anchorMin = Vector2.zero; left.anchorMax = new Vector2(0.5f, 1f); left.offsetMin = Vector2.zero; left.offsetMax = new Vector2(-539f, 0f);
            var leftImg = left.gameObject.AddComponent<Image>(); leftImg.color = Color.black; leftImg.raycastTarget = false;
            var right = NewRect("Right", scope);
            right.anchorMin = new Vector2(0.5f, 0f); right.anchorMax = Vector2.one; right.offsetMin = new Vector2(539f, 0f); right.offsetMax = Vector2.zero;
            var rightImg = right.gameObject.AddComponent<Image>(); rightImg.color = Color.black; rightImg.raycastTarget = false;
            var breathBar = NewRect("BreathBar", scope);
            Anchor(breathBar, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(320f, 14f));
            var bbImg = breathBar.gameObject.AddComponent<Image>(); bbImg.color = new Color(0f, 0f, 0f, 0.6f); bbImg.raycastTarget = false;
            var bbFill = NewRect("Fill", breathBar);
            bbFill.anchorMin = Vector2.zero; bbFill.anchorMax = Vector2.one; bbFill.offsetMin = bbFill.offsetMax = Vector2.zero;
            var bbFillImg = bbFill.gameObject.AddComponent<Image>(); bbFillImg.color = parchment; bbFillImg.raycastTarget = false;
            SetRefs(scope.gameObject.AddComponent<ScopeOverlayView>(),
                ("group", group), ("breathFill", bbFill), ("breathFillGraphic", bbFillImg), ("breathBar", breathBar.gameObject));

            // Bottom right, as in the plan's HUD: fire (largest), scope, reload, hold breath.
            RectTransform RoundButton(string name, Vector2 pos, float size, Color color, string controlPath, string text, int fontSize, Color textColor)
            {
                var r = NewRect(name, safe);
                Anchor(r, new Vector2(1f, 0f), pos, new Vector2(size, size));
                var img = r.gameObject.AddComponent<Image>(); img.sprite = knob; img.color = color;
                r.gameObject.AddComponent<OnScreenButton>().controlPath = controlPath;
                var t = NewText("Label", r, font, text, fontSize, textColor, TextAnchor.MiddleCenter);
                t.fontStyle = FontStyle.Bold;
                Fill(t.rectTransform, Vector2.zero, Vector2.zero);
                return r;
            }
            RoundButton("FireButton", new Vector2(-200f, 250f), 210f, new Color(amber.r, amber.g, amber.b, 0.92f), "<Gamepad>/rightTrigger", "Fire", 40, ink);
            RoundButton("ScopeButton", new Vector2(-410f, 165f), 140f, new Color(forest.r, forest.g, forest.b, 0.85f), "<Gamepad>/leftTrigger", "Scope", 28, parchment);
            RoundButton("ReloadButton", new Vector2(-575f, 90f), 110f, new Color(forest.r, forest.g, forest.b, 0.85f), "<Gamepad>/buttonWest", "Reload", 24, parchment);
            RoundButton("BreathButton", new Vector2(-120f, 480f), 120f, new Color(forest.r, forest.g, forest.b, 0.85f), "<Gamepad>/rightShoulder", "Breath", 24, parchment);

            var ammo = NewText("Ammo", safe, font, "5 / 5", 30, parchment, TextAnchor.MiddleCenter);
            Anchor(ammo.rectTransform, new Vector2(1f, 0f), new Vector2(-575f, 175f), new Vector2(160f, 44f));
            ammo.gameObject.AddComponent<Shadow>();
            SetRefs(ammo.gameObject.AddComponent<AmmoView>(), ("label", ammo));

            // Messages (top centre, under the wind) and the red flash when an animal hits the hunter.
            var msg = NewRect("HuntMessage", safe);
            Anchor(msg, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(1120f, 64f));
            var msgGroup = msg.gameObject.AddComponent<CanvasGroup>(); msgGroup.alpha = 0f; msgGroup.blocksRaycasts = false;
            var msgBg = msg.gameObject.AddComponent<Image>(); msgBg.color = new Color(forest.r, forest.g, forest.b, 0.8f); msgBg.raycastTarget = false;
            var msgText = NewText("Label", msg, font, "", 30, parchment, TextAnchor.MiddleCenter);
            Fill(msgText.rectTransform, new Vector2(16f, 0f), new Vector2(-16f, 0f));
            var flash = NewRect("DamageFlash", hudRoot);
            Stretch(flash);
            var flashImg = flash.gameObject.AddComponent<Image>(); flashImg.color = new Color(0.78f, 0.27f, 0.18f, 0f); flashImg.raycastTarget = false;
            SetRefs(msg.gameObject.AddComponent<HuntMessageView>(), ("group", msgGroup), ("label", msgText), ("damageFlash", flashImg));
        }
    }
}
