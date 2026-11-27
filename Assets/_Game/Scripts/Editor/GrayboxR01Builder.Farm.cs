using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Editor
{
    /// <summary>
    /// The farm at the edge of the Meadow (owner, 2026-11-27: "it lacks a farm look"; chose a Himalayan hill farm,
    /// each piece from the best source). This part is the farm kit: natural timber pieces generated in code with
    /// Poly Haven CC0 photo textures (bark logs, split rails, planks, end grain, hay), each object one combined mesh
    /// so a phone draws a whole fence in a few draw calls. Every piece leans, sags and varies a little, from a
    /// fixed seed, so rebuilds give the same farm. Meshes are saved as assets and rebuilt in place.
    /// </summary>
    public static partial class GrayboxR01Builder
    {
        const string FarmDir = "Assets/_Game/Art/Environment/Farm";
        const string FarmTexDir = FarmDir + "/Textures";
        const string FarmMatDir = FarmDir + "/Materials";
        const string FarmMeshDir = FarmDir + "/Generated";
        const string FarmPrefabDir = "Assets/_Game/Prefabs/Environment/Farm";

        sealed class FarmMats
        {
            public Material bark, wood, endGrain, roof, hay, water;
        }

        static FarmMats farmMats;

        // ---------- materials ----------

        static FarmMats EnsureFarmMaterials(StringBuilder log)
        {
            EnsureFolder(FarmMatDir);
            EnsureFolder(FarmMeshDir);
            Texture2D Tex(string file, bool normal)
            {
                string path = $"{FarmTexDir}/{file}";
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                if (imp != null)
                {
                    bool changed = false;
                    if (normal && imp.textureType != TextureImporterType.NormalMap) { imp.textureType = TextureImporterType.NormalMap; changed = true; }
                    if (imp.maxTextureSize != 1024) { imp.maxTextureSize = 1024; changed = true; }
                    var android = imp.GetPlatformTextureSettings("Android");
                    if (!android.overridden || android.format != TextureImporterFormat.ASTC_6x6)
                    {
                        android.overridden = true; android.format = TextureImporterFormat.ASTC_6x6; android.maxTextureSize = 1024;
                        imp.SetPlatformTextureSettings(android); changed = true;
                    }
                    if (changed) imp.SaveAndReimport();
                }
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            Material Lit(string name, string id, Color tint, float smooth)
            {
                string path = $"{FarmMatDir}/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name }; AssetDatabase.CreateAsset(m, path); }
                if (id != null)
                {
                    m.SetTexture("_BaseMap", Tex($"PH_{id}_diff_1k.jpg", false));
                    m.SetTexture("_BumpMap", Tex($"PH_{id}_nor_gl_1k.jpg", true));
                    m.EnableKeyword("_NORMALMAP");
                }
                m.SetColor("_BaseColor", tint);
                m.SetFloat("_Smoothness", smooth);
                m.SetFloat("_EnvironmentReflections", 0f);
                m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                m.enableInstancing = true;
                EditorUtility.SetDirty(m);
                return m;
            }
            var mats = new FarmMats
            {
                bark = Lit("M_Farm_Bark", "pine_bark", Color.white, 0.08f),
                wood = Lit("M_Farm_RoughWood", "rough_wood", Color.white, 0.1f),
                roof = Lit("M_Farm_RoofPlanks", "rough_wood", new Color(0.86f, 0.83f, 0.8f), 0.1f),   // weathered grey boards, like the rails (roof_planks was too orange)
                hay = Lit("M_Farm_Hay", "reed_roof_03", Color.white, 0.05f),
                endGrain = Lit("M_Farm_EndGrain", null, Color.white, 0.1f),
                water = Lit("M_Farm_Water", null, new Color(0.11f, 0.14f, 0.12f), 0.85f),
            };
            // end grain: growth rings and radial cracks, in the rough wood's tones
            string ringPath = $"{FarmTexDir}/T_Farm_EndGrain.png";
            if (!File.Exists(ringPath))
            {
                const int N = 256;
                var px = new Color[N * N];
                var rng = new System.Random(11);
                float[] cracks = { (float)rng.NextDouble() * 6.28f, (float)rng.NextDouble() * 6.28f, (float)rng.NextDouble() * 6.28f };
                for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v), a = Mathf.Atan2(v, u);
                    float wob = r + 0.02f * Mathf.Sin(a * 5f) + 0.015f * Mathf.Sin(a * 11f + 1.3f);
                    float rings = 0.5f + 0.5f * Mathf.Sin(wob * 70f);
                    var c = Color.Lerp(new Color(0.62f, 0.5f, 0.36f), new Color(0.47f, 0.36f, 0.25f), rings * 0.7f);
                    c = Color.Lerp(c, new Color(0.38f, 0.3f, 0.22f), SmoothEdge(0.82f, 1f, r));          // weathered rim
                    foreach (float ca in cracks)
                        if (Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, ca * Mathf.Rad2Deg)) < 2.5f * (1.2f - r) && r > 0.15f && r < 0.9f)
                            c *= 0.55f;
                    px[y * N + x] = c;
                }
                var t = new Texture2D(N, N, TextureFormat.RGB24, false);
                t.SetPixels(px); t.Apply();
                File.WriteAllBytes(ringPath, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(ringPath);
            }
            mats.endGrain.SetTexture("_BaseMap", Tex("T_Farm_EndGrain.png", false));
            AssetDatabase.SaveAssets();
            log.AppendLine("farm: materials (bark, rough wood, roof planks, hay, end grain, water)");
            return mats;
        }

        // ---------- mesh building ----------

        /// <summary>Collects geometry for one farm object: submesh 0 bark, 1 sawn wood, 2 end grain, 3 roof, 4 hay, 5 water.</summary>
        sealed class FarmMesh
        {
            public const int Bark = 0, Wood = 1, End = 2, Roof = 3, Hay = 4, Water = 5, Count = 6;
            readonly List<Vector3> v = new List<Vector3>();
            readonly List<Vector3> n = new List<Vector3>();
            readonly List<Vector2> uv = new List<Vector2>();
            readonly List<int>[] tris = new List<int>[Count];
            public FarmMesh() { for (int i = 0; i < Count; i++) tris[i] = new List<int>(); }

            int Vert(Vector3 p, Vector3 normal, Vector2 t) { v.Add(p); n.Add(normal); uv.Add(t); return v.Count - 1; }
            void Tri(int sub, int a, int b, int c) { tris[sub].Add(a); tris[sub].Add(b); tris[sub].Add(c); }

            /// <summary>A round log or post from a to b (bark sides, end grain caps). UVs in metres of texture.</summary>
            public void Log(Vector3 a, Vector3 b, float ra, float rb, int sides, System.Random rng, float tile = 1.2f, bool capA = true, bool capB = true, int side = Bark)
            {
                Vector3 axis = (b - a).normalized;
                Vector3 x = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.95f ? Vector3.up : Vector3.right).normalized;
                Vector3 y = Vector3.Cross(axis, x);
                float len = Vector3.Distance(a, b);
                float twist = (float)rng.NextDouble() * 6.28f;
                const int rings = 4;
                int start = v.Count;
                var radial = new float[sides + 1];
                for (int s = 0; s <= sides; s++) radial[s] = 1f + ((float)rng.NextDouble() - 0.5f) * 0.12f;
                radial[sides] = radial[0];
                for (int r = 0; r <= rings; r++)
                {
                    float t = r / (float)rings;
                    float rad = Mathf.Lerp(ra, rb, t) * (1f + ((float)rng.NextDouble() - 0.5f) * 0.06f);
                    Vector3 c = Vector3.Lerp(a, b, t);
                    for (int s = 0; s <= sides; s++)
                    {
                        float ang = twist + s / (float)sides * Mathf.PI * 2f;
                        Vector3 dir = x * Mathf.Cos(ang) + y * Mathf.Sin(ang);
                        Vector3 p = c + dir * rad * radial[s];
                        Vert(p, dir, new Vector2(s / (float)sides * 2f * Mathf.PI * Mathf.Max(ra, rb) / tile, t * len / tile));
                    }
                }
                for (int r = 0; r < rings; r++)
                for (int s = 0; s < sides; s++)
                {
                    int i0 = start + r * (sides + 1) + s, i1 = i0 + 1, j0 = i0 + sides + 1, j1 = j0 + 1;
                    Tri(side, i0, i1, j0); Tri(side, i1, j1, j0);
                }
                void Cap(Vector3 centre, float rad, Vector3 normal, int ring, bool flip)
                {
                    int c0 = Vert(centre, normal, new Vector2(0.5f, 0.5f));
                    int first = v.Count;
                    for (int s = 0; s < sides; s++)
                    {
                        Vector3 p = v[start + ring * (sides + 1) + s];
                        Vector3 d = (p - centre) / Mathf.Max(0.001f, rad);
                        Vert(p, normal, new Vector2(0.5f + Vector3.Dot(d, x) * 0.5f, 0.5f + Vector3.Dot(d, y) * 0.5f));
                    }
                    for (int s = 0; s < sides; s++)
                    {
                        int p0 = first + s, p1 = first + (s + 1) % sides;
                        if (flip) Tri(End, c0, p1, p0); else Tri(End, c0, p0, p1);
                    }
                }
                if (capA) Cap(a, ra, -axis, 0, true);
                if (capB) Cap(b, rb, axis, rings, false);
            }

            /// <summary>A box (plank, beam) with sawn-wood UVs in metres; its two smallest faces get end grain.</summary>
            public void Box(Vector3 centre, Quaternion rot, Vector3 size, float tile = 1.2f, int sub = Wood, bool endGrain = true)
            {
                Vector3 h = size * 0.5f;
                int longest = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
                for (int f = 0; f < 6; f++)
                {
                    int axis = f / 2; float sgn = f % 2 == 0 ? 1f : -1f;
                    Vector3 nrm = Vector3.zero; nrm[axis] = sgn;
                    int ua = (axis + 1) % 3, va = (axis + 2) % 3;
                    if (va == longest) { int tmp = ua; ua = va; va = tmp; }   // grain along the long axis
                    int s = endGrain && axis == longest ? End : sub;
                    int i0 = v.Count;
                    for (int k = 0; k < 4; k++)
                    {
                        float du = (k == 1 || k == 2) ? 1f : -1f, dv = k >= 2 ? 1f : -1f;
                        Vector3 p = Vector3.zero; p[axis] = sgn * h[axis]; p[ua] = du * h[ua]; p[va] = dv * h[va];
                        Vector2 t = s == End ? new Vector2(0.5f + du * 0.5f, 0.5f + dv * 0.5f)
                                             : new Vector2(p[ua] / tile, p[va] / tile);
                        Vert(centre + rot * p, rot * nrm, t);
                    }
                    bool outward = Vector3.Dot(Vector3.Cross(v[i0 + 1] - v[i0], v[i0 + 3] - v[i0]), rot * nrm) > 0f;
                    if (outward) { Tri(s, i0, i0 + 3, i0 + 1); Tri(s, i0 + 1, i0 + 3, i0 + 2); }
                    else { Tri(s, i0, i0 + 1, i0 + 3); Tri(s, i0 + 1, i0 + 2, i0 + 3); }
                }
            }

            /// <summary>A split rail: an uneven five-sided timber from a to b, sawn-wood texture, end grain ends.</summary>
            public void Rail(Vector3 a, Vector3 b, float thick, System.Random rng)
            {
                Vector3 axis = (b - a).normalized;
                Vector3 x = Vector3.Cross(axis, Vector3.up).normalized;
                if (x.sqrMagnitude < 0.01f) x = Vector3.right;
                Vector3 y = Vector3.Cross(x, axis);
                float sag = thick * (0.5f + (float)rng.NextDouble());
                var profile = new Vector2[5];
                for (int i = 0; i < 5; i++)
                {
                    float ang = i / 5f * Mathf.PI * 2f + 0.3f;
                    float r = thick * (0.55f + (float)rng.NextDouble() * 0.25f);
                    profile[i] = new Vector2(Mathf.Cos(ang) * r * 1.25f, Mathf.Sin(ang) * r * 0.85f);
                }
                float len = Vector3.Distance(a, b);
                const int segs = 3;
                int start = v.Count;
                for (int sgi = 0; sgi <= segs; sgi++)
                {
                    float t = sgi / (float)segs;
                    Vector3 c = Vector3.Lerp(a, b, t) - y * sag * 4f * t * (1f - t);
                    for (int i = 0; i <= 5; i++)
                    {
                        var p2 = profile[i % 5];
                        Vector3 dir = (x * p2.x + y * p2.y).normalized;
                        Vert(c + x * p2.x + y * p2.y, dir, new Vector2(i * thick * 0.9f / 1.2f, t * len / 1.2f));
                    }
                }
                for (int sgi = 0; sgi < segs; sgi++)
                for (int i = 0; i < 5; i++)
                {
                    int i0 = start + sgi * 6 + i, i1 = i0 + 1, j0 = i0 + 6, j1 = j0 + 1;
                    Tri(Wood, i0, j0, i1); Tri(Wood, i1, j0, j1);
                }
                foreach (int end in new[] { 0, segs })
                {
                    Vector3 c = Vector3.Lerp(a, b, end / (float)segs);
                    Vector3 nrm = end == 0 ? -axis : axis;
                    int c0 = Vert(c, nrm, new Vector2(0.5f, 0.5f)), first = v.Count;
                    for (int i = 0; i < 5; i++) Vert(v[start + end * 6 + i], nrm, new Vector2(0.5f + profile[i].x / thick * 0.4f, 0.5f + profile[i].y / thick * 0.4f));
                    for (int i = 0; i < 5; i++)
                    {
                        int p0 = first + i, p1 = first + (i + 1) % 5;
                        if (end == 0) Tri(End, c0, p0, p1); else Tri(End, c0, p1, p0);
                    }
                }
            }

            /// <summary>A shape turned around a vertical axis (hay stack): profile of (radius, height) from the bottom up.</summary>
            public void Lathe(Vector3 basePos, Vector2[] profile, int sides, System.Random rng, int sub, float tile = 1.5f, float lumpiness = 0.08f)
            {
                int start = v.Count;
                var lump = new float[sides + 1];
                for (int s = 0; s <= sides; s++) lump[s] = 1f + ((float)rng.NextDouble() - 0.5f) * lumpiness * 2f;
                lump[sides] = lump[0];
                float run = 0f;
                for (int r = 0; r < profile.Length; r++)
                {
                    if (r > 0) run += Vector2.Distance(profile[r], profile[r - 1]);
                    for (int s = 0; s <= sides; s++)
                    {
                        float ang = s / (float)sides * Mathf.PI * 2f;
                        Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                        float rad = profile[r].x * lump[s] * (1f + ((float)rng.NextDouble() - 0.5f) * lumpiness);
                        Vert(basePos + dir * rad + Vector3.up * profile[r].y, dir, new Vector2(s / (float)sides * 2f * Mathf.PI * profile[0].x / tile, run / tile));
                    }
                }
                for (int r = 0; r < profile.Length - 1; r++)
                for (int s = 0; s < sides; s++)
                {
                    int i0 = start + r * (sides + 1) + s, i1 = i0 + 1, j0 = i0 + sides + 1, j1 = j0 + 1;
                    Tri(sub, i0, j0, i1); Tri(sub, i1, j0, j1);
                }
            }

            /// <summary>A flat quad (the water in the trough).</summary>
            public void Quad(Vector3 centre, Quaternion rot, Vector2 size, int sub)
            {
                int i0 = v.Count;
                Vector3 up = rot * Vector3.up;
                Vert(centre + rot * new Vector3(-size.x, 0, -size.y) * 0.5f, up, new Vector2(0, 0));
                Vert(centre + rot * new Vector3(size.x, 0, -size.y) * 0.5f, up, new Vector2(1, 0));
                Vert(centre + rot * new Vector3(size.x, 0, size.y) * 0.5f, up, new Vector2(1, 1));
                Vert(centre + rot * new Vector3(-size.x, 0, size.y) * 0.5f, up, new Vector2(0, 1));
                Tri(sub, i0, i0 + 2, i0 + 1); Tri(sub, i0, i0 + 3, i0 + 2);
            }

            /// <summary>Saves (or refreshes in place) the mesh asset and puts it on a new child of parent.</summary>
            public GameObject Build(string assetName, Transform parent, FarmMats mats)
            {
                var used = new List<int>();
                for (int i = 0; i < Count; i++) if (tris[i].Count > 0) used.Add(i);
                var mesh = new Mesh { name = assetName, indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv);
                mesh.subMeshCount = used.Count;
                for (int i = 0; i < used.Count; i++) mesh.SetTriangles(tris[used[i]], i);
                // normals are set per piece: radial on logs and rails (smooth), per face on boxes (crisp)
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                string path = $"{FarmMeshDir}/{assetName}.asset";
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (saved) { EditorUtility.CopySerialized(mesh, saved); saved.name = assetName; Object.DestroyImmediate(mesh); mesh = saved; EditorUtility.SetDirty(saved); }
                else AssetDatabase.CreateAsset(mesh, path);

                var go = new GameObject(assetName);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                var all = new[] { mats.bark, mats.wood, mats.endGrain, mats.roof, mats.hay, mats.water };
                var list = new Material[used.Count];
                for (int i = 0; i < used.Count; i++) list[i] = all[used[i]];
                mr.sharedMaterials = list;
                mr.shadowCastingMode = ShadowCastingMode.On;
                return go;
            }
        }

        // Many meshes rebuilt in one session share the folder: unique, stable asset names per piece.
        static void SolidBox(Transform parent, string name, Vector3 localCentre, Quaternion localRot, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCentre;
            go.transform.localRotation = localRot;
            go.AddComponent<BoxCollider>().size = size;
        }

        // ---------- pieces ----------

        /// <summary>
        /// A split-rail fence along local points (posts are bark logs, rails split timber). Spans listed in
        /// <paramref name="openSpans"/> get no rails. Adds one box collider per railed span.
        /// </summary>
        static void RailFence(FarmMesh m, Transform colliders, Vector3[] points, int rails, float height, int seed, HashSet<int> openSpans = null)
        {
            var rng = new System.Random(seed);
            var tops = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 lean = new Vector3(((float)rng.NextDouble() - 0.5f) * 0.12f, 0f, ((float)rng.NextDouble() - 0.5f) * 0.12f);
                float h = height * (0.95f + (float)rng.NextDouble() * 0.12f);
                tops[i] = points[i] + Vector3.up * h + lean;
                m.Log(points[i] - Vector3.up * 0.15f, tops[i], 0.085f, 0.07f, 7, rng, 1.2f, false, true);
            }
            for (int i = 0; i < points.Length - 1; i++)
            {
                if (openSpans != null && openSpans.Contains(i)) continue;
                for (int r = 0; r < rails; r++)
                {
                    float f = (r + 1f) / (rails + 0.6f);
                    Vector3 a = Vector3.Lerp(points[i], tops[i], f), b = Vector3.Lerp(points[i + 1], tops[i + 1], f);
                    Vector3 dir = (b - a).normalized;
                    a -= dir * 0.12f; b += dir * 0.12f;   // rails run past the posts a little, as nailed timber does
                    a += Vector3.up * ((float)rng.NextDouble() - 0.5f) * 0.05f;
                    b += Vector3.up * ((float)rng.NextDouble() - 0.5f) * 0.05f;
                    m.Rail(a, b, 0.065f, rng);
                }
                if (colliders)
                {
                    Vector3 c = (points[i] + points[i + 1]) * 0.5f + Vector3.up * height * 0.5f;
                    Vector3 d = points[i + 1] - points[i]; d.y = 0f;
                    SolidBox(colliders, "FenceSpan", c, Quaternion.LookRotation(d), new Vector3(0.15f, height, d.magnitude));
                }
            }
        }

        /// <summary>A hay stack built around a pole, as hill farms store hay: lumpy cone, pole sticking out the top.</summary>
        static void HayStack(FarmMesh m, Vector3 basePos, float radius, float height, int seed)
        {
            var rng = new System.Random(seed);
            var profile = new[]
            {
                new Vector2(radius * 0.92f, 0f), new Vector2(radius, height * 0.12f), new Vector2(radius * 0.97f, height * 0.35f),
                new Vector2(radius * 0.8f, height * 0.6f), new Vector2(radius * 0.48f, height * 0.82f), new Vector2(radius * 0.12f, height * 0.97f),
                new Vector2(0.02f, height),
            };
            m.Lathe(basePos, profile, 14, rng, FarmMesh.Hay, 1.4f, 0.09f);
            m.Log(basePos + Vector3.up * (height * 0.6f), basePos + Vector3.up * (height + 0.6f) + new Vector3(0.05f, 0f, -0.03f), 0.05f, 0.035f, 6, rng, 1.2f, false, true);
        }

        /// <summary>Split firewood stacked between two end posts: rows of short logs, end grain facing out.</summary>
        static void WoodPile(FarmMesh m, Vector3 centre, Quaternion rot, float length, float height, int seed)
        {
            var rng = new System.Random(seed);
            const float r = 0.075f, depth = 0.45f;
            int perRow = Mathf.Max(2, Mathf.FloorToInt(length / (r * 2.05f)));
            int rows = Mathf.Max(1, Mathf.FloorToInt(height / (r * 1.8f)));
            for (int row = 0; row < rows; row++)
            for (int i = 0; i < perRow - (row % 2); i++)
            {
                float x = -length * 0.5f + r + i * r * 2.05f + (row % 2) * r;
                float y = r + row * r * 1.75f;
                float jitter = ((float)rng.NextDouble() - 0.5f) * 0.08f;
                Vector3 a = centre + rot * new Vector3(x, y, -depth * 0.5f + jitter), b = centre + rot * new Vector3(x, y, depth * 0.5f + jitter);
                m.Log(a, b, r * (0.8f + (float)rng.NextDouble() * 0.35f), r * (0.8f + (float)rng.NextDouble() * 0.35f), 6, rng);
            }
            foreach (float sx in new[] { -1f, 1f })
                m.Log(centre + rot * new Vector3(sx * (length * 0.5f + 0.06f), -0.2f, 0f), centre + rot * new Vector3(sx * (length * 0.5f + 0.06f), height + 0.15f, 0f), 0.06f, 0.05f, 6, rng);
        }

        /// <summary>A plank water trough on two log feet, with water in it.</summary>
        static void Trough(FarmMesh m, Vector3 centre, Quaternion rot, float length, int seed)
        {
            var rng = new System.Random(seed);
            const float w = 0.5f, h = 0.32f, t = 0.05f, lift = 0.12f;
            m.Box(centre + rot * new Vector3(0, lift + t * 0.5f, 0), rot, new Vector3(w, t, length));                          // floor
            m.Box(centre + rot * new Vector3(w * 0.5f - t * 0.5f, lift + h * 0.5f, 0), rot, new Vector3(t, h, length + 0.06f));     // sides
            m.Box(centre + rot * new Vector3(-w * 0.5f + t * 0.5f, lift + h * 0.5f, 0), rot, new Vector3(t, h, length + 0.06f));
            m.Box(centre + rot * new Vector3(0, lift + h * 0.5f, length * 0.5f - t * 0.5f), rot, new Vector3(w - 2 * t, h, t));      // ends
            m.Box(centre + rot * new Vector3(0, lift + h * 0.5f, -length * 0.5f + t * 0.5f), rot, new Vector3(w - 2 * t, h, t));
            m.Quad(centre + rot * new Vector3(0, lift + h * 0.78f, 0), rot, new Vector2(w - 2 * t, length - 2 * t), FarmMesh.Water);
            foreach (float z in new[] { -length * 0.32f, length * 0.32f })
                m.Log(centre + rot * new Vector3(-w * 0.62f, lift * 0.5f, z), centre + rot * new Vector3(w * 0.62f, lift * 0.5f, z), 0.07f, 0.065f, 6, rng);
        }

        // ---------- the goat pen ----------

        /// <summary>
        /// The goat pen of the first hunt, in natural timber: bark posts, three split rails a side (goats climb), the
        /// west side torn open by the wolf (one rail on the ground, one hanging from the post). Same footprint and
        /// broken side as before, so the attack clues stay where they were. A water trough and a hay stack beside it.
        /// </summary>
        static void BuildGoatPenTimber(Transform pen, FarmMats mats, StringBuilder log)
        {
            float h = PenHalf;
            var m = new FarmMesh();
            var col = new GameObject("Colliders").transform;
            col.SetParent(pen, false);
            const float height = 1.15f;
            // north, east and south sides, then the intact northern half of the west side
            RailFence(m, col, new[] { new Vector3(-h, 0, h), new Vector3(0, 0, h + 0.04f), new Vector3(h, 0, h) }, 3, height, 101);
            RailFence(m, col, new[] { new Vector3(h, 0, h), new Vector3(h + 0.05f, 0, 0), new Vector3(h, 0, -h) }, 3, height, 102);
            RailFence(m, col, new[] { new Vector3(h, 0, -h), new Vector3(0, 0, -h - 0.04f), new Vector3(-h, 0, -h) }, 3, height, 103);
            RailFence(m, col, new[] { new Vector3(-h, 0, h), new Vector3(-h, 0, 0) }, 3, height, 104);
            // the torn west half: the corner post stands, one rail on the ground, one hanging from the middle post
            var rng = new System.Random(105);
            m.Rail(new Vector3(-h - 0.9f, 0.06f, -1.7f), new Vector3(-h - 0.2f, 0.07f, -0.15f), 0.065f, rng);
            m.Rail(new Vector3(-h - 0.05f, 0.78f, -0.05f), new Vector3(-h - 0.35f, 0.04f, -1.25f), 0.065f, rng);
            // a gate on the east side is not needed: goats are let out over the low south rail by the farmer
            m.Build("Farm_GoatPen", pen, mats);

            var extras = new FarmMesh();
            Trough(extras, new Vector3(h + 0.9f, 0f, 0.6f), Quaternion.identity, 1.6f, 106);
            HayStack(extras, new Vector3(h + 1.6f, 0f, -1.4f), 0.75f, 1.6f, 107);
            extras.Build("Farm_GoatPenExtras", pen, mats);
            SolidBox(col, "Trough", new Vector3(h + 0.9f, 0.25f, 0.6f), Quaternion.identity, new Vector3(0.55f, 0.5f, 1.7f));
            SolidBox(col, "Hay", new Vector3(h + 1.6f, 0.6f, -1.4f), Quaternion.identity, new Vector3(1.3f, 1.2f, 1.3f));
            log.AppendLine("farm: goat pen rebuilt in timber (split rails, bark posts, torn west side), trough and hay stack");
        }

        // ---------- the farmstead plan (spawn-relative: x east, z north) ----------
        // East of the camp, away from the wolf's trail (which leaves the camp south-west, then north-west).

        static readonly Vector2 HouseAt = new Vector2(25f, 15f);
        const float HouseYaw = 70f;                       // the door (the gabled side) faces back toward the camp
        static readonly Vector2 ShelterAt = new Vector2(14f, 9.6f);
        const float ShelterYaw = -100f;                   // open side toward the pen
        static readonly Vector2[] PaddockFence =
            { new Vector2(13.5f, -1.5f), new Vector2(19f, -1f), new Vector2(25f, -1.6f), new Vector2(30.5f, -2.2f),
              new Vector2(31f, -8f), new Vector2(31.3f, -14f), new Vector2(25f, -14.4f), new Vector2(19f, -14f),
              new Vector2(14f, -13.4f), new Vector2(13.6f, -9f), new Vector2(13.4f, -5.5f), new Vector2(13.5f, -1.5f) };
        const int PaddockGateSpan = 9;                    // between (13.6, -9) and (13.4, -5.5): faces the camp's path
        static readonly Vector2 FieldCentre = new Vector2(23f, 25.5f);
        static readonly Vector2 FieldHalf = new Vector2(6.5f, 3.3f);
        static readonly (Vector2 a, Vector2 b)[] StoneWalls =
            { (new Vector2(15.5f, 21.4f), new Vector2(30.5f, 21.4f)), (new Vector2(30.6f, 21.4f), new Vector2(30.6f, 30f)) };
        static readonly (Vector2 at, float radius, float height)[] HayStacks =
            { (new Vector2(29.5f, 8.5f), 0.85f, 1.9f), (new Vector2(31.8f, 10.6f), 0.75f, 1.7f), (new Vector2(18.2f, 5.6f), 0.7f, 1.5f) };
        static readonly Vector2 WoodPileAt = new Vector2(21.3f, 11.6f);
        const float WoodPileYaw = 70f;

        /// <summary>Areas the farm occupies: scatter is kept out of them (all of it, or only trees, bushes and rocks).</summary>
        static readonly (Vector2 centre, Vector2 half, float yaw, bool keepGrass)[] FarmAreas =
        {
            (HouseAt, new Vector2(5f, 4.6f), HouseYaw, false),
            (ShelterAt, new Vector2(2.8f, 2.4f), ShelterYaw, false),
            (FieldCentre, FieldHalf + new Vector2(0.5f, 0.5f), 0f, false),
            (new Vector2(23f, 21.4f), new Vector2(7.8f, 0.9f), 0f, false),
            (new Vector2(30.6f, 25.7f), new Vector2(0.9f, 4.7f), 0f, false),
            (new Vector2(22.4f, -7.8f), new Vector2(9.2f, 6.8f), 0f, true),   // the paddock: grass stays, no trees or rocks
            (new Vector2(30.6f, 9.5f), new Vector2(2.4f, 2.2f), 0f, false),   // hay stacks
            (new Vector2(18.2f, 5.6f), new Vector2(1.1f, 1.1f), 0f, false),
            (WoodPileAt, new Vector2(1.6f, 0.9f), WoodPileYaw, false),
        };

        /// <summary>True when a spawn-relative point is inside the farm (grass and flowers may stay where keepGrass).</summary>
        static bool InFarm(Vector2 rel, float clearance, bool detail)
        {
            foreach (var a in FarmAreas)
            {
                if (detail && a.keepGrass) continue;
                Vector2 d = rel - a.centre;
                float yr = -a.yaw * Mathf.Deg2Rad;
                Vector2 l = new Vector2(d.x * Mathf.Cos(yr) - d.y * Mathf.Sin(yr), d.x * Mathf.Sin(yr) + d.y * Mathf.Cos(yr));
                if (Mathf.Abs(l.x) < a.half.x + clearance && Mathf.Abs(l.y) < a.half.y + clearance) return true;
            }
            return false;
        }

        /// <summary>Farm ground on the layer mask: trampled earth around the house and shelter, the tilled field.</summary>
        static void PaintFarmGround(Color[] px, Vector2 origin, System.Func<Vector2, Vector2> world, Quaternion rot)
        {
            PaintBlob(px, origin, world(FieldCentre), rot, FieldHalf, 0, 0.35f);                                       // the field: earth
            PaintBlob(px, origin, world(HouseAt + new Vector2(-5.5f, -2.5f)), rot, new Vector2(3.2f, 2.6f), 0, 0.8f, 0.3f);   // the yard by the door
            PaintBlob(px, origin, world(ShelterAt), rot, new Vector2(2.6f, 2.2f), 1, 0.8f, 0.2f);                      // the shelter floor: mud
            PaintBlob(px, origin, world(new Vector2(14f, -7.2f)), rot, new Vector2(2.2f, 1.8f), 1, 0.8f, 0.4f);        // the paddock gate
        }

        // ---------- building it ----------

        static void BuildFarmstead(Transform root, Vector3 spawn, Quaternion rot, StringBuilder log)
        {
            if (farmMats == null) farmMats = EnsureFarmMaterials(log);
            var mats = farmMats;
            Vector3 W(Vector2 rel) => GroundPoint(spawn + rot * new Vector3(rel.x, 0f, rel.y));
            var farm = new GameObject("Farm").transform;
            farm.SetParent(root, false);
            var col = new GameObject("Colliders").transform;
            col.SetParent(farm, false);

            // the farmhouse: the old game's cottage, baked to a plain mesh, colour matched to the meadow
            BuildFarmHouse(farm, W(HouseAt), rot * Quaternion.Euler(0f, HouseYaw, 0f), log);

            // the goat shelter: bark posts, beams, a plank roof falling to the back, plank back and sides
            var shelter = new GameObject("GoatShelter").transform;
            shelter.SetParent(farm, false);
            shelter.SetPositionAndRotation(W(ShelterAt), rot * Quaternion.Euler(0f, ShelterYaw, 0f));
            BuildShelter(shelter, mats);

            // the paddock: a split-rail fence following the ground, with an open gate toward the camp path
            {
                var m = new FarmMesh();
                var pts = new List<Vector3>();
                foreach (var p in PaddockFence) pts.Add(farm.InverseTransformPoint(W(p)));
                var posts = new List<Vector3>();
                var openPosts = new HashSet<int>();
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    int n = Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(pts[i], pts[i + 1]) / 2.4f));
                    for (int k = 0; k < n; k++)
                    {
                        Vector3 p = Vector3.Lerp(pts[i], pts[i + 1], k / (float)n);
                        p = farm.InverseTransformPoint(GroundPoint(farm.TransformPoint(p)));
                        if (i == PaddockGateSpan) openPosts.Add(posts.Count);
                        posts.Add(p);
                    }
                }
                posts.Add(posts[0]);
                RailFence(m, col, posts.ToArray(), 2, 1.25f, 201, openPosts);
                // the gate, swung open
                Vector3 hinge = farm.InverseTransformPoint(W(PaddockFence[PaddockGateSpan]));
                var rng = new System.Random(202);
                Vector3 swing = Quaternion.Euler(0f, 60f, 0f) * (farm.InverseTransformPoint(W(PaddockFence[PaddockGateSpan + 1])) - hinge).normalized * 2.4f;
                for (int r = 0; r < 3; r++)
                    m.Rail(hinge + Vector3.up * (0.3f + r * 0.38f), hinge + swing + Vector3.up * (0.35f + r * 0.38f), 0.06f, rng);
                m.Rail(hinge + Vector3.up * 0.2f, hinge + swing + Vector3.up * 1.1f, 0.06f, rng);   // the brace
                m.Build("Farm_Paddock", farm, mats);
            }

            // dry-stone field walls: the old game's wall sections, end to end, following the ground
            BuildStoneWalls(farm, col, W, log);

            // hay stacks and the firewood pile against the house
            {
                var m = new FarmMesh();
                int seed = 300;
                foreach (var h in HayStacks)
                {
                    Vector3 at = farm.InverseTransformPoint(W(h.at));
                    HayStack(m, at - Vector3.up * 0.05f, h.radius, h.height, seed++);
                    SolidBox(col, "Hay", at + Vector3.up * h.height * 0.4f, Quaternion.identity, new Vector3(h.radius * 1.6f, h.height * 0.8f, h.radius * 1.6f));
                }
                Vector3 wp = farm.InverseTransformPoint(W(WoodPileAt));
                var wr = Quaternion.Inverse(farm.rotation) * rot * Quaternion.Euler(0f, WoodPileYaw, 0f);
                WoodPile(m, wp, wr, 2.4f, 1.05f, 310);
                SolidBox(col, "WoodPile", wp + Vector3.up * 0.55f, wr, new Vector3(2.6f, 1.1f, 0.6f));
                m.Build("Farm_HayAndWood", farm, mats);
            }
            log.AppendLine("farm: house, goat shelter, paddock with gate, dry-stone walls, hay stacks, firewood");
            PlaceLivestock(farm, spawn, rot, log);
        }

        static void BuildShelter(Transform shelter, FarmMats mats)
        {
            var m = new FarmMesh();
            var rng = new System.Random(150);
            const float w = 4.2f, d = 3f, front = 2.25f, back = 1.65f;
            // posts: the front row is taller (the roof falls to the back)
            var posts = new[] { new Vector3(-w / 2, 0, d / 2), new Vector3(0, 0, d / 2), new Vector3(w / 2, 0, d / 2), new Vector3(-w / 2, 0, -d / 2), new Vector3(w / 2, 0, -d / 2) };
            foreach (var p in posts)
            {
                float top = p.z > 0 ? front : back;
                m.Log(p - Vector3.up * 0.2f, p + Vector3.up * top, 0.11f, 0.095f, 7, rng, 1.2f, false, true);
            }
            m.Log(new Vector3(-w / 2 - 0.3f, front, d / 2), new Vector3(w / 2 + 0.3f, front, d / 2), 0.09f, 0.085f, 7, rng);
            m.Log(new Vector3(-w / 2 - 0.3f, back, -d / 2), new Vector3(w / 2 + 0.3f, back, -d / 2), 0.09f, 0.085f, 7, rng);
            for (int i = 0; i < 4; i++)
            {
                float x = -w / 2 + i * w / 3f;
                m.Log(new Vector3(x, front + 0.12f, d / 2 + 0.5f), new Vector3(x, back + 0.12f, -d / 2 - 0.45f), 0.06f, 0.055f, 6, rng);
            }
            // the plank roof, overhanging front and back
            Vector3 slopeA = new Vector3(0, front + 0.22f, d / 2 + 0.55f), slopeB = new Vector3(0, back + 0.22f, -d / 2 - 0.5f);
            Vector3 along = slopeB - slopeA;
            var roofRot = Quaternion.LookRotation(along.normalized, Vector3.Cross(Vector3.right, along).normalized);
            int boards = 11;
            for (int i = 0; i < boards; i++)
            {
                float x = -w / 2 - 0.45f + (w + 0.9f) * (i + 0.5f) / boards;
                float jitter = ((float)rng.NextDouble() - 0.5f) * 0.03f;
                m.Box((slopeA + slopeB) * 0.5f + new Vector3(x, jitter, 0), roofRot * Quaternion.Euler(0, 0, ((float)rng.NextDouble() - 0.5f) * 3f),
                      new Vector3((w + 0.9f) / boards * 0.96f, 0.035f, along.magnitude), 1.2f, FarmMesh.Roof);
            }
            // back wall and sides: vertical rough boards with gaps, ends a little ragged
            void Boards(Vector3 from, Vector3 to, float hFrom, float hTo)
            {
                float len = Vector3.Distance(from, to);
                int n = Mathf.Max(2, Mathf.RoundToInt(len / 0.24f));
                var dir = (to - from).normalized;
                var r = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up), Vector3.up);
                for (int i = 0; i < n; i++)
                {
                    float t = (i + 0.5f) / n;
                    float hh = Mathf.Lerp(hFrom, hTo, t) - 0.05f - (float)rng.NextDouble() * 0.12f;
                    Vector3 c = Vector3.Lerp(from, to, t) + Vector3.up * (hh * 0.5f - 0.05f);
                    m.Box(c, r, new Vector3(len / n * 0.92f, hh, 0.03f));
                }
            }
            Boards(new Vector3(-w / 2, 0, -d / 2 - 0.12f), new Vector3(w / 2, 0, -d / 2 - 0.12f), back, back);
            Boards(new Vector3(-w / 2 - 0.12f, 0, -d / 2), new Vector3(-w / 2 - 0.12f, 0, d / 2 - 0.9f), back, front * 0.92f);
            Boards(new Vector3(w / 2 + 0.12f, 0, -d / 2), new Vector3(w / 2 + 0.12f, 0, d / 2 - 0.9f), back, front * 0.92f);
            // a hay rack against the back wall
            m.Log(new Vector3(-w / 2 + 0.3f, 1.1f, -d / 2 + 0.25f), new Vector3(w / 2 - 0.3f, 1.1f, -d / 2 + 0.25f), 0.04f, 0.04f, 6, rng);
            m.Lathe(new Vector3(0, 0.05f, -d / 2 + 0.55f), new[] { new Vector2(0.55f, 0), new Vector2(0.6f, 0.15f), new Vector2(0.45f, 0.35f), new Vector2(0.05f, 0.45f) }, 10, rng, FarmMesh.Hay, 1.2f, 0.2f);
            m.Build("Farm_GoatShelter", shelter, mats);
            var col = new GameObject("Colliders").transform;
            col.SetParent(shelter, false);
            SolidBox(col, "Back", new Vector3(0, back * 0.5f, -d / 2 - 0.12f), Quaternion.identity, new Vector3(w, back, 0.1f));
            SolidBox(col, "SideL", new Vector3(-w / 2 - 0.12f, back * 0.5f, -0.45f), Quaternion.identity, new Vector3(0.1f, back, d - 0.9f));
            SolidBox(col, "SideR", new Vector3(w / 2 + 0.12f, back * 0.5f, -0.45f), Quaternion.identity, new Vector3(0.1f, back, d - 0.9f));
            foreach (var p in posts) SolidBox(col, "Post", p + Vector3.up * 1f, Quaternion.identity, new Vector3(0.22f, 2f, 0.22f));
        }

        const string FarmStagingDir = "Assets/_Incoming/OldGame/Environment/Farm";

        static void BuildFarmHouse(Transform farm, Vector3 at, Quaternion facing, StringBuilder log)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FarmStagingDir + "/Prefabs/Environment/Props/Buildings/CottageFarmSmall.prefab");
            if (!prefab)
            {
                // staging is not in the repository: reuse the house baked earlier
                var keptMesh = AssetDatabase.LoadAssetAtPath<Mesh>(FarmMeshDir + "/Farm_House.asset");
                var keptMat = AssetDatabase.LoadAssetAtPath<Material>(FarmMatDir + "/M_Farm_House.mat");
                if (!keptMesh || !keptMat) { log.AppendLine("WARNING: farm cottage not in staging and no baked house; no farmhouse"); return; }
                PlaceHouse(farm, at, facing, keptMesh, keptMat, log);
                return;
            }
            var temp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var smr = temp.transform.Find("Box02_0").GetComponent<SkinnedMeshRenderer>();
                var baked = new Mesh();
                smr.BakeMesh(baked, true);
                // the baked mesh is in the renderer's space: move it into the house root's space, foot at zero
                var toRoot = temp.transform.worldToLocalMatrix * smr.transform.localToWorldMatrix;
                var verts = baked.vertices; var norms = baked.normals;
                for (int i = 0; i < verts.Length; i++) { verts[i] = toRoot.MultiplyPoint3x4(verts[i]); norms[i] = toRoot.MultiplyVector(norms[i]).normalized; }
                float minY = float.MaxValue;
                foreach (var v3 in verts) minY = Mathf.Min(minY, v3.y);
                for (int i = 0; i < verts.Length; i++) verts[i].y -= minY;
                baked.vertices = verts; baked.normals = norms;
                baked.RecalculateBounds(); baked.RecalculateTangents();
                baked.name = "Farm_House";
                string meshPath = FarmMeshDir + "/Farm_House.asset";
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (saved) { EditorUtility.CopySerialized(baked, saved); saved.name = "Farm_House"; Object.DestroyImmediate(baked); baked = saved; EditorUtility.SetDirty(saved); }
                else AssetDatabase.CreateAsset(baked, meshPath);

                var src = smr.sharedMaterial;
                string matPath = FarmMatDir + "/M_Farm_House.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_Farm_House" }; AssetDatabase.CreateAsset(mat, matPath); }
                var tex = src ? (src.HasProperty("_BaseMap") ? src.GetTexture("_BaseMap") : null) ?? src.mainTexture : null;
                mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", new Color(1.18f, 1.15f, 1.08f));   // the old texture is dark: in shade it went near black
                mat.SetFloat("_Smoothness", 0.12f);
                mat.SetFloat("_EnvironmentReflections", 0f);
                mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                EditorUtility.SetDirty(mat);
                PlaceHouse(farm, at, facing, baked, mat, log);
            }
            finally { Object.DestroyImmediate(temp); }
        }

        static void PlaceHouse(Transform farm, Vector3 at, Quaternion facing, Mesh mesh, Material mat, StringBuilder log)
        {
            var house = new GameObject("FarmHouse");
            house.transform.SetParent(farm, false);
            house.transform.SetPositionAndRotation(at - Vector3.up * 0.25f, facing);   // sunk a little so no corner floats on the slope
            house.AddComponent<MeshFilter>().sharedMesh = mesh;
            house.AddComponent<MeshRenderer>().sharedMaterial = mat;
            var box = house.AddComponent<BoxCollider>();
            box.center = mesh.bounds.center; box.size = mesh.bounds.size;
            log.AppendLine($"farm: farmhouse {mesh.triangles.Length / 3} triangles, {mesh.bounds.size.x:0.0} x {mesh.bounds.size.z:0.0} m");
        }

        static void BuildStoneWalls(Transform farm, Transform col, System.Func<Vector2, Vector3> W, StringBuilder log)
        {
            string[] pieces = { "wall1_old", "wall3", "wall2_old", "wall4" };
            var prefabs = new List<GameObject>();
            EnsureFolder(FarmPrefabDir);
            foreach (var p in pieces)
            {
                // our copy of each piece (the meadow-toned stone), made once from the staged Autumn pack prefab
                string ours = $"{FarmPrefabDir}/Farm_Wall_{p}.prefab";
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(ours);
                if (!go)
                {
                    var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{FarmStagingDir}/EnvironmentPacks/Autumn/Addons/{p}.prefab");
                    if (!src) continue;
                    var tmp = (GameObject)PrefabUtility.InstantiatePrefab(src);
                    try
                    {
                        PrefabUtility.UnpackPrefabInstance(tmp, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                        tmp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                        foreach (var r in tmp.GetComponentsInChildren<Renderer>()) r.sharedMaterials = StoneMaterials(r.sharedMaterials);
                        go = PrefabUtility.SaveAsPrefabAsset(tmp, ours);
                    }
                    finally { Object.DestroyImmediate(tmp); }
                }
                prefabs.Add(go);
            }
            if (prefabs.Count == 0) { log.AppendLine("WARNING: stone wall pieces missing (neither ours nor staged)"); return; }
            var walls = new GameObject("StoneWalls").transform;
            walls.SetParent(farm, false);
            var rng = new System.Random(400);
            int count = 0;
            foreach (var (a, b) in StoneWalls)
            {
                float len = Vector2.Distance(a, b);
                const float piece = 4.3f;
                int n = Mathf.Max(1, Mathf.RoundToInt(len / piece));
                for (int i = 0; i < n; i++)
                {
                    Vector3 pa = W(Vector2.Lerp(a, b, i / (float)n)), pb = W(Vector2.Lerp(a, b, (i + 1f) / n));
                    Vector3 pc = W(Vector2.Lerp(a, b, (i + 0.5f) / n));
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[rng.Next(prefabs.Count)]);
                    PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    go.transform.SetParent(walls, true);
                    Vector3 dir = pb - pa;
                    // the pieces run along their x axis: turn x along the wall, tilt with the slope, sink the foot
                    var look = Quaternion.LookRotation(Vector3.Cross(dir.normalized, Vector3.up), Vector3.up);
                    var tilt = Quaternion.FromToRotation(new Vector3(dir.x, 0f, dir.z).normalized, dir.normalized);
                    go.transform.SetPositionAndRotation(pc - Vector3.up * 0.18f, tilt * look * Quaternion.Euler(0f, ((float)rng.NextDouble() - 0.5f) * 3f, 0f));
                    go.transform.localScale = new Vector3(Vector3.Distance(pa, pb) / piece * 1.02f, 0.75f + (float)rng.NextDouble() * 0.15f, 1f);
                    foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterials = StoneMaterials(r.sharedMaterials);
                    SolidBox(col, "Wall", farm.InverseTransformPoint(pc + Vector3.up * 0.55f), Quaternion.Inverse(farm.rotation) * look, new Vector3(Vector3.Distance(pa, pb), 1.1f, 0.8f));
                    count++;
                }
            }
            log.AppendLine($"farm: {count} dry-stone wall sections");
        }

        // copies of the wall materials, toned toward the meadow's mossy grey boulders (the Autumn pack's are warm beige)
        static Material[] StoneMaterials(Material[] source)
        {
            var result = new Material[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                var s = source[i];
                if (!s) continue;
                if (s.name.StartsWith("M_Farm_")) { result[i] = s; continue; }
                string path = $"{FarmMatDir}/M_Farm_{s.name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!m) { m = new Material(s) { name = "M_Farm_" + s.name }; AssetDatabase.CreateAsset(m, path); }
                else m.CopyPropertiesFromMaterial(s);
                m.SetColor("_BaseColor", new Color(1.45f, 1.48f, 1.38f));   // the Autumn stone is far darker than the meadow's boulders: in shade it went black
                m.SetFloat("_Smoothness", 0.08f);
                EditorUtility.SetDirty(m);
                result[i] = m;
            }
            return result;
        }
    }
}
