using System;
using Game.Player;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Animals
{
    public enum AnimalState
    {
        Feed,
        Roam,
        Alert,
        Flee,
        /// <summary>Wounded: running from the shot, bleeding.</summary>
        WoundedFlee,
        /// <summary>Wounded and lying down, slowly recovering.</summary>
        Rest,
        /// <summary>Wounded and cornered: running at the hunter.</summary>
        Charge,
        Dead,
        /// <summary>Escaped (left the area).</summary>
        Gone
    }

    /// <summary>
    /// The animal's state machine on a navigation mesh (plan section 17, "Animal AI"). Healthy: feeds at its home,
    /// roams around it, freezes and listens when unsure, flees when sure or when it hears a shot. Wounded: runs,
    /// bleeds, lies down to recover; a hunter who comes close is charged (plan: wolves and bears charge when wounded
    /// or cornered); fully recovered, it gets up and escapes. A kill drops it.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class AnimalBrain : MonoBehaviour
    {
        [SerializeField] AnimalBehaviourSettings settings;
        [SerializeField] AnimalSenses senses;
        [SerializeField] AnimalHealth health;
        [Tooltip("Visual root, tipped over when it dies.")]
        [SerializeField] Transform visual;
        [SerializeField] AnimalState startState = AnimalState.Feed;

        NavMeshAgent agent;
        FirstPersonController hunter;
        Vector3 home;
        float stateUntil, waitUntil, nextRepath;
        bool moving;

        public AnimalBehaviourSettings Settings => settings;
        public AnimalSenses Senses => senses;
        public AnimalHealth Health => health;
        public AnimalState State { get; private set; }
        /// <summary>True once it has healed from a wound while resting (the hunter was too slow).</summary>
        public bool Recovered { get; private set; }
        public event Action<AnimalState> StateChanged;
        public static event Action<AnimalBrain, AnimalState> AnyStateChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => AnyStateChanged = null;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (!senses) senses = GetComponent<AnimalSenses>();
            if (!health) health = GetComponent<AnimalHealth>();
            home = transform.position;
            if (health) health.Hit += OnHit;
            senses.HeardLoudNoise += OnLoudNoise;
        }

        void OnDestroy()
        {
            if (health) health.Hit -= OnHit;
            if (senses) senses.HeardLoudNoise -= OnLoudNoise;
        }

        void Start() => Enter(startState);

        void Update()
        {
            float s = senses.Suspicion;
            switch (State)
            {
                case AnimalState.Feed:
                    if (s >= settings.alertThreshold) Enter(AnimalState.Alert);
                    else if (Time.time > stateUntil) Enter(AnimalState.Roam);
                    break;

                case AnimalState.Roam:
                    if (s >= settings.alertThreshold) { Enter(AnimalState.Alert); break; }
                    if (moving && Arrived(0.5f))
                    {
                        moving = false;
                        waitUntil = Time.time + UnityEngine.Random.Range(settings.roamPause.x, settings.roamPause.y);
                    }
                    if (!moving && Time.time > waitUntil) MoveToRoamPoint();
                    break;

                case AnimalState.Alert:
                    Face(senses.LastStimulusPosition);
                    if (s >= 1f) Enter(AnimalState.Flee);
                    else if (s < settings.alertThreshold * 0.5f && Time.time > stateUntil) Enter(AnimalState.Roam);
                    break;

                case AnimalState.Flee:
                    if (Arrived(1.5f)) Enter(AnimalState.Gone);
                    break;

                case AnimalState.WoundedFlee:
                    if (Arrived(1.5f)) Enter(AnimalState.Rest);
                    break;

                case AnimalState.Rest:
                    if (health.Recover(Time.deltaTime)) { Recovered = true; Enter(AnimalState.Flee); break; }   // recovered: gets up and escapes
                    if (HunterDistance() < settings.chargeDistance) Enter(AnimalState.Charge);
                    else if (s >= 1f) Enter(AnimalState.WoundedFlee);                          // disturbed again: runs on
                    break;

                case AnimalState.Charge:
                    if (!hunter) { Enter(AnimalState.WoundedFlee); break; }
                    if (Time.time > nextRepath) { agent.SetDestination(hunter.transform.position); nextRepath = Time.time + 0.2f; }
                    if (HunterDistance() < 1.6f)
                    {
                        hunter.TakeHit(transform.position, settings.chargeKnockback);
                        Enter(AnimalState.WoundedFlee);
                    }
                    else if (Time.time > stateUntil) Enter(AnimalState.WoundedFlee);
                    break;
            }
        }

        void OnHit(HitOutcome outcome)
        {
            if (health.IsDead) Enter(AnimalState.Dead);
            else Enter(AnimalState.WoundedFlee);
        }

        void OnLoudNoise(Vector3 from)
        {
            if (State == AnimalState.Feed || State == AnimalState.Roam || State == AnimalState.Alert) Enter(AnimalState.Flee);
        }

        void Enter(AnimalState next)
        {
            State = next;
            float speedScale = health && health.LegWound ? settings.legWoundSpeed : 1f;
            switch (next)
            {
                case AnimalState.Feed:
                    Stop();
                    stateUntil = Time.time + settings.feedSeconds;
                    break;
                case AnimalState.Roam:
                    Go(settings.walkSpeed);
                    moving = false;
                    waitUntil = Time.time + 1f;
                    break;
                case AnimalState.Alert:
                    Stop();
                    stateUntil = Time.time + settings.alertListenSeconds;
                    break;
                case AnimalState.Flee:
                    Go(settings.fleeSpeed * speedScale);
                    agent.SetDestination(PointAway(settings.fleeDistance));
                    break;
                case AnimalState.WoundedFlee:
                    Go(settings.fleeSpeed * 0.8f * speedScale);
                    agent.SetDestination(PointAway(settings.woundedFleeDistance));
                    break;
                case AnimalState.Rest:
                    Stop();
                    senses.Calm();
                    break;
                case AnimalState.Charge:
                    if (!hunter) hunter = FindFirstObjectByType<FirstPersonController>();
                    Go(settings.chargeSpeed * speedScale);
                    nextRepath = 0f;
                    stateUntil = Time.time + settings.chargeGiveUpSeconds;
                    break;
                case AnimalState.Dead:
                    Stop();
                    agent.enabled = false;
                    senses.enabled = false;
                    if (visual) visual.localRotation = Quaternion.Euler(0f, 0f, 90f) * visual.localRotation;   // placeholder fall
                    break;
                case AnimalState.Gone:
                    gameObject.SetActive(false);
                    break;
            }
            StateChanged?.Invoke(next);
            AnyStateChanged?.Invoke(this, next);
        }

        void Go(float speed)
        {
            if (!agent.isOnNavMesh) return;
            agent.isStopped = false;
            agent.speed = speed;
            agent.acceleration = Mathf.Max(8f, speed * 3f);
        }

        void Stop()
        {
            if (agent.enabled && agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
            moving = false;
        }

        bool Arrived(float within) => agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance < within;

        float HunterDistance()
        {
            if (!hunter) hunter = FindFirstObjectByType<FirstPersonController>();
            return hunter ? Vector3.Distance(hunter.transform.position, transform.position) : float.MaxValue;
        }

        void MoveToRoamPoint()
        {
            for (int i = 0; i < 8; i++)
            {
                Vector2 r = UnityEngine.Random.insideUnitCircle * settings.roamRadius;
                if (NavMesh.SamplePosition(home + new Vector3(r.x, 0f, r.y), out var hit, 4f, NavMesh.AllAreas))
                {
                    agent.SetDestination(hit.position);
                    moving = true;
                    return;
                }
            }
            waitUntil = Time.time + 2f;
        }

        Vector3 PointAway(float distance)
        {
            Vector3 away = transform.position - senses.LastStimulusPosition;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -transform.forward;
            away.Normalize();
            foreach (float turn in new[] { 0f, 35f, -35f, 70f, -70f, 110f, -110f })
            {
                Vector3 p = transform.position + Quaternion.Euler(0f, turn, 0f) * away * distance;
                if (NavMesh.SamplePosition(p, out var hit, 15f, NavMesh.AllAreas)) return hit.position;
            }
            return transform.position + away * 10f;
        }

        void Face(Vector3 target)
        {
            Vector3 dir = target - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 180f * Time.deltaTime);
        }
    }
}
