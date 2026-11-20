using UnityEngine;

namespace Game.Core.Wind
{
    /// <summary>Wind tuning for a region or hunt. Starting values; the hunt generator will vary them later.</summary>
    [CreateAssetMenu(menuName = "Game/World/Wind Settings", fileName = "WindSettings")]
    public sealed class WindSettings : ScriptableObject
    {
        [Tooltip("Direction the wind blows toward, degrees clockwise from north (+Z).")]
        public float baseDirection = 135f;
        [Range(0f, 1f)] public float baseStrength = 0.5f;
        [Tooltip("How far the direction wanders to either side, degrees.")]
        public float directionWander = 30f;
        [Range(0f, 1f)] public float strengthWander = 0.25f;
        [Tooltip("Seconds for one slow wander cycle.")]
        public float wanderPeriodSeconds = 90f;
    }
}
