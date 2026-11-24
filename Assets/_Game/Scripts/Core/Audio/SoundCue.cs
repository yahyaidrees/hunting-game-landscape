using UnityEngine;
using UnityEngine.Audio;

namespace Game.Core.Audio
{
    /// <summary>Which player volume slider a sound follows (plan section 14: Music, Sound and Voice sliders).</summary>
    public enum SoundCategory { Sound, Music, Voice }

    /// <summary>
    /// One sound the game can play: 3 to 5 versions picked without repeating, a small random pitch and volume change
    /// (plan section 14, "Variation"), its mixer group and how it carries in the world. Placeholders say what the
    /// final sound must be, so a generated file can replace them without code changes.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Audio/Sound Cue", fileName = "Cue")]
    public sealed class SoundCue : ScriptableObject
    {
        public AudioClip[] clips;
        public AudioMixerGroup output;
        public SoundCategory category = SoundCategory.Sound;
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0f, 0.5f)] public float volumeJitter = 0.1f;
        public Vector2 pitch = new Vector2(0.95f, 1.05f);

        [Header("World")]
        [Tooltip("0 = heard the same everywhere (UI, the hunter's own rifle), 1 = positioned in the world.")]
        [Range(0f, 1f)] public float spatialBlend = 1f;
        public float minDistance = 2f;
        public float maxDistance = 60f;
        [Tooltip("Higher plays first when too many sounds compete (0 to 256, lower number = more important in Unity).")]
        [Range(0, 256)] public int priority = 128;
        public bool loop;

        [Tooltip("Placeholder: what the final sound must be (prompt id from plan section 14). Empty when final.")]
        [TextArea] public string placeholderFor;

        int last = -1;

        public bool IsPlayable => clips != null && clips.Length > 0;

        /// <summary>A version different from the last one when there is more than one.</summary>
        public AudioClip Pick()
        {
            if (!IsPlayable) return null;
            if (clips.Length == 1) return clips[0];
            int i = Random.Range(0, clips.Length - 1);
            if (i >= last) i++;
            last = i;
            return clips[i];
        }
    }
}
