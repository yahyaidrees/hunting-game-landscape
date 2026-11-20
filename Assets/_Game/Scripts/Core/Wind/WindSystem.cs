using UnityEngine;

namespace Game.Core.Wind
{
    /// <summary>
    /// The scene's wind (plan section 6, "Senses and environment"): a base direction and strength that wander slowly.
    /// Animals smell the hunter when the wind carries scent toward them; the HUD shows it as an arrow.
    /// </summary>
    public sealed class WindSystem : MonoBehaviour
    {
        [SerializeField] WindSettings settings;

        public static WindSystem Active { get; private set; }
        /// <summary>Direction the wind blows toward, degrees clockwise from north (+Z).</summary>
        public float DirectionDegrees { get; private set; }
        /// <summary>0 calm .. 1 strong.</summary>
        public float Strength { get; private set; }
        /// <summary>Unit vector the wind blows toward (horizontal).</summary>
        public Vector3 Direction => Quaternion.Euler(0f, DirectionDegrees, 0f) * Vector3.forward;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Active = null;

        void OnEnable()
        {
            Active = this;
            Tick(Time.time);
        }

        void OnDisable()
        {
            if (Active == this) Active = null;
        }

        void Update() => Tick(Time.time);

        void Tick(float t)
        {
            if (!settings) return;
            float p = settings.wanderPeriodSeconds > 0f ? t / settings.wanderPeriodSeconds : 0f;
            DirectionDegrees = settings.baseDirection + (Mathf.PerlinNoise(p, 0.37f) * 2f - 1f) * settings.directionWander;
            Strength = Mathf.Clamp01(settings.baseStrength + (Mathf.PerlinNoise(0.71f, p) * 2f - 1f) * settings.strengthWander);
        }
    }
}
