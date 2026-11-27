using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Weapons;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The hunter's rifle as a weapon kit (HQ FPS Weapons from the old project, the owner's Asset Store licence):
    /// arms and hunting rifle on one rig, its clips as overrides of the shared first-person animator, URP materials
    /// with the wood warmed and the steel blued (owner, 2026-11-27: it looked grey and heavy), a scout scope over the
    /// handguard (clear of the top-loading clip), jacket sleeves on the bare arms, and only the triangles the camera
    /// can ever see. The files in Art are the source; the staging copy is only needed the first time.
    /// </summary>
    public static partial class GrayboxR01Builder
    {
        const string RifleSrc = "Assets/_Incoming/OldGame/Props/Weapons/HQFPSWeapons";
        const string RifleArt = "Assets/_Game/Art/Weapons/HuntingRifle";
        const string RifleGenerated = RifleArt + "/Generated";
        const string WeaponPrefabDir = "Assets/_Game/Prefabs/Weapons";
        const string RifleOverridePath = "Assets/_Game/Animation/Controllers/FP_HuntingRifle.overrideController";

        /// <summary>
        /// Where the model sits under the holder. The rig is a whole body with the arms at eye height above its feet,
        /// so its root goes 1.6 m below the eye (the old game's own prefab used the same offset).
        /// </summary>
        static readonly Vector3 RifleModelOffset = new Vector3(0.016f, -1.602f, 0.294f);
        static readonly Vector3 RifleHipPosition = Vector3.zero;
        static readonly Vector3 RifleAimPosition = new Vector3(0f, 0f, 0.05f);
        /// <summary>Muzzle flash position on the holder: just past the barrel tip in the idle pose.</summary>
        static readonly Vector3 RifleMuzzle = new Vector3(0.05f, -0.03f, 0.80f);

        /// <summary>Set by Build(): the player builder uses the real rifle when it is there.</summary>
        static WeaponKit rifleKit;

        static WeaponKit EnsureRifleKit(WeaponDefinition rifleData, StringBuilder log)
        {
            foreach (var f in new[] { RifleArt + "/Model", RifleArt + "/Textures", RifleArt + "/Materials", RifleGenerated, WeaponPrefabDir, "Assets/_Game/Animation/Controllers" })
                EnsureFolder(f);

            // the model and its clips
            string model = RifleArt + "/Model/FP_HuntingRifle.fbx";
            if (!File.Exists(model))
            {
                string staged = $"{RifleSrc}/HuntingRifle/FP_HuntingRifle.fbx";
                if (!File.Exists(staged)) { log.AppendLine("rifle: model neither in Art nor in staging, keeping the grey rifle"); return null; }
                AssetDatabase.CopyAsset(staged, model);
                log.AppendLine("copied " + model);
            }
            var mi = (ModelImporter)AssetImporter.GetAtPath(model);
            var clipSettings = mi.clipAnimations.Length > 0 ? mi.clipAnimations : mi.defaultClipAnimations;
            bool reimport = mi.materialImportMode != ModelImporterMaterialImportMode.None || mi.animationType != ModelImporterAnimationType.Generic
                            || mi.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel || mi.clipAnimations.Length == 0
                            || clipSettings.Any(c => c.loopTime != c.name.Contains("Idle"));
            if (reimport)
            {
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
                mi.animationType = ModelImporterAnimationType.Generic;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                foreach (var c in clipSettings) c.loopTime = c.name.Contains("Idle");
                mi.clipAnimations = clipSettings;
                mi.SaveAndReimport();
            }
            var clips = AssetDatabase.LoadAllAssetsAtPath(model).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToArray();
            AnimationClip C(string part) => clips.FirstOrDefault(c => c.name == part) ?? clips.FirstOrDefault(c => c.name.EndsWith("_" + part));
            var roles = new Dictionary<string, AnimationClip>
            {
                [WeaponViewModel.Ready] = C("Ready"), [WeaponViewModel.Idle] = C("Idle"), [WeaponViewModel.Fire] = C("Fire"),
                [WeaponViewModel.Reload] = C("Reload"), [WeaponViewModel.ReloadEmpty] = C("ReloadEmpty"),
            };

            // textures (first person: always on screen but small; 1024 on a phone)
            Texture2D Tex(string staged, string name, bool normal, bool sRGB, int max)
            {
                string to = $"{RifleArt}/Textures/{name}";
                if (!File.Exists(to))
                {
                    string from = $"{RifleSrc}/{staged}";
                    if (!File.Exists(from)) { log.AppendLine($"WARNING: rifle texture {name} missing"); return null; }
                    AssetDatabase.CopyAsset(from, to);
                }
                var ti = (TextureImporter)AssetImporter.GetAtPath(to);
                var android = ti.GetPlatformTextureSettings("Android");
                var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (ti.textureType != type || ti.sRGBTexture != sRGB || ti.maxTextureSize != max || !android.overridden || android.format != TextureImporterFormat.ASTC_6x6)
                {
                    ti.textureType = type; ti.sRGBTexture = sRGB; ti.maxTextureSize = max; ti.mipmapEnabled = true;
                    ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                        { name = "Android", overridden = true, maxTextureSize = max, format = TextureImporterFormat.ASTC_6x6, compressionQuality = 50 });
                    ti.SaveAndReimport();
                }
                return AssetDatabase.LoadAssetAtPath<Texture2D>(to);
            }
            Tex("HuntingRifle/Texture/1.png", "T_HuntingRifle_Albedo.png", false, true, 1024);
            var bodyNormal = Tex("HuntingRifle/Materials/HuntingRifle_01_NRM.png", "T_HuntingRifle_Normal.png", true, false, 1024);
            var bodyMask = Tex("HuntingRifle/Materials/HuntingRifle_01_MASK.png", "T_HuntingRifle_Mask.png", false, false, 1024);
            var magAlbedo = Tex("HuntingRifle/Materials/HuntingRifle_Clip.png", "T_HuntingRifle_Magazine_Albedo.png", false, true, 512);
            var magNormal = Tex("HuntingRifle/Materials/HuntingRifle_Clip_NRM.png", "T_HuntingRifle_Magazine_Normal.png", true, false, 512);
            var magMask = Tex("HuntingRifle/Materials/HuntingRifle_Clip_MASK.png", "T_HuntingRifle_Magazine_Mask.png", false, false, 512);
            Tex("_FP_Arms/Materials/Arm_Standard.png", "T_FPArms_Albedo.png", false, true, 1024);
            var bodyAlbedo = GradeRifleAlbedo(log);

            // URP Lit; the pack's mask maps hold metal in red, ambient occlusion in green and smoothness in alpha
            Material Lit(string name, Texture2D albedo, Texture2D normal, Texture2D mask, float smoothness, Color tint)
            {
                string p = $"{RifleArt}/Materials/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name }; AssetDatabase.CreateAsset(m, p); log.AppendLine("created " + p); }
                m.SetTexture("_BaseMap", albedo);
                m.SetColor("_BaseColor", tint);
                if (normal) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
                if (mask)
                {
                    m.SetTexture("_MetallicGlossMap", mask); m.EnableKeyword("_METALLICSPECGLOSSMAP");
                    m.SetTexture("_OcclusionMap", mask); m.EnableKeyword("_OCCLUSIONMAP");
                }
                else m.SetFloat("_Metallic", 0f);
                m.SetFloat("_Smoothness", smoothness);
                m.SetFloat("_ReceiveShadows", 0f); m.EnableKeyword("_RECEIVE_SHADOWS_OFF");   // overlay camera: no shadow map of its own
                EditorUtility.SetDirty(m);
                return m;
            }
            var body = Lit("M_HuntingRifle", bodyAlbedo, bodyNormal, bodyMask, 0.62f, Color.white);   // oiled wood and worn bluing, not polish
            var magazine = Lit("M_HuntingRifle_Magazine", magAlbedo, magNormal, magMask, 0.7f, Color.white);
            var arms = Lit("M_FPArms", PaintRifleSleevesLater, null, null, 0.3f, Color.white);
            var scopeBody = Lit("M_RifleScope", null, null, null, 0.32f, new Color(0.045f, 0.045f, 0.05f));   // matte black anodised
            scopeBody.SetFloat("_Metallic", 0.3f);
            var scopeLens = Lit("M_RifleScopeLens", null, null, null, 0.95f, new Color(0.03f, 0.05f, 0.08f));

            var animations = WeaponKitBuilder.EnsureOverride(RifleOverridePath, roles, log);
            var prefab = BuildRifleViewModelPrefab(model, roles, animations, body, magazine, arms, scopeBody, scopeLens, log);

            var kit = EnsureAsset<WeaponKit>(WeaponsDataDir + "/Kit_HuntingRifle.asset", log);
            kit.definition = rifleData;
            kit.viewModel = prefab;
            kit.animations = animations;
            kit.modelOffset = RifleModelOffset;
            kit.hipPosition = RifleHipPosition;
            kit.aimPosition = RifleAimPosition;
            kit.muzzle = RifleMuzzle;
            kit.motion ??= new WeaponMotionSettings();
            EditorUtility.SetDirty(kit);
            log.AppendLine($"rifle kit: {clips.Length} clips; " + string.Join(", ", roles.Select(kv => $"{kv.Key} {(kv.Value ? "ok" : "MISSING")}")));
            return kit;
        }

        // the arms' texture is painted when the trimmed arms are known (BuildRifleViewModelPrefab); a placeholder until then
        static Texture2D PaintRifleSleevesLater => AssetDatabase.LoadAssetAtPath<Texture2D>(RifleArt + "/Textures/T_FPArms_Sleeve.png")
                                                   ?? AssetDatabase.LoadAssetAtPath<Texture2D>(RifleArt + "/Textures/T_FPArms_Albedo.png");

        /// <summary>
        /// The pack's colour map with the wood warmed and deepened and the steel darkened toward bluing: in the
        /// meadow's light the stock read grey and the whole rifle like bare metal. Wood and metal come from the mask
        /// (metal in red). Written once at 2048 (imported at 1024); delete the file to make it again.
        /// </summary>
        static Texture2D GradeRifleAlbedo(StringBuilder log)
        {
            string outPath = RifleArt + "/Textures/T_HuntingRifle_AlbedoGraded.png";
            if (!File.Exists(outPath))
            {
                var albedo = new Texture2D(2, 2, TextureFormat.RGBA32, false); albedo.LoadImage(File.ReadAllBytes(RifleArt + "/Textures/T_HuntingRifle_Albedo.png"));
                var mask = new Texture2D(2, 2, TextureFormat.RGBA32, false); mask.LoadImage(File.ReadAllBytes(RifleArt + "/Textures/T_HuntingRifle_Mask.png"));
                const int n = 2048;
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                    Color c = albedo.GetPixelBilinear(u, v);
                    float metal = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.6f, mask.GetPixelBilinear(u, v).r));
                    float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                    // wood: more colour, warmer, a little darker (walnut, oiled)
                    Color wood = new Color(lum + (c.r - lum) * 1.6f, lum + (c.g - lum) * 1.6f, lum + (c.b - lum) * 1.6f);
                    wood = new Color(wood.r * 1.02f, wood.g * 0.80f, wood.b * 0.58f) * 0.92f;
                    // steel: darker, a cool blue-black
                    Color steel = new Color(lum * 0.62f, lum * 0.66f, lum * 0.74f);
                    var o = Color.Lerp(wood, steel, metal);
                    o.a = 1f;
                    px[y * n + x] = o;
                }
                Object.DestroyImmediate(albedo); Object.DestroyImmediate(mask);
                var tex = new Texture2D(n, n, TextureFormat.RGB24, false);
                tex.SetPixels(px);
                File.WriteAllBytes(outPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(outPath);
                WeaponKitBuilder.ImportColour(outPath, 1024);
                log.AppendLine("rifle: graded colour map written");
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        }

        /// <summary>
        /// The first-person model as a prefab: the rig with our materials, a scout scope on the rifle's body bone,
        /// jacket sleeves on the arms, and every mesh cut down to what the camera sees in any of the gun's clips.
        /// </summary>
        static GameObject BuildRifleViewModelPrefab(string modelPath, Dictionary<string, AnimationClip> roles, AnimatorOverrideController animations,
            Material body, Material magazine, Material arms, Material scopeBody, Material scopeLens, StringBuilder log)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                go.name = "VM_HuntingRifle";
                var skins = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);

                // what the camera can see over every clip: 55 degree view, phones up to 21:9, 14 degrees spare for hand motion
                var visible = WeaponKitBuilder.SampleVisibility(go, roles.Values, new[] { RifleHipPosition, RifleAimPosition }, RifleModelOffset, 55f, 2.4f, 14f);
                var sleeves = new List<(Mesh, WeaponKitBuilder.SleeveMap)>();
                int before = 0, after = 0;
                foreach (var s in skins)
                {
                    var full = s.sharedMesh;
                    before += full.triangles.Length / 3;
                    string meshName = "FP_" + s.name.Replace(',', '_').Replace('.', '_');
                    var trimmed = WeaponKitBuilder.TrimHidden(full, visible[s], meshName) ?? Object.Instantiate(full);
                    s.sharedMesh = trimmed;
                    var map = s.name.StartsWith("Arm") ? WeaponKitBuilder.MapSleeve(s) : null;
                    WeaponKitBuilder.ThickenSleeve(trimmed, map);
                    after += trimmed.triangles.Length / 3;
                    s.sharedMesh = WeaponKitBuilder.SaveMesh(trimmed, $"{RifleGenerated}/{meshName}.asset");   // trimmed may be gone after this
                    if (map != null) sleeves.Add((s.sharedMesh, map));
                }
                log.AppendLine($"rifle: {before} triangles, {after} after removing what the camera never sees");

                // jacket sleeves painted into the arms' colour map (olive canvas)
                string sleevePath = RifleArt + "/Textures/T_FPArms_Sleeve.png";
                WeaponKitBuilder.PaintSleeves(RifleArt + "/Textures/T_FPArms_Albedo.png", sleeves, sleevePath, 2048, new Color(0.36f, 0.34f, 0.25f));
                WeaponKitBuilder.ImportColour(sleevePath, 1024);
                arms.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(sleevePath));
                EditorUtility.SetDirty(arms);

                foreach (var s in skins)
                {
                    s.sharedMaterials = s.name.StartsWith("Arm") ? new[] { arms } : new[] { body, magazine };
                    s.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    s.receiveShadows = false;
                    s.updateWhenOffscreen = true;   // never culled by its own bounds
                    s.skinnedMotionVectors = false;
                }

                // the scout scope rides on the rifle's body bone
                var weaponBone = go.GetComponentsInChildren<Transform>(true).First(t => t.name == "Weapon");
                var scope = new GameObject("Scope");
                scope.transform.SetParent(weaponBone, false);
                float boneScale = weaponBone.lossyScale.x;
                scope.transform.localPosition = new Vector3(-0.28f, 0f, 0.058f) / boneScale;   // eyepiece end, 5.8 cm over the bore
                scope.transform.localRotation = Quaternion.LookRotation(new Vector3(-1f, 0f, 0f), new Vector3(0f, 0f, 1f));   // +z along the barrel, +y up
                scope.transform.localScale = Vector3.one / boneScale;
                scope.AddComponent<MeshFilter>().sharedMesh = WeaponKitBuilder.SaveMesh(BuildScoutScope(), $"{RifleGenerated}/FP_RifleScope.asset");
                var sr = scope.AddComponent<MeshRenderer>();
                sr.sharedMaterials = new[] { scopeBody, scopeLens };
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sr.receiveShadows = false;

                var animator = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
                animator.runtimeAnimatorController = animations;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (roles.TryGetValue(WeaponViewModel.Idle, out var idle) && idle) idle.SampleAnimation(go, 0f);   // rest in the idle pose
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                return PrefabUtility.SaveAsPrefabAsset(go, $"{WeaponPrefabDir}/VM_HuntingRifle.prefab");
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>
        /// A 1-inch scout scope, 26 cm long, in metres: +z toward the muzzle from the eyepiece, +y up. Eyepiece and
        /// objective bells, the tube, elevation and windage turrets, two rings on bases down to the handguard (which
        /// slopes, so the front base is taller). Submesh 0 is the matte black body, 1 the two lenses. About 900 triangles.
        /// </summary>
        static Mesh BuildScoutScope()
        {
            var k = new ScopeMesh();
            Vector3 z = Vector3.forward, x = Vector3.right, y = Vector3.up;
            const int seg = 20;
            k.Lathe(0, Vector3.zero, z, x, new[]
            {
                (0.003f, 0.0128f), (0f, 0.0128f), (0f, 0.0135f), (0.004f, 0.0150f), (0.050f, 0.0150f), (0.058f, 0.0127f),
                (0.185f, 0.0127f), (0.215f, 0.0172f), (0.255f, 0.0178f), (0.262f, 0.0170f), (0.262f, 0.0160f), (0.258f, 0.0160f),
            }, seg);
            k.Disk(1, new Vector3(0f, 0f, 0.003f), -z, x, 0.0128f, seg);   // eyepiece lens
            k.Disk(1, new Vector3(0f, 0f, 0.258f), z, x, 0.0160f, seg);    // objective lens

            // turret housing, elevation turret on top, windage on the right
            k.Box(0, new Vector3(0f, 0f, 0.12f), new Vector3(0.0145f, 0.0145f, 0.016f));
            k.Lathe(0, new Vector3(0f, 0f, 0.12f), y, x, new[] { (0.0140f, 0.0085f), (0.0235f, 0.0085f), (0.0245f, 0.0078f) }, 14);
            k.Disk(0, new Vector3(0f, 0.0245f, 0.12f), y, x, 0.0078f, 14);
            k.Lathe(0, new Vector3(0f, 0f, 0.12f), x, z, new[] { (0.0140f, 0.0075f), (0.0225f, 0.0075f), (0.0235f, 0.0068f) }, 14);
            k.Disk(0, new Vector3(0.0235f, 0f, 0.12f), x, z, 0.0068f, 14);

            // rings and their bases (to the handguard: 2.9 cm below the axis at the rear ring, 4.0 cm at the front)
            foreach (var (at, drop) in new[] { (0.075f, 0.032f), (0.175f, 0.043f) })
            {
                k.Lathe(0, new Vector3(0f, 0f, at), z, x, new[] { (-0.006f, 0.0127f), (-0.006f, 0.0152f), (0.006f, 0.0152f), (0.006f, 0.0127f) }, seg);
                float top = -0.011f, bottom = -drop;
                k.Box(0, new Vector3(0f, (top + bottom) * 0.5f, at), new Vector3(0.0065f, (top - bottom) * 0.5f, 0.0055f));
                k.Box(0, new Vector3(0f, bottom + 0.002f, at), new Vector3(0.0095f, 0.002f, 0.009f));   // the clamp foot
            }
            return k.ToMesh("FP_RifleScope");
        }

        /// <summary>A small mesh maker for turned and boxy parts (front faces where cross(b - a, c - a) points).</summary>
        sealed class ScopeMesh
        {
            readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
            readonly List<Vector2> uv = new List<Vector2>();
            readonly List<int>[] subs = { new List<int>(), new List<int>() };

            void Tri(int s, int a, int b, int c) { subs[s].Add(a); subs[s].Add(b); subs[s].Add(c); }

            /// <summary>Turns a (height, radius) profile around the axis; each step has its own normals (crisp edges).</summary>
            public void Lathe(int s, Vector3 origin, Vector3 axis, Vector3 side, (float h, float r)[] profile, int segs)
            {
                var up = Vector3.Cross(axis, side).normalized;
                for (int p = 0; p < profile.Length - 1; p++)
                {
                    var (h0, r0) = profile[p]; var (h1, r1) = profile[p + 1];
                    var slope = new Vector2(h1 - h0, -(r1 - r0)).normalized;   // radial and axial parts of the normal
                    int start = v.Count;
                    for (int i = 0; i <= segs; i++)
                    {
                        float a = i / (float)segs * Mathf.PI * 2f;
                        var radial = side * Mathf.Cos(a) + up * Mathf.Sin(a);
                        var normal = (radial * slope.x + axis * slope.y).normalized;
                        v.Add(origin + axis * h0 + radial * r0); n.Add(normal); uv.Add(new Vector2(i / (float)segs, p));
                        v.Add(origin + axis * h1 + radial * r1); n.Add(normal); uv.Add(new Vector2(i / (float)segs, p + 1));
                    }
                    for (int i = 0; i < segs; i++)
                    {
                        int a0 = start + i * 2, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3;
                        Tri(s, a0, b0, a1); Tri(s, a1, b0, b1);
                    }
                }
            }

            /// <summary>A flat disc facing along facing.</summary>
            public void Disk(int s, Vector3 centre, Vector3 facing, Vector3 side, float radius, int segs)
            {
                var axis = facing.normalized;
                var up = Vector3.Cross(axis, side).normalized;
                int c = v.Count;
                v.Add(centre); n.Add(axis); uv.Add(new Vector2(0.5f, 0.5f));
                for (int i = 0; i <= segs; i++)
                {
                    float a = i / (float)segs * Mathf.PI * 2f;
                    var radial = side * Mathf.Cos(a) + up * Mathf.Sin(a);
                    v.Add(centre + radial * radius); n.Add(axis); uv.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
                }
                for (int i = 0; i < segs; i++) Tri(s, c, c + 1 + i, c + 2 + i);
            }

            public void Box(int s, Vector3 centre, Vector3 half)
            {
                void Face(Vector3 normal, Vector3 u, Vector3 w)
                {
                    var c = centre + Vector3.Scale(normal, half);
                    u = Vector3.Scale(u, half); w = Vector3.Scale(w, half);
                    int i = v.Count;
                    v.Add(c - u - w); v.Add(c + u - w); v.Add(c + u + w); v.Add(c - u + w);
                    for (int k = 0; k < 4; k++) n.Add(normal);
                    uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(Vector2.one); uv.Add(Vector2.up);
                    Tri(s, i, i + 1, i + 3); Tri(s, i + 1, i + 2, i + 3);
                }
                Face(Vector3.right, Vector3.up, Vector3.forward); Face(Vector3.left, Vector3.forward, Vector3.up);
                Face(Vector3.up, Vector3.forward, Vector3.right); Face(Vector3.down, Vector3.right, Vector3.forward);
                Face(Vector3.forward, Vector3.right, Vector3.up); Face(Vector3.back, Vector3.up, Vector3.right);
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv);
                m.subMeshCount = subs.Length;
                for (int s = 0; s < subs.Length; s++) m.SetTriangles(subs[s], s);
                m.RecalculateBounds(); m.RecalculateTangents();
                return m;
            }
        }

        /// <summary>
        /// The holder (moved by WeaponController: hip, aim, recoil), the motion transform under it (WeaponMotion: breathing,
        /// steps, look lag) and the kit's model under that, on the first-person layer.
        /// </summary>
        static (Transform holder, Transform mount, Animator animator) BuildKitViewModel(Transform pivot, WeaponKit kit, int layer)
        {
            var holder = new GameObject("Rifle").transform;
            holder.SetParent(pivot, false);
            var mount = new GameObject("Motion").transform;
            mount.SetParent(holder, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(kit.viewModel);
            model.name = "ViewModel";
            model.transform.SetParent(mount, false);
            model.transform.localPosition = kit.modelOffset;
            foreach (var t in holder.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            return (holder, mount, model.GetComponentInChildren<Animator>());
        }
    }
}
