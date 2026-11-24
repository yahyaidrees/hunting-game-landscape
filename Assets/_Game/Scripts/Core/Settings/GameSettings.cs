using System;
using UnityEngine;

namespace Game.Core.Settings
{
    /// <summary>
    /// Player options that the whole game reads (plan sections 6 and 14): blood on or off, and the Music, Sound and
    /// Voice volumes. Stored on the device; the settings screen (Phase 2) writes them, everything else listens.
    /// </summary>
    public static class GameSettings
    {
        const string BloodKey = "settings.blood", MusicKey = "settings.volume.music", SoundKey = "settings.volume.sound", VoiceKey = "settings.volume.voice";

        /// <summary>Raised after any option changes.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Changed = null;

        /// <summary>Light blood (drops, a small puff). Off shows neutral marks instead; the trail still works.</summary>
        public static bool Blood
        {
            get => PlayerPrefs.GetInt(BloodKey, 1) == 1;
            set { PlayerPrefs.SetInt(BloodKey, value ? 1 : 0); Changed?.Invoke(); }
        }

        public static float MusicVolume { get => PlayerPrefs.GetFloat(MusicKey, 0.8f); set => SetVolume(MusicKey, value); }
        public static float SoundVolume { get => PlayerPrefs.GetFloat(SoundKey, 1f); set => SetVolume(SoundKey, value); }
        public static float VoiceVolume { get => PlayerPrefs.GetFloat(VoiceKey, 1f); set => SetVolume(VoiceKey, value); }

        static void SetVolume(string key, float value)
        {
            PlayerPrefs.SetFloat(key, Mathf.Clamp01(value));
            Changed?.Invoke();
        }
    }
}
