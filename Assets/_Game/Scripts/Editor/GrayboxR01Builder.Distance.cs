using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Meadow look, step D: distance and mood (style sheet: "layered ridges fading to blue, snowy peaks on the skyline,
    /// mist in the low valleys"; and no haze over the hunting area).
    ///
    /// A far mountain range is generated as a heightfield (foothills from 1.3 km, forested middle ranges, and a great
    /// snowy range 13 to 44 km out, highest to the west and north-west where the morning sun lights its faces) and
    /// rendered once, by a column ray-caster, into a 360 degree panorama: sun and cool sky light, snow by height and
    /// slope, forest and rock, haze that turns each farther layer bluer until it meets the sky's horizon colour, mist
    /// lying in the low valleys, and cloud on the high slopes. In the game it is one eye-relative band behind the
    /// environment's own painted mountains (the SV_CloudBand shader they use): one texture read per pixel, no
    /// geometry. The panorama is cached: it is only rendered again when FarRangeVersion changes.
    /// </summary>
    public static partial class GrayboxR01Builder
    {
        const string DistanceDir = "Assets/_Game/Art/Environment/Distance";
        const string FarRangeVersion = "far-range v8";
        const int FarW = 4096, FarH = 768;                    // per half panorama (180 degrees)
        static readonly float FarTanMin = Mathf.Tan(-3f * Mathf.Deg2Rad), FarTanMax = Mathf.Tan(28f * Mathf.Deg2Rad);
        const float FarEyeH = 20f;                             // the meadow's height under the eye
        const int FarQueue = 2955;                             // just before the painted mountains (2960): behind them

        static void BuildDistance(StringBuilder log)
        {
            var backdrop = GameObject.Find("AlpineEnv/Backdrop");
            var refBand = backdrop ? backdrop.transform.Find("Mountains0") : null;
            var refMat = refBand ? refBand.GetComponent<Renderer>().sharedMaterial : null;
            if (!refMat) { log.AppendLine("distance: no painted mountain backdrop found; skipped"); return; }
            EnsureFolder(DistanceDir);

            // the sky dome's colour just above the horizon: the far range fades into exactly this
            Color haze = SkyHorizonColour();
            var day = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/_Game/Art/Environment/AlpineEnv/Lighting/AL_Day.asset");
            Vector3 euler = day ? new SerializedObject(day).FindProperty("lightRotation").vector3Value : new Vector3(52f, 238f, 0f);
            Vector3 toSun = -(Quaternion.Euler(euler) * Vector3.forward).normalized;

            string[] texPaths = { DistanceDir + "/T_FarRange0.png", DistanceDir + "/T_FarRange1.png" };
            bool cached = true;
            foreach (var p in texPaths) { var imp = AssetImporter.GetAtPath(p); if (!File.Exists(p) || imp == null || imp.userData != FarRangeVersion) cached = false; }
            if (!cached)
            {
                var t0 = System.DateTime.Now;
                var halves = RenderFarRange(toSun, haze, log);
                for (int i = 0; i < 2; i++)
                {
                    File.WriteAllBytes(texPaths[i], PngRect(halves[i], FarW, FarH));
                    AssetDatabase.ImportAsset(texPaths[i]);
                    var ti = (TextureImporter)AssetImporter.GetAtPath(texPaths[i]);
                    ti.textureType = TextureImporterType.Default;
                    ti.sRGBTexture = true;
                    ti.alphaSource = TextureImporterAlphaSource.FromInput;
                    ti.alphaIsTransparency = true;
                    ti.mipmapEnabled = true;
                    ti.npotScale = TextureImporterNPOTScale.None;   // keep 768 rows as rendered
                    ti.wrapMode = TextureWrapMode.Clamp;
                    ti.maxTextureSize = FarW;
                    ti.textureCompression = TextureImporterCompression.Compressed;
                    ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                    { name = "Android", overridden = true, maxTextureSize = FarW, format = TextureImporterFormat.ASTC_6x6, compressionQuality = 50 });
                    ti.userData = FarRangeVersion;
                    ti.SaveAndReimport();
                }
                log.AppendLine($"distance: far range rendered in {(System.DateTime.Now - t0).TotalSeconds:F0} s");
            }

            // two half rings, eye-relative (the band shader adds the camera position), heights in tan(elevation)
            for (int i = 0; i < 2; i++)
            {
                var mesh = FarRingMesh(i);
                string matPath = $"{DistanceDir}/R01_FarRange{i}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (!mat) { mat = new Material(refMat) { name = $"R01_FarRange{i}" }; AssetDatabase.CreateAsset(mat, matPath); }
                mat.shader = refMat.shader;
                mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(texPaths[i]));
                mat.SetColor("_Tint", Color.white);
                mat.SetFloat("_Haze", 0f);
                mat.SetFloat("_DepthK", refMat.GetFloat("_DepthK"));
                mat.renderQueue = FarQueue;
                EditorUtility.SetDirty(mat);

                var t = backdrop.transform.Find($"FarRange{i}");
                var go = t ? t.gameObject : new GameObject($"FarRange{i}");
                go.transform.SetParent(backdrop.transform, false);
                go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
                if (!go.TryGetComponent<MeshFilter>(out var mf)) mf = go.AddComponent<MeshFilter>();
                if (!go.TryGetComponent<MeshRenderer>(out var mr)) mr = go.AddComponent<MeshRenderer>();
                mf.sharedMesh = mesh;
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
            log.AppendLine($"distance: far range band behind the painted mountains (haze {haze.r:F2}, {haze.g:F2}, {haze.b:F2})");
        }

        static Color SkyHorizonColour()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes("Assets/_Game/Art/Environment/AlpineEnv/Baked/AL_Sky.png"));
            Color s = Color.black; int n = 0;
            for (int y = 0; y < tex.height * 0.12f; y += 4)           // 0 to about 10 degrees on the dome
            for (int x = 0; x < tex.width; x += 8) { s += tex.GetPixel(x, y); n++; }
            Object.DestroyImmediate(tex);
            s /= Mathf.Max(1, n); s.a = 1f;
            return s;
        }

        static Mesh FarRingMesh(int half)
        {
            string path = $"{DistanceDir}/R01_FarRing{half}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created = !mesh;
            if (created) mesh = new Mesh { name = $"R01_FarRing{half}" };
            mesh.Clear();
            const int Seg = 64; const float R = 250f;
            var verts = new Vector3[(Seg + 1) * 2]; var uvs = new Vector2[verts.Length]; var cols = new Color[verts.Length];
            var tris = new int[Seg * 6];
            for (int k = 0; k <= Seg; k++)
            {
                float a = (half + k / (float)Seg) * Mathf.PI;         // azimuth from +z toward +x
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * R;
                verts[k * 2] = dir + Vector3.up * FarTanMin * R;
                verts[k * 2 + 1] = dir + Vector3.up * FarTanMax * R;
                uvs[k * 2] = new Vector2(k / (float)Seg, 0f); uvs[k * 2 + 1] = new Vector2(k / (float)Seg, 1f);
                cols[k * 2] = cols[k * 2 + 1] = Color.white;
                if (k == Seg) break;
                int b = k * 2, t = k * 6;
                tris[t] = b; tris[t + 1] = b + 1; tris[t + 2] = b + 3; tris[t + 3] = b; tris[t + 4] = b + 3; tris[t + 5] = b + 2;
            }
            mesh.vertices = verts; mesh.uv = uvs; mesh.colors = cols; mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);   // eye-relative: never culled
            if (created) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // ---------- the far range ----------

        static float Smooth01(float a, float b, float x) => SmoothEdge(a, b, x);

        static float Fbm(float x, float z, int oct, float seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f, f = 1f;
            for (int i = 0; i < oct; i++) { sum += Mathf.PerlinNoise(x * f + seed + i * 17.3f, z * f - seed + i * 9.1f) * amp; norm += amp; amp *= 0.5f; f *= 2.03f; }
            return sum / norm;
        }

        // ridged multifractal: sharp crests and branching valleys, about 0..1
        static float Ridged(float x, float z, int oct, float seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f, f = 1f, w = 1f;
            for (int i = 0; i < oct; i++)
            {
                float n = 1f - Mathf.Abs(Mathf.PerlinNoise(x * f + seed + i * 31.7f, z * f + seed - i * 13.9f) * 2f - 1f);
                n *= n; n *= w; w = Mathf.Clamp01(n * 1.8f);
                sum += n * amp; norm += amp; amp *= 0.5f; f *= 2.07f;
            }
            return Mathf.Min(sum / norm * 1.35f, 1.25f);
        }

        static float FarHeight(float x, float z)
        {
            float r = Mathf.Sqrt(x * x + z * z);
            float az = Mathf.Atan2(x, z) * Mathf.Rad2Deg;
            float foot = Smooth01(1300f, 2600f, r) * (30f + 120f * Fbm(x / 1800f, z / 1800f, 5, 3.1f));   // low: under the painted hills
            // forested ridges that rise with distance, so each one shows above the one in front of it
            float mid = Smooth01(3000f, 7000f, r) * Ridged(x / 4200f, z / 4200f, 6, 11.7f) * (250f + r * 0.07f) * (0.5f + 0.5f * Fbm(x / 9000f, z / 9000f, 3, 7.3f));
            float da = Mathf.DeltaAngle(az, -40f);                  // the great range is highest to the north-west
            float azW = 0.15f + 0.85f * Mathf.Exp(-(da * da) / (80f * 80f));
            float v = Ridged(x / 14000f, z / 14000f, 7, 23.9f) * azW;              // broad massifs
            float great = Smooth01(9000f, 15000f, r) * (1f - Smooth01(30000f, 38000f, r))
                          * 7500f * (1f - Mathf.Exp(-1.6f * Mathf.Pow(v, 1.6f)));   // eases off toward 7 km: no needles
            return foot + mid + great;
        }

        static Color[][] RenderFarRange(Vector3 toSun, Color hazeSrgb, StringBuilder log)
        {
            // heightfield grid
            const int N = 3072; const float Extent = 46000f;
            float cell = 2f * Extent / (N - 1);
            var hgt = new float[N * N];
            float maxH = 0f;
            for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                float h = FarHeight(-Extent + i * cell, -Extent + j * cell);
                hgt[j * N + i] = h; if (h > maxH) maxH = h;
            }
            float H(float x, float z)
            {
                float fx = Mathf.Clamp((x + Extent) / cell, 0f, N - 1.001f), fz = Mathf.Clamp((z + Extent) / cell, 0f, N - 1.001f);
                int ix = (int)fx, iz = (int)fz; float tx = fx - ix, tz = fz - iz;
                int k = iz * N + ix;
                return Mathf.Lerp(Mathf.Lerp(hgt[k], hgt[k + 1], tx), Mathf.Lerp(hgt[k + N], hgt[k + N + 1], tx), tz);
            }

            Vector3 Lin(Color c) => new Vector3(Mathf.GammaToLinearSpace(c.r), Mathf.GammaToLinearSpace(c.g), Mathf.GammaToLinearSpace(c.b));
            Vector3 haze = Lin(hazeSrgb);
            // the backdrop is drawn unlit and then brightened by the game's post-processing like everything else: lit snow
            // should land about as bright as a sunlit cloud on the sky dome, not glare
            Vector3 sun = new Vector3(1f, 0.94f, 0.83f) * 0.72f;
            Vector3 skyAmb = new Vector3(0.30f, 0.37f, 0.52f);       // cool blue in shade (style sheet)
            Vector3 snowAlb = Lin(new Color(0.93f, 0.94f, 0.96f)), rockAlb = Lin(new Color(0.5f, 0.5f, 0.5f)), rockDark = Lin(new Color(0.35f, 0.35f, 0.37f));
            Vector3 forestAlb = Lin(new Color(0.16f, 0.24f, 0.13f)), meadowAlb = Lin(new Color(0.40f, 0.47f, 0.24f)), alpineAlb = Lin(new Color(0.42f, 0.42f, 0.30f));
            Vector3 mistCol = Lin(new Color(0.92f, 0.94f, 0.97f)), cloudCol = Lin(new Color(0.95f, 0.96f, 0.98f));
            Vector3 Mul(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);

            Vector3 Shade(float x, float z, float h, float d)
            {
                float e = Mathf.Max(cell, d * 0.0015f);
                float hx = H(x + e, z) - H(x - e, z), hz = H(x, z + e) - H(x, z - e);
                // fine relief the heightfield is too coarse to hold: gullies and ribs, as a bump on the normal
                const float B = 60f;
                float bx = Mathf.PerlinNoise((x + B) / 260f, z / 260f) - Mathf.PerlinNoise((x - B) / 260f, z / 260f)
                         + 0.5f * (Mathf.PerlinNoise((x + B) / 95f + 3f, z / 95f) - Mathf.PerlinNoise((x - B) / 95f + 3f, z / 95f));
                float bz = Mathf.PerlinNoise(x / 260f, (z + B) / 260f) - Mathf.PerlinNoise(x / 260f, (z - B) / 260f)
                         + 0.5f * (Mathf.PerlinNoise(x / 95f + 3f, (z + B) / 95f) - Mathf.PerlinNoise(x / 95f + 3f, (z - B) / 95f));
                float relief = Smooth01(800f, 2500f, h) * 2.2f;   // the high mountains are craggy, the forested ridges smooth
                var n = new Vector3(-hx / (2f * e) - bx * relief, 1f, -hz / (2f * e) - bz * relief).normalized;
                float slope = Mathf.Acos(n.y) * Mathf.Rad2Deg;
                float detail = Mathf.PerlinNoise(x / 310f + 5.3f, z / 310f - 2.1f);
                // albedo: forest low, alpine meadow above the tree line, rock on steep ground and high up, snow on top
                float treeLine = 2300f + 300f * detail;
                Vector3 low = Vector3.Lerp(forestAlb, meadowAlb, Mathf.Clamp01((Mathf.PerlinNoise(x / 900f, z / 900f) - 0.55f) * 3f));
                Vector3 alb = Vector3.Lerp(low, alpineAlb, Smooth01(treeLine - 150f, treeLine + 150f, h));
                float steep = Mathf.Lerp(44f, 34f, Smooth01(1500f, 3000f, h));   // forest clings to steeper ground low down
                float rocky = Mathf.Max(Smooth01(steep, steep + 10f, slope), Smooth01(3000f, 3500f, h) * 0.7f);
                alb = Vector3.Lerp(alb, Vector3.Lerp(rockAlb, rockDark, detail), rocky);
                float snowLine = 3000f + 500f * (Fbm(x / 2500f, z / 2500f, 3, 41f) - 0.5f) * 2f;
                float streak = Mathf.PerlinNoise(x / 140f + 9f, z / 140f - 4f) - 0.5f;   // snow lies in gullies, rock shows on ribs
                float snow = Smooth01(snowLine - 120f, snowLine + 120f, h) * (1f - Smooth01(52f, 62f, slope + streak * 22f));
                snow = Mathf.Max(snow, Smooth01(snowLine + 700f, snowLine + 1100f, h) * (1f - Smooth01(62f, 72f, slope)));   // high faces hold snow longer
                alb = Vector3.Lerp(alb, snowAlb, snow);
                // light: sun and a cool sky
                float ndl = Mathf.Max(0f, Vector3.Dot(n, toSun));
                Vector3 light = sun * ndl + skyAmb * (0.55f + 0.45f * n.y);
                return Mul(alb, light);
            }

            // distance haze: each farther layer bluer (red fades first), thinner high up, ending in the sky colour
            Vector3 Air(Vector3 c, float d, float h)
            {
                float tau = d / 12000f * (0.3f + 0.7f * Mathf.Exp(-Mathf.Max(0f, h) / 2000f));   // thick low down, thin and clear up high
                var tr = new Vector3(Mathf.Exp(-tau * 1.35f), Mathf.Exp(-tau), Mathf.Exp(-tau * 0.62f));
                return Mul(c, tr) + Mul(haze, Vector3.one - tr);
            }

            var halves = new[] { new Color[FarW * FarH], new Color[FarW * FarH] };
            var clear = new Color(hazeSrgb.r, hazeSrgb.g, hazeSrgb.b, 0f);   // transparent, with the sky's colour for clean mipmaps
            for (int i = 0; i < FarW * FarH; i++) { halves[0][i] = clear; halves[1][i] = clear; }
            float rowsPerTan = FarH / (FarTanMax - FarTanMin);
            const float EarthR2 = 2f * 6371000f;

            for (int c = 0; c < FarW * 2; c++)
            {
                var px = halves[c / FarW]; int col = c % FarW;
                float a = (c + 0.5f) / (FarW * 2f) * Mathf.PI * 2f;
                float dx = Mathf.Sin(a), dz = Mathf.Cos(a);
                float top = 0f;                       // rows below this are filled
                bool first = true, prevValid = false; Vector3 prevCol = Vector3.zero; float prevRow = 0f, topRowF = 0f; Vector3 topCol = Vector3.zero;
                for (float d = 1200f; d < 45000f; d += Mathf.Max(5f, d * 0.0018f))
                {
                    float x = dx * d, z = dz * d;
                    float h = H(x, z);
                    float rowF = ((h - FarEyeH - d * d / EarthR2) / d - FarTanMin) * rowsPerTan;
                    if (rowF <= top && !first) { prevValid = false; continue; }   // hidden behind a nearer ridge
                    var cLit = Air(Shade(x, z, h, d), d, h);
                    // mist lying in the low valleys, cloud clinging to the high slopes
                    float mistTop = 230f + 220f * Fbm(x / 4200f, z / 4200f, 3, 57f);
                    float mist = Smooth01(3000f, 7000f, d) * Mathf.Clamp01((mistTop - h) / 160f)
                                 * Mathf.Clamp01(Fbm(x / 2600f, z / 2600f, 4, 63f) * 3f - 1.1f);   // only some valleys, in patches
                    // and a deeper mist along the foot of the great range, as in the style sheet
                    float footMist = Smooth01(8000f, 13000f, d) * Mathf.Clamp01((1400f - h) / 600f)
                                     * Mathf.Clamp01(Fbm(x / 3200f, z / 3200f, 4, 81f) * 2.6f - 0.85f);
                    mist = Mathf.Max(mist, footMist);
                    cLit = Vector3.Lerp(cLit, Air(mistCol, d * 0.6f, h), mist * 0.9f);
                    float band = Mathf.Exp(-Mathf.Pow((h - 2400f) / 650f, 2f));
                    float cloud = band * Mathf.Clamp01(Fbm(x / 2300f, z / 2300f, 4, 71f) * 2.6f - 1.05f) * Smooth01(9000f, 15000f, d);
                    cLit = Vector3.Lerp(cLit, Air(cloudCol, d * 0.7f, h), cloud * 0.8f);

                    if (first) { prevCol = cLit; prevRow = Mathf.Min(rowF, 0f); first = false; }
                    else if (!prevValid) { prevCol = cLit; prevRow = top; }   // a farther range rising behind: no blend with the ridge in front
                    prevValid = true;
                    int r0 = Mathf.Max(0, (int)top), r1 = Mathf.Min(FarH, (int)rowF);
                    for (int r = r0; r < r1; r++)
                    {
                        // the face between the previous sample and this one, interpolated row by row
                        float f = rowF > prevRow ? Mathf.Clamp01((r + 0.5f - prevRow) / (rowF - prevRow)) : 1f;
                        var v = Vector3.Lerp(prevCol, cLit, f);
                        px[r * FarW + col] = new Color(Mathf.LinearToGammaSpace(v.x), Mathf.LinearToGammaSpace(v.y), Mathf.LinearToGammaSpace(v.z), 1f);
                    }
                    if (rowF > top) { top = rowF; topRowF = rowF; topCol = cLit; }
                    prevCol = cLit; prevRow = rowF;
                    if (top >= FarH) break;
                }
                // soft silhouette: the partly covered top row
                int tr = (int)topRowF;
                if (tr >= 0 && tr < FarH)
                    px[tr * FarW + col] = new Color(Mathf.LinearToGammaSpace(topCol.x), Mathf.LinearToGammaSpace(topCol.y), Mathf.LinearToGammaSpace(topCol.z), topRowF - tr);
            }
            log.AppendLine($"distance: heightfield {N} px over {2 * Extent / 1000f:F0} km, highest peak {maxH:F0} m");
            return halves;
        }
    }
}
