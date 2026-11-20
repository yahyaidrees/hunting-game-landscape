using UnityEngine;

namespace Game.Core.Surfaces
{
    /// <summary>Marks a collider as a surface (for example a wooden watchtower floor or a rock).</summary>
    public sealed class SurfaceTag : MonoBehaviour
    {
        public SurfaceDefinition surface;
    }
}
