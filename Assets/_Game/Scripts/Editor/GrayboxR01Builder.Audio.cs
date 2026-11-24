using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Game.Core.Audio;
using Game.Core.Surfaces;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    /// <summary>
    /// Sound and impact effects for the slice (plan sections 13 and 14): the old game's usable sounds copied from
    /// staging, clearly named placeholders (PH_*) generated for the missing ones, one sound cue per sound, the game
    /// mixer with its groups and snapshots, and the particle prefabs for dust, hit puffs and barrel smoke.
    /// Cues are only filled in when first created, so replacing a placeholder by a final sound sticks.
    /// </summary>
    public static partial class GrayboxR01Builder
    {
        const string AudioDir = "Assets/_Game/Audio";
        const string CueDir = AudioDir + "/Cues";
        const string PlaceholderDir = AudioDir + "/Placeholders";
        const string MixerPath = AudioDir + "/Mixers/GameMixer.mixer";
        const string VfxDir = "Assets/_Game/Art/VFX";
        const string VfxPrefabDir = "Assets/_Game/Prefabs/VFX";
        const string OldGameDir = "Assets/_Incoming/OldGame";
        static readonly string[] MixerGroups = { "Music", "Ambience", "Animals", "Weapons", "Player", "UI", "Voice", "Cinematic" };

        public sealed class AudioAssets
        {
            public AudioMixer mixer;
            public SoundCue shot, bolt, dryFire, reload, scope, hitAnimal, killCameraWhoosh, defaultImpact;
            public SoundCue knockedDown, defaultSteps, wolfHowl, wolfAlarm, wolfCharge, wolfHurt, ambience;
            public ParticleSystem impactDust, hitPuff, barrelSmoke;
            public Material muzzleFlash;
        }

        /// <summary>Set by Build(): the player, wolf and scene builders read it.</summary>
        static AudioAssets audioAssets;

        static AudioAssets EnsureAudioAssets(Dictionary<SurfaceKind, SurfaceDefinition> surfaces, StringBuilder log)
        {
            foreach (var f in new[] { CueDir, PlaceholderDir, AudioDir + "/Mixers", AudioDir + "/Weapons", AudioDir + "/Player",
                                      AudioDir + "/Ambience", AudioDir + "/Animals/Wolf", VfxDir, VfxPrefabDir })
                EnsureFolder(f);

            var groups = EnsureMixer(log, out var mixer);
            AudioMixerGroup G(string n) => groups.TryGetValue(n, out var g) ? g : null;

            // ---- the old game's sounds that fit (copied; staging stays untouched) ----
            AudioClip Old(string src, string dst, bool mono) => CopyOldClip($"{OldGameDir}/{src}", $"{AudioDir}/{dst}", mono, log);
            var scopeClip = Old("Audio/Weapons/sniperZoomSound.mp3", "Weapons/SFX_ScopeZoom.mp3", false);
            var hitClip = Old("Audio/Weapons/bodyPartHitSound.mp3", "Weapons/SFX_HitAnimal.mp3", true);
            var whooshClip = Old("Audio/Weapons/slowmotionSound.mp3", "Weapons/SFX_KillCameraWhoosh.mp3", false);
            var painClip = Old("Audio/Player/malePainVoice.mp3", "Player/VO_HunterKnockedDown.mp3", true);
            var meadowClip = Old("Audio/Ambience/naturesounds.mp3", "Ambience/AMB_Meadow_Day.mp3", false);
            const string wolfSrc = "Animals/_Predators/Wolf/Audio/";
            var howls = new[] { Old(wolfSrc + "Wolf Howl 1.wav", "Animals/Wolf/WOLF_Howl_01.wav", true), Old(wolfSrc + "Wolf Howl 2.wav", "Animals/Wolf/WOLF_Howl_02.wav", true) };
            var bark = Old(wolfSrc + "Wolf Bark.wav", "Animals/Wolf/WOLF_Bark_01.wav", true);
            var barkDouble = Old(wolfSrc + "Wolf Bark Double.wav", "Animals/Wolf/WOLF_BarkDouble_01.wav", true);
            var whine = Old(wolfSrc + "Whining.wav", "Animals/Wolf/WOLF_Whine_01.wav", true);

            // ---- placeholders for what the old game did not have ----
            void PH(string name, int count, Func<int, float[]> make)
            {
                for (int i = 1; i <= count; i++) WriteIfChanged($"{PlaceholderDir}/PH_{name}_{i:00}.wav", Synth.Wav(make(i)));
            }
            PH("RifleShot", 3, Synth.RifleShot);
            PH("Bolt", 2, Synth.Bolt);
            PH("DryFire", 2, Synth.DryFire);
            PH("Reload", 1, Synth.Reload);
            foreach (Synth.ImpactKind k in Enum.GetValues(typeof(Synth.ImpactKind)))
                PH("Impact_" + k, 3, s => Synth.Impact(s, k));
            var steps = new Dictionary<SurfaceKind, bool>();
            foreach (SurfaceKind k in Enum.GetValues(typeof(SurfaceKind)))
            {
                PH("Step_" + k, 4, s => Synth.Step(s, k));
                steps[k] = true;
            }
            AssetDatabase.Refresh();
            ImportPlaceholders();

            // ---- cues ----
            var a = new AudioAssets { mixer = mixer };
            a.shot = Cue("Cue_RifleShot", c => { c.clips = PHClips("RifleShot", 3); c.output = G("Weapons"); c.spatialBlend = 0f; c.priority = 0; c.volumeJitter = 0.05f; c.pitch = new Vector2(0.97f, 1.03f); c.placeholderFor = "WPN-01 hunting rifle shot + WPN-02 echo, open land"; }, log);
            a.bolt = Cue("Cue_Bolt", c => { c.clips = PHClips("Bolt", 2); c.output = G("Weapons"); c.spatialBlend = 0f; c.volume = 0.7f; c.placeholderFor = "WPN-04 bolt action"; }, log);
            a.dryFire = Cue("Cue_DryFire", c => { c.clips = PHClips("DryFire", 2); c.output = G("Weapons"); c.spatialBlend = 0f; c.volume = 0.8f; c.placeholderFor = "WPN-06 empty weapon"; }, log);
            a.reload = Cue("Cue_Reload", c => { c.clips = PHClips("Reload", 1); c.output = G("Weapons"); c.spatialBlend = 0f; c.volume = 0.7f; c.placeholderFor = "WPN-05 rifle reload"; }, log);
            a.scope = Cue("Cue_Scope", c => { c.clips = new[] { scopeClip }; c.output = G("Weapons"); c.spatialBlend = 0f; c.volume = 0.5f; }, log);
            a.hitAnimal = Cue("Cue_HitAnimal", c => { c.clips = new[] { hitClip }; c.output = G("Weapons"); c.minDistance = 4f; c.maxDistance = 150f; c.priority = 10; c.placeholderFor = "WPN-15 hit on animal (old game sound for now)"; }, log);
            a.killCameraWhoosh = Cue("Cue_KillCameraWhoosh", c => { c.clips = new[] { whooshClip }; c.output = G("Cinematic"); c.spatialBlend = 0f; c.volume = 0.9f; c.pitch = new Vector2(0.75f, 0.8f); c.placeholderFor = "WPN-13 kill camera bullet (old game sound for now)"; }, log);
            SoundCue ImpactCue(string kind) => Cue("Cue_Impact_" + kind, c => { c.clips = PHClips("Impact_" + kind, 3); c.output = G("Weapons"); c.minDistance = 3f; c.maxDistance = 120f; c.placeholderFor = "WPN-14 miss, by surface: " + kind.ToLowerInvariant(); }, log);
            a.defaultImpact = ImpactCue("Dirt");
            var impactBySurface = new Dictionary<SurfaceKind, SoundCue>
            {
                [SurfaceKind.Grass] = a.defaultImpact, [SurfaceKind.Mud] = a.defaultImpact,
                [SurfaceKind.Rock] = ImpactCue("Rock"), [SurfaceKind.Wood] = ImpactCue("Wood"),
                [SurfaceKind.ShallowWater] = ImpactCue("Water"), [SurfaceKind.DryLeaves] = ImpactCue("Leaves"),
            };
            var plyIds = new Dictionary<SurfaceKind, string> { [SurfaceKind.Grass] = "PLY-01", [SurfaceKind.DryLeaves] = "PLY-02", [SurfaceKind.Mud] = "PLY-03", [SurfaceKind.Rock] = "PLY-04", [SurfaceKind.ShallowWater] = "PLY-05", [SurfaceKind.Wood] = "PLY-06" };
            var stepCues = new Dictionary<SurfaceKind, SoundCue>();
            foreach (var k in steps.Keys)
                stepCues[k] = Cue("Cue_Step_" + k, c => { c.clips = PHClips("Step_" + k, 4); c.output = G("Player"); c.spatialBlend = 0f; c.volume = 0.55f; c.pitch = new Vector2(0.92f, 1.08f); c.placeholderFor = $"{plyIds[k]} footsteps, {k}"; }, log);
            a.defaultSteps = stepCues[SurfaceKind.Grass];
            a.knockedDown = Cue("Cue_HunterKnockedDown", c => { c.clips = new[] { painClip }; c.output = G("Voice"); c.category = SoundCategory.Voice; c.spatialBlend = 0f; c.placeholderFor = "PLY-10 player hit by a charge (old game sound for now)"; }, log);
            a.wolfHowl = Cue("Cue_Wolf_Howl", c => { c.clips = howls; c.output = G("Animals"); c.minDistance = 20f; c.maxDistance = 450f; c.priority = 20; c.pitch = new Vector2(0.95f, 1.03f); c.placeholderFor = "PRD-01 wolf: long lonely howl, far (old game sounds for now)"; }, log);
            a.wolfAlarm = Cue("Cue_Wolf_Alarm", c => { c.clips = new[] { bark }; c.output = G("Animals"); c.minDistance = 6f; c.maxDistance = 160f; c.priority = 30; c.placeholderFor = "PRD-01 wolf: low warning growl (old bark for now)"; }, log);
            a.wolfCharge = Cue("Cue_Wolf_Charge", c => { c.clips = new[] { barkDouble }; c.output = G("Animals"); c.minDistance = 5f; c.maxDistance = 100f; c.priority = 10; c.pitch = new Vector2(0.82f, 0.9f); c.placeholderFor = "PRD-01 wolf: snarling attack (old double bark for now)"; }, log);
            a.wolfHurt = Cue("Cue_Wolf_Hurt", c => { c.clips = new[] { whine }; c.output = G("Animals"); c.minDistance = 4f; c.maxDistance = 90f; c.priority = 20; }, log);
            a.ambience = Cue("Cue_Ambience_Meadow", c => { c.clips = new[] { meadowClip }; c.output = G("Ambience"); c.spatialBlend = 0f; c.loop = true; c.volume = 0.55f; c.volumeJitter = 0f; c.pitch = Vector2.one; c.priority = 64; c.placeholderFor = "AMB-03 meadow, day (old 7 s loop for now)"; }, log);

            // ---- surfaces: their footsteps, bullet impacts and dust colour (kept if already set by hand) ----
            var dust = new Dictionary<SurfaceKind, Color>
            {
                [SurfaceKind.Grass] = new Color(0.42f, 0.38f, 0.26f, 0.75f), [SurfaceKind.DryLeaves] = new Color(0.52f, 0.40f, 0.22f, 0.8f),
                [SurfaceKind.Mud] = new Color(0.26f, 0.19f, 0.12f, 0.85f), [SurfaceKind.Rock] = new Color(0.62f, 0.60f, 0.56f, 0.8f),
                [SurfaceKind.ShallowWater] = new Color(0.85f, 0.9f, 0.95f, 0.7f), [SurfaceKind.Wood] = new Color(0.55f, 0.42f, 0.28f, 0.8f),
            };
            foreach (var (kind, s) in surfaces)
            {
                if (!s.footsteps) s.footsteps = stepCues[kind];
                if (!s.bulletImpact) s.bulletImpact = impactBySurface[kind];
                if (s.impactColor == new Color(0.45f, 0.38f, 0.28f, 0.8f)) s.impactColor = dust[kind];
                EditorUtility.SetDirty(s);
            }

            // ---- effects ----
            var soft = EnsureVfxMaterial("M_SoftParticle", "T_SoftParticle.png", MakeSoftDot);
            a.muzzleFlash = EnsureVfxMaterial("M_MuzzleFlash", "T_MuzzleFlash.png", MakeFlashStar);
            a.impactDust = BuildParticlePrefab("VFX_ImpactDust", soft, ps =>
            {
                var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f); m.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3.5f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.25f); m.gravityModifier = 0.35f; m.maxParticles = 40;
                Burst(ps, 18); Cone(ps, 28f); FadeAndGrow(ps, 1.8f);
                var limit = ps.limitVelocityOverLifetime; limit.enabled = true; limit.dampen = 0.25f; limit.limit = 0.6f;
            }, log);
            a.hitPuff = BuildParticlePrefab("VFX_HitPuff", soft, ps =>
            {
                var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f); m.gravityModifier = 0.15f; m.maxParticles = 16;
                Burst(ps, 9); Cone(ps, 35f); FadeAndGrow(ps, 1.6f);
            }, log);
            a.barrelSmoke = BuildParticlePrefab("VFX_BarrelSmoke", soft, ps =>
            {
                var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f); m.startColor = new Color(0.85f, 0.85f, 0.85f, 0.22f); m.maxParticles = 12;
                Burst(ps, 8); Cone(ps, 20f); FadeAndGrow(ps, 4f);
                var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World;
                v.x = 0f; v.y = 0.15f; v.z = 0f;
            }, log);

            AssetDatabase.SaveAssets();
            return a;
        }

        // ---------- clips ----------

        static AudioClip CopyOldClip(string src, string dst, bool mono, StringBuilder log)
        {
            if (!File.Exists(dst))
            {
                if (!File.Exists(src)) { log.AppendLine("WARNING: old sound missing " + src); return null; }
                AssetDatabase.CopyAsset(src, dst);
                log.AppendLine("copied " + dst);
            }
            var imp = (AudioImporter)AssetImporter.GetAtPath(dst);
            imp.forceToMono = mono;   // positioned sounds are mono: half the memory, and 3D panning needs one channel
            imp.loadInBackground = false;
            imp.defaultSampleSettings = new AudioImporterSampleSettings
            {
                loadType = AudioClipLoadType.CompressedInMemory, compressionFormat = AudioCompressionFormat.Vorbis,
                quality = 0.6f, sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate, preloadAudioData = true,
            };
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(dst);
        }

        static void ImportPlaceholders()
        {
            foreach (var path in Directory.GetFiles(PlaceholderDir, "PH_*.wav").Select(p => p.Replace('\\', '/')))
            {
                var imp = (AudioImporter)AssetImporter.GetAtPath(path);
                if (!imp) continue;
                var s = imp.defaultSampleSettings;
                if (s.loadType == AudioClipLoadType.DecompressOnLoad && s.compressionFormat == AudioCompressionFormat.ADPCM) continue;
                imp.defaultSampleSettings = new AudioImporterSampleSettings
                {
                    loadType = AudioClipLoadType.DecompressOnLoad, compressionFormat = AudioCompressionFormat.ADPCM,
                    sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate, preloadAudioData = true,
                };
                imp.SaveAndReimport();
            }
        }

        static AudioClip[] PHClips(string name, int count) =>
            Enumerable.Range(1, count).Select(i => AssetDatabase.LoadAssetAtPath<AudioClip>($"{PlaceholderDir}/PH_{name}_{i:00}.wav")).Where(c => c).ToArray();

        static SoundCue Cue(string name, Action<SoundCue> setup, StringBuilder log)
        {
            string path = $"{CueDir}/{name}.asset";
            var cue = AssetDatabase.LoadAssetAtPath<SoundCue>(path);
            if (cue) return cue;   // filled once: later edits (a final sound replacing a placeholder) are kept
            cue = ScriptableObject.CreateInstance<SoundCue>();
            setup(cue);
            AssetDatabase.CreateAsset(cue, path);
            log.AppendLine("created " + path);
            return cue;
        }

        // ---------- mixer ----------

        // Unity has no public API to build a mixer, so its editor classes are used by reflection (created once).
        static Dictionary<string, AudioMixerGroup> EnsureMixer(StringBuilder log, out AudioMixer mixer)
        {
            mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (!mixer) mixer = CreateMixer(log);
            var m = mixer;
            return MixerGroups.Select(n => m.FindMatchingGroups(n).FirstOrDefault(g => g.name == n)).Where(g => g)
                              .ToDictionary(g => g.name, g => g);
        }

        static AudioMixer CreateMixer(StringBuilder log)
        {
            const BindingFlags BF = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var asm = typeof(UnityEditor.Editor).Assembly;
            var ctrlT = asm.GetType("UnityEditor.Audio.AudioMixerController");
            var groupT = asm.GetType("UnityEditor.Audio.AudioMixerGroupController");
            var effT = asm.GetType("UnityEditor.Audio.AudioMixerEffectController");
            var ctrl = (AudioMixer)ctrlT.GetMethod("CreateMixerControllerAtPath", BF).Invoke(null, new object[] { MixerPath });
            var master = ctrlT.GetProperty("masterGroup", BF).GetValue(ctrl);
            var g = new Dictionary<string, object>();
            foreach (var n in MixerGroups)
            {
                g[n] = ctrlT.GetMethod("CreateNewGroup", BF).Invoke(ctrl, new object[] { n, false });
                ctrlT.GetMethod("AddChildToParent", BF).Invoke(ctrl, new[] { g[n], master });
            }

            // low-pass on the ambience: held breath muffles the world
            object lowpass = null;
            try
            {
                lowpass = Activator.CreateInstance(effT, BF, null, new object[] { "Lowpass Simple" }, null);
                effT.GetMethod("PreallocateGUIDs", BF).Invoke(lowpass, null);
                AssetDatabase.AddObjectToAsset((Object)lowpass, ctrl);
                groupT.GetMethod("InsertEffect", BF).Invoke(g["Ambience"], new[] { lowpass, (object)0 });
            }
            catch (Exception e) { log.AppendLine("mixer: low-pass not added (" + (e.InnerException ?? e).Message + ")"); lowpass = null; }

            var target = ctrlT.GetProperty("TargetSnapshot", BF);
            var normal = target.GetValue(ctrl);
            ((Object)normal).name = "Normal";
            void Vol(string group, object snap, float db) => groupT.GetMethod("SetValueForVolume", BF).Invoke(g[group], new[] { ctrl, snap, (object)db });
            void Pitch(string group, object snap, float p) => groupT.GetMethod("SetValueForPitch", BF).Invoke(g[group], new[] { ctrl, snap, (object)p });
            void Cutoff(object snap, float hz) { if (lowpass != null) effT.GetMethod("SetValueForParameter", BF).Invoke(lowpass, new[] { ctrl, snap, "Cutoff freq", (object)hz }); }
            object Clone(string name)
            {
                target.SetValue(ctrl, normal);
                ctrlT.GetMethod("CloneNewSnapshotFromTarget", BF).Invoke(ctrl, new object[] { false });
                var s = target.GetValue(ctrl);
                ((Object)s).name = name;
                return s;
            }

            Vol("Music", normal, -4f);
            Cutoff(normal, 22000f);
            // scope: the world opens up a little, the hunter's own sounds step back
            var scope = Clone("Scope"); Vol("Ambience", scope, 2f); Vol("Player", scope, -4f);
            // held breath: ambience low and muffled, the hunter quiet
            var breath = Clone("HoldBreath"); Vol("Ambience", breath, -8f); Cutoff(breath, 900f); Vol("Player", breath, -10f); Vol("Music", breath, -10f);
            // kill camera: everything slowed and low (the cinematic whoosh is not)
            var kill = Clone("KillCamera");
            foreach (var n in new[] { "Ambience", "Animals", "Weapons", "Player" }) Pitch(n, kill, 0.55f);
            Vol("Ambience", kill, -10f); Vol("Music", kill, -14f);

            target.SetValue(ctrl, normal);
            ctrlT.GetProperty("startSnapshot", BF).SetValue(ctrl, normal);
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            log.AppendLine("created " + MixerPath);
            return ctrl;
        }

        // ---------- effects ----------

        static Material EnsureVfxMaterial(string name, string textureName, Func<byte[]> makeTexture)
        {
            string texPath = $"{VfxDir}/{textureName}";
            if (WriteIfChanged(texPath, makeTexture()))
            {
                AssetDatabase.ImportAsset(texPath);
                var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.maxTextureSize = 128;
                ti.SaveAndReimport();
            }
            string matPath = $"{VfxDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (!mat) { mat = new Material(Shader.Find("Sprites/Default")) { name = name }; AssetDatabase.CreateAsset(mat, matPath); }
            mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static ParticleSystem BuildParticlePrefab(string name, Material mat, Action<ParticleSystem> setup, StringBuilder log)
        {
            var go = new GameObject(name);
            try
            {
                var ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.duration = 1f; main.loop = false; main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var emission = ps.emission; emission.rateOverTime = 0f;
                var r = go.GetComponent<ParticleSystemRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                setup(ps);
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{VfxPrefabDir}/{name}.prefab");
                log.AppendLine($"saved {VfxPrefabDir}/{name}.prefab");
                return prefab.GetComponent<ParticleSystem>();
            }
            finally { Object.DestroyImmediate(go); }
        }

        static void Burst(ParticleSystem ps, short count)
        {
            var e = ps.emission;
            e.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
        }

        static void Cone(ParticleSystem ps, float angle)
        {
            var s = ps.shape; s.enabled = true; s.shapeType = ParticleSystemShapeType.Cone; s.angle = angle; s.radius = 0.02f;
        }

        static void FadeAndGrow(ParticleSystem ps, float grow)
        {
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, grow)));
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.3f), new GradientAlphaKey(0f, 1f) },
            };
        }

        static byte[] MakeSoftDot()
        {
            const int n = 64;
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n - 0.5f, v = (y + 0.5f) / n - 0.5f;
                float r = Mathf.Sqrt(u * u + v * v) * 2f;
                float a = Mathf.Pow(1f - SmoothEdge(0f, 1f, r), 1.5f);
                px[y * n + x] = new Color(1f, 1f, 1f, a);
            }
            return Png(px, n);
        }

        // a hot core with a few rays, tinted by the material colour
        static byte[] MakeFlashStar()
        {
            const int n = 128;
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n - 0.5f, v = (y + 0.5f) / n - 0.5f;
                float r = Mathf.Sqrt(u * u + v * v) * 2f, ang = Mathf.Atan2(v, u);
                float rays = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 3f)), 12f) * (1f - SmoothEdge(0f, 1f, r));
                float core = 1f - SmoothEdge(0f, 0.35f, r);
                float a = Mathf.Clamp01(core + rays * 0.8f);
                px[y * n + x] = new Color(1f, Mathf.Lerp(0.75f, 1f, core), Mathf.Lerp(0.4f, 0.95f, core), a);
            }
            return Png(px, n);
        }

        // ---------- scene ----------

        static void BuildAudioScene(Transform root, StringBuilder log)
        {
            var a = audioAssets;
            if (a == null) return;
            var go = new GameObject("Audio");
            go.transform.SetParent(root, false);
            SetRefs(go.AddComponent<AudioService>(), ("mixer", a.mixer));
            SetRefs(go.AddComponent<AmbienceLoop>(), ("cue", a.ambience));
            log.AppendLine("audio: service, mixer and meadow ambience in the scene");
        }
    }
}
