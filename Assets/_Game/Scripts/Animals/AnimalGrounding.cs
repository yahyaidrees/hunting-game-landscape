using UnityEngine;
using UnityEngine.AI;

namespace Game.Animals
{
    /// <summary>
    /// Fits an animal's visible body to the ground and smooths how it turns, without touching the navigation agent
    /// (the root stays upright and on the navigation mesh; only the model under it moves):
    /// - pitch from the ground under its front and back feet; roll too when it lies down or dies, so it rests on a slope
    ///   instead of sinking into it, and only a little when standing (animals stay upright sideways);
    /// - height from the real ground, not the slightly smoothed navigation mesh;
    /// - the body follows the agent's heading with a short lag, so path corners do not snap, and leans into turns at speed.
    /// </summary>
    [DefaultExecutionOrder(100)]   // after the agent and the brain have moved the root this frame
    public sealed class AnimalGrounding : MonoBehaviour
    {
        [SerializeField] Transform model;
        [SerializeField] AnimalBrain brain;
        [Tooltip("Half the distance between the front and back feet (metres).")]
        [SerializeField] float halfLength = 0.55f;
        [Tooltip("Half the distance between the left and right feet (metres).")]
        [SerializeField] float halfWidth = 0.16f;
        [SerializeField] float maxPitch = 28f;
        [SerializeField] float maxRoll = 22f;
        [Tooltip("Share of the ground's side slope the body follows while standing (lying down follows all of it).")]
        [SerializeField, Range(0f, 1f)] float standingRoll = 0.3f;
        [Tooltip("Most the body tilts sideways with the slope while standing (degrees): animals stay upright.")]
        [SerializeField] float maxStandingRoll = 8f;
        [Tooltip("Seconds for the body to catch up with a change of slope or height.")]
        [SerializeField] float tiltSmoothing = 0.12f;
        [Tooltip("Seconds of lag between the agent's heading and the body's (hides snaps at path corners).")]
        [SerializeField] float yawSmoothing = 0.1f;
        [Tooltip("Lean into turns: degrees of roll per (degree per second of turning x metre per second).")]
        [SerializeField] float turnLean = 0.004f;
        [SerializeField] float maxLean = 9f;
        [Tooltip("Largest height correction from the navigation mesh to the real ground (metres).")]
        [SerializeField] float maxHeightOffset = 0.35f;

        NavMeshAgent agent;
        readonly RaycastHit[] hits = new RaycastHit[8];
        float yaw, yawVelocity, lastRootYaw, pitch, roll, height;
        bool started;

        public float TurnRate { get; private set; }   // degrees per second of the root, smoothed

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (!brain) brain = GetComponent<AnimalBrain>();
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || !model) return;
            float rootYaw = transform.eulerAngles.y;
            if (!started) { yaw = lastRootYaw = rootYaw; started = true; }

            // heading: follow the root with a short lag
            float rate = Mathf.DeltaAngle(lastRootYaw, rootYaw) / dt;
            lastRootYaw = rootYaw;
            TurnRate = Mathf.Lerp(TurnRate, rate, 1f - Mathf.Exp(-10f * dt));
            yaw = Mathf.SmoothDampAngle(yaw, rootYaw, ref yawVelocity, yawSmoothing, Mathf.Infinity, dt);

            // ground under the feet, in the body's own heading
            Quaternion heading = Quaternion.Euler(0f, yaw, 0f);
            Vector3 fwd = heading * Vector3.forward, right = heading * Vector3.right, p = transform.position;
            bool front = Ground(p + fwd * halfLength, out float yFront);
            bool back = Ground(p - fwd * halfLength, out float yBack);
            bool left = Ground(p - right * halfWidth, out float yLeft);
            bool rightHit = Ground(p + right * halfWidth, out float yRight);

            var state = brain ? brain.State : AnimalState.Feed;
            bool lying = state == AnimalState.Rest || state == AnimalState.Dead;
            float targetPitch = front && back ? -Mathf.Atan2(yFront - yBack, 2f * halfLength) * Mathf.Rad2Deg : 0f;
            float targetRoll = left && rightHit ? Mathf.Atan2(yRight - yLeft, 2f * halfWidth) * Mathf.Rad2Deg * (lying ? 1f : standingRoll) : 0f;
            if (!lying) targetRoll = Mathf.Clamp(targetRoll, -maxStandingRoll, maxStandingRoll);
            float speed = agent && agent.enabled ? agent.velocity.magnitude : 0f;
            if (!lying) targetRoll += Mathf.Clamp(-TurnRate * speed * turnLean, -maxLean, maxLean);   // lean into the turn
            targetPitch = Mathf.Clamp(targetPitch, -maxPitch, maxPitch);
            targetRoll = Mathf.Clamp(targetRoll, -maxRoll, maxRoll);

            // height: the body sits on the real ground between its feet
            float targetHeight = 0f;
            if (front || back)
            {
                float ground = front && back ? (yFront + yBack) * 0.5f : front ? yFront : yBack;
                targetHeight = Mathf.Clamp(ground - p.y, -maxHeightOffset, maxHeightOffset);
            }

            float k = 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, tiltSmoothing));
            pitch = Mathf.Lerp(pitch, targetPitch, k);
            roll = Mathf.Lerp(roll, targetRoll, k);
            height = Mathf.Lerp(height, targetHeight, k);

            model.SetPositionAndRotation(p + Vector3.up * height, heading * Quaternion.Euler(pitch, 0f, roll));
        }

        // the highest solid ground under a point (not the animal itself, not triggers like bushes or hit zones)
        bool Ground(Vector3 at, out float y)
        {
            y = 0f;
            int n = Physics.RaycastNonAlloc(at + Vector3.up * 1.2f, Vector3.down, hits, 3f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c.transform.IsChildOf(transform) || c is CharacterController) continue;
                if (hits[i].distance < best) { best = hits[i].distance; y = hits[i].point.y; }
            }
            return best < float.MaxValue;
        }
    }
}
