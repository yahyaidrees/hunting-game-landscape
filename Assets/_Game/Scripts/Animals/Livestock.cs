using Game.Core.Noise;
using UnityEngine;

namespace Game.Animals
{
    /// <summary>
    /// Farm animals going about their day (plan section 8: the farm is alive): plays one of its idles (graze, chew,
    /// breathe, look around) for a while, now and then walks slowly to another spot inside its area, following the
    /// ground. A gunshot within earshot startles it into a short run away from the sound, still inside its area.
    /// Not part of the hunt: no senses, no health. Animation stops while it is out of view (Animator culling).
    /// </summary>
    public sealed class Livestock : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [Tooltip("Animator states played while standing, chosen at random (graze, chew, breathe, look around).")]
        [SerializeField] string[] idleStates = { "IdleGraze" };
        [SerializeField] string walkState = "Walk";
        [SerializeField] string runState = "Run";

        [Header("Area (world space; the animal stays inside it)")]
        [SerializeField] Vector3 areaCentre;
        [SerializeField] Vector2 areaHalf = new Vector2(3f, 3f);
        [SerializeField] float areaYaw;

        [Header("Movement")]
        [SerializeField] float walkSpeed = 0.6f;
        [SerializeField] float runSpeed = 3f;
        [SerializeField] float turnDegreesPerSecond = 90f;
        [Tooltip("Seconds standing before it may wander.")]
        [SerializeField] Vector2 idleSeconds = new Vector2(6f, 18f);
        [Tooltip("Seconds per idle clip before it may pick another.")]
        [SerializeField] Vector2 idleClipSeconds = new Vector2(4f, 9f);
        [Tooltip("A gunshot closer than this (metres) startles it.")]
        [SerializeField] float startleRadius = 70f;

        enum Mode { Idle, Walk, Run }
        Mode mode;
        Vector3 target;
        float modeUntil, nextIdleClip;
        string currentState;
        readonly RaycastHit[] hits = new RaycastHit[8];
        Collider[] own;

        void Awake()
        {
            if (!animator) animator = GetComponentInChildren<Animator>();
            own = GetComponentsInChildren<Collider>();
        }

        void OnEnable() => NoiseEvents.Emitted += OnNoise;
        void OnDisable() => NoiseEvents.Emitted -= OnNoise;

        void Start()
        {
            // a different moment for every animal, so a herd does not move in step
            Idle(Random.Range(0f, idleSeconds.y));
            if (animator) animator.Update(Random.Range(0f, 3f));
        }

        /// <summary>Sets the area it stays in (the builder places the animals).</summary>
        public void SetArea(Vector3 centre, Vector2 half, float yaw)
        {
            areaCentre = centre; areaHalf = half; areaYaw = yaw;
        }

        void OnNoise(Vector3 position, float radius, NoiseKind kind)
        {
            if (kind != NoiseKind.Gunshot || (position - transform.position).sqrMagnitude > startleRadius * startleRadius) return;
            Vector3 away = transform.position - position; away.y = 0f;
            target = Clamp(transform.position + away.normalized * Random.Range(4f, 8f));
            mode = Mode.Run;
            modeUntil = Time.time + Random.Range(2f, 3.5f);
            Play(runState);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            switch (mode)
            {
                case Mode.Idle:
                    if (Time.time >= nextIdleClip && idleStates.Length > 1)
                    {
                        Play(idleStates[Random.Range(0, idleStates.Length)]);
                        nextIdleClip = Time.time + Random.Range(idleClipSeconds.x, idleClipSeconds.y);
                    }
                    if (Time.time >= modeUntil)
                    {
                        target = RandomPointInArea();
                        mode = Mode.Walk;
                        Play(walkState);
                    }
                    break;
                case Mode.Walk:
                case Mode.Run:
                    float speed = mode == Mode.Run ? runSpeed : walkSpeed;
                    Vector3 to = target - transform.position; to.y = 0f;
                    bool arrived = to.magnitude < 0.3f || (mode == Mode.Run && Time.time >= modeUntil);
                    if (arrived) { Idle(Random.Range(idleSeconds.x, idleSeconds.y)); break; }
                    var want = Quaternion.LookRotation(to.normalized, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnDegreesPerSecond * (mode == Mode.Run ? 2.5f : 1f) * dt);
                    // walk forward only once roughly facing the target: no sliding sideways
                    float facing = Mathf.Clamp01(Vector3.Dot(transform.forward, to.normalized));
                    Vector3 next = transform.position + transform.forward * speed * facing * facing * dt;
                    transform.position = Ground(Clamp(next));
                    break;
            }
        }

        void Idle(float seconds)
        {
            mode = Mode.Idle;
            modeUntil = Time.time + seconds;
            nextIdleClip = 0f;
            if (idleStates.Length > 0) Play(idleStates[Random.Range(0, idleStates.Length)]);
        }

        void Play(string state)
        {
            if (!animator || string.IsNullOrEmpty(state) || state == currentState) return;
            animator.CrossFadeInFixedTime(state, 0.35f);
            currentState = state;
        }

        Vector3 RandomPointInArea()
        {
            var local = new Vector3(Random.Range(-areaHalf.x, areaHalf.x), 0f, Random.Range(-areaHalf.y, areaHalf.y));
            return areaCentre + Quaternion.Euler(0f, areaYaw, 0f) * local;
        }

        Vector3 Clamp(Vector3 p)
        {
            var inv = Quaternion.Euler(0f, -areaYaw, 0f);
            Vector3 l = inv * (p - areaCentre);
            l.x = Mathf.Clamp(l.x, -areaHalf.x, areaHalf.x);
            l.z = Mathf.Clamp(l.z, -areaHalf.y, areaHalf.y);
            Vector3 r = areaCentre + Quaternion.Euler(0f, areaYaw, 0f) * new Vector3(l.x, 0f, l.z);
            r.y = p.y;
            return r;
        }

        // the ground under a point, ignoring the animal's own collider and anything that is not solid
        Vector3 Ground(Vector3 p)
        {
            int n = Physics.RaycastNonAlloc(p + Vector3.up * 2f, Vector3.down, hits, 6f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; Vector3 point = p;
            for (int i = 0; i < n; i++)
            {
                bool mine = false;
                foreach (var c in own) if (hits[i].collider == c) { mine = true; break; }
                if (mine || hits[i].collider.GetComponentInParent<Livestock>()) continue;
                if (hits[i].distance < best) { best = hits[i].distance; point = hits[i].point; }
            }
            return point;
        }
    }
}
