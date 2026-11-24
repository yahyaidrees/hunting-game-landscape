using System.IO;
using System.Linq;
using System.Text;
using Game.Core.Surfaces;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Meadow look, step B: the ground (style sheet: SourceAssets/Generated/Images/StyleSheet). Paints the hunting
    /// area's ground layers into a mask the ground shader reads: the hunt's own ground zones become real mud, leaf
    /// litter and rock with soft, irregular edges, and worn earth paths lead out from the camp. The ground shader adds
    /// rock on steep slopes and colour variation across the grass. The ground zones that drive noise and footprints
    /// are unchanged. Layer photos are CC0 (Poly Haven).
    /// </summary>
    public static partial class GrayboxR01Builder
    {
        const string GroundArtDir = "Assets/_Game/Art/Environment/Ground";
        const string GroundMaterialPath = "Assets/_Game/Art/Environment/AlpineEnv/Materials/AL_Ground.mat";
        const string LayerMaskPath = GroundArtDir + "/Generated/T_R01_GroundLayers.png";
        const string TintNoisePath = GroundArtDir + "/Generated/T_GroundTintNoise.png";
        const int LayerResolution = 1024;
        const float LayerAreaSize = 240f;                                   // metres, square
        static readonly Vector2 LayerAreaCentre = new Vector2(-10f, 15f);    // spawn-relative: the hunting area

        /// <summary>Striped graybox markers on the ground zones (replaced by the ground layers).</summary>
        const bool ShowGrayboxPatchMarkers = false;

        // Worn paths people walk (spawn-relative): camp to the goat pen gate, and from the camp out into the meadow.
        // The wolf's own trail is never painted: finding it is the game.
        static readonly Vector2[][] HumanPaths =
        {
            new[] { new Vector2(0.8f, 3.6f), new Vector2(3f, 4.6f), new Vector2(4.7f, 5.4f) },
            new[] { new Vector2(-1.2f, 2.6f), new Vector2(-0.4f, -4f), new Vector2(0.6f, -12f), new Vector2(3.5f, -24f), new Vector2(8f, -38f), new Vector2(11f, -58f) },
        };

        static void BuildGroundLayers(Vector3 spawn, Quaternion rot, StringBuilder log)
        {
            EnsureFolder(GroundArtDir + "/Generated");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
            if (!mat || !mat.HasProperty("_LayerMask")) { log.AppendLine("ground layers: ground material or shader not ready"); return; }

            Texture2D Layer(string file) => ImportGroundTexture($"{GroundArtDir}/Textures/{file}");
            var earth = Layer("PH_forest_ground_06_diff_1k.jpg");
            var mud = Layer("PH_brown_mud_02_diff_1k.jpg");
            var leaves = Layer("PH_forest_leaves_03_diff_1k.jpg");
            var rock = Layer("PH_mossy_rock_diff_1k.jpg");

            if (WriteIfChanged(TintNoisePath, MakeTintNoise(256))) ImportMaskTexture(TintNoisePath, repeat: true);

            // ---- paint the mask: R earth, G mud, B leaf litter, A rock ----
            Vector3 centre = spawn + rot * new Vector3(LayerAreaCentre.x, 0f, LayerAreaCentre.y);
            Vector2 origin = new Vector2(centre.x, centre.z) - Vector2.one * (LayerAreaSize * 0.5f);
            var px = new Color[LayerResolution * LayerResolution];
            Vector2 World(Vector2 rel) { Vector3 w = spawn + rot * new Vector3(rel.x, 0f, rel.y); return new Vector2(w.x, w.z); }

            foreach (var p in Patches)
            {
                int channel = p.kind switch
                {
                    SurfaceKind.Mud => 1,
                    SurfaceKind.ShallowWater => 1,   // wet ground for now; a water surface comes with the stream work
                    SurfaceKind.DryLeaves => 2,
                    SurfaceKind.Rock => 3,
                    _ => -1,
                };
                if (channel < 0) continue;
                // leaves lie in loose drifts with grass between them; mud and rock are solid
                float breakup = p.kind == SurfaceKind.DryLeaves ? 0.75f : 0f;
                PaintBlob(px, origin, World(p.offset), rot, p.size * 0.5f, channel, 0.8f, breakup);
            }
            foreach (var path in HumanPaths) PaintPath(px, origin, path.Select(World).ToArray(), 0.55f, 0);

            var tex = new Texture2D(LayerResolution, LayerResolution, TextureFormat.RGBA32, false, true);
            tex.SetPixels(px);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            if (WriteIfChanged(LayerMaskPath, png)) ImportMaskTexture(LayerMaskPath, repeat: false);

            // ---- the ground material ----
            bool first = !mat.GetTexture("_LayerMask");
            mat.SetTexture("_LayerMask", AssetDatabase.LoadAssetAtPath<Texture2D>(LayerMaskPath));
            mat.SetVector("_LayerArea", new Vector4(origin.x, origin.y, 1f / LayerAreaSize, 1f / LayerAreaSize));
            mat.SetTexture("_EarthTex", earth);
            mat.SetTexture("_MudTex", mud);
            mat.SetTexture("_LeavesTex", leaves);
            mat.SetTexture("_RockTex", rock);
            mat.SetTexture("_TintNoise", AssetDatabase.LoadAssetAtPath<Texture2D>(TintNoisePath));
            if (first)
            {
                // starting values (tune on the material afterwards): each photo at its real size
                mat.SetVector("_LayerTiles", new Vector4(1f / 2.11f, 1f / 1.3f, 1f / 2.3f, 1f / 3f));
                mat.SetVector("_LayerLight", new Vector4(1.3f, 1.1f, 1.45f, 1.05f));   // tuned by eye in the Meadow
                mat.SetVector("_SlopeRock", new Vector4(0.26f, 0.42f, 0f, 0f));        // only the truly steep spots
                mat.SetFloat("_MacroLuma", MacroLuma(mat, origin));
                log.AppendLine("ground layers: material set up");
            }
            EditorUtility.SetDirty(mat);
            log.AppendLine($"ground layers: {Patches.Length} ground zones and {HumanPaths.Length} paths painted over {LayerAreaSize:0} m");
        }

        // A soft, irregular patch filling a ground zone (rounded, with a noisy edge, not a rectangle).
        static void PaintBlob(Color[] px, Vector2 origin, Vector2 centre, Quaternion rot, Vector2 half, int channel, float edgeNoise, float breakup = 0f)
        {
            float reach = half.magnitude + 2f;
            Quaternion inv = Quaternion.Inverse(rot);
            float radius = Mathf.Min(half.x, half.y) * 0.6f;
            ForPixels(px, origin, centre, reach, (i, w) =>
            {
                Vector3 local = inv * new Vector3(w.x - centre.x, 0f, w.y - centre.y);
                // distance outside a rounded rectangle (negative inside)
                Vector2 q = new Vector2(Mathf.Abs(local.x), Mathf.Abs(local.z)) - (half - Vector2.one * radius);
                float d = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
                d += (Mathf.PerlinNoise(w.x * 0.35f + channel * 17f, w.y * 0.35f) - 0.5f) * 2f * edgeNoise;
                float a = Mathf.Clamp01(0.5f - d / 0.9f);
                // a little noise inside too, so the middle is not one flat colour
                a *= Mathf.Lerp(0.82f, 1f, Mathf.PerlinNoise(w.x * 1.3f + 7f, w.y * 1.3f + channel));
                if (breakup > 0f)
                {
                    float drift = Mathf.PerlinNoise(w.x * 0.9f + 13f, w.y * 0.9f + 5f) * 0.7f + Mathf.PerlinNoise(w.x * 2.7f, w.y * 2.7f + 9f) * 0.3f;
                    a *= Mathf.Lerp(1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.32f, 0.62f, drift)), breakup);
                }
                Accumulate(px, i, channel, a);
            });
        }

        // A worn path along a polyline: full in the middle, fraying at the edges.
        static void PaintPath(Color[] px, Vector2 origin, Vector2[] points, float halfWidth, int channel)
        {
            for (int s = 1; s < points.Length; s++)
            {
                Vector2 a = points[s - 1], b = points[s];
                ForPixels(px, origin, (a + b) * 0.5f, (b - a).magnitude * 0.5f + halfWidth + 1f, (i, w) =>
                {
                    Vector2 ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(w - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                    float d = (a + ab * t - w).magnitude;
                    float wobble = (Mathf.PerlinNoise(w.x * 0.6f, w.y * 0.6f + 31f) - 0.5f) * 0.5f;
                    float alpha = Mathf.Clamp01((halfWidth + wobble - d) / 0.35f);
                    alpha *= Mathf.Lerp(0.7f, 1f, Mathf.PerlinNoise(w.x * 2.1f + 3f, w.y * 2.1f));   // patchy wear
                    Accumulate(px, i, channel, alpha);
                });
            }
        }

        static void ForPixels(Color[] px, Vector2 origin, Vector2 centre, float reach, System.Action<int, Vector2> each)
        {
            float mpp = LayerAreaSize / LayerResolution;
            int x0 = Mathf.Max(1, Mathf.FloorToInt((centre.x - reach - origin.x) / mpp));
            int x1 = Mathf.Min(LayerResolution - 2, Mathf.CeilToInt((centre.x + reach - origin.x) / mpp));
            int y0 = Mathf.Max(1, Mathf.FloorToInt((centre.y - reach - origin.y) / mpp));
            int y1 = Mathf.Min(LayerResolution - 2, Mathf.CeilToInt((centre.y + reach - origin.y) / mpp));
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                each(y * LayerResolution + x, new Vector2(origin.x + (x + 0.5f) * mpp, origin.y + (y + 0.5f) * mpp));
        }

        static void Accumulate(Color[] px, int i, int channel, float a)
        {
            var c = px[i];
            c[channel] = Mathf.Max(c[channel], a);
            px[i] = c;
        }

        // Soft noise that tiles: large sunny and cool patches across the grass.
        static byte[] MakeTintNoise(int n)
        {
            var px = new Color[n * n];
            float Tileable(float x, float y, float period)
            {
                // blend four shifted samples so the edges meet
                float fx = x / n, fy = y / n;
                float s00 = Mathf.PerlinNoise(x / n * period, y / n * period);
                float s10 = Mathf.PerlinNoise((x - n) / n * period + 50f, y / n * period);
                float s01 = Mathf.PerlinNoise(x / n * period, (y - n) / n * period + 50f);
                float s11 = Mathf.PerlinNoise((x - n) / n * period + 50f, (y - n) / n * period + 50f);
                return Mathf.Lerp(Mathf.Lerp(s00, s10, fx), Mathf.Lerp(s01, s11, fx), fy);
            }
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float v = Tileable(x, y, 3f) * 0.7f + Tileable(x, y, 7f) * 0.3f;
                v = Mathf.Clamp01((v - 0.5f) * 2.2f + 0.5f);
                px[y * n + x] = new Color(v, v, v, 1f);
            }
            return Png(px, n);
        }

        // Average brightness of the painted grass over the hunting area: photo layers are lit to match it.
        static float MacroLuma(Material mat, Vector2 areaOrigin)
        {
            var macro = mat.GetTexture("_MacroMap") as Texture2D;
            Vector4 area = mat.GetVector("_MacroArea");
            if (!macro || area.z <= 0f) return 0.3f;
            var rt = RenderTexture.GetTemporary(256, 256, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(macro, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var read = new Texture2D(256, 256, TextureFormat.RGBA32, false, true);
            read.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            double sum = 0; int count = 0;
            for (int y = 0; y < 256; y++)
            for (int x = 0; x < 256; x++)
            {
                // world position of this macro texel; only the hunting area counts
                float wx = area.x + (x + 0.5f) / 256f / area.z, wz = area.y + (y + 0.5f) / 256f / area.w;
                if (wx < areaOrigin.x || wz < areaOrigin.y || wx > areaOrigin.x + LayerAreaSize || wz > areaOrigin.y + LayerAreaSize) continue;
                // the blit decoded the sRGB map into this linear target: values are already what the shader sees
                var c = read.GetPixel(x, y);
                sum += c.r * 0.3 + c.g * 0.59 + c.b * 0.11;
                count++;
            }
            Object.DestroyImmediate(read);
            return count > 0 ? (float)(sum / count) : 0.3f;
        }

        static Texture2D ImportGroundTexture(string path)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti)
            {
                ti.sRGBTexture = true;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.maxTextureSize = 1024;
                ti.anisoLevel = 4;
                ti.mipmapEnabled = true;
                ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                { name = "Android", overridden = true, maxTextureSize = 1024, format = TextureImporterFormat.ASTC_6x6, compressionQuality = 50 });
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void ImportMaskTexture(string path, bool repeat)
        {
            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.sRGBTexture = false;   // data, not colour
            ti.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ti.mipmapEnabled = true;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            { name = "Android", overridden = true, maxTextureSize = 1024, format = TextureImporterFormat.ASTC_4x4, compressionQuality = 50 });
            ti.SaveAndReimport();
        }
    }
}
