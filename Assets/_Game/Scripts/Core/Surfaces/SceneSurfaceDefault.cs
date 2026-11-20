using UnityEngine;

namespace Game.Core.Surfaces
{
    /// <summary>The surface used where nothing more specific is marked (the meadow's grass).</summary>
    [ExecuteAlways]
    public sealed class SceneSurfaceDefault : MonoBehaviour
    {
        public SurfaceDefinition surface;

        void OnEnable() => SurfaceQuery.Default = surface;

        void OnDisable()
        {
            if (SurfaceQuery.Default == surface) SurfaceQuery.Default = null;
        }
    }
}
