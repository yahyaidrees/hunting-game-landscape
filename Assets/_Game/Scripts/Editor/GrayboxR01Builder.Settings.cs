using System.IO;
using System.Linq;
using System.Text;
using Game.Core.Settings;
using Game.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// Controls and settings (plan sections 12 and 13): the pause button and settings panel of the hunt HUD, the
    /// left-handed layout and button size (HudLayout), and the three graphics tiers with their pipeline assets.
    /// </summary>
    public static partial class GrayboxR01Builder
    {
        const string SettingsDataDir = Data + "/Settings";
        const string QualityTiersPath = SettingsDataDir + "/QualityTiers.asset";
        const string MobilePipelinePath = "Assets/Settings/Mobile_RPAsset.asset";
        const string BlobShadowTexPath = DecalTexDir + "/T_BlobShadow.png";
        const string BlobShadowMatPath = GrayboxMatDir + "/M_BlobShadow.mat";

        // Thumb controls that the button size option scales (direct children of the HUD's SafeArea).
        static readonly string[] ThumbControls =
            { "MoveStick", "CrouchButton", "FireButton", "ScopeButton", "ReloadButton", "BreathButton", "Ammo", "HunterSense", "Inspect" };

        // ---------- graphics tiers ----------

        // One copy of the phone pipeline asset per tier (same renderer, so decals and the rest stay identical),
        // re-synced from it on every build, then given the tier's render scale and shadows (plan section 13).
        static QualityTierSet EnsureQualityTiers(StringBuilder log)
        {
            EnsureFolder(SettingsDataDir);
            var mobile = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(MobilePipelinePath);
            if (!mobile) { log.AppendLine("WARNING: " + MobilePipelinePath + " not found; no graphics tiers"); return null; }

            UniversalRenderPipelineAsset Tier(string name, float renderScale, bool shadows, float distance, int cascades, bool soft)
            {
                string path = $"{SettingsDataDir}/URP_{name}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (!asset)
                {
                    AssetDatabase.CopyAsset(MobilePipelinePath, path);
                    asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                    log.AppendLine("created " + path);
                }
                else EditorUtility.CopySerialized(mobile, asset);
                asset.name = "URP_" + name;
                asset.renderScale = renderScale;
                asset.shadowDistance = distance;
                asset.shadowCascadeCount = cascades;
                var so = new SerializedObject(asset);
                so.FindProperty("m_MainLightShadowsSupported").boolValue = shadows;
                so.FindProperty("m_SoftShadowsSupported").boolValue = soft;
                var split = so.FindProperty("m_Cascade2Split");
                if (split != null) split.floatValue = 0.3f;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                return asset;
            }

            var set = EnsureAsset<QualityTierSet>(QualityTiersPath, log);
            set.low.pipeline = Tier("Low", 0.7f, false, 25f, 1, false);   // baked shadows only; animals get a blob
            set.medium.pipeline = Tier("Medium", 0.85f, true, 25f, 1, false);
            set.high.pipeline = Tier("High", 1f, true, 50f, 2, true);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            log.AppendLine("graphics tiers: Low 0.7 no real-time shadows, Medium 0.85 shadows 25 m, High 1.0 soft shadows 50 m in 2 cascades");
            return set;
        }

        static void AddQualityTiers(Transform root, StringBuilder log)
        {
            var set = EnsureQualityTiers(log);
            if (!set) return;
            var go = new GameObject("QualityTiers");
            go.transform.SetParent(root, false);
            SetRefs(go.AddComponent<QualityTierApplier>(), ("tiers", set));
        }

        // A soft dark oval under an animal for the Low tier, where real-time shadows are off.
        static void AddBlobShadow(Transform animal, Vector2 size, StringBuilder log)
        {
            var mat = EnsureBlobShadowMaterial(log);
            if (!mat) return;
            var go = new GameObject("BlobShadow");
            go.transform.SetParent(animal, false);
            go.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // project straight down
            var projector = go.AddComponent<DecalProjector>();
            projector.material = mat;
            projector.size = new Vector3(size.x, size.y, 1.4f);
            projector.pivot = new Vector3(0f, 0f, 0.7f);
            projector.drawDistance = 80f;
            projector.startAngleFade = 50f; projector.endAngleFade = 80f;
            go.SetActive(false);
            SetRefs(animal.gameObject.AddComponent<QualityTierObject>(), ("target", go));
        }

        static Material EnsureBlobShadowMaterial(StringBuilder log)
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(UrpDecalMat);
            if (!source) { log.AppendLine("WARNING: URP Decal.mat not found; no blob shadows"); return null; }
            if (!File.Exists(BlobShadowTexPath))
            {
                const int N = 128;
                var px = new Color32[N * N];
                for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a) * 0.55f;   // soft all the way to the middle, never black
                    px[y * N + x] = new Color32(0, 0, 0, (byte)(a * 255f));
                }
                var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
                tex.SetPixels32(px);
                File.WriteAllBytes(BlobShadowTexPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(BlobShadowTexPath);
                var imp = (TextureImporter)AssetImporter.GetAtPath(BlobShadowTexPath);
                imp.alphaIsTransparency = true;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.SaveAndReimport();
            }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(BlobShadowMatPath);
            if (!mat)
            {
                mat = new Material(source) { name = "M_BlobShadow" };
                AssetDatabase.CreateAsset(mat, BlobShadowMatPath);
                log.AppendLine("created " + BlobShadowMatPath);
            }
            mat.SetTexture("Base_Map", AssetDatabase.LoadAssetAtPath<Texture2D>(BlobShadowTexPath));
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------- HUD layout ----------

        static void AddHudLayout(RectTransform safe)
        {
            var thumbs = ThumbControls.Select(n => safe.Find(n)).Where(t => t).Cast<Object>().ToArray();
            var layout = safe.gameObject.AddComponent<HudLayout>();
            var so = new SerializedObject(layout);
            SetArray(so.FindProperty("thumbControls"), thumbs);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- pause button and settings panel ----------

        static void AddSettingsHud(RectTransform root, RectTransform safe, Font font, Sprite knob, Color parchment, Color forest)
        {
            Color ink = new Color32(0x24, 0x2A, 0x22, 0xFF);
            Color amber = new Color32(0xE9, 0xA2, 0x3B, 0xFF);
            Color slate = new Color32(0x5F, 0x7C, 0x8A, 0xFF);
            Color bark = new Color32(0x4A, 0x37, 0x28, 0xFF);

            // Top right, as in the plan's HUD: pause (the map button sits to its left).
            var pauseRect = NewRect("PauseButton", safe);
            Anchor(pauseRect, new Vector2(1f, 1f), new Vector2(-62f, -70f), new Vector2(96f, 80f));
            var pauseImg = pauseRect.gameObject.AddComponent<Image>();
            pauseImg.color = new Color(forest.r, forest.g, forest.b, 0.85f);
            var pauseButton = pauseRect.gameObject.AddComponent<Button>(); pauseButton.targetGraphic = pauseImg;
            var bars = NewText("Label", pauseRect, font, "II", 38, parchment, TextAnchor.MiddleCenter);
            bars.fontStyle = FontStyle.Bold;
            Fill(bars.rectTransform, Vector2.zero, Vector2.zero);

            // The panel: full-screen dim (catches touches) and a parchment card.
            var menu = NewRect("PauseMenu", root);
            Stretch(menu);
            var screen = NewRect("Screen", menu);
            Stretch(screen);
            screen.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);
            var card = NewRect("Card", screen);
            Anchor(card, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600f, 900f));
            card.gameObject.AddComponent<Image>().color = new Color(parchment.r, parchment.g, parchment.b, 0.98f);
            card.gameObject.AddComponent<Outline>().effectColor = new Color(bark.r, bark.g, bark.b, 0.9f);
            var band = NewRect("Header", card);
            band.anchorMin = new Vector2(0f, 1f); band.anchorMax = Vector2.one; band.pivot = new Vector2(0.5f, 1f);
            band.anchoredPosition = Vector2.zero; band.sizeDelta = new Vector2(0f, 96f);
            band.gameObject.AddComponent<Image>().color = forest;
            var title = NewText("Title", band, font, "Paused", 46, parchment, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            Fill(title.rectTransform, new Vector2(40f, 0f), new Vector2(-40f, 0f));

            const float ColW = 740f, Left = 40f, Right = 820f, Top = 130f, RowH = 86f;
            const float LabelW = 270f, ValueW = 110f;

            Text Label(RectTransform parent, string text, float x, float y, float w, int size, Color c, TextAnchor a, FontStyle style = FontStyle.Normal)
            {
                var t = NewText(text.Replace(' ', '_'), parent, font, text, size, c, a);
                t.fontStyle = style;
                var r = t.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0f, 1f); r.pivot = new Vector2(0f, 0.5f);
                r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, 60f);
                return t;
            }
            RectTransform Row(string name, float x, int index, string label)
            {
                var r = NewRect(name, card);
                r.anchorMin = r.anchorMax = new Vector2(0f, 1f); r.pivot = new Vector2(0f, 0.5f);
                r.anchoredPosition = new Vector2(x, -(Top + 60f + index * RowH)); r.sizeDelta = new Vector2(ColW, 72f);
                var t = NewText("Label", r, font, label, 30, ink, TextAnchor.MiddleLeft);
                var tr = t.rectTransform;
                tr.anchorMin = new Vector2(0f, 0f); tr.anchorMax = new Vector2(0f, 1f); tr.pivot = new Vector2(0f, 0.5f);
                tr.anchoredPosition = Vector2.zero; tr.sizeDelta = new Vector2(LabelW, 0f);
                return r;
            }
            RectTransform Area(RectTransform row, float from, float width)
            {
                var a = NewRect("Control", row);
                a.anchorMin = new Vector2(0f, 0f); a.anchorMax = new Vector2(0f, 1f); a.pivot = new Vector2(0f, 0.5f);
                a.anchoredPosition = new Vector2(from, 0f); a.sizeDelta = new Vector2(width, 0f);
                return a;
            }
            Image Box(RectTransform r, Color c)
            {
                var img = r.gameObject.AddComponent<Image>(); img.color = c; return img;
            }
            void SliderRow(float x, int index, string label, SettingKey key)
            {
                var row = Row(label.Replace(' ', '_'), x, index, label);
                var area = Area(row, LabelW, ColW - LabelW - ValueW);
                var bg = NewRect("Background", area);
                Fill(bg, new Vector2(0f, 28f), new Vector2(0f, -28f));
                Box(bg, new Color(bark.r, bark.g, bark.b, 0.35f)).raycastTarget = false;
                var fillArea = NewRect("Fill Area", area);
                Fill(fillArea, new Vector2(0f, 28f), new Vector2(-20f, -28f));
                var fill = NewRect("Fill", fillArea);
                fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f); fill.sizeDelta = new Vector2(20f, 0f);
                Box(fill, forest).raycastTarget = false;
                var handleArea = NewRect("Handle Slide Area", area);
                Fill(handleArea, new Vector2(20f, 0f), new Vector2(-20f, 0f));
                var handle = NewRect("Handle", handleArea);
                handle.anchorMin = new Vector2(0f, 0f); handle.anchorMax = new Vector2(0f, 1f); handle.sizeDelta = new Vector2(52f, -12f);
                var hImg = handle.gameObject.AddComponent<Image>(); hImg.sprite = knob; hImg.color = amber;
                var slider = area.gameObject.AddComponent<Slider>();
                slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = hImg;
                slider.direction = Slider.Direction.LeftToRight;
                // the whole row height is the touch target (48 dp or more), not just the thin track
                Box(area, new Color(0f, 0f, 0f, 0f));
                var value = NewText("Value", row, font, "", 30, ink, TextAnchor.MiddleRight);
                var vr = value.rectTransform;
                vr.anchorMin = new Vector2(1f, 0f); vr.anchorMax = Vector2.one; vr.pivot = new Vector2(1f, 0.5f);
                vr.anchoredPosition = Vector2.zero; vr.sizeDelta = new Vector2(ValueW - 10f, 0f);
                var comp = row.gameObject.AddComponent<SettingSliderRow>();
                SetRefs(comp, ("slider", slider), ("valueLabel", value));
                SetEnum(comp, "key", (int)key);
            }
            void ToggleRow(float x, int index, string label, SettingKey key)
            {
                var row = Row(label.Replace(' ', '_'), x, index, label);
                var area = Area(row, LabelW, 170f);
                area.sizeDelta = new Vector2(170f, -12f);
                var img = Box(area, slate);
                var b = area.gameObject.AddComponent<Button>(); b.targetGraphic = img;
                var state = NewText("State", area, font, "Off", 30, ink, TextAnchor.MiddleCenter);
                state.fontStyle = FontStyle.Bold;
                Fill(state.rectTransform, Vector2.zero, Vector2.zero);
                var comp = row.gameObject.AddComponent<SettingToggleRow>();
                SetRefs(comp, ("button", b), ("stateLabel", state), ("background", img));
                SetEnum(comp, "key", (int)key);
            }
            void ChoiceRow(float x, int index, string label, SettingKey key, (string text, int value)[] options)
            {
                var row = Row(label.Replace(' ', '_'), x, index, label);
                float w = (ColW - LabelW - 10f * (options.Length - 1)) / options.Length;
                for (int i = 0; i < options.Length; i++)
                {
                    var o = NewRect(options[i].text, row);
                    o.anchorMin = new Vector2(0f, 0f); o.anchorMax = new Vector2(0f, 1f); o.pivot = new Vector2(0f, 0.5f);
                    o.anchoredPosition = new Vector2(LabelW + i * (w + 10f), 0f); o.sizeDelta = new Vector2(w, -12f);
                    var img = Box(o, slate);
                    o.gameObject.AddComponent<Button>().targetGraphic = img;
                    var t = NewText("Label", o, font, options[i].text, 28, ink, TextAnchor.MiddleCenter);
                    t.fontStyle = FontStyle.Bold;
                    Fill(t.rectTransform, Vector2.zero, Vector2.zero);
                    var opt = o.gameObject.AddComponent<SettingChoiceOption>();
                    var so = new SerializedObject(opt);
                    so.FindProperty("value").intValue = options[i].value;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                SetEnum(row.gameObject.AddComponent<SettingChoiceRow>(), "key", (int)key);
            }

            Label(card, "CONTROLS", Left, Top, ColW, 28, forest, TextAnchor.MiddleLeft, FontStyle.Bold);
            SliderRow(Left, 0, "Look speed", SettingKey.LookSensitivity);
            SliderRow(Left, 1, "Scope look speed", SettingKey.ScopeSensitivity);
            SliderRow(Left, 2, "Button size", SettingKey.ButtonScale);
            ToggleRow(Left, 3, "Invert look", SettingKey.InvertLook);
            ToggleRow(Left, 4, "Left-handed", SettingKey.LeftHanded);

            Label(card, "GRAPHICS AND SOUND", Right, Top, ColW, 28, forest, TextAnchor.MiddleLeft, FontStyle.Bold);
            ChoiceRow(Right, 0, "Quality", SettingKey.QualityTier,
                new[] { ("Auto", GameSettings.AutoTier), ("Low", 0), ("Medium", 1), ("High", 2) });
            ChoiceRow(Right, 1, "Frame rate", SettingKey.FrameRate, new[] { ("Auto", 0), ("30", 30), ("45", 45), ("60", 60) });
            SliderRow(Right, 2, "Music", SettingKey.MusicVolume);
            SliderRow(Right, 3, "Sound", SettingKey.SoundVolume);
            SliderRow(Right, 4, "Voice", SettingKey.VoiceVolume);
            ToggleRow(Right, 5, "Blood", SettingKey.Blood);

            var info = NewText("Info", card, font, "", 24, new Color(ink.r, ink.g, ink.b, 0.7f), TextAnchor.MiddleCenter);
            var ir = info.rectTransform;
            ir.anchorMin = new Vector2(0f, 0f); ir.anchorMax = new Vector2(1f, 0f); ir.pivot = new Vector2(0.5f, 0f);
            ir.anchoredPosition = new Vector2(0f, 150f); ir.sizeDelta = new Vector2(-80f, 40f);
            SetRefs(info.gameObject.AddComponent<SettingsInfoView>(), ("label", info));

            Button Footer(string name, string label, float x, float width, Color color)
            {
                var r = NewRect(name, card);
                Anchor(r, new Vector2(0.5f, 0f), new Vector2(x, 76f), new Vector2(width, 96f));
                var img = Box(r, color);
                var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = img;
                var t = NewText("Label", r, font, label, 36, ink, TextAnchor.MiddleCenter);
                t.fontStyle = FontStyle.Bold;
                Fill(t.rectTransform, Vector2.zero, Vector2.zero);
                return b;
            }
            var restart = Footer("RestartButton", "Restart hunt", -520f, 340f, slate);
            var reset = Footer("ResetControlsButton", "Reset controls", 0f, 340f, slate);
            var done = Footer("DoneButton", "Done", 520f, 340f, amber);

            // The briefing card gets its own way in, so controls can be set before the first hunt starts.
            var brief = root.Find("HuntFlow/Briefing/Card") as RectTransform;
            Button briefSettings = null;
            if (brief)
            {
                var r = NewRect("SettingsButton", brief);
                Anchor(r, new Vector2(1f, 1f), new Vector2(-130f, -70f), new Vector2(200f, 72f));
                var img = Box(r, slate);
                briefSettings = r.gameObject.AddComponent<Button>(); briefSettings.targetGraphic = img;
                var t = NewText("Label", r, font, "Settings", 30, ink, TextAnchor.MiddleCenter);
                t.fontStyle = FontStyle.Bold;
                Fill(t.rectTransform, Vector2.zero, Vector2.zero);
            }

            var view = menu.gameObject.AddComponent<PauseMenuView>();
            SetRefs(view, ("panel", screen.gameObject), ("pauseButton", pauseButton), ("doneButton", done),
                ("restartButton", restart), ("resetControlsButton", reset), ("title", title));
            var vso = new SerializedObject(view);
            SetArray(vso.FindProperty("extraOpeners"), briefSettings ? new Object[] { briefSettings } : new Object[0]);
            vso.ApplyModifiedPropertiesWithoutUndo();
            screen.gameObject.SetActive(false);
        }

        static void SetEnum(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).enumValueIndex = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
