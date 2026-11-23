using UnityEngine;

namespace Game.Weapons
{
    /// <summary>Tuning for the slow-motion kill camera (plan section 6, "Shooting", and the camera table in section 14).</summary>
    [CreateAssetMenu(menuName = "Game/Weapons/Kill Camera Settings", fileName = "KillCameraSettings")]
    public sealed class KillCameraSettings : ScriptableObject
    {
        [Tooltip("Play it on clean heart-and-lung kills. Later this becomes a player option (always, sometimes, off).")]
        public bool playOnCleanKills = true;
        [Tooltip("Closer shots skip it: the flight is too short to show.")]
        public float minDistance = 15f;

        [Header("Bullet flight")]
        [Tooltip("Game time speed while the bullet flies; the world almost stops.")]
        public float flightTimeScale = 0.02f;
        [Tooltip("How fast the bullet crosses the screen, in metres per real second.")]
        public float bulletScreenSpeed = 24f;
        [Tooltip("Real seconds the flight lasts, whatever the distance (min, max).")]
        public Vector2 flightSeconds = new Vector2(1.2f, 2.4f);
        public float followFieldOfView = 42f;
        [Tooltip("Behind, beside and above the bullet, in its own space (metres).")]
        public Vector3 followOffset = new Vector3(0.1f, 0.05f, -0.42f);
        [Tooltip("Length of the air trail behind the bullet (metres).")]
        public float trailLength = 1.6f;

        [Header("Impact")]
        [Tooltip("Bullet distance from the animal at which the view cuts to the side angle.")]
        public float cutDistance = 3f;
        [Tooltip("Distance of the side camera from the impact point.")]
        public float impactCameraDistance = 2.6f;
        public float impactFieldOfView = 34f;
        [Tooltip("Real seconds after the impact before play resumes.")]
        public float afterImpactSeconds = 2.4f;
        [Tooltip("Game time speed right after the impact; it eases back to 1 by the end.")]
        public float afterImpactTimeScale = 0.2f;
        [Tooltip("Slow push in on the impact over the after-impact time (fraction of the distance).")]
        public float pushIn = 0.18f;
        public float shakeDegrees = 1.4f;
        public float shakeSeconds = 0.45f;

        [Tooltip("Real seconds after the shot before a tap can skip (the tap that fired must not skip it).")]
        public float skipDelay = 0.35f;
    }
}
