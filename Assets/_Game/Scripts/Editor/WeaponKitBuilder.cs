using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Weapons;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Tools that turn a first-person weapon model (arms and gun on one rig, like the HQ FPS Weapons pack) into a
    /// WeaponKit. Every gun goes through the same steps: its clips become overrides of the shared animator, the
    /// triangles the camera never sees in any of its animations are removed, and the bare arms get a jacket sleeve.
    /// </summary>
    public static class WeaponKitBuilder
    {
        public const string BaseControllerPath = "Assets/_Game/Animation/Controllers/FP_Weapon.controller";
        public static readonly string[] Roles =
            { WeaponViewModel.Ready, WeaponViewModel.Idle, WeaponViewModel.Fire, WeaponViewModel.Reload, WeaponViewModel.ReloadEmpty };

        // ---------- animation ----------

        /// <summary>
        /// The shared first-person animator: one state per role, each playing a placeholder clip that a kit's
        /// override replaces. Idle is the default; the others return to it. Reloads take their speed from a parameter.
        /// </summary>
        public static AnimatorController EnsureBaseController()
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(BaseControllerPath)
                       ?? AnimatorController.CreateAnimatorControllerAtPath(BaseControllerPath);
            var placeholders = AssetDatabase.LoadAllAssetsAtPath(BaseControllerPath).OfType<AnimationClip>().ToDictionary(c => c.name);
            AnimationClip Placeholder(string role)
            {
                string name = WeaponViewModel.RoleClip(role);
                if (placeholders.TryGetValue(name, out var c)) return c;
                c = new AnimationClip { name = name };
                if (role == WeaponViewModel.Idle)
                {
                    var s = AnimationUtility.GetAnimationClipSettings(c); s.loopTime = true; AnimationUtility.SetAnimationClipSettings(c, s);
                }
                AssetDatabase.AddObjectToAsset(c, ctrl);
                return c;
            }

            foreach (var p in new[] { WeaponViewModel.ReloadSpeedParameter, WeaponViewModel.ReloadEmptySpeedParameter })
                if (ctrl.parameters.All(x => x.name != p)) ctrl.AddParameter(new AnimatorControllerParameter { name = p, type = AnimatorControllerParameterType.Float, defaultFloat = 1f });

            var sm = ctrl.layers[0].stateMachine;
            foreach (var s in sm.states.ToArray()) sm.RemoveState(s.state);
            var states = new Dictionary<string, AnimatorState>();
            foreach (var role in Roles)
            {
                var st = sm.AddState(role);
                st.motion = Placeholder(role);
                states[role] = st;
            }
            states[WeaponViewModel.Reload].speedParameter = WeaponViewModel.ReloadSpeedParameter;
            states[WeaponViewModel.Reload].speedParameterActive = true;
            states[WeaponViewModel.ReloadEmpty].speedParameter = WeaponViewModel.ReloadEmptySpeedParameter;
            states[WeaponViewModel.ReloadEmpty].speedParameterActive = true;
            sm.defaultState = states[WeaponViewModel.Idle];
            foreach (var role in Roles.Where(r => r != WeaponViewModel.Idle))
            {
                var t = states[role].AddTransition(states[WeaponViewModel.Idle]);
                t.hasExitTime = true; t.exitTime = 0.95f; t.duration = 0.15f; t.hasFixedDuration = true;
            }
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        /// <summary>A gun's clips for the roles, as an override of the shared animator (saved at path).</summary>
        public static AnimatorOverrideController EnsureOverride(string path, IReadOnlyDictionary<string, AnimationClip> roleClips, System.Text.StringBuilder log)
        {
            var baseCtrl = EnsureBaseController();
            var ov = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            if (!ov) { ov = new AnimatorOverrideController(baseCtrl); AssetDatabase.CreateAsset(ov, path); }
            ov.runtimeAnimatorController = baseCtrl;
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            ov.GetOverrides(pairs);
            for (int i = 0; i < pairs.Count; i++)
            {
                string role = pairs[i].Key.name.Replace("Role_", "");
                roleClips.TryGetValue(role, out var clip);
                if (!clip) log.AppendLine($"WARNING: {Path.GetFileName(path)}: no clip for role {role}");
                pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[i].Key, clip);
            }
            ov.ApplyOverrides(pairs);
            EditorUtility.SetDirty(ov);
            return ov;
        }

        // ---------- hidden triangles ----------

        /// <summary>
        /// Plays every clip on the model (placed where it hangs in front of the camera, camera at the origin looking
        /// along +z) and marks each vertex that comes within the view, widened by a margin for the hand motion. The
        /// view is the widest phone screen; it is tested at every given holder position (hip and aim).
        /// </summary>
        public static Dictionary<SkinnedMeshRenderer, bool[]> SampleVisibility(GameObject model, IEnumerable<AnimationClip> clips,
            IEnumerable<Vector3> holderPositions, Vector3 modelOffset, float verticalFov, float widestAspect, float marginDegrees, float samplesPerSecond = 30f)
        {
            var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var visible = renderers.ToDictionary(r => r, r => new bool[r.sharedMesh.vertexCount]);
            float halfV = verticalFov * 0.5f;
            float halfH = Mathf.Atan(Mathf.Tan(halfV * Mathf.Deg2Rad) * widestAspect) * Mathf.Rad2Deg;
            float tanV = Mathf.Tan(Mathf.Min(85f, halfV + marginDegrees) * Mathf.Deg2Rad);
            float tanH = Mathf.Tan(Mathf.Min(85f, halfH + marginDegrees) * Mathf.Deg2Rad);
            var holders = holderPositions.ToArray();
            var animated = model.GetComponentInChildren<Animator>() ? model.GetComponentInChildren<Animator>().gameObject : model;
            var baked = new Mesh();
            var keepPos = model.transform.position; var keepRot = model.transform.rotation;
            model.transform.SetPositionAndRotation(modelOffset, Quaternion.identity);
            try
            {
                foreach (var clip in clips.Where(c => c))
                {
                    int n = Mathf.Max(1, Mathf.CeilToInt(clip.length * samplesPerSecond));
                    for (int i = 0; i <= n; i++)
                    {
                        clip.SampleAnimation(animated, clip.length * i / n);
                        foreach (var r in renderers)
                        {
                            r.BakeMesh(baked, true);
                            var vs = baked.vertices;
                            var m = Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one);
                            var flags = visible[r];
                            for (int k = 0; k < vs.Length; k++)
                            {
                                if (flags[k]) continue;
                                Vector3 p0 = m.MultiplyPoint3x4(vs[k]);
                                foreach (var h in holders)
                                {
                                    Vector3 p = p0 + h;
                                    if (p.z > 0.02f && Mathf.Abs(p.x) < p.z * tanH && Mathf.Abs(p.y) < p.z * tanV) { flags[k] = true; break; }
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                model.transform.SetPositionAndRotation(keepPos, keepRot);
                Object.DestroyImmediate(baked);
            }
            return visible;
        }

        /// <summary>
        /// A copy of the mesh with only the triangles that have a visible vertex, unused vertices dropped. Keeps
        /// bone weights and bind poses, so it drops into the same skinned renderer. Null if it has blend shapes.
        /// </summary>
        public static Mesh TrimHidden(Mesh source, bool[] visible, string name)
        {
            if (source.blendShapeCount > 0) return null;
            int subCount = source.subMeshCount;
            var keptTris = new List<int>[subCount];
            var used = new bool[source.vertexCount];
            for (int s = 0; s < subCount; s++)
            {
                var tris = source.GetTriangles(s);
                keptTris[s] = new List<int>(tris.Length);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    if (!(visible[a] || visible[b] || visible[c])) continue;
                    keptTris[s].Add(a); keptTris[s].Add(b); keptTris[s].Add(c);
                    used[a] = used[b] = used[c] = true;
                }
            }
            var remap = new int[source.vertexCount];
            var order = new List<int>();
            for (int i = 0; i < used.Length; i++) { remap[i] = used[i] ? order.Count : -1; if (used[i]) order.Add(i); }

            T[] Pick<T>(T[] all) => all == null || all.Length != source.vertexCount ? null : order.Select(i => all[i]).ToArray();
            var mesh = new Mesh { name = name };
            mesh.indexFormat = order.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = Pick(source.vertices);
            var normals = Pick(source.normals); if (normals != null) mesh.normals = normals;
            var tangents = Pick(source.tangents); if (tangents != null) mesh.tangents = tangents;
            var colors = Pick(source.colors32); if (colors != null) mesh.colors32 = colors;
            for (int ch = 0; ch < 4; ch++)
            {
                var uv = new List<Vector2>(); source.GetUVs(ch, uv);
                if (uv.Count == source.vertexCount) mesh.SetUVs(ch, order.Select(i => uv[i]).ToList());
            }
            mesh.boneWeights = Pick(source.boneWeights);
            mesh.bindposes = source.bindposes;
            mesh.subMeshCount = subCount;
            for (int s = 0; s < subCount; s++) mesh.SetTriangles(keptTris[s].Select(i => remap[i]).ToArray(), s);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Saves a generated mesh at path, keeping the file (and its GUID) when it exists.</summary>
        public static Mesh SaveMesh(Mesh mesh, string path)
        {
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!saved) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            string n = mesh.name;
            EditorUtility.CopySerialized(mesh, saved);
            saved.name = n;
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(saved);
            return saved;
        }

        // ---------- sleeves ----------

        /// <summary>Where the sleeve is on one arm mesh: per vertex 0 (skin) to 1 (sleeve), and metres back from the cuff.</summary>
        public sealed class SleeveMap
        {
            public float[] sleeve, along;
            public float unitsPerMetre = 1f;
        }

        /// <summary>
        /// The sleeve starts a little behind the wrist (cuffFraction of the forearm back from the hand bone) and covers
        /// everything up to the shoulder. Found from the bind pose: the hand and forearm bones give the wrist and the
        /// line of the forearm. Null when the arm has no hand or forearm bone.
        /// </summary>
        public static SleeveMap MapSleeve(SkinnedMeshRenderer arm, float cuffFraction = 0.28f, float forearmMetres = 0.26f)
        {
            var mesh = arm.sharedMesh;
            // both arms of a rig share one bone list: take the hand bone this mesh is weighted to, and its own forearm
            var weight = new float[arm.bones.Length];
            foreach (var w in mesh.boneWeights) { weight[w.boneIndex0] += w.weight0; weight[w.boneIndex1] += w.weight1; }
            int hand = -1;
            for (int i = 0; i < arm.bones.Length; i++)
                if (arm.bones[i] && arm.bones[i].name.StartsWith("Hand") && (hand < 0 || weight[i] > weight[hand])) hand = i;
            if (hand < 0 || weight[hand] <= 0f) return null;
            var forearmBone = arm.bones[hand].parent;
            int fore = forearmBone ? System.Array.IndexOf(arm.bones, forearmBone) : -1;
            if (fore < 0) return null;
            Vector3 Bone(int i) => mesh.bindposes[i].inverse.MultiplyPoint3x4(Vector3.zero);
            Vector3 wrist = Bone(hand), elbow = Bone(fore);
            Vector3 back = (elbow - wrist).normalized;
            float length = Vector3.Distance(elbow, wrist);
            var map = new SleeveMap { sleeve = new float[mesh.vertexCount], along = new float[mesh.vertexCount], unitsPerMetre = length / forearmMetres };
            var vs = mesh.vertices;
            float cuff = cuffFraction * length, soft = 0.012f * map.unitsPerMetre;
            for (int i = 0; i < vs.Length; i++)
            {
                float t = Vector3.Dot(vs[i] - wrist, back) - cuff;
                map.along[i] = t / map.unitsPerMetre;
                map.sleeve[i] = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-soft * 0.5f, soft * 0.5f, t));
            }
            return map;
        }

        /// <summary>Sleeve cloth stands off the skin: pushes sleeve vertices out along their normals (thicker at the cuff hem).</summary>
        public static void ThickenSleeve(Mesh mesh, SleeveMap map, float metres = 0.006f, float hemMetres = 0.003f, float hemWidth = 0.02f)
        {
            if (map == null) return;
            var vs = mesh.vertices; var ns = mesh.normals;
            for (int i = 0; i < vs.Length; i++)
            {
                if (map.sleeve[i] <= 0f) continue;
                float hem = map.along[i] >= 0f && map.along[i] < hemWidth ? hemMetres * (1f - map.along[i] / hemWidth) : 0f;
                vs[i] += ns[i] * ((metres + hem) * map.sleeve[i] * map.unitsPerMetre);
            }
            mesh.vertices = vs;
            mesh.RecalculateBounds();
        }

        /// <summary>
        /// Paints the sleeves into a copy of the arms' skin texture: the sleeve areas (found by rasterising each arm's
        /// triangles in UV space) become canvas with weave, creases and a hem, and the skin just below the cuff gets
        /// its shadow. Writes a PNG at outPath (size x size) and imports it as colour.
        /// </summary>
        public static Texture2D PaintSleeves(string skinPath, IEnumerable<(Mesh mesh, SleeveMap map)> arms, string outPath, int size, Color cloth)
        {
            var skin = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            skin.LoadImage(File.ReadAllBytes(skinPath));
            var mask = new float[size * size];
            var along = new float[size * size];
            var covered = new bool[size * size];
            foreach (var (mesh, map) in arms)
            {
                if (map == null) continue;
                var uv = mesh.uv; var tris = mesh.triangles;
                for (int i = 0; i < tris.Length; i += 3)
                    Rasterise(uv[tris[i]] * size, uv[tris[i + 1]] * size, uv[tris[i + 2]] * size, size,
                        new Vector2(map.sleeve[tris[i]], map.along[tris[i]]), new Vector2(map.sleeve[tris[i + 1]], map.along[tris[i + 1]]),
                        new Vector2(map.sleeve[tris[i + 2]], map.along[tris[i + 2]]), mask, along, covered);
            }
            Dilate(mask, along, covered, size, 8);

            var px = new Color[size * size];
            float inv = 1f / size;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int k = y * size + x;
                Color c = skin.GetPixelBilinear((x + 0.5f) * inv, (y + 0.5f) * inv);
                float a = along[k];
                if (covered[k] && a < 0f && a > -0.014f) c *= Mathf.Lerp(0.62f, 1f, -a / 0.014f);   // shadow just below the cuff
                float m = mask[k];
                if (m > 0f)
                {
                    // canvas: fine weave, mottling, creases across the arm, a darker hem with its seam
                    float weave = (Mathf.PerlinNoise(x * 0.45f, y * 0.45f) - 0.5f) * 0.10f + (((x + y) & 1) == 0 ? 0.015f : -0.015f);
                    float mottle = (Mathf.PerlinNoise(x * 0.006f + 3.1f, y * 0.006f) - 0.5f) * 0.14f;
                    float crease = Mathf.Sin(a * 85f + Mathf.PerlinNoise(x * 0.012f, y * 0.012f) * 7f);
                    float shade = 1f + weave + mottle - 0.16f * Mathf.Pow(Mathf.Max(0f, crease), 3f) + 0.05f * Mathf.Max(0f, -crease);
                    if (a >= 0f && a < 0.024f) shade *= 0.82f;                       // the hem
                    if (a >= 0.022f && a < 0.026f) shade *= 0.7f;                    // its seam
                    if (a >= 0f && a < 0.003f) shade *= 0.75f;                       // the cuff's edge in shadow
                    Color fabric = cloth * shade; fabric.a = 1f;
                    c = Color.Lerp(c, fabric, m);
                }
                c.a = 1f;
                px[k] = c;
            }
            Object.DestroyImmediate(skin);
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            tex.SetPixels(px);
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(outPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        }

        static void Rasterise(Vector2 a, Vector2 b, Vector2 c, int size, Vector2 va, Vector2 vb, Vector2 vc, float[] mask, float[] along, bool[] covered)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)))), x1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)))), y1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
            float area = (b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y);
            if (Mathf.Abs(area) < 1e-8f) return;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float w0 = ((b.x - p.x) * (c.y - p.y) - (c.x - p.x) * (b.y - p.y)) / area;
                float w1 = ((c.x - p.x) * (a.y - p.y) - (a.x - p.x) * (c.y - p.y)) / area;
                float w2 = 1f - w0 - w1;
                const float e = -0.02f;
                if (w0 < e || w1 < e || w2 < e) continue;
                var v = va * w0 + vb * w1 + vc * w2;
                int k = y * size + x;
                if (!covered[k] || v.x > mask[k]) { mask[k] = v.x; along[k] = v.y; }
                covered[k] = true;
            }
        }

        // spreads the painted values a few pixels past each UV island, so filtering and mip maps never show seams
        static void Dilate(float[] mask, float[] along, bool[] covered, int size, int passes)
        {
            for (int pass = 0; pass < passes; pass++)
            {
                var add = new List<(int k, float m, float a)>();
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int k = y * size + x;
                    if (covered[k]) continue;
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                        if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
                        int n = ny * size + nx;
                        if (covered[n]) { add.Add((k, mask[n], along[n])); break; }
                    }
                }
                foreach (var (k, m, a) in add) { mask[k] = m; along[k] = a; covered[k] = true; }
            }
        }

        /// <summary>Colour texture import for phones: max size, ASTC 6x6 on Android.</summary>
        public static void ImportColour(string path, int max, bool sRGB = true)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = sRGB;
            ti.maxTextureSize = max;
            ti.mipmapEnabled = true;
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                { name = "Android", overridden = true, maxTextureSize = max, format = TextureImporterFormat.ASTC_6x6, compressionQuality = 50 });
            ti.SaveAndReimport();
        }
    }
}
