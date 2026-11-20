using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Animals;
using Game.Core.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Editor
{
    /// <summary>
    /// The real wolf through the asset pipeline (plan section 15): the old game's "Wolf Realistic" model and its
    /// animations are copied from staging into the final folders, textures capped at 2048, materials rebuilt on the
    /// project's URP animal shader, an animator controller built from the clips, levels of detail set, and hit zones
    /// attached to the bones. Staging stays untouched.
    /// </summary>
    public static partial class GrayboxR01Builder
    {
        const string WolfSrc = "Assets/_Incoming/OldGame/Animals/_Predators/Wolf";
        const string WolfArt = "Assets/_Game/Art/Animals/Predators/Wolf";
        const string WolfAnimDir = "Assets/_Game/Animation/DogFamily/Wolf";
        const string WolfControllerPath = "Assets/_Game/Animation/Controllers/Wolf.controller";
        const string RealWolfPrefabPath = "Assets/_Game/Prefabs/Animals/Wolf.prefab";
        const string WolfMotionNode = "CG";

        // file (under Animations v2), looping
        static readonly (string file, bool loop)[] WolfClips =
        {
            ("Idles/Wolfr_Idle", true), ("Idles/Wolfr_Idle Look Left", false), ("Idles/Wolfr_Idle Look Right", false), ("Idles/Wolfr_Idle_Smell", false),
            ("Locomotion/Wolfr_Walk_Forward", true), ("Locomotion/Wolfr_Trot_Forward", true), ("Locomotion/Wolfr_Run_Forward", true),
            ("Stances/Sneak/Wolfr_Sneak_Forward", true), ("Stances/Sneak/Wolfr_Sneak_Idle", true),
            ("Actions/Wolfr_Eat", true), ("Actions/Wolfr_Howl", false), ("Actions/Wolfr_Lie_01", true),
            ("Stances/Combat/Wolfr_Idle_Combat", true), ("Attacks/Wolfr_Attack_JumpForward", false), ("Attacks/Wolfr_Attack_Bite_Forward", false),
            ("Damage/Wolfr_Damage_Front_L", false), ("Stances/Injured/Wolfr_Injured_Walk_Forward", true), ("Stances/Injured/Wolfr_Injured_Idle", true),
            ("Deaths/Wolfr_Death_01", false),
        };

        /// <summary>Builds the real wolf prefab, or returns null when the source model is not in staging.</summary>
        static GameObject EnsureRealWolf(AnimalBehaviourSettings behaviour, Material blood, StringBuilder log)
        {
            string srcModel = WolfSrc + "/Models/Wolf Realistic.fbx";
            if (!File.Exists(srcModel)) { log.AppendLine("real wolf: source model not in staging, using the placeholder"); return null; }
            foreach (var f in new[] { WolfArt + "/Model", WolfArt + "/Textures", WolfArt + "/Materials", WolfAnimDir, "Assets/_Game/Animation/Controllers" }) EnsureFolder(f);

            // model: Generic rig, root bone CG carries the motion, no materials or animation from the file
            string model = WolfArt + "/Model/Wolf_Realistic.fbx";
            if (!File.Exists(model)) { AssetDatabase.CopyAsset(srcModel, model); log.AppendLine("copied " + model); }
            var mi = (ModelImporter)AssetImporter.GetAtPath(model);
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.motionNodeName = WolfMotionNode;
            mi.SaveAndReimport();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(model).OfType<Avatar>().FirstOrDefault();

            // textures, capped at 2048 (plan budget: predators 2048), ASTC 6x6 on Android
            var albedo = CopyTexture("Wolf_Body_Albedo_Gray.png", "T_Wolf_Body_Albedo.png", TextureImporterType.Default, true, log);
            var normal = CopyTexture("Wolf_Body_Normal.png", "T_Wolf_Body_Normal.png", TextureImporterType.NormalMap, false, log);
            var furMap = CopyTexture("Wolf_Fur_Map (A H ID RT).png", "T_Wolf_Fur_Map.png", TextureImporterType.Default, false, log);
            var furNormal = CopyTexture("Wolf_Fur_Normal.png", "T_Wolf_Fur_Normal.png", TextureImporterType.NormalMap, false, log);

            // materials on the project's animal shader
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Game/Art/Shaders/Animal.shader");
            Material Mat(string name, System.Action<Material> setup)
            {
                string p = $"{WolfArt}/Materials/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (!m) { m = new Material(shader) { name = name }; AssetDatabase.CreateAsset(m, p); log.AppendLine("created " + p); }
                m.shader = shader;
                setup(m);
                EditorUtility.SetDirty(m);
                return m;
            }
            var body = Mat("M_Wolf_Body", m => { m.SetTexture("_BaseMap", albedo); m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", 1.2f); m.SetFloat("_Smoothness", 0.12f); });
            var fur = Mat("M_Wolf_Fur", m =>
            {
                m.SetTexture("_BaseMap", albedo); m.SetTexture("_FurMap", furMap); m.SetTexture("_FurNormal", furNormal);
                m.SetFloat("_Fur", 1f); m.EnableKeyword("_FUR"); m.SetFloat("_Cutoff", 0.5f); m.SetFloat("_Cull", 0f);
                m.SetColor("_RootTint", new Color(0.37f, 0.25f, 0.25f)); m.SetFloat("_FurNormalScale", 0.5f); m.SetFloat("_Smoothness", 0.08f);
                m.renderQueue = 2450;   // alpha-tested
            });
            var claws = Mat("M_Wolf_Claws", m => { m.SetTexture("_BaseMap", albedo); m.SetFloat("_Smoothness", 0.3f); });
            var eyes = Mat("M_Wolf_Eyes", m => { m.SetTexture("_BaseMap", Texture2D.whiteTexture); m.SetColor("_BaseColor", new Color(0.09f, 0.06f, 0.03f)); m.SetFloat("_Smoothness", 0.9f); m.SetFloat("_Wrap", 0f); });
            var mouth = Mat("M_Wolf_Mouth", m => { m.SetTexture("_BaseMap", Texture2D.whiteTexture); m.SetColor("_BaseColor", new Color(0.42f, 0.2f, 0.2f)); m.SetFloat("_Smoothness", 0.4f); });
            var whiskers = Mat("M_Wolf_Whiskers", m => { m.SetTexture("_BaseMap", Texture2D.whiteTexture); m.SetColor("_BaseColor", new Color(0.75f, 0.73f, 0.7f)); m.SetFloat("_Cull", 0f); });

            // animations: copied clips, root motion extracted from CG (the agent moves the wolf), loop flags set
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var (file, loop) in WolfClips)
            {
                string name = Path.GetFileName(file);
                string src = Directory.GetFiles(Path.GetDirectoryName($"{WolfSrc}/Animations v2/{file}"), name + ".*")
                                      .FirstOrDefault(p => p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))?.Replace('\\', '/');
                if (src == null) { log.AppendLine("WARNING: missing clip " + file); continue; }
                string dst = $"{WolfAnimDir}/{name}.fbx";
                if (!File.Exists(dst)) AssetDatabase.CopyAsset(src, dst);
                var ai = (ModelImporter)AssetImporter.GetAtPath(dst);
                ai.materialImportMode = ModelImporterMaterialImportMode.None;
                ai.animationType = ModelImporterAnimationType.Generic;
                ai.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                ai.sourceAvatar = avatar;
                ai.motionNodeName = WolfMotionNode;
                ai.importAnimation = true;
                var defs = ai.defaultClipAnimations;
                foreach (var c in defs)
                {
                    c.name = name;
                    c.loopTime = loop;
                    c.lockRootRotation = true;      // turns stay in the pose; the agent turns the wolf
                    c.lockRootHeightY = true;       // vertical motion stays in the pose
                    c.lockRootPositionXZ = false;   // forward motion extracted (and not applied): feet stay under the agent
                    c.keepOriginalPositionY = true;
                }
                ai.clipAnimations = defs;
                ai.SaveAndReimport();
                var clip = AssetDatabase.LoadAllAssetsAtPath(dst).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
                if (clip) clips[name] = clip;
            }
            log.AppendLine($"real wolf: {clips.Count} animation clips");

            var controller = BuildWolfController(clips, log);
            return BuildRealWolfPrefab(model, controller, behaviour, blood, body, fur, claws, eyes, mouth, whiskers, log);
        }

        static Texture2D CopyTexture(string srcName, string dstName, TextureImporterType type, bool sRGB, StringBuilder log)
        {
            string src = $"{WolfSrc}/Materials & Textures/Realistic/{srcName}";
            string dst = $"{WolfArt}/Textures/{dstName}";
            if (!File.Exists(dst)) { AssetDatabase.CopyAsset(src, dst); log.AppendLine("copied " + dst); }
            var ti = (TextureImporter)AssetImporter.GetAtPath(dst);
            ti.textureType = type;
            ti.sRGBTexture = type == TextureImporterType.NormalMap || sRGB;
            ti.maxTextureSize = 2048;
            ti.mipmapEnabled = true;
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android", overridden = true, maxTextureSize = 2048, format = TextureImporterFormat.ASTC_6x6,
                compressionQuality = 50
            });
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
        }

        static float RunClipSpeed = 8.5f;   // ground speed of the run clip, measured while building the controller

        static AnimatorController BuildWolfController(Dictionary<string, AnimationClip> clips, StringBuilder log)
        {
            if (File.Exists(WolfControllerPath)) AssetDatabase.DeleteAsset(WolfControllerPath);   // rebuilt from the clips each time
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(WolfControllerPath);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            // playback rate of the locomotion sets: above the run clip's own ground speed the legs cycle faster (no gallop clip)
            ctrl.AddParameter(new AnimatorControllerParameter { name = AnimalAnimator.MoveScaleParam, type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            var sm = ctrl.layers[0].stateMachine;
            AnimationClip C(string n) => clips.TryGetValue(n, out var c) ? c : null;
            // blend thresholds from each clip's real ground speed, so the feet match the agent's speed
            float Spd(string n, float fallback) { var c = C(n); float s = c ? c.averageSpeed.magnitude : 0f; return s > 0.2f ? s : fallback; }

            var loco = ctrl.CreateBlendTreeInController(AnimalAnimator.Locomotion, out var tree, 0);
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(C("Wolfr_Idle"), 0f);
            tree.AddChild(C("Wolfr_Walk_Forward"), Spd("Wolfr_Walk_Forward", 1.3f));
            tree.AddChild(C("Wolfr_Trot_Forward"), Spd("Wolfr_Trot_Forward", 3.8f));
            tree.AddChild(C("Wolfr_Run_Forward"), Spd("Wolfr_Run_Forward", 8.5f));
            sm.defaultState = loco;

            var injured = ctrl.CreateBlendTreeInController(AnimalAnimator.InjuredLocomotion, out var limp, 0);
            limp.blendParameter = "Speed";
            limp.useAutomaticThresholds = false;
            limp.AddChild(C("Wolfr_Injured_Idle"), 0f);
            limp.AddChild(C("Wolfr_Injured_Walk_Forward"), Spd("Wolfr_Injured_Walk_Forward", 1.1f));
            limp.AddChild(C("Wolfr_Run_Forward"), Spd("Wolfr_Run_Forward", 8.5f));
            foreach (var s in new[] { loco, injured }) { s.speedParameterActive = true; s.speedParameter = AnimalAnimator.MoveScaleParam; }
            RunClipSpeed = Spd("Wolfr_Run_Forward", 8.5f);

            void State(string name, string clip) { var s = sm.AddState(name); s.motion = C(clip); }
            State(AnimalAnimator.Eat, "Wolfr_Eat");
            State(AnimalAnimator.Alert, "Wolfr_Idle_Combat");
            State(AnimalAnimator.Rest, "Wolfr_Lie_01");
            State(AnimalAnimator.Death, "Wolfr_Death_01");
            State(AnimalAnimator.Attack, "Wolfr_Attack_JumpForward");
            State(AnimalAnimator.Hit, "Wolfr_Damage_Front_L");
            State("Howl", "Wolfr_Howl");
            State("Sniff", "Wolfr_Idle_Smell");
            State("Sneak", "Wolfr_Sneak_Forward");
            log.AppendLine($"wolf controller: walk {Spd("Wolfr_Walk_Forward", 1.3f):0.0} m/s, trot {Spd("Wolfr_Trot_Forward", 3.8f):0.0}, run {Spd("Wolfr_Run_Forward", 8.5f):0.0}");
            return ctrl;
        }

        static GameObject BuildRealWolfPrefab(string modelPath, AnimatorController controller, AnimalBehaviourSettings behaviour, Material blood,
            Material body, Material fur, Material claws, Material eyes, Material mouth, Material whiskers, StringBuilder log)
        {
            var root = new GameObject("Wolf");
            try
            {
                var agent = root.AddComponent<NavMeshAgent>();
                agent.speed = behaviour.walkSpeed; agent.radius = 0.35f; agent.height = 0.95f;
                agent.angularSpeed = 300f; agent.acceleration = 8f; agent.stoppingDistance = 0.2f;
                var rb = root.AddComponent<Rigidbody>(); rb.isKinematic = true;
                var senses = root.AddComponent<AnimalSenses>(); SetRefs(senses, ("settings", behaviour));
                var brain = root.AddComponent<AnimalBrain>(); SetRefs(brain, ("settings", behaviour), ("senses", senses));
                var health = root.AddComponent<AnimalHealth>(); SetRefs(health, ("settings", behaviour));
                SetRefs(brain, ("health", health));
                var bloodTrail = root.AddComponent<BloodTrailEmitter>(); SetRefs(bloodTrail, ("health", health), ("dropMaterial", blood));

                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
                model.name = "Model";
                model.transform.SetParent(root.transform, false);
                if (!model.TryGetComponent<Animator>(out var animator)) animator = model.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = true;          // extracted from the pose, then dropped by the sink:
                model.AddComponent<RootMotionSink>();     // the agent moves the wolf, the legs stay under it
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                var anim = root.AddComponent<AnimalAnimator>();
                SetRefs(anim, ("animator", animator), ("brain", brain));
                var animSo = new SerializedObject(anim);
                animSo.FindProperty("runClipSpeed").floatValue = RunClipSpeed;
                animSo.ApplyModifiedPropertiesWithoutUndo();

                // materials per part
                var parts = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).ToDictionary(r => r.name);
                foreach (var r in parts.Values)
                {
                    Material m = r.name.Contains("Fur") ? fur : r.name.Contains("Body") ? body : r.name.Contains("Claws") ? claws
                               : r.name.Contains("Eyes") ? eyes : r.name.Contains("Mouth") ? mouth : whiskers;
                    r.sharedMaterials = Enumerable.Repeat(m, r.sharedMesh ? r.sharedMesh.subMeshCount : 1).ToArray();
                    bool castShadows = r.name.Contains("Body") || r.name.Contains("Fur_LOD0") || r.name.Contains("Fur_LOD1");
                    r.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                Renderer P(string n) => parts.TryGetValue(n, out var r) ? r : null;
                Renderer[] Set(params string[] names) => names.Select(P).Where(r => r).ToArray();
                // the importer already adds an LODGroup for the _LOD0/_LOD1 mesh names; replace its levels with ours
                if (!model.TryGetComponent<LODGroup>(out var lod)) lod = model.AddComponent<LODGroup>();
                lod.SetLODs(new[]
                {
                    new LOD(0.25f, Set("Wolf Body_LOD0", "Wolf Fur_LOD0", "Wolf Claws", "Wolf Eyes", "Wolf Mouth", "Wolf Wiskers")),
                    new LOD(0.08f, Set("Wolf Body_LOD1", "Wolf Fur_LOD1", "Wolf Claws", "Wolf Eyes")),
                    new LOD(0.01f, Set("Wolf Body_LOD1", "Wolf Fur_LOD2")),
                });
                lod.RecalculateBounds();

                AddBoneHitboxes(model.transform);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, RealWolfPrefabPath);
                int lod0 = Set("Wolf Body_LOD0", "Wolf Fur_LOD0", "Wolf Claws", "Wolf Eyes", "Wolf Mouth", "Wolf Wiskers").Sum(r => ((SkinnedMeshRenderer)r).sharedMesh.triangles.Length / 3);
                log.AppendLine($"saved {RealWolfPrefabPath} (LOD0 {lod0} triangles)");
                return prefab;
            }
            finally { Object.DestroyImmediate(root); }
        }

        // Hit zones follow the bones, so the heart and lungs stay behind the front legs whatever the animation does.
        static void AddBoneHitboxes(Transform model)
        {
            var bones = model.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            Transform B(string n) => bones.TryGetValue(n, out var t) ? t : null;

            void Capsule(HitZone zone, Transform parent, Vector3 a, Vector3 b, float radius)
            {
                if (!parent) return;
                var go = new GameObject($"Hit_{zone}_{parent.name}");
                go.transform.SetParent(parent, false);
                go.transform.position = (a + b) * 0.5f;
                go.transform.rotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
                float s = Mathf.Max(0.0001f, go.transform.lossyScale.x);
                var c = go.AddComponent<CapsuleCollider>();
                c.direction = 1; c.radius = radius / s; c.height = ((b - a).magnitude + 2f * radius) / s; c.isTrigger = true;
                go.AddComponent<Hitbox>().zone = zone;
            }
            void Sphere(HitZone zone, Transform parent, Vector3 centre, float radius)
            {
                if (!parent) return;
                var go = new GameObject($"Hit_{zone}_{parent.name}");
                go.transform.SetParent(parent, false);
                go.transform.position = centre;
                var c = go.AddComponent<SphereCollider>();
                c.radius = radius / Mathf.Max(0.0001f, go.transform.lossyScale.x); c.isTrigger = true;
                go.AddComponent<Hitbox>().zone = zone;
            }

            var pelvis = B("Pelvis"); var spine1 = B("Spine1"); var spine2 = B("Spine2"); var neck = B("Neck"); var head = B("Head");
            if (pelvis && spine2)
            {
                Vector3 along = (spine2.position - pelvis.position).normalized;
                // from the haunches (behind the pelvis) to the chest
                Capsule(HitZone.Body, spine1, pelvis.position - along * 0.15f + Vector3.down * 0.06f, spine2.position + Vector3.down * 0.06f, 0.17f);
            }
            if (spine2 && head) Capsule(HitZone.Body, neck, spine2.position, head.position, 0.1f);
            if (spine2) Sphere(HitZone.Vital, spine2, spine2.position + new Vector3(0f, -0.13f, -0.03f), 0.12f);   // heart and lungs
            if (head) Sphere(HitZone.Head, head, head.position + new Vector3(0f, 0.01f, 0.1f), 0.12f);
            foreach (var side in new[] { "L", "R" })
            {
                Capsule(HitZone.Leg, B($"{side} UpperArm"), B($"{side} UpperArm")?.position ?? Vector3.zero, B($"{side} Forearm")?.position ?? Vector3.zero, 0.05f);
                Capsule(HitZone.Leg, B($"{side} Forearm"), B($"{side} Forearm")?.position ?? Vector3.zero, B($"{side} Hand")?.position ?? Vector3.zero, 0.04f);
                Capsule(HitZone.Leg, B($"{side} Thigh"), B($"{side} Thigh")?.position ?? Vector3.zero, B($"{side} Calf")?.position ?? Vector3.zero, 0.06f);
                Capsule(HitZone.Leg, B($"{side} Calf"), B($"{side} Calf")?.position ?? Vector3.zero, B($"{side} Foot")?.position ?? Vector3.zero, 0.045f);
            }
        }
    }
}
