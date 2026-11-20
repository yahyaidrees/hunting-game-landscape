using UnityEngine;

namespace Game.Core.Surfaces
{
    /// <summary>
    /// Answers "what surface is at this point": a SurfaceTag on the collider first, then the highest-priority
    /// SurfaceZone, then the scene default. Later the ground shaders' splat/macro maps can be added as a fourth source.
    /// </summary>
    public static class SurfaceQuery
    {
        /// <summary>Set by the scene's SceneSurfaceDefault.</summary>
        public static SurfaceDefinition Default { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Default = null;

        public static SurfaceDefinition Resolve(Collider hitCollider, Vector3 point)
        {
            if (hitCollider && hitCollider.TryGetComponent(out SurfaceTag tag) && tag.surface)
                return tag.surface;
            var zone = SurfaceZone.Find(point);
            return zone ? zone.surface : Default;
        }
    }
}
