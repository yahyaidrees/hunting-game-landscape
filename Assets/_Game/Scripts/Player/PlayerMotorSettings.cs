using UnityEngine;

namespace Game.Player
{
    /// <summary>Movement, body and look tuning for the first-person hunter. Starting values; tune on a phone.</summary>
    [CreateAssetMenu(menuName = "Game/Player/Motor Settings", fileName = "PlayerMotorSettings")]
    public sealed class PlayerMotorSettings : ScriptableObject
    {
        [Header("Movement (m/s with the stick pushed fully)")]
        public float walkSpeed = 2.8f;
        public float crouchSpeed = 1.3f;
        [Tooltip("How fast the speed follows the stick (m/s per second).")]
        public float acceleration = 12f;
        public float gravity = -20f;

        [Header("Body")]
        public float standHeight = 1.8f;
        public float crouchHeight = 1.1f;
        public float radius = 0.3f;
        public float standEyeHeight = 1.65f;
        public float crouchEyeHeight = 0.95f;
        [Tooltip("Height change speed when crouching or standing (metres per second).")]
        public float crouchTransitionSpeed = 4f;

        [Header("Look")]
        [Tooltip("Degrees turned by dragging one full screen height (touch or mouse drag).")]
        public float dragDegreesPerScreenHeight = 160f;
        [Tooltip("Degrees per second with a gamepad stick or arrow keys pushed fully.")]
        public float stickDegreesPerSecond = 140f;
        public float minPitch = -80f;
        public float maxPitch = 80f;

        [Header("Steps and head bob")]
        public float stepLengthStanding = 0.8f;
        public float stepLengthCrouched = 0.55f;
        [Tooltip("Head bob height in metres at full walking speed (0 = off).")]
        public float headBobAmplitude = 0.02f;
    }
}
