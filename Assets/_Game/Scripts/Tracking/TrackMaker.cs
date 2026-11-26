using UnityEngine;

namespace Game.Tracking
{
    /// <summary>
    /// On an animal: leaves its prints as it moves during the hunt, one per step, left and right in turn. Walking
    /// steps are the species' walk step; running ones are much longer, so the gap itself tells the hunter it ran
    /// (plan section 6: walking or running by stride length). Prints only show where the ground holds them.
    /// </summary>
    public sealed class TrackMaker : MonoBehaviour
    {
        [SerializeField] SpeciesTrackDefinition species;
        [Tooltip("Above this speed (m/s) the animal is running: longer steps, and the print reads as running.")]
        [SerializeField] float runSpeed = 3f;
        [Tooltip("Running step as a multiple of the walking step.")]
        [SerializeField] float runStepMultiplier = 2.6f;

        Vector3 last;
        float travelled, speed;
        bool left, started;

        public SpeciesTrackDefinition Species => species;

        void OnEnable() => started = false;

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || !species) return;
            Vector3 p = transform.position;
            if (!started) { last = p; started = true; return; }
            Vector3 moved = p - last; moved.y = 0f;
            last = p;
            float d = moved.magnitude;
            if (d > 5f) { travelled = 0f; return; }   // teleported (respawn, placement): no prints in between
            speed = Mathf.Lerp(speed, d / dt, 1f - Mathf.Exp(-dt * 6f));
            if (d < 1e-4f) return;
            travelled += d;

            bool running = speed > runSpeed;
            float step = running ? species.walkStep * runStepMultiplier : species.walkStep;
            if (travelled < step) return;
            travelled -= step;
            var sys = TrackingSystem.Active;
            if (!sys) return;
            left = !left;
            Vector3 dir = moved / d;
            Vector3 side = Vector3.Cross(Vector3.up, dir) * (left ? -species.trackHalfWidth : species.trackHalfWidth);
            sys.AddLivePrint(species, p + side, dir, step, running);
        }
    }
}
