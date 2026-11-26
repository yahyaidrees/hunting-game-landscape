using UnityEngine;

namespace Game.Tracking
{
    /// <summary>
    /// One print on the ground that the hunter can read (plan section 6: footprints tell species, size, direction,
    /// and walking or running by the stride). Either part of an authored trail (aged with the trail) or a live print
    /// an animal left while the hunt runs.
    /// </summary>
    public sealed class Footprint
    {
        public Vector3 position;
        /// <summary>Horizontal direction of travel.</summary>
        public Vector3 direction;
        public SpeciesTrackDefinition species;
        /// <summary>The trail it belongs to, or null for a live print.</summary>
        public Trail trail;
        /// <summary>Distance along the trail (trail prints only).</summary>
        public float pathDistance;
        /// <summary>Distance from the previous print of the same animal (left to right), in metres.</summary>
        public float step;
        public bool running;
        /// <summary>Trail prints: the trail's age. Live prints: unused.</summary>
        public float ageHours;
        /// <summary>Time.time when a live print was made, or -1 for trail prints.</summary>
        public float madeAt = -1f;
        /// <summary>Already read by the hunter (no Inspect button for it again; a tap still re-reads it).</summary>
        public bool read;

        public bool IsLive => madeAt >= 0f;
        public float AgeHours => IsLive ? (Time.time - madeAt) / 3600f : ageHours;
    }

    /// <summary>Finds the ground under a point, ignoring characters (the hunter's capsule, an animal).</summary>
    public static class TrackGround
    {
        public static bool Find(Vector3 p, RaycastHit[] buffer, out RaycastHit ground)
        {
            ground = default;
            int n = Physics.RaycastNonAlloc(p + Vector3.up * 20f, Vector3.down, buffer, 40f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (buffer[i].collider is CharacterController) continue;
                if (buffer[i].collider.attachedRigidbody && buffer[i].collider.attachedRigidbody.isKinematic
                    && buffer[i].collider.GetComponentInParent<UnityEngine.AI.NavMeshAgent>()) continue;   // animals
                if (buffer[i].distance < best) { best = buffer[i].distance; ground = buffer[i]; }
            }
            return best < float.MaxValue;
        }
    }
}
