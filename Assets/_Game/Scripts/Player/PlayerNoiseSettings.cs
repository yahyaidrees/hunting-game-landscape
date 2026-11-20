using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// How the hunter's noise is built from ground, speed and posture (plan section 6, "Sound").
    /// Animals will hear the player within Level x maxHearingRadius metres. Starting values; tune in play tests.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Player/Noise Settings", fileName = "PlayerNoiseSettings")]
    public sealed class PlayerNoiseSettings : ScriptableObject
    {
        [Tooltip("Noise used when no surface is known.")]
        [Range(0f, 1f)] public float defaultSurfaceNoise = 0.35f;
        [Tooltip("Crouching multiplies the noise by this.")]
        [Range(0f, 1f)] public float crouchMultiplier = 0.45f;
        [Tooltip("How fast the level rises and falls (per second).")]
        public float riseSpeed = 3f;
        public float fallSpeed = 1.2f;
        [Tooltip("Distance in metres at which an animal hears a noise level of 1.")]
        public float maxHearingRadius = 40f;

        [Header("Meter bands")]
        [Range(0f, 1f)] public float quietBelow = 0.25f;
        [Range(0f, 1f)] public float loudFrom = 0.6f;
    }
}
