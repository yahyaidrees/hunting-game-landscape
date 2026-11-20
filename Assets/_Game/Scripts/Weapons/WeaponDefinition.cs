using UnityEngine;

namespace Game.Weapons
{
    /// <summary>One weapon's handling and ballistics (plan section 9: damage, range, stability, reload, noise). Starting values.</summary>
    [CreateAssetMenu(menuName = "Game/Weapons/Weapon", fileName = "Weapon")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        public string displayName = "Hunting rifle";

        [Header("Ammunition")]
        public int magazineSize = 5;
        [Tooltip("Seconds between shots (working the bolt).")]
        public float cycleSeconds = 1.1f;
        public float reloadSeconds = 2.6f;

        [Header("Ballistics")]
        [Tooltip("Muzzle velocity, metres per second. Bullet drop comes from gravity over the flight time.")]
        public float muzzleVelocity = 820f;
        public float maxRange = 400f;
        [Tooltip("Damage multiplier (upgraded calibres hit harder).")]
        public float damageMultiplier = 1f;

        [Header("Optics")]
        public float hipFieldOfView = 60f;
        [Tooltip("Field of view through the scope (60 / 15 = 4x).")]
        public float scopeFieldOfView = 15f;
        public float scopeInSeconds = 0.2f;

        [Header("Stability")]
        [Tooltip("Sway in degrees while aiming through the scope, standing still.")]
        public float scopeSwayDegrees = 0.55f;
        public float hipSwayDegrees = 1.5f;
        public float swaySpeed = 0.45f;
        [Tooltip("Sway multiplier right after moving at full speed (heavy breathing); it settles over a few seconds.")]
        public float movementSwayMultiplier = 2.5f;
        [Tooltip("Crouching multiplies sway by this.")]
        public float crouchSwayMultiplier = 0.7f;
        [Tooltip("Holding breath multiplies sway by this.")]
        public float holdBreathSwayMultiplier = 0.15f;
        public float holdBreathSeconds = 4f;
        [Tooltip("Shaky recovery after breath runs out.")]
        public float breathRecoverSeconds = 3f;
        public float recoilDegrees = 5f;

        [Header("Noise")]
        [Tooltip("Animals within this distance hear the shot.")]
        public float shotHeardRadius = 250f;
    }
}
