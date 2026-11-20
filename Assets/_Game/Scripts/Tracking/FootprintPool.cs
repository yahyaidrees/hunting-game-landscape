using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.Tracking
{
    /// <summary>
    /// A fixed set of decal projectors reused for every footprint (plan section 17: pooled footprint decals).
    /// When the pool runs out the oldest print is reused.
    /// </summary>
    public sealed class FootprintPool : MonoBehaviour
    {
        DecalProjector[] items;
        int next;

        public int Capacity => items?.Length ?? 0;
        public int Used { get; private set; }

        public void Init(int size, float drawDistance)
        {
            items = new DecalProjector[Mathf.Max(1, size)];
            for (int i = 0; i < items.Length; i++)
            {
                var go = new GameObject("Print");
                go.transform.SetParent(transform, false);
                go.SetActive(false);
                var p = go.AddComponent<DecalProjector>();
                p.drawDistance = drawDistance;
                p.fadeScale = 0.85f;
                items[i] = p;
            }
        }

        /// <summary>Places a print on the ground at <paramref name="position"/>, its top pointing along <paramref name="forward"/>.</summary>
        public DecalProjector Place(Vector3 position, Vector3 normal, Vector3 forward, Vector2 size, Material material, float opacity)
        {
            var p = items[next];
            next = (next + 1) % items.Length;
            Used = Mathf.Min(Used + 1, items.Length);

            Vector3 fwd = Vector3.ProjectOnPlane(forward, normal);
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.ProjectOnPlane(Vector3.forward, normal);
            p.transform.SetPositionAndRotation(position, Quaternion.LookRotation(-normal, fwd.normalized));
            p.material = material;
            p.size = new Vector3(size.x, size.y, 0.4f);
            p.pivot = Vector3.zero;
            p.fadeFactor = Mathf.Clamp01(opacity);
            p.gameObject.SetActive(true);
            return p;
        }

        public void ClearAll()
        {
            if (items == null) return;
            foreach (var p in items) p.gameObject.SetActive(false);
            next = 0;
            Used = 0;
        }
    }
}
