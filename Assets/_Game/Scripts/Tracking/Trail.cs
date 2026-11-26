using System.Collections.Generic;
using Game.Core.Surfaces;
using UnityEngine;

namespace Game.Tracking
{
    /// <summary>
    /// The path one animal took, with its clues as children. Lays the animal's footprints along the path: one print
    /// per step, alternating left and right, as clear as the ground allows (mud perfect, rock and water none) and as
    /// faded as the trail's age. Later the hunt generator (plan section 16) will pick paths from authored routes.
    /// </summary>
    public sealed class Trail : MonoBehaviour
    {
        public SpeciesTrackDefinition species;
        [Tooltip("Hours since the animal walked this trail when the hunt starts.")]
        public float ageHours = 3f;
        [Tooltip("The path in world space, from where the animal came to where it went.")]
        public List<Vector3> points = new List<Vector3>();

        readonly List<Clue> clues = new List<Clue>();
        readonly RaycastHit[] hits = new RaycastHit[16];
        float[] cumulative = new float[0];

        public IReadOnlyList<Clue> Clues => clues;
        public float Length { get; private set; }
        /// <summary>How far along the path the hunter has uncovered it (for the map).</summary>
        public float RevealedDistance { get; private set; }
        public int PrintsLaid { get; private set; }

        public void Build(FootprintPool pool, TrackingSettings settings)
        {
            ComputeLengths();
            clues.Clear();
            GetComponentsInChildren(clues);
            foreach (var c in clues) c.Bind(this, ProjectDistance(c.Position));
            clues.Sort((a, b) => a.PathDistance.CompareTo(b.PathDistance));
            LayPrints(pool, settings);
        }

        public void Reveal(float distance) => RevealedDistance = Mathf.Max(RevealedDistance, distance);

        void ComputeLengths()
        {
            cumulative = new float[points.Count];
            Length = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                Length += Vector3.Distance(Flat(points[i - 1]), Flat(points[i]));
                cumulative[i] = Length;
            }
        }

        void LayPrints(FootprintPool pool, TrackingSettings s)
        {
            PrintsLaid = 0;
            if (!species || points.Count < 2 || pool == null) return;
            float opacity = FreshnessUtil.Opacity(ageHours, s);
            if (opacity <= 0.01f) return;
            var freshness = FreshnessUtil.Of(ageHours, s);
            var material = freshness == Freshness.Old && species.oldPrintMaterial ? species.oldPrintMaterial : species.freshPrintMaterial;
            if (!material) return;

            bool left = false;
            for (float d = species.walkStep * 0.5f; d < Length; d += species.walkStep)
            {
                Sample(d, out Vector3 pos, out Vector3 dir);
                left = !left;
                Vector3 side = Vector3.Cross(Vector3.up, dir).normalized * (left ? -species.trackHalfWidth : species.trackHalfWidth);
                if (!TrackGround.Find(pos + side, hits, out RaycastHit hit)) continue;

                var surface = SurfaceQuery.Resolve(hit.collider, hit.point);
                float clarity = surface ? surface.trackClarity : 0.5f;
                if (clarity < s.minTrackClarity) continue;   // rock and water: the trail breaks, the hunter must search

                var record = new Footprint
                {
                    position = hit.point, direction = dir, species = species, trail = this, pathDistance = d,
                    step = species.walkStep, ageHours = ageHours,
                };
                pool.Place(hit.point, hit.normal, dir, species.printSize, material, opacity * Mathf.Sqrt(clarity), record);
                PrintsLaid++;
            }
        }

        /// <summary>Point and travel direction at a distance along the path (flattened to the horizontal).</summary>
        public void Sample(float distance, out Vector3 position, out Vector3 direction)
        {
            position = points.Count > 0 ? points[0] : transform.position;
            direction = Vector3.forward;
            for (int i = 1; i < points.Count; i++)
            {
                if (distance > cumulative[i] && i < points.Count - 1) continue;
                float seg = cumulative[i] - cumulative[i - 1];
                float t = seg > 0f ? Mathf.Clamp01((distance - cumulative[i - 1]) / seg) : 0f;
                position = Vector3.Lerp(points[i - 1], points[i], t);
                direction = Flat(points[i] - points[i - 1]).normalized;
                return;
            }
        }

        /// <summary>Distance along the path of the point on it nearest to <paramref name="p"/>.</summary>
        public float ProjectDistance(Vector3 p)
        {
            float best = float.MaxValue, result = 0f;
            Vector3 fp = Flat(p);
            for (int i = 1; i < points.Count; i++)
            {
                Vector3 a = Flat(points[i - 1]), b = Flat(points[i]);
                Vector3 ab = b - a;
                float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(fp - a, ab) / ab.sqrMagnitude) : 0f;
                float dist = (a + ab * t - fp).sqrMagnitude;
                if (dist < best) { best = dist; result = cumulative[i - 1] + ab.magnitude * t; }
            }
            return result;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.91f, 0.64f, 0.23f);
            for (int i = 1; i < points.Count; i++) Gizmos.DrawLine(points[i - 1] + Vector3.up * 0.2f, points[i] + Vector3.up * 0.2f);
        }
    }
}
