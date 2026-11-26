using Game.Core.Alarms;
using Game.Core.Analytics;
using Game.Core.Audio;
using Game.Core.Noise;
using Game.Player;
using UnityEngine;

namespace Game.Animals
{
    /// <summary>
    /// Birds that give a predator away (plan section 6, "Alarm calls"; sounds CLU-01 and CLU-02).
    /// A Flock hides in a bush: a predator moving close, or running past further off, makes it burst out and fly
    /// away from the danger. A hunter walking upright too close flushes it too, and then nearby animals hear the
    /// birds and turn wary. The flock comes back after a minute or two.
    /// A Sentinel sits in a tree and scolds, a sharp call every few seconds, while a predator is near; it tires
    /// after a while and rests before it scolds again. Sentinels ignore the hunter.
    /// </summary>
    public sealed class AlarmBirds : MonoBehaviour
    {
        public enum Kind { Flock, Sentinel }

        [SerializeField] Kind kind = Kind.Flock;
        [SerializeField] SoundCue call;
        [Tooltip("Flock only: the burst of birds (a child particle system).")]
        [SerializeField] ParticleSystem birds;

        [Header("Flock")]
        [Tooltip("A predator moving this close (metres) flushes the flock.")]
        [SerializeField] float predatorRadius = 11f;
        [Tooltip("A running predator (fleeing, charging) flushes it from this far.")]
        [SerializeField] float runningPredatorRadius = 20f;
        [Tooltip("A moving hunter flushes it within this distance, plus the next value times the hunter's noise (0..1).")]
        [SerializeField] float hunterBaseRadius = 2.5f;
        [SerializeField] float hunterNoiseRadius = 9f;
        [Tooltip("Animals within this distance of birds the hunter flushed hear them and turn wary.")]
        [SerializeField] float heardRadius = 45f;
        [Tooltip("Seconds until the flock is back in its bush.")]
        [SerializeField] Vector2 returnSeconds = new Vector2(70f, 120f);

        [Header("Sentinel")]
        [Tooltip("A predator within this distance (moving or not) makes the sentinel scold.")]
        [SerializeField] float sentinelRadius = 24f;
        [SerializeField] Vector2 callGapSeconds = new Vector2(2.2f, 3.6f);
        [Tooltip("How long one bout of scolding lasts, then it rests.")]
        [SerializeField] float scoldSeconds = 14f;
        [SerializeField] Vector2 restSeconds = new Vector2(50f, 90f);

        const float RunningSpeed = 4f, MovingSpeed = 0.4f;

        float nextCheck, readyAt, scoldUntil, nextCall;
        FirstPersonController hunter;
        PlayerNoise hunterNoise;

        public Kind Type => kind;
        /// <summary>Flock: in its bush, ready to burst. Sentinel: not resting.</summary>
        public bool Ready => Time.time >= readyAt;

        void Update()
        {
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + 0.2f + Random.value * 0.1f;
            if (!hunter)
            {
                hunter = FindFirstObjectByType<FirstPersonController>();
                if (hunter) hunterNoise = hunter.GetComponent<PlayerNoise>();
            }
            if (kind == Kind.Flock) UpdateFlock();
            else UpdateSentinel();
        }

        static bool IsAlarming(AnimalBrain a) =>
            a && a.Settings && a.Settings.causesAlarmCalls && a.State != AnimalState.Dead && a.State != AnimalState.Gone;

        static float FlatDistance(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

        void UpdateFlock()
        {
            if (!Ready) return;
            Vector3 p = transform.position;
            foreach (var a in AnimalBrain.All)
            {
                if (!IsAlarming(a)) continue;
                float d = FlatDistance(a.transform.position, p), speed = a.Speed;
                if ((speed > MovingSpeed && d < predatorRadius) || (speed > RunningSpeed && d < runningPredatorRadius))
                {
                    Flush(a.transform.position, false);
                    return;
                }
            }
            if (hunter && hunter.SpeedFraction > 0.15f)
            {
                float r = hunterBaseRadius + hunterNoiseRadius * (hunterNoise ? hunterNoise.Level : 0.5f);
                if (FlatDistance(hunter.transform.position, p) < r) Flush(hunter.transform.position, true);
            }
        }

        /// <summary>The birds burst out and fly away from <paramref name="danger"/>.</summary>
        public void Flush(Vector3 danger, bool byHunter)
        {
            readyAt = Time.time + Random.Range(returnSeconds.x, returnSeconds.y);
            Vector3 p = transform.position;
            Vector3 away = p - danger; away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            away.Normalize();
            if (birds)
            {
                var vel = birds.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.World;
                float drift = Random.Range(6f, 8f);
                vel.x = new ParticleSystem.MinMaxCurve(away.x * drift);
                vel.y = new ParticleSystem.MinMaxCurve(0.6f);
                vel.z = new ParticleSystem.MinMaxCurve(away.z * drift);
                birds.Play(true);
            }
            if (call) AudioService.Play(call, p + Vector3.up);
            if (byHunter) NoiseEvents.Emit(p, heardRadius, NoiseKind.BirdAlarm);
            AlarmEvents.Raise(new AlarmCall(p, AlarmKind.BirdFlush, byHunter));
            Log(AlarmKind.BirdFlush, byHunter);
        }

        void UpdateSentinel()
        {
            if (!Ready) return;
            Vector3 p = transform.position;
            bool near = false;
            foreach (var a in AnimalBrain.All)
                if (IsAlarming(a) && FlatDistance(a.transform.position, p) < sentinelRadius) { near = true; break; }
            if (!near) { scoldUntil = 0f; return; }

            if (scoldUntil <= 0f)
            {
                scoldUntil = Time.time + scoldSeconds;
                nextCall = Time.time + Random.Range(0.2f, 1f);
                Log(AlarmKind.BirdScold, false);
            }
            if (Time.time >= scoldUntil)
            {
                readyAt = Time.time + Random.Range(restSeconds.x, restSeconds.y);
                scoldUntil = 0f;
                return;
            }
            if (Time.time < nextCall) return;
            nextCall = Time.time + Random.Range(callGapSeconds.x, callGapSeconds.y);
            if (call) AudioService.Play(call, p);
            AlarmEvents.Raise(new AlarmCall(p, AlarmKind.BirdScold, false));
        }

        void Log(AlarmKind k, bool byHunter)
        {
            float toHunter = hunter ? FlatDistance(hunter.transform.position, transform.position) : -1f;
            AnalyticsEvents.Log("alarm_call", ("kind", k), ("by", byHunter ? "hunter" : "predator"), ("hunter_distance", Mathf.RoundToInt(toHunter)));
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, kind == Kind.Flock ? predatorRadius : sentinelRadius);
        }
    }
}
