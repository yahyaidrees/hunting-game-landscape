using System.IO;
using System.Text;
using Game.Tracking;
using Game.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>Tracking part of the graybox builder: data, print and glow textures, HUD, and the authored wolf trail.</summary>
    public static partial class GrayboxR01Builder
    {
        const string TrackingDataDir = Data + "/Tracking";
        const string TrackTexDir = "Assets/_Game/Art/Decals/Tracks";
        const string TrackingMatDir = "Assets/_Game/Art/Materials/Tracking";

        public sealed class TrackingAssets
        {
            public TrackingSettings settings;
            public ClueCatalog catalog;
            public SpeciesTrackDefinition wolf;
            public Material glow, killSite, scat, fur, twig;
        }

        // ---------- assets ----------

        static TrackingAssets EnsureTrackingAssets(StringBuilder log)
        {
            foreach (var f in new[] { TrackingDataDir, TrackTexDir, TrackingMatDir }) EnsureFolder(f);
            var a = new TrackingAssets();
            var source = AssetDatabase.LoadAssetAtPath<Material>(UrpDecalMat);

            // Decal draw order: surface patches 0 < kill site 2 < footprints 5 < clue glow 10.
            var fresh = DecalMaterial("M_Print_DogFamily_Fresh", "Print_DogFamily_Fresh.png", () => MakeDogPrint(false), source, log, 5);
            var old = DecalMaterial("M_Print_DogFamily_Old", "Print_DogFamily_Old.png", () => MakeDogPrint(true), source, log, 5);
            a.glow = DecalMaterial("M_ClueGlow", "ClueGlow_Ring.png", MakeGlowRing, source, log, 10);
            a.killSite = DecalMaterial("M_KillSite", "KillSite_Patch.png", MakeKillPatch, source, log, 2);
            a.scat = LitMaterial("GB_Scat", new Color(0.16f, 0.12f, 0.08f), log);
            a.fur = LitMaterial("GB_Fur", new Color(0.62f, 0.60f, 0.56f), log);
            a.twig = LitMaterial("GB_Twig", new Color(0.36f, 0.26f, 0.16f), log);

            a.settings = EnsureAsset<TrackingSettings>(TrackingDataDir + "/TrackingSettings.asset", log);
            if (!a.settings.glowMaterial) { a.settings.glowMaterial = a.glow; EditorUtility.SetDirty(a.settings); }

            string wolfPath = TrackingDataDir + "/Tracks_Wolf.asset";
            a.wolf = AssetDatabase.LoadAssetAtPath<SpeciesTrackDefinition>(wolfPath);
            if (!a.wolf)
            {
                a.wolf = ScriptableObject.CreateInstance<SpeciesTrackDefinition>();
                a.wolf.speciesId = "wolf";
                a.wolf.displayName = "Wolf";
                a.wolf.family = TrackFamily.DogFamily;
                // Real wolf prints are about 9.5 x 11.5 cm; drawn 1.5x larger so they read on a phone screen.
                a.wolf.printSize = new Vector2(0.14f, 0.17f);
                a.wolf.walkStep = 0.7f;
                a.wolf.trackHalfWidth = 0.06f;
                a.wolf.printHint = "Four toes with claw tips and a three-lobed heel pad: a dog-family print. " +
                                   "At about 11 cm long it is too big for a fox or a jackal. A wolf passed here.";
                AssetDatabase.CreateAsset(a.wolf, wolfPath);
                log.AppendLine("created " + wolfPath);
            }
            a.wolf.freshPrintMaterial = fresh;
            a.wolf.oldPrintMaterial = old;
            EditorUtility.SetDirty(a.wolf);

            string catPath = TrackingDataDir + "/ClueCatalog.asset";
            a.catalog = AssetDatabase.LoadAssetAtPath<ClueCatalog>(catPath);
            if (!a.catalog)
            {
                a.catalog = ScriptableObject.CreateInstance<ClueCatalog>();
                void Add(ClueKind k, string n, string d) => a.catalog.entries.Add(new ClueCatalog.Entry { kind = k, displayName = n, description = d });
                Add(ClueKind.Footprint, "Footprint", "Count the toes and look for claw marks. Dog-family prints show claw tips; cat prints do not, because cats pull their claws in.");
                Add(ClueKind.Scat, "Scat", "Droppings tell you what passed and what it ate. Fresh scat is dark and moist; old scat is dry and pale.");
                Add(ClueKind.FurOrTwig, "Fur", "Hair snagged on grass or a bush shows the animal's size and the way it went.");
                Add(ClueKind.ClawMarks, "Claw marks", "Scratches on a tree mark territory. Their height shows how big the animal is.");
                Add(ClueKind.DragMarks, "Drag marks", "A wide scuffed line: a predator carried its kill this way.");
                Add(ClueKind.KillSite, "Kill site", "Predators come back to feed, often at dusk. Wait in cover downwind, or place a trail camera.");
                Add(ClueKind.BloodTrail, "Blood trail", "A wounded animal's path. Follow it before the animal recovers.");
                AssetDatabase.CreateAsset(a.catalog, catPath);
                log.AppendLine("created " + catPath);
            }
            return a;
        }

        static Material DecalMaterial(string name, string textureFile, System.Func<byte[]> makeTexture, Material source, StringBuilder log, int drawOrder)
        {
            string texPath = $"{TrackTexDir}/{textureFile}";
            if (!File.Exists(texPath))
            {
                File.WriteAllBytes(texPath, makeTexture());
                AssetDatabase.ImportAsset(texPath);
                var imp = (TextureImporter)AssetImporter.GetAtPath(texPath);
                imp.alphaIsTransparency = true;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.SaveAndReimport();
            }
            string matPath = $"{TrackingMatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (!mat)
            {
                mat = new Material(source) { name = name };
                AssetDatabase.CreateAsset(mat, matPath);
                log.AppendLine("created " + matPath);
            }
            mat.SetTexture("Base_Map", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            mat.SetFloat("_DrawOrder", drawOrder);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material LitMaterial(string name, Color color, StringBuilder log)
        {
            string path = $"{GrayboxMatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(mat, path);
            log.AppendLine("created " + path);
            return mat;
        }

        // ---------- textures ----------

        /// <summary>
        /// A dog-family (canid) print, top = direction of travel: three-lobed heel pad, four toes with the two middle
        /// toes leading, and claw tips in front of every toe. Old prints are softer, paler and crumbled.
        /// </summary>
        static byte[] MakeDogPrint(bool old)
        {
            const int n = 128;
            // x, y, radius x, radius y, rotation (radians); coordinates 0..1, y = forward
            var pads = new (float x, float y, float rx, float ry, float rot)[]
            {
                (0.50f, 0.31f, 0.19f, 0.14f, 0f),          // heel pad
                (0.38f, 0.24f, 0.10f, 0.09f, 0f),          // heel rear lobes
                (0.62f, 0.24f, 0.10f, 0.09f, 0f),
                (0.26f, 0.56f, 0.085f, 0.11f, -0.35f),     // outer toes
                (0.74f, 0.56f, 0.085f, 0.11f, 0.35f),
                (0.41f, 0.72f, 0.085f, 0.115f, -0.10f),    // middle toes, further forward
                (0.59f, 0.72f, 0.085f, 0.115f, 0.10f),
            };
            var claws = new (float x, float y, float rx, float ry, float rot)[]
            {
                (0.21f, 0.70f, 0.026f, 0.045f, -0.35f),
                (0.79f, 0.70f, 0.026f, 0.045f, 0.35f),
                (0.395f, 0.875f, 0.024f, 0.045f, -0.10f),
                (0.605f, 0.875f, 0.024f, 0.045f, 0.10f),
            };
            float edge = old ? 0.55f : 0.18f;
            Color ink = old ? new Color(0.30f, 0.24f, 0.17f) : new Color(0.11f, 0.075f, 0.05f);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                float inside = 0f;
                foreach (var p in pads) inside = Mathf.Max(inside, Ellipse(u, v, p, edge));
                foreach (var c in claws) inside = Mathf.Max(inside, Ellipse(u, v, c, edge) * (old ? 0.5f : 0.85f));
                float a = inside;
                if (old)
                {
                    float crumble = Mathf.PerlinNoise(u * 14f + 3.1f, v * 14f + 7.7f);
                    a *= Mathf.Lerp(0.35f, 0.85f, crumble);
                }
                // fresh prints are darker (moist) in the middle and lighter at the pressed-out rim
                float shade = old ? 1f : Mathf.Lerp(1.6f, 0.85f, inside);
                px[y * n + x] = new Color(ink.r * shade, ink.g * shade, ink.b * shade, Mathf.Clamp01(a) * (old ? 0.8f : 0.95f));
            }
            return Png(px, n);
        }

        static float Ellipse(float u, float v, (float x, float y, float rx, float ry, float rot) e, float edge)
        {
            float dx = u - e.x, dy = v - e.y;
            float c = Mathf.Cos(-e.rot), s = Mathf.Sin(-e.rot);
            float lx = dx * c - dy * s, ly = dx * s + dy * c;
            float d = 1f - Mathf.Sqrt((lx / e.rx) * (lx / e.rx) + (ly / e.ry) * (ly / e.ry));   // 1 at centre, 0 at the edge
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d / edge));
        }

        static byte[] MakeGlowRing()
        {
            const int n = 128;
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n - 0.5f, v = (y + 0.5f) / n - 0.5f;
                float r = Mathf.Sqrt(u * u + v * v);
                float ring = Mathf.Exp(-Mathf.Pow((r - 0.36f) / 0.06f, 2f));
                float fill = 0.18f * Mathf.Clamp01(1f - r / 0.4f);
                px[y * n + x] = new Color(0.97f, 0.72f, 0.30f, Mathf.Clamp01(ring + fill) * (r < 0.5f ? 1f : 0f));
            }
            return Png(px, n);
        }

        static byte[] MakeKillPatch()
        {
            const int n = 128;
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n - 0.5f, v = (y + 0.5f) / n - 0.5f;
                float r = Mathf.Sqrt(u * u + v * v) + 0.12f * (Mathf.PerlinNoise(u * 6f + 2f, v * 6f + 5f) - 0.5f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((0.42f - r) / 0.12f)) * 0.7f;
                float grain = 0.85f + 0.15f * Mathf.PerlinNoise(u * 30f, v * 30f);
                px[y * n + x] = new Color(0.20f * grain, 0.10f * grain, 0.07f * grain, a);
            }
            return Png(px, n);
        }

        static byte[] Png(Color[] px, int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            var png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            return png;
        }

        // ---------- HUD ----------

        static void AddTrackingHud(RectTransform safe, Font font, Sprite knob, Color parchment, Color forest)
        {
            Color ink = new Color32(0x26, 0x1E, 0x17, 0xFF);
            Color bark = new Color32(0x4A, 0x37, 0x28, 0xFF);
            Color amber = new Color32(0xE9, 0xA2, 0x3B, 0xFF);

            // Top left: objective and clue progress.
            var obj = NewRect("Objective", safe);
            Anchor(obj, new Vector2(0f, 1f), new Vector2(260f, -75f), new Vector2(480f, 104f));
            var objBg = obj.gameObject.AddComponent<Image>();
            objBg.color = new Color(forest.r, forest.g, forest.b, 0.75f); objBg.raycastTarget = false;
            var objTitle = NewText("Title", obj, font, "Track the wolf", 30, parchment, TextAnchor.UpperLeft);
            objTitle.fontStyle = FontStyle.Bold;
            Fill(objTitle.rectTransform, new Vector2(18f, 50f), new Vector2(-18f, -10f));
            var objProgress = NewText("Progress", obj, font, "Clues found 0 of 0", 24, parchment, TextAnchor.UpperLeft);
            objProgress.horizontalOverflow = HorizontalWrapMode.Wrap;
            Fill(objProgress.rectTransform, new Vector2(18f, 8f), new Vector2(-18f, -50f));
            SetRefs(obj.gameObject.AddComponent<ObjectivePanelView>(), ("title", objTitle), ("progress", objProgress));

            // Left edge, middle: Hunter Sense.
            var sense = NewRect("HunterSense", safe);
            Anchor(sense, new Vector2(0f, 0.5f), new Vector2(110f, 40f), new Vector2(140f, 140f));
            var senseImg = sense.gameObject.AddComponent<Image>();
            senseImg.sprite = knob; senseImg.color = new Color(forest.r, forest.g, forest.b, 0.85f);
            var senseButton = sense.gameObject.AddComponent<Button>();
            senseButton.targetGraphic = senseImg;
            var cd = NewRect("Cooldown", sense);
            Fill(cd, Vector2.zero, Vector2.zero);
            var cdImg = cd.gameObject.AddComponent<Image>();
            cdImg.sprite = knob; cdImg.color = new Color(0f, 0f, 0f, 0.55f); cdImg.raycastTarget = false;
            cdImg.type = Image.Type.Filled; cdImg.fillMethod = Image.FillMethod.Vertical;
            cdImg.fillOrigin = (int)Image.OriginVertical.Top; cdImg.fillAmount = 0f;
            var senseLabel = NewText("Label", sense, font, "Sense", 28, parchment, TextAnchor.MiddleCenter);
            Fill(senseLabel.rectTransform, Vector2.zero, Vector2.zero);
            SetRefs(sense.gameObject.AddComponent<HunterSenseButtonView>(), ("button", senseButton), ("cooldownFill", cdImg), ("label", senseLabel));

            // Right side, above crouch: Inspect (lantern amber: the main action while tracking).
            var inspect = NewRect("Inspect", safe);
            Anchor(inspect, new Vector2(1f, 0f), new Vector2(-620f, 320f), new Vector2(230f, 92f));
            var inspectVisual = NewRect("Button", inspect);
            Fill(inspectVisual, Vector2.zero, Vector2.zero);
            var inspImg = inspectVisual.gameObject.AddComponent<Image>();
            inspImg.color = amber;
            var inspButton = inspectVisual.gameObject.AddComponent<Button>();
            inspButton.targetGraphic = inspImg;
            var inspLabel = NewText("Label", inspectVisual, font, "Inspect", 34, ink, TextAnchor.MiddleCenter);
            inspLabel.fontStyle = FontStyle.Bold;
            Fill(inspLabel.rectTransform, Vector2.zero, Vector2.zero);
            SetRefs(inspect.gameObject.AddComponent<InspectButtonView>(), ("button", inspButton), ("visual", inspectVisual.gameObject));

            // Bottom centre, above the noise meter: the clue card.
            var cardHolder = NewRect("ClueCard", safe);
            Anchor(cardHolder, new Vector2(0.5f, 0f), new Vector2(0f, 330f), new Vector2(880f, 260f));
            var card = NewRect("Panel", cardHolder);
            Fill(card, Vector2.zero, Vector2.zero);
            var cardImg = card.gameObject.AddComponent<Image>();
            cardImg.color = new Color(parchment.r, parchment.g, parchment.b, 0.97f);
            card.gameObject.AddComponent<Outline>().effectColor = new Color(bark.r, bark.g, bark.b, 0.9f);
            var header = NewRect("Header", card);
            header.anchorMin = new Vector2(0f, 1f); header.anchorMax = Vector2.one; header.pivot = new Vector2(0.5f, 1f);
            header.anchoredPosition = Vector2.zero; header.sizeDelta = new Vector2(0f, 58f);
            var headerImg = header.gameObject.AddComponent<Image>(); headerImg.color = forest; headerImg.raycastTarget = false;
            var cardTitle = NewText("Title", header, font, "", 32, parchment, TextAnchor.MiddleLeft);
            cardTitle.fontStyle = FontStyle.Bold;
            Fill(cardTitle.rectTransform, new Vector2(22f, 0f), new Vector2(-80f, 0f));
            var close = NewRect("Close", header);
            Anchor(close, new Vector2(1f, 0.5f), new Vector2(-34f, 0f), new Vector2(56f, 56f));
            var closeImg = close.gameObject.AddComponent<Image>(); closeImg.color = new Color(0f, 0f, 0f, 0f);
            var closeButton = close.gameObject.AddComponent<Button>(); closeButton.targetGraphic = closeImg;
            var closeLabel = NewText("X", close, font, "X", 32, parchment, TextAnchor.MiddleCenter);
            Fill(closeLabel.rectTransform, Vector2.zero, Vector2.zero);
            var fresh = NewText("Freshness", card, font, "", 26, bark, TextAnchor.UpperLeft);
            fresh.fontStyle = FontStyle.Italic;
            fresh.rectTransform.anchorMin = new Vector2(0f, 1f); fresh.rectTransform.anchorMax = Vector2.one;
            fresh.rectTransform.pivot = new Vector2(0.5f, 1f);
            fresh.rectTransform.anchoredPosition = new Vector2(0f, -68f); fresh.rectTransform.sizeDelta = new Vector2(-44f, 34f);
            var body = NewText("Body", card, font, "", 25, ink, TextAnchor.UpperLeft);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            Fill(body.rectTransform, new Vector2(22f, 14f), new Vector2(-22f, -108f));
            SetRefs(cardHolder.gameObject.AddComponent<ClueCardView>(),
                ("panel", card.gameObject), ("title", cardTitle), ("freshness", fresh), ("body", body), ("closeButton", closeButton));

            // Top right: hunter's map button and panel (the panel is last, so it draws over everything).
            var mapButtonRect = NewRect("MapButton", safe);
            Anchor(mapButtonRect, new Vector2(1f, 1f), new Vector2(-120f, -70f), new Vector2(180f, 80f));
            var mapBtnImg = mapButtonRect.gameObject.AddComponent<Image>();
            mapBtnImg.color = new Color(forest.r, forest.g, forest.b, 0.85f);
            var mapButton = mapButtonRect.gameObject.AddComponent<Button>(); mapButton.targetGraphic = mapBtnImg;
            var mapLabel = NewText("Label", mapButtonRect, font, "Map", 32, parchment, TextAnchor.MiddleCenter);
            Fill(mapLabel.rectTransform, Vector2.zero, Vector2.zero);

            var mapPanel = NewRect("MapPanel", safe);
            Anchor(mapPanel, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(740f, 800f));
            var mapBg = mapPanel.gameObject.AddComponent<Image>();
            mapBg.color = new Color(parchment.r, parchment.g, parchment.b, 0.97f);
            mapPanel.gameObject.AddComponent<Outline>().effectColor = new Color(bark.r, bark.g, bark.b, 0.9f);
            var mapTitle = NewText("Title", mapPanel, font, "Hunter's map", 34, ink, TextAnchor.MiddleCenter);
            mapTitle.fontStyle = FontStyle.Bold;
            mapTitle.rectTransform.anchorMin = new Vector2(0f, 1f); mapTitle.rectTransform.anchorMax = Vector2.one;
            mapTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            mapTitle.rectTransform.anchoredPosition = new Vector2(0f, -10f); mapTitle.rectTransform.sizeDelta = new Vector2(0f, 56f);
            var mapArea = NewRect("Map", mapPanel);
            Fill(mapArea, new Vector2(24f, 24f), new Vector2(-24f, -76f));
            mapArea.gameObject.AddComponent<CanvasRenderer>();   // was not added automatically for this custom graphic
            var mapGraphic = mapArea.gameObject.AddComponent<TrailMapGraphic>();
            mapGraphic.raycastTarget = false;
            SetRefs(mapButtonRect.gameObject.AddComponent<MapPanelView>(),
                ("toggleButton", mapButton), ("panel", mapPanel.gameObject), ("map", mapGraphic));
        }

        static void Fill(RectTransform r, Vector2 offsetMin, Vector2 offsetMax)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = offsetMin; r.offsetMax = offsetMax;
        }

        // ---------- scene ----------

        // The wolf's path relative to the spawn (x right, z forward): from beside the camp, south through the mud,
        // west, across the rock patch (no prints there), then north-west to where it fed.
        static readonly Vector2[] WolfPath =
        {
            new Vector2(2f, 4f), new Vector2(-3f, -2f), new Vector2(-6f, -7f), new Vector2(-13f, -4f),
            new Vector2(-17f, 3f), new Vector2(-16f, 12f), new Vector2(-20f, 22f), new Vector2(-26f, 32f),
        };

        static void BuildTrackingScene(Transform root, Vector3 spawn, Quaternion rot, TrackingAssets a, StringBuilder log)
        {
            Vector3 W(Vector2 rel) => GroundPoint(spawn + rot * new Vector3(rel.x, 0f, rel.y));

            var sysGo = new GameObject("TrackingSystem");
            sysGo.transform.SetParent(root, false);
            sysGo.transform.position = spawn + rot * new Vector3(-10f, 0f, 10f);   // centre of the hunter's map
            var sys = sysGo.AddComponent<TrackingSystem>();
            SetRefs(sys, ("settings", a.settings), ("catalog", a.catalog));
            log.AppendLine($"tracking system refs: settings={(sys.Settings ? "ok" : "NULL")} (asset {(a.settings ? "ok" : "NULL")}), catalog={(sys.Catalog ? "ok" : "NULL")} (asset {(a.catalog ? "ok" : "NULL")})");

            var trailGo = new GameObject("Trail_Wolf");
            trailGo.transform.SetParent(root, false);
            trailGo.transform.position = W(WolfPath[0]);
            var trail = trailGo.AddComponent<Trail>();
            trail.species = a.wolf;
            trail.ageHours = 3f;
            trail.points.Clear();
            foreach (var p in WolfPath) trail.points.Add(W(p));

            Vector2 Along(int i, float t) => Vector2.Lerp(WolfPath[i], WolfPath[i + 1], t);
            Vector2 Side(int i, float metres)
            {
                Vector2 d = (WolfPath[i + 1] - WolfPath[i]).normalized;
                return new Vector2(-d.y, d.x) * metres;
            }

            AddClue(trail, "Clue_1_Footprint", ClueKind.Footprint, W(WolfPath[0]), 0f, null, a, 1.2f);
            var scat = AddClue(trail, "Clue_2_Scat", ClueKind.Scat, W(Along(1, 0.45f) + Side(1, 0.5f)), 3f, null, a, 1.2f);
            Scatter(scat.transform, a.scat);
            AddClue(trail, "Clue_3_MudPrint", ClueKind.Footprint, W(WolfPath[2]), 0f,
                "A perfect print in soft mud: four toes, claw tips and a three-lobed heel pad. Mud keeps every detail, " +
                "so it is the best place to judge size and freshness.", a, 1.2f);
            var fur = AddClue(trail, "Clue_4_Fur", ClueKind.FurOrTwig, W(Along(5, 0.3f) + Side(5, 0.3f)), 0f,
                "Grey fur caught in the grass just past the rocks. The rock was too hard for prints, " +
                "but the wolf came through here.", a, 1.2f);
            FurAndTwig(fur.transform, a.fur, a.twig);
            var kill = AddClue(trail, "Clue_5_KillSite", ClueKind.KillSite, W(WolfPath[WolfPath.Length - 1]), 0f,
                "The wolf killed here and ate. It will come back to feed, most likely at dusk. " +
                "Find cover downwind and wait, or place a trail camera.", a, 2.6f);
            GroundDecal(kill.transform, "Patch", a.killSite, new Vector2(2.2f, 1.6f));

            log.AppendLine($"wolf trail: {trail.points.Count} points, 5 clues");
        }

        static Clue AddClue(Trail trail, string name, ClueKind kind, Vector3 position, float extraAge, string hint,
                            TrackingAssets a, float glowSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(trail.transform, false);
            go.transform.position = position;
            var clue = go.AddComponent<Clue>();
            clue.kind = kind;
            clue.extraAgeHours = extraAge;
            clue.hintOverride = hint ?? "";
            var glow = GroundDecal(go.transform, "Glow", a.glow, new Vector2(glowSize, glowSize));
            glow.gameObject.SetActive(false);
            SetRefs(clue, ("glow", glow));
            return clue;
        }

        static DecalProjector GroundDecal(Transform parent, string name, Material mat, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var p = go.AddComponent<DecalProjector>();
            p.material = mat;
            p.size = new Vector3(size.x, size.y, 2f);
            p.pivot = new Vector3(0f, 0f, 1f);
            p.drawDistance = 80f;
            return p;
        }

        // Graybox stand-ins until real clue models exist (plan: animal and clue art come later).
        static void Scatter(Transform parent, Material mat)
        {
            var spots = new[] { new Vector3(0f, 0.02f, 0f), new Vector3(0.06f, 0.02f, 0.04f), new Vector3(-0.05f, 0.02f, 0.05f) };
            for (int i = 0; i < spots.Length; i++)
                Primitive(PrimitiveType.Capsule, parent, "Scat_" + i, spots[i], Quaternion.Euler(90f, 35f * i, 0f), new Vector3(0.035f, 0.03f, 0.035f), mat);
        }

        static void FurAndTwig(Transform parent, Material fur, Material twig)
        {
            Primitive(PrimitiveType.Sphere, parent, "Fur", new Vector3(0f, 0.03f, 0f), Quaternion.identity, new Vector3(0.13f, 0.045f, 0.09f), fur);
            Primitive(PrimitiveType.Cylinder, parent, "Twig", new Vector3(0.1f, 0.02f, 0.05f), Quaternion.Euler(0f, 30f, 85f), new Vector3(0.012f, 0.13f, 0.012f), twig);
        }

        static void Primitive(PrimitiveType type, Transform parent, string name, Vector3 localPos, Quaternion localRot, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }
}
