using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Animals;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The farm's animals (owner, 2026-11-27: a farm look). The old game's livestock pack (goat, cow, sheep, chicken:
    /// graze, chew, breathe, look around, walk, run, four levels of detail each) on URP materials, one animator per
    /// species and a Livestock behaviour. Kept to a few animals with early level-of-detail switches: the Galaxy A30s
    /// has little to spare, and the hunt mostly faces away from the farm (animation stops out of view).
    /// </summary>
    public static partial class GrayboxR01Builder
    {
        const string LivestockArtDir = "Assets/_Game/Art/Animals/Livestock";
        const string LivestockPrefabDir = "Assets/_Game/Prefabs/Animals/Livestock";

        sealed class Species
        {
            public string name, model;
            public (string texture, string suffix)[] variants;   // body colours
            public string[] idles;
            public string walk = "Walk", run = "Run";
            public float walkSpeed, runSpeed;
            public float[] lods;   // screen heights where each level ends
        }

        static readonly Species[] FarmSpecies =
        {
            new Species { name = "Goat", model = "Goat",
                variants = new[] { ("T_Goat_BaseColor", "") },
                idles = new[] { "IdleLookAround", "IdleBreathe", "IdleBaaa", "IdleLookAround" }, walk = "walk", walkSpeed = 0.55f, runSpeed = 3.2f,
                lods = new[] { 0.28f, 0.14f, 0.06f, 0.012f } },
            new Species { name = "Cow", model = "Cow",
                variants = new[] { ("T_Cow1_BaseColor", ""), ("T_Cow2_BaseColor", "_B") },
                idles = new[] { "IdleGraze", "IdleGraze", "IdleChew", "IdleBreathe", "IdleLookAround" }, walkSpeed = 0.7f, runSpeed = 3f,
                lods = new[] { 0.3f, 0.15f, 0.07f, 0.015f } },
            new Species { name = "Sheep", model = "Sheep",
                variants = new[] { ("T_Sheep_BaseColor", "") },
                idles = new[] { "IdleGraze", "IdleGraze", "IdleChew", "IdleBaaa", "IdleLookAround" }, walkSpeed = 0.6f, runSpeed = 3.2f,
                lods = new[] { 0.25f, 0.12f, 0.05f, 0.01f } },
            new Species { name = "Chicken", model = "Chicken",
                variants = new[] { ("T_ChickenA_BaseColor", ""), ("T_ChickenB_BaseColor", "_B") },
                idles = new[] { "Idle1", "Idle2", "Idle1" }, walkSpeed = 0.45f, runSpeed = 2.4f,
                lods = new[] { 0.12f, 0.008f } },
        };

        static Texture2D LivestockTexture(string root, string file, bool normal)
        {
            string p = AssetDatabase.FindAssets(file + " t:Texture2D", new[] { root }).Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(x => System.IO.Path.GetFileNameWithoutExtension(x) == file);
            if (p == null) return null;
            var ti = (TextureImporter)AssetImporter.GetAtPath(p);
            bool dirty = false;
            if (normal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; dirty = true; }
            if (ti.maxTextureSize > 1024) { ti.maxTextureSize = 1024; dirty = true; }
            var android = ti.GetPlatformTextureSettings("Android");
            if (!android.overridden || android.format != TextureImporterFormat.ASTC_6x6)
            {
                android.overridden = true; android.format = TextureImporterFormat.ASTC_6x6; android.maxTextureSize = 1024;
                ti.SetPlatformTextureSettings(android); dirty = true;
            }
            if (dirty) ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }

        // URP Lit from the pack's maps. The fur shell (LOD0 and LOD1 only) is the same maps, cut out at the texture's alpha.
        static Material LivestockMaterial(Species sp, string root, string baseTex, string suffix, bool fur)
        {
            string name = $"M_{sp.name}{(fur ? "Fur" : "")}{suffix}";
            string matPath = $"{LivestockArtDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name }; AssetDatabase.CreateAsset(m, matPath); }
            m.SetTexture("_BaseMap", LivestockTexture(root, baseTex, false));
            var n = LivestockTexture(root, $"T_{sp.name}_Normal", true);
            if (n) { m.SetTexture("_BumpMap", n); m.EnableKeyword("_NORMALMAP"); }
            var o = LivestockTexture(root, $"T_{sp.name}_Occlusion", false);
            if (o) { m.SetTexture("_OcclusionMap", o); m.EnableKeyword("_OCCLUSIONMAP"); }
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.12f);
            m.SetFloat("_SpecularHighlights", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            if (fur)
            {
                m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.06f); m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cull", 0f);   // fur cards are seen from both sides
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                m.SetOverrideTag("RenderType", "TransparentCutout");
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        // the pack's FBX files map their materials to stray .mat files by texture name: ours are set on the prefabs instead
        static void NoImportedMaterials(string path)
        {
            if (AssetImporter.GetAtPath(path) is ModelImporter imp && imp.materialImportMode != ModelImporterMaterialImportMode.None)
            {
                imp.materialImportMode = ModelImporterMaterialImportMode.None;
                imp.SaveAndReimport();
            }
        }

        static int LodIndex(string name)
        {
            int k = name.LastIndexOf("LOD", System.StringComparison.Ordinal);
            return k >= 0 && int.TryParse(name.Substring(k + 3), out int level) ? level : 0;
        }

        static GameObject[] EnsureLivestock(StringBuilder log)
        {
            EnsureFolder(LivestockArtDir);
            EnsureFolder(LivestockPrefabDir);
            var prefabs = new List<GameObject>();
            foreach (var sp in FarmSpecies)
            {
                // the old game's pack: models, the clips used and their maps (moved out of staging, 2026-11-27)
                string root = $"{LivestockArtDir}/{sp.name}";
                string dir = root;
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{dir}/{sp.model}.FBX");
                if (!model) { log.AppendLine($"WARNING: livestock {sp.name}: no {sp.model}.FBX in {dir}"); continue; }
                NoImportedMaterials($"{dir}/{sp.model}.FBX");

                // the clips: in-place versions, looping
                var clipFor = new Dictionary<string, AnimationClip>();
                foreach (var state in sp.idles.Concat(new[] { sp.walk, sp.run }).Distinct())
                {
                    string path = $"{dir}/{sp.model}@{state}.FBX";
                    var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (imp == null) { log.AppendLine($"WARNING: {sp.name}: no clip {state}"); continue; }
                    NoImportedMaterials(path);
                    var clips = imp.clipAnimations.Length > 0 ? imp.clipAnimations : imp.defaultClipAnimations;
                    bool changed = imp.clipAnimations.Length == 0;
                    foreach (var c in clips) if (!c.loopTime) { c.loopTime = true; changed = true; }
                    if (changed) { imp.clipAnimations = clips; imp.SaveAndReimport(); }
                    var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
                    if (clip) clipFor[state] = clip;
                }

                // animator: one state per clip, the first idle by default
                string ctrlPath = $"Assets/_Game/Animation/Controllers/Livestock_{sp.name}.controller";
                var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath) ?? AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
                var sm = ctrl.layers[0].stateMachine;
                foreach (var s in sm.states.ToArray()) sm.RemoveState(s.state);
                foreach (var kv in clipFor) sm.AddState(kv.Key).motion = kv.Value;
                var first = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == sp.idles[0]);
                if (first != null) sm.defaultState = first;
                EditorUtility.SetDirty(ctrl);

                foreach (var (tex, suffix) in sp.variants)
                {
                    var body = LivestockMaterial(sp, root, tex, suffix, false);
                    var fur = LivestockMaterial(sp, root, tex, suffix, true);
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
                    try
                    {
                        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                        go.name = sp.name + suffix;
                        int lod0 = 0;
                        foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        {
                            // the pack's layout: submesh 0 is the body, submesh 1 (near levels only) the fur shell
                            int subs = r.sharedMesh ? r.sharedMesh.subMeshCount : 1;
                            r.sharedMaterials = Enumerable.Range(0, subs).Select(i => i == 0 ? body : fur).ToArray();
                            bool near = r.name.Contains("LOD0") || r.name.Contains("LOD1");
                            r.shadowCastingMode = near ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                            r.updateWhenOffscreen = false;
                            r.skinnedMotionVectors = false;
                            if (r.name.Contains("LOD0") && r.sharedMesh) lod0 += r.sharedMesh.triangles.Length / 3;
                        }
                        var animator = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
                        animator.runtimeAnimatorController = ctrl;
                        animator.applyRootMotion = false;
                        animator.cullingMode = AnimatorCullingMode.CullCompletely;
                        // one level per *_LODn renderer, switching early (the A30s has little to spare)
                        var lod = go.GetComponent<LODGroup>() ?? go.AddComponent<LODGroup>();
                        var byLevel = go.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                            .Select(r => (r, n: LodIndex(r.name))).GroupBy(x => x.n).OrderBy(g => g.Key).ToArray();
                        var levels = new LOD[byLevel.Length];
                        for (int i = 0; i < levels.Length; i++)
                            levels[i] = new LOD(i < sp.lods.Length ? sp.lods[i] : 0.01f, byLevel[i].Select(x => (Renderer)x.r).ToArray());
                        lod.SetLODs(levels);
                        lod.RecalculateBounds();
                        // a body-sized capsule along the animal (bounds in the prefab's own space)
                        var rends = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                        var b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds);
                        b.center -= go.transform.position;
                        var cap = go.AddComponent<CapsuleCollider>();
                        cap.direction = 2;
                        cap.radius = Mathf.Max(0.1f, Mathf.Min(b.size.x, b.size.y) * 0.4f);
                        cap.height = Mathf.Max(cap.radius * 2f, b.size.z * 0.85f);
                        cap.center = new Vector3(b.center.x, Mathf.Max(cap.radius, b.size.y * 0.55f), b.center.z);
                        var ls = go.AddComponent<Livestock>();
                        var so = new SerializedObject(ls);
                        so.FindProperty("animator").objectReferenceValue = animator;
                        var idles = so.FindProperty("idleStates");
                        idles.arraySize = sp.idles.Length;
                        for (int i = 0; i < sp.idles.Length; i++) idles.GetArrayElementAtIndex(i).stringValue = sp.idles[i];
                        so.FindProperty("walkState").stringValue = sp.walk;
                        so.FindProperty("runState").stringValue = sp.run;
                        so.FindProperty("walkSpeed").floatValue = sp.walkSpeed;
                        so.FindProperty("runSpeed").floatValue = sp.runSpeed;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        prefabs.Add(PrefabUtility.SaveAsPrefabAsset(go, $"{LivestockPrefabDir}/{go.name}.prefab"));
                        log.AppendLine($"livestock: {go.name}, {clipFor.Count} clips, LOD0 {lod0} triangles, capsule r {cap.radius:0.00} h {cap.height:0.00}");
                    }
                    finally { Object.DestroyImmediate(go); }
                }
            }
            AssetDatabase.SaveAssets();
            return prefabs.Where(p => p).ToArray();
        }

        /// <summary>Goats in the pen (too scared to eat, as the briefing says), cows and sheep in the paddock, chickens in the yard.</summary>
        static void PlaceLivestock(Transform farm, Vector3 spawn, Quaternion rot, StringBuilder log)
        {
            var prefabs = EnsureLivestock(log).ToDictionary(p => p.name);
            GameObject P(string n) => prefabs.TryGetValue(n, out var p) ? p : null;
            var herd = new GameObject("Livestock").transform;
            herd.SetParent(farm, false);
            Vector3 W(Vector2 rel) => GroundPoint(spawn + rot * new Vector3(rel.x, 0f, rel.y));
            float yaw = rot.eulerAngles.y;
            var rng = new System.Random(500);
            int count = 0;
            void Put(string name, Vector2 areaCentre, Vector2 half, int n)
            {
                var prefab = P(name);
                if (!prefab) return;
                for (int i = 0; i < n; i++)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, farm.gameObject.scene);
                    go.transform.SetParent(herd, true);
                    Vector2 rel = areaCentre + new Vector2(((float)rng.NextDouble() * 2f - 1f) * half.x * 0.8f, ((float)rng.NextDouble() * 2f - 1f) * half.y * 0.8f);
                    go.transform.SetPositionAndRotation(W(rel), rot * Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
                    go.GetComponent<Livestock>().SetArea(W(areaCentre), half, yaw);
                    EditorUtility.SetDirty(go.GetComponent<Livestock>());
                    count++;
                }
            }
            Put("Goat", PenCentre, new Vector2(PenHalf - 0.55f, PenHalf - 0.55f), 3);
            Put("Cow", new Vector2(22.4f, -7.8f), new Vector2(7f, 4.8f), 1);
            Put("Cow_B", new Vector2(22.4f, -7.8f), new Vector2(7f, 4.8f), 1);
            Put("Sheep", new Vector2(22.4f, -7.8f), new Vector2(7f, 4.8f), 3);
            Put("Chicken", new Vector2(18.5f, 14f), new Vector2(1.8f, 1.6f), 2);
            Put("Chicken_B", new Vector2(18.5f, 14f), new Vector2(1.8f, 1.6f), 2);
            log.AppendLine($"livestock: {count} animals placed (goats in the pen, cows and sheep in the paddock, chickens in the yard)");
        }
    }
}
