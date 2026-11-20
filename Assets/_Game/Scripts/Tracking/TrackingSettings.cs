using UnityEngine;

namespace Game.Tracking
{
    /// <summary>Tuning for tracking: freshness, inspecting, clue glow, Hunter Sense, map and footprints. Starting values.</summary>
    [CreateAssetMenu(menuName = "Game/Tracking/Tracking Settings", fileName = "TrackingSettings")]
    public sealed class TrackingSettings : ScriptableObject
    {
        [Header("Freshness (hours since the animal passed)")]
        public float freshBelowHours = 4f;
        public float oldFromHours = 16f;
        [Tooltip("Prints older than this are gone.")]
        public float vanishAfterHours = 60f;
        [Range(0f, 1f)] public float freshOpacity = 1f;
        [Range(0f, 1f)] public float oldOpacity = 0.4f;

        [Header("Inspecting")]
        [Tooltip("How close the hunter must be to inspect a clue (metres).")]
        public float inspectRange = 4f;
        [Tooltip("How near a tap must land to a clue on screen, in pixels at a 1080p screen.")]
        public float tapRadiusReferencePixels = 90f;
        [Tooltip("A clue within this angle of the view centre shows the Inspect button.")]
        public float aimAngle = 14f;

        [Header("Clue glow (assist; stage 1 of the tracking curve)")]
        public bool glowNearbyClues = true;
        public float glowRadius = 14f;

        [Header("Hunter Sense")]
        public float senseCooldownSeconds = 25f;
        public float senseDurationSeconds = 6f;
        public float senseRadius = 60f;

        [Header("Hunter's map")]
        [Tooltip("Half the width of the area the map shows, in metres.")]
        public float mapHalfSizeMetres = 45f;

        [Header("Footprints")]
        public int footprintPoolSize = 512;
        public float footprintDrawDistance = 45f;
        [Tooltip("Ground clearer than this (0..1 track clarity) holds no visible print.")]
        [Range(0f, 1f)] public float minTrackClarity = 0.05f;

        [Header("Visuals")]
        [Tooltip("Decal material of the glow ring under a clue.")]
        public Material glowMaterial;
    }

    public enum Freshness
    {
        Fresh,
        Medium,
        Old
    }

    public static class FreshnessUtil
    {
        public static Freshness Of(float ageHours, TrackingSettings s) =>
            ageHours < s.freshBelowHours ? Freshness.Fresh : ageHours < s.oldFromHours ? Freshness.Medium : Freshness.Old;

        /// <summary>Print opacity for an age: full when fresh, fading to oldOpacity, gone after vanishAfterHours.</summary>
        public static float Opacity(float ageHours, TrackingSettings s)
        {
            if (ageHours >= s.vanishAfterHours) return 0f;
            float t = Mathf.InverseLerp(0f, s.oldFromHours, ageHours);
            float o = Mathf.Lerp(s.freshOpacity, s.oldOpacity, t);
            float vanish = Mathf.InverseLerp(s.vanishAfterHours, s.oldFromHours, ageHours);   // 1 until old, then fades out
            return o * Mathf.Clamp01(vanish);
        }

        public static string Label(Freshness f) =>
            f == Freshness.Fresh ? "Fresh: it passed within the last few hours" :
            f == Freshness.Medium ? "Medium: several hours old" :
            "Old: probably from yesterday";
    }
}
