using System;
using Game.Core.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>The options a settings row can show (each maps to one GameSettings property).</summary>
    public enum SettingKey
    {
        LookSensitivity, ScopeSensitivity, ButtonScale, MusicVolume, SoundVolume, VoiceVolume,
        InvertLook, LeftHanded, Blood, QualityTier, FrameRate
    }

    static class SettingAccess
    {
        public static float GetFloat(SettingKey key) => key switch
        {
            SettingKey.LookSensitivity => GameSettings.LookSensitivity,
            SettingKey.ScopeSensitivity => GameSettings.ScopeSensitivity,
            SettingKey.ButtonScale => GameSettings.ButtonScale,
            SettingKey.MusicVolume => GameSettings.MusicVolume,
            SettingKey.SoundVolume => GameSettings.SoundVolume,
            SettingKey.VoiceVolume => GameSettings.VoiceVolume,
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "not a slider option")
        };

        public static void SetFloat(SettingKey key, float v)
        {
            switch (key)
            {
                case SettingKey.LookSensitivity: GameSettings.LookSensitivity = v; break;
                case SettingKey.ScopeSensitivity: GameSettings.ScopeSensitivity = v; break;
                case SettingKey.ButtonScale: GameSettings.ButtonScale = v; break;
                case SettingKey.MusicVolume: GameSettings.MusicVolume = v; break;
                case SettingKey.SoundVolume: GameSettings.SoundVolume = v; break;
                case SettingKey.VoiceVolume: GameSettings.VoiceVolume = v; break;
                default: throw new ArgumentOutOfRangeException(nameof(key), key, "not a slider option");
            }
        }

        public static (float min, float max) Range(SettingKey key) => key switch
        {
            SettingKey.LookSensitivity => (GameSettings.MinLook, GameSettings.MaxLook),
            SettingKey.ScopeSensitivity => (GameSettings.MinScope, GameSettings.MaxScope),
            SettingKey.ButtonScale => (GameSettings.MinButton, GameSettings.MaxButton),
            _ => (0f, 1f)
        };

        public static string Format(SettingKey key, float v) => key switch
        {
            SettingKey.LookSensitivity or SettingKey.ScopeSensitivity => $"{v:0.0}x",
            _ => $"{Mathf.RoundToInt(v * 100f)}%"
        };

        public static bool GetBool(SettingKey key) => key switch
        {
            SettingKey.InvertLook => GameSettings.InvertLook,
            SettingKey.LeftHanded => GameSettings.LeftHanded,
            SettingKey.Blood => GameSettings.Blood,
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "not an on/off option")
        };

        public static void SetBool(SettingKey key, bool v)
        {
            switch (key)
            {
                case SettingKey.InvertLook: GameSettings.InvertLook = v; break;
                case SettingKey.LeftHanded: GameSettings.LeftHanded = v; break;
                case SettingKey.Blood: GameSettings.Blood = v; break;
                default: throw new ArgumentOutOfRangeException(nameof(key), key, "not an on/off option");
            }
        }

        public static int GetInt(SettingKey key) => key switch
        {
            SettingKey.QualityTier => GameSettings.QualityTierChoice,
            SettingKey.FrameRate => GameSettings.FrameRateChoice,
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "not a choice option")
        };

        public static void SetInt(SettingKey key, int v)
        {
            switch (key)
            {
                case SettingKey.QualityTier: GameSettings.QualityTierChoice = v; break;
                case SettingKey.FrameRate: GameSettings.FrameRateChoice = v; break;
                default: throw new ArgumentOutOfRangeException(nameof(key), key, "not a choice option");
            }
        }
    }
}
