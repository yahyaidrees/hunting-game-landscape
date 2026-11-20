using UnityEngine;

namespace Game.Core.Surfaces
{
    public enum SurfaceKind
    {
        Grass,
        DryLeaves,
        Mud,
        Rock,
        ShallowWater,
        Wood
    }

    /// <summary>
    /// One ground type (plan section 4, "Ground types"). Each has its own noise value, footprint clarity and,
    /// later, footstep sounds and footprint look. Values are starting points to tune, not code constants.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Surfaces/Surface Definition", fileName = "Surface")]
    public sealed class SurfaceDefinition : ScriptableObject
    {
        public SurfaceKind kind;
        public string displayName = "Grass";

        [Tooltip("How loud walking on it is at full walking speed (0 silent .. 1 very loud).")]
        [Range(0f, 1f)] public float noise = 0.35f;

        [Tooltip("How clearly it holds a footprint (mud 1 = perfect prints, rock 0 = none).")]
        [Range(0f, 1f)] public float trackClarity = 0.5f;

        [Tooltip("Walking speed multiplier (mud and water slow the player down).")]
        [Range(0.2f, 1.5f)] public float speedMultiplier = 1f;

        [Tooltip("Colour of the graybox decal that marks this surface.")]
        public Color debugColor = new Color(0.4f, 0.7f, 0.3f, 0.45f);
    }
}
