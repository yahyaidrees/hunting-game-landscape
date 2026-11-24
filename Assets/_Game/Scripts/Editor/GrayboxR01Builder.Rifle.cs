using System.IO;
using System.Linq;
using System.Text;
using Game.Weapons;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The hunter's first-person rifle from the old project (HQ FPS Weapons, the owner's Asset Store licence):
    /// arms and hunting rifle copied from staging, materials rebuilt on URP Lit, an animator for draw, idle, fire
    /// (with the bolt cycle) and reload. Replaces the grey block rifle; staging stays untouched.
    /// </summary>
    public static partial class GrayboxR01Builder
    {
        const string RifleSrc = "Assets/_Incoming/OldGame/Props/Weapons/HQFPSWeapons";
        const string RifleArt = "Assets/_Game/Art/Weapons/HuntingRifle";
        const string RifleControllerPath = "Assets/_Game/Animation/Controllers/FP_HuntingRifle.controller";

        public sealed class RifleAssets
        {
            public GameObject model;
            public AnimatorController controller;
            public Material body, magazine, arms;
        }

        static RifleAssets EnsureRifleAssets(WeaponDefinition rifleData, StringBuilder log)
        {
            string srcModel = $"{RifleSrc}/HuntingRifle/FP_HuntingRifle.fbx";
            if (!File.Exists(srcModel)) { log.AppendLine("rifle: source not in staging, keeping the grey rifle"); return null; }
            foreach (var f in new[] { RifleArt + "/Model", RifleArt + "/Textures", RifleArt + "/Materials", "Assets/_Game/Animation/Controllers" }) EnsureFolder(f);

            string model = RifleArt + "/Model/FP_HuntingRifle.fbx";
            if (!File.Exists(model)) { AssetDatabase.CopyAsset(srcModel, model); log.AppendLine("copied " + model); }
            var mi = (ModelImporter)AssetImporter.GetAtPath(model);
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            var clips = mi.clipAnimations.Length > 0 ? mi.clipAnimations : mi.defaultClipAnimations;
            foreach (var c in clips) c.loopTime = c.name.Contains("Idle");
            mi.clipAnimations = clips;
            mi.SaveAndReimport();

            Texture2D Tex(string src, string dst, bool normal, bool sRGB, int max)
            {
                string from = $"{RifleSrc}/{src}", to = $"{RifleArt}/Textures/{dst}";
                if (!File.Exists(to)) { AssetDatabase.CopyAsset(from, to); log.AppendLine("copied " + to); }
                var ti = (TextureImporter)AssetImporter.GetAtPath(to);
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = sRGB;
                ti.maxTextureSize = max;
                ti.mipmapEnabled = true;
                ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                { name = "Android", overridden = true, maxTextureSize = max, format = TextureImporterFormat.ASTC_6x6, compressionQuality = 50 });
                ti.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Texture2D>(to);
            }
            // first person: always on screen, but small; 1024 is plenty on a phone
            var bodyAlbedo = Tex("HuntingRifle/Texture/1.png", "T_HuntingRifle_Albedo.png", false, true, 1024);
            var bodyNormal = Tex("HuntingRifle/Materials/HuntingRifle_01_NRM.png", "T_HuntingRifle_Normal.png", true, false, 1024);
            var bodyMask = Tex("HuntingRifle/Materials/HuntingRifle_01_MASK.png", "T_HuntingRifle_Mask.png", false, false, 1024);
            var magAlbedo = Tex("HuntingRifle/Materials/HuntingRifle_Clip.png", "T_HuntingRifle_Magazine_Albedo.png", false, true, 512);
            var magNormal = Tex("HuntingRifle/Materials/HuntingRifle_Clip_NRM.png", "T_HuntingRifle_Magazine_Normal.png", true, false, 512);
            var magMask = Tex("HuntingRifle/Materials/HuntingRifle_Clip_MASK.png", "T_HuntingRifle_Magazine_Mask.png", false, false, 512);
            var armsAlbedo = Tex("_FP_Arms/Materials/Arm_Standard.png", "T_FPArms_Albedo.png", false, true, 1024);

            // URP Lit; the pack's mask maps hold metal in red, ambient occlusion in green and smoothness in alpha
            Material Lit(string name, Texture2D albedo, Texture2D normal, Texture2D mask, float smoothness)
            {
                string p = $"{RifleArt}/Materials/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name }; AssetDatabase.CreateAsset(m, p); log.AppendLine("created " + p); }
                m.SetTexture("_BaseMap", albedo);
                m.SetColor("_BaseColor", Color.white);
                if (normal) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
                if (mask)
                {
                    m.SetTexture("_MetallicGlossMap", mask); m.EnableKeyword("_METALLICSPECGLOSSMAP");
                    m.SetTexture("_OcclusionMap", mask); m.EnableKeyword("_OCCLUSIONMAP");
                    m.SetFloat("_Smoothness", smoothness);
                }
                else { m.SetFloat("_Metallic", 0f); m.SetFloat("_Smoothness", smoothness); }
                m.SetFloat("_ReceiveShadows", 0f); m.EnableKeyword("_RECEIVE_SHADOWS_OFF");   // overlay camera: no shadow map of its own
                EditorUtility.SetDirty(m);
                return m;
            }
            var r = new RifleAssets
            {
                body = Lit("M_HuntingRifle", bodyAlbedo, bodyNormal, bodyMask, 1f),
                magazine = Lit("M_HuntingRifle_Magazine", magAlbedo, magNormal, magMask, 1f),
                arms = Lit("M_FPArms", armsAlbedo, null, null, 0.35f),
                model = AssetDatabase.LoadAssetAtPath<GameObject>(model),
            };
            r.controller = BuildRifleController(model, rifleData, log);
            return r;
        }

        static AnimatorController BuildRifleController(string modelPath, WeaponDefinition rifleData, StringBuilder log)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToArray();
            AnimationClip C(string part) => clips.FirstOrDefault(c => c.name == part) ?? clips.FirstOrDefault(c => c.name.EndsWith("_" + part));

            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(RifleControllerPath);
            if (ctrl)
            {
                var old = ctrl.layers[0].stateMachine;
                foreach (var s in old.states) old.RemoveState(s.state);
            }
            else ctrl = AnimatorController.CreateAnimatorControllerAtPath(RifleControllerPath);
            var sm = ctrl.layers[0].stateMachine;

            AnimatorState State(string name, AnimationClip clip, float speed = 1f)
            {
                var s = sm.AddState(name);
                s.motion = clip;
                s.speed = speed;
                return s;
            }
            var idle = State(WeaponViewModel.Idle, C("Idle"));
            sm.defaultState = idle;
            var ready = State(WeaponViewModel.Ready, C("Ready"));
            var fire = State(WeaponViewModel.Fire, C("Fire"));
            // reloads are stretched to the rifle's real reload time
            var reloadClip = C("Reload");
            var emptyClip = C("ReloadEmpty");
            var reload = State(WeaponViewModel.Reload, reloadClip, reloadClip ? reloadClip.length / Mathf.Max(0.1f, rifleData.reloadSeconds) : 1f);
            var empty = State(WeaponViewModel.ReloadEmpty, emptyClip, emptyClip ? emptyClip.length / Mathf.Max(0.1f, rifleData.reloadSeconds) : 1f);
            foreach (var s in new[] { ready, fire, reload, empty })
            {
                var t = s.AddTransition(idle);
                t.hasExitTime = true; t.exitTime = 0.95f; t.duration = 0.15f; t.hasFixedDuration = true;
            }
            EditorUtility.SetDirty(ctrl);
            log.AppendLine($"rifle animator: {clips.Length} clips; fire {(C("Fire") ? "ok" : "MISSING")}, reload {(reloadClip ? "ok" : "MISSING")}, draw {(C("Ready") ? "ok" : "MISSING")}");
            return ctrl;
        }

        /// <summary>The animated arms and rifle under the camera, on the first-person layer. Returns the holder.</summary>
        static Transform BuildRifleViewModel(Transform pivot, RifleAssets r, int layer)
        {
            var holder = new GameObject("Rifle").transform;   // moved by WeaponController (hip, aim, recoil)
            holder.SetParent(pivot, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(r.model);
            model.name = "ViewModel";
            model.transform.SetParent(holder, false);
            model.transform.localPosition = RifleModelOffset;
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            foreach (var rend in model.GetComponentsInChildren<Renderer>(true))
            {
                rend.sharedMaterials = rend.name.StartsWith("Arm") ? new[] { r.arms } : new[] { r.body, r.magazine };
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
                if (rend is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;   // never culled by its own bounds
            }
            if (!model.TryGetComponent<Animator>(out var animator)) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = r.controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.keepAnimatorStateOnDisable = true;   // hidden while scoped: carry on where it was
            return holder;
        }

        /// <summary>
        /// Where the model sits under the camera. The rig is a whole body with the arms at eye height above its feet,
        /// so its root goes 1.6 m below the eye (the old game's own prefab used the same offset).
        /// </summary>
        static readonly Vector3 RifleModelOffset = new Vector3(0.016f, -1.602f, 0.294f);
        static readonly Vector3 RifleHipPosition = Vector3.zero;
        static readonly Vector3 RifleAimPosition = new Vector3(0f, 0f, 0.05f);
        /// <summary>Muzzle flash position on the holder: just past the barrel tip in the idle pose.</summary>
        static readonly Vector3 RifleMuzzle = new Vector3(0.05f, -0.03f, 0.80f);

        /// <summary>Set by Build(): the player builder uses the real rifle when it is there.</summary>
        static RifleAssets rifleAssets;
    }
}
