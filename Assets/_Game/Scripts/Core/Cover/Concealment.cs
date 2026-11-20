using UnityEngine;

namespace Game.Core.Cover
{
    /// <summary>
    /// Soft cover (plan section 6: "bushes and tall grass hide you"): a trigger volume that does not block movement
    /// but cuts how well an animal sees the hunter through or inside it. Each layer between them multiplies visibility.
    /// </summary>
    public sealed class Concealment : MonoBehaviour
    {
        [Tooltip("Visibility through this cover (0 hides completely, 1 hides nothing).")]
        [Range(0f, 1f)] public float visibility = 0.35f;
    }
}
