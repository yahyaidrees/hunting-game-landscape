using Game.Core.Settings;
using UnityEngine;
using UnityEngine.Audio;

namespace Game.Core.Audio
{
    /// <summary>Mixer snapshots (plan section 14, "Mixer snapshots"). Higher values win when several apply.</summary>
    public enum MixState { Normal = 0, Scope = 1, HoldBreath = 2, KillCamera = 3 }

    /// <summary>
    /// Plays sound cues from a pool of audio sources (no allocations per sound) and moves the mixer between
    /// snapshots. One per scene. Volumes follow the player's Music, Sound and Voice settings.
    /// </summary>
    public sealed class AudioService : MonoBehaviour
    {
        [SerializeField] AudioMixer mixer;
        [SerializeField] int voices = 24;
        [Tooltip("Snapshot names in the mixer, in MixState order.")]
        [SerializeField] string[] snapshotNames = { "Normal", "Scope", "HoldBreath", "KillCamera" };
        [SerializeField] float snapshotSeconds = 0.25f;

        static AudioService instance;
        AudioSource[] pool;
        float[] baseVolume;
        SoundCategory[] categories;
        int next;
        MixState mix = MixState.Normal;

        public static AudioService Instance => instance;
        public MixState Mix => mix;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        void Awake()
        {
            instance = this;
            pool = new AudioSource[voices];
            baseVolume = new float[voices];
            categories = new SoundCategory[voices];
            for (int i = 0; i < voices; i++) pool[i] = NewVoice(i);
            if (mixer) mixer.updateMode = AudioMixerUpdateMode.UnscaledTime;   // snapshots still move in slow motion
            GameSettings.Changed += ApplyVolumes;
        }

        void OnDestroy()
        {
            GameSettings.Changed -= ApplyVolumes;
            if (instance == this) instance = null;
        }

        /// <summary>Plays a cue at a point. A delay (seconds of real time) lets far sounds arrive late.</summary>
        public static AudioSource Play(SoundCue cue, Vector3 position, float delay = 0f, float volumeScale = 1f, float pitchScale = 1f)
            => instance ? instance.PlayInternal(cue, position, null, delay, volumeScale, pitchScale) : null;

        /// <summary>Plays a cue that follows a transform (loops, or a howl from a moving animal).</summary>
        public static AudioSource PlayAttached(SoundCue cue, Transform follow, float volumeScale = 1f)
            => instance && follow ? instance.PlayInternal(cue, follow.position, follow, 0f, volumeScale, 1f) : null;

        public static void SetMix(MixState state, float seconds = -1f)
        {
            if (!instance || instance.mix == state) return;
            instance.mix = state;
            if (!instance.mixer) return;
            var snap = instance.mixer.FindSnapshot(instance.snapshotNames[(int)state]);
            if (snap) snap.TransitionTo(seconds >= 0f ? seconds : instance.snapshotSeconds);
        }

        public static float CategoryVolume(SoundCategory c) => c switch
        {
            SoundCategory.Music => GameSettings.MusicVolume,
            SoundCategory.Voice => GameSettings.VoiceVolume,
            _ => GameSettings.SoundVolume,
        };

        AudioSource PlayInternal(SoundCue cue, Vector3 position, Transform follow, float delay, float volumeScale, float pitchScale)
        {
            if (!cue || !cue.IsPlayable) return null;
            var s = TakeVoice(cue.priority);
            int i = System.Array.IndexOf(pool, s);
            s.transform.SetParent(follow ? follow : transform, false);
            s.transform.position = position;
            s.clip = cue.Pick();
            s.outputAudioMixerGroup = cue.output;
            s.loop = cue.loop;
            s.spatialBlend = cue.spatialBlend;
            s.minDistance = cue.minDistance;
            s.maxDistance = cue.maxDistance;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.priority = cue.priority;
            s.pitch = Random.Range(cue.pitch.x, cue.pitch.y) * pitchScale;
            baseVolume[i] = Mathf.Clamp01(cue.volume * (1f - Random.Range(0f, cue.volumeJitter)) * volumeScale);
            categories[i] = cue.category;
            s.volume = baseVolume[i] * CategoryVolume(cue.category);
            if (delay > 0f) s.PlayDelayed(delay); else s.Play();
            return s;
        }

        AudioSource NewVoice(int i)
        {
            var go = new GameObject("Voice" + i);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.dopplerLevel = 0f;
            return s;
        }

        // an idle voice, or else the least important / oldest one
        AudioSource TakeVoice(int priority)
        {
            for (int k = 0; k < pool.Length; k++)
            {
                int i = (next + k) % pool.Length;
                if (!pool[i]) pool[i] = NewVoice(i);   // destroyed along with an object it followed
                if (!pool[i].isPlaying) { next = (i + 1) % pool.Length; return Reset(pool[i]); }
            }
            int pick = next;
            for (int k = 0; k < pool.Length; k++)
                if (pool[k].priority > pool[pick].priority) pick = k;
            next = (pick + 1) % pool.Length;
            return Reset(pool[pick]);
        }

        AudioSource Reset(AudioSource s)
        {
            s.Stop();
            if (s.transform.parent != transform) s.transform.SetParent(transform, false);
            if (!s.gameObject.activeSelf) s.gameObject.SetActive(true);
            return s;
        }

        void ApplyVolumes()
        {
            for (int i = 0; i < pool.Length; i++)
                if (pool[i]) pool[i].volume = baseVolume[i] * CategoryVolume(categories[i]);
        }

        /// <summary>Stops a looping sound started by this service and returns its voice to the pool.</summary>
        public static void Stop(AudioSource source)
        {
            if (!instance || !source) return;
            instance.Reset(source);
        }
    }
}
