using System.Collections.Generic;
using UnityEngine;

namespace Game.Core.Surfaces
{
    /// <summary>
    /// A box-shaped area of one surface laid over a ground mesh that has no per-pixel surface data yet
    /// (a mud patch, a carpet of dry leaves). No physics: a point test against the oriented box.
    /// Where zones overlap, the highest priority wins.
    /// </summary>
    [ExecuteAlways]
    public sealed class SurfaceZone : MonoBehaviour
    {
        public SurfaceDefinition surface;
        [Tooltip("Box size in metres (x width, y height, z depth), centred on this transform.")]
        public Vector3 size = new Vector3(6f, 6f, 6f);
        public int priority;

        static readonly List<SurfaceZone> Active = new List<SurfaceZone>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Active.Clear();

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);

        public bool Contains(Vector3 worldPoint)
        {
            Vector3 p = transform.InverseTransformPoint(worldPoint);
            Vector3 h = size * 0.5f;
            return Mathf.Abs(p.x) <= h.x && Mathf.Abs(p.y) <= h.y && Mathf.Abs(p.z) <= h.z;
        }

        /// <summary>The highest-priority zone containing the point, or null.</summary>
        public static SurfaceZone Find(Vector3 worldPoint)
        {
            SurfaceZone best = null;
            for (int i = 0; i < Active.Count; i++)
            {
                var z = Active[i];
                if (z.surface && z.Contains(worldPoint) && (best == null || z.priority > best.priority))
                    best = z;
            }
            return best;
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Color c = surface ? surface.debugColor : Color.magenta;
            c.a = 0.9f;
            Gizmos.color = c;
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
    }
}
