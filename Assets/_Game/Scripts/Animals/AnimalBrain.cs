using System;
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
        Gone
    }

    /// <summary>
    /// The animal's state machine on a navigation mesh (plan section 17, "Animal AI"): it feeds at its home, roams
    /// around it, freezes and listens when unsure (turning toward what it sensed), and flees once sure. Charging when
    /// wounded or cornered arrives with shooting.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class AnimalBrain : MonoBehaviour
    {
        [SerializeField] AnimalBehaviourSettings settings;
        [SerializeField] AnimalSenses senses;
        [SerializeField] AnimalState startState = AnimalState.Feed;

        NavMeshAgent agent;
        Vector3 home;
        float stateUntil, waitUntil;
        bool moving;

        public AnimalBehaviourSettings Settings => settings;
        public AnimalSenses Senses => senses;
        public AnimalState State { get; private set; }
        public event Action<AnimalState> StateChanged;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (!senses) senses = GetComponent<AnimalSenses>();
            home = transform.position;
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
                    if (moving && !agent.pathPending && agent.remainingDistance < 0.5f)
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
                    if (!agent.pathPending && agent.remainingDistance < 1.5f) Enter(AnimalState.Gone);
                    break;
            }
        }

        void Enter(AnimalState next)
        {
            State = next;
            switch (next)
            {
                case AnimalState.Feed:
                    Stop();
                    stateUntil = Time.time + settings.feedSeconds;
                    break;
                case AnimalState.Roam:
                    agent.isStopped = false;
                    agent.speed = settings.walkSpeed;
                    moving = false;
                    waitUntil = Time.time + 1f;
                    break;
                case AnimalState.Alert:
                    Stop();
                    stateUntil = Time.time + settings.alertListenSeconds;
                    break;
                case AnimalState.Flee:
                    agent.isStopped = false;
                    agent.speed = settings.fleeSpeed;
                    agent.acceleration = 25f;
                    agent.SetDestination(FleePoint());
                    break;
                case AnimalState.Gone:
                    gameObject.SetActive(false);   // escaped; the hunt result will handle this later
                    break;
            }
            StateChanged?.Invoke(next);
        }

        void Stop()
        {
            if (agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
            moving = false;
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

        Vector3 FleePoint()
        {
            Vector3 away = transform.position - senses.LastStimulusPosition;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -transform.forward;
            away.Normalize();
            foreach (float turn in new[] { 0f, 35f, -35f, 70f, -70f, 110f, -110f })
            {
                Vector3 p = transform.position + Quaternion.Euler(0f, turn, 0f) * away * settings.fleeDistance;
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
