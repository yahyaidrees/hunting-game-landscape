using System;
using UnityEngine;

namespace Game.Core.Settings
{
    /// <summary>Graphics tier (plan section 13). Auto picks one from the phone on first launch.</summary>
    public enum QualityTier { Low = 0, Medium = 1, High = 2 }

    /// <summary>
    /// Player options that the whole game reads (plan sections 6, 12, 13 and 14): controls (look and scope
    /// sensitivity, invert look, left-handed layout, button size), graphics (quality tier, frame rate), the Music,
    /// Sound and Voice volumes, and blood on or off. Stored on the device. Values are cached in memory, because
    /// PlayerPrefs on Android goes through Java and must not be read every frame. Call Flush when a settings screen closes.
    /// </summary>
    public static class GameSettings
    {
        const string BloodKey = "settings.blood", MusicKey = "settings.volume.music", SoundKey = "settings.volume.sound", VoiceKey = "settings.volume.voice";
        const string LookKey = "settings.look.sensitivity", ScopeKey = "settings.look.scope", InvertKey = "settings.look.invert";
        const string LeftKey = "settings.hud.lefthanded", ButtonKey = "settings.hud.buttonscale";
        const string TierKey = "settings.graphics.tier", FpsKey = "settings.graphics.fps";
        const string MeasuredKey = "settings.graphics.measured", MeasuredDoneKey = "settings.graphics.measured.done";
        const string MeasuredDeviceKey = "settings.graphics.measured.device", MeasuredVersionKey = "settings.graphics.measured.version";
        /// <summary>Raise when the measuring rules change, so every phone measures itself again.</summary>
        public const int MeasureVersion = 1;

        public const float MinLook = 0.3f, MaxLook = 2.5f, MinScope = 0.3f, MaxScope = 2f, MinButton = 0.8f, MaxButton = 1.3f;
        /// <summary>QualityTierChoice value meaning "pick from the phone".</summary>
        public const int AutoTier = -1;

        static bool loaded;
        static bool blood, invertLook, leftHanded;
        static float music, sound, voice, look, scope, buttonScale;
        static int tierChoice, frameRate;
        static int measured = -1;   // -1: this device has not been measured
        static bool measuredDone;

        /// <summary>Raised after any option changes.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Changed = null; loaded = false; }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            blood = PlayerPrefs.GetInt(BloodKey, 1) == 1;
            music = PlayerPrefs.GetFloat(MusicKey, 0.8f);
            sound = PlayerPrefs.GetFloat(SoundKey, 1f);
            voice = PlayerPrefs.GetFloat(VoiceKey, 1f);
            look = Mathf.Clamp(PlayerPrefs.GetFloat(LookKey, 1f), MinLook, MaxLook);
            scope = Mathf.Clamp(PlayerPrefs.GetFloat(ScopeKey, 1f), MinScope, MaxScope);
            invertLook = PlayerPrefs.GetInt(InvertKey, 0) == 1;
            leftHanded = PlayerPrefs.GetInt(LeftKey, 0) == 1;
            buttonScale = Mathf.Clamp(PlayerPrefs.GetFloat(ButtonKey, 1f), MinButton, MaxButton);
            tierChoice = Mathf.Clamp(PlayerPrefs.GetInt(TierKey, AutoTier), AutoTier, (int)QualityTier.High);
            frameRate = PlayerPrefs.GetInt(FpsKey, 0);
            // a measurement counts only on the same device model and with the same measuring rules
            bool valid = PlayerPrefs.GetInt(MeasuredVersionKey, 0) == MeasureVersion
                         && PlayerPrefs.GetString(MeasuredDeviceKey, "") == SystemInfo.deviceModel;
            measured = valid ? Mathf.Clamp(PlayerPrefs.GetInt(MeasuredKey, -1), -1, (int)QualityTier.High) : -1;
            measuredDone = valid && measured >= 0 && PlayerPrefs.GetInt(MeasuredDoneKey, 0) == 1;
        }

        static void SaveInt(string key, int value) { PlayerPrefs.SetInt(key, value); Changed?.Invoke(); }
        static void SaveFloat(string key, float value) { PlayerPrefs.SetFloat(key, value); Changed?.Invoke(); }

        /// <summary>Light blood (drops, a small puff). Off shows neutral marks instead; the trail still works.</summary>
        public static bool Blood { get { Load(); return blood; } set { Load(); blood = value; SaveInt(BloodKey, value ? 1 : 0); } }

        public static float MusicVolume { get { Load(); return music; } set { Load(); music = Mathf.Clamp01(value); SaveFloat(MusicKey, music); } }
        public static float SoundVolume { get { Load(); return sound; } set { Load(); sound = Mathf.Clamp01(value); SaveFloat(SoundKey, sound); } }
        public static float VoiceVolume { get { Load(); return voice; } set { Load(); voice = Mathf.Clamp01(value); SaveFloat(VoiceKey, voice); } }

        /// <summary>Multiplier on drag and stick look speed (1 = the tuned default).</summary>
        public static float LookSensitivity { get { Load(); return look; } set { Load(); look = Mathf.Clamp(value, MinLook, MaxLook); SaveFloat(LookKey, look); } }
        /// <summary>Extra multiplier while looking through the scope (on top of the zoom's own slow-down).</summary>
        public static float ScopeSensitivity { get { Load(); return scope; } set { Load(); scope = Mathf.Clamp(value, MinScope, MaxScope); SaveFloat(ScopeKey, scope); } }
        /// <summary>Dragging up looks down, like a flight stick.</summary>
        public static bool InvertLook { get { Load(); return invertLook; } set { Load(); invertLook = value; SaveInt(InvertKey, value ? 1 : 0); } }

        /// <summary>Mirrors the hunt HUD: the move stick goes right, fire and look go left.</summary>
        public static bool LeftHanded { get { Load(); return leftHanded; } set { Load(); leftHanded = value; SaveInt(LeftKey, value ? 1 : 0); } }
        /// <summary>Size of the thumb controls (stick and action buttons).</summary>
        public static float ButtonScale { get { Load(); return buttonScale; } set { Load(); buttonScale = Mathf.Clamp(value, MinButton, MaxButton); SaveFloat(ButtonKey, buttonScale); } }

        /// <summary>The player's tier choice: AutoTier (-1) or a QualityTier value.</summary>
        public static int QualityTierChoice
        {
            get { Load(); return tierChoice; }
            set { Load(); tierChoice = Mathf.Clamp(value, AutoTier, (int)QualityTier.High); SaveInt(TierKey, tierChoice); }
        }
        /// <summary>The tier in use: the player's choice, or the one detected for this phone.</summary>
        public static QualityTier Tier => QualityTierChoice == AutoTier ? DetectedTier : (QualityTier)QualityTierChoice;

        /// <summary>Frame rate cap chosen by the player: 30, 45 or 60, or 0 for the tier's default.</summary>
        public static int FrameRateChoice { get { Load(); return frameRate; } set { Load(); frameRate = value is 30 or 45 or 60 ? value : 0; SaveInt(FpsKey, frameRate); } }

        /// <summary>
        /// The tier this phone gets on Auto: the one measured on this device (AutoTierCalibrator times real hunting
        /// frames), or until then the estimate from memory.
        /// </summary>
        public static QualityTier DetectedTier { get { Load(); return measured >= 0 ? (QualityTier)measured : EstimatedTier; } }

        /// <summary>The Auto tier comes from a finished measurement on this device (not the memory estimate).</summary>
        public static bool TierMeasured { get { Load(); return measuredDone; } }

        /// <summary>
        /// Saves the Auto tier for this device. <paramref name="done"/> false while measuring is still going on (the
        /// tier is used, but measuring resumes next time); true when finished, or after the in-game safety step-down.
        /// </summary>
        public static void SetMeasuredTier(QualityTier tier, bool done)
        {
            Load();
            measured = (int)tier;
            measuredDone = done;
            PlayerPrefs.SetInt(MeasuredKey, measured);
            PlayerPrefs.SetInt(MeasuredDoneKey, done ? 1 : 0);
            PlayerPrefs.SetString(MeasuredDeviceKey, SystemInfo.deviceModel);
            PlayerPrefs.SetInt(MeasuredVersionKey, MeasureVersion);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>
        /// The starting estimate before a device is measured, from memory and graphics features (plan section 13: Low
        /// about 3 GB, Medium 4 to 6 GB, High 8 GB or more). On the test phones memory alone guessed wrong both ways
        /// (a 6 GB Mali-G52 phone needs Low, a 4 GB one manages Medium), which is why Auto measures.
        /// </summary>
        public static QualityTier EstimatedTier
        {
            get
            {
                int ram = SystemInfo.systemMemorySize;   // MB
                if (ram <= 0) return QualityTier.Medium;
                if (ram < 3800 || SystemInfo.graphicsShaderLevel < 45 || SystemInfo.processorCount < 6) return QualityTier.Low;
                if (ram < 7200) return QualityTier.Medium;
                return QualityTier.High;
            }
        }

        /// <summary>Writes changed options to disk (sliders change them many times a second, so not on every change).</summary>
        public static void Flush() => PlayerPrefs.Save();

        /// <summary>Controls back to their defaults (sound, graphics and blood keep their values).</summary>
        public static void ResetControls()
        {
            Load();
            look = 1f; scope = 1f; invertLook = false; leftHanded = false; buttonScale = 1f;
            PlayerPrefs.SetFloat(LookKey, look); PlayerPrefs.SetFloat(ScopeKey, scope);
            PlayerPrefs.SetInt(InvertKey, 0); PlayerPrefs.SetInt(LeftKey, 0); PlayerPrefs.SetFloat(ButtonKey, buttonScale);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
