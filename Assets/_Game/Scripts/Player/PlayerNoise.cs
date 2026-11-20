using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// The hunter's noise level, 0..1: the ground's noise x speed x posture, smoothed. Shown on the noise meter;
    /// animals will hear the player within <see cref="HearingRadius"/> metres.
    /// </summary>
    public sealed class PlayerNoise : MonoBehaviour
    {
        [SerializeField] FirstPersonController controller;
        [SerializeField] PlayerNoiseSettings settings;

        public PlayerNoiseSettings Settings => settings;
        public float Level { get; private set; }
        public float HearingRadius => Level * settings.maxHearingRadius;

        void Awake()
        {
            if (!controller) controller = GetComponent<FirstPersonController>();
        }

        void Update()
        {
            float target = 0f;
            if (controller.IsGrounded)
            {
                var surface = controller.CurrentSurface;
                float ground = surface ? surface.noise : settings.defaultSurfaceNoise;
                float posture = controller.IsCrouched ? settings.crouchMultiplier : 1f;
                target = ground * controller.SpeedFraction * posture;
            }
            float rate = target > Level ? settings.riseSpeed : settings.fallSpeed;
            Level = Mathf.MoveTowards(Level, target, rate * Time.deltaTime);
        }
    }
}
