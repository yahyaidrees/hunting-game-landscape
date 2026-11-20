using Game.Core.Cover;
using Game.Core.Wind;
using Game.Player;
using UnityEngine;

namespace Game.Animals
{
    public enum Stimulus
    {
        None,
        Sound,
        Scent,
        Sight,
        TooClose
    }

    /// <summary>
    /// Builds the animal's suspicion of the hunter (0..1) from hearing (the hunter's noise), smell (scent carried
    /// downwind) and sight (movement in the open; crouching and standing still help; hills block the view).
    /// With no stimulus, suspicion slowly fades.
    /// </summary>
    public sealed class AnimalSenses : MonoBehaviour
    {
        [SerializeField] AnimalBehaviourSettings settings;

        readonly RaycastHit[] hits = new RaycastHit[16];
        FirstPersonController hunter;
        PlayerNoise noise;

        public float Suspicion { get; private set; }
        public Stimulus LastStimulus { get; private set; }
        public Vector3 LastStimulusPosition { get; private set; }
        /// <summary>This frame's suspicion gain per second from each sense (for the dev overlay and tuning).</summary>
        public float Hearing { get; private set; }
        public float Smell { get; private set; }
        public float Sight { get; private set; }
        public float DistanceToHunter { get; private set; }

        void Update()
        {
            if (!hunter)
            {
                hunter = FindFirstObjectByType<FirstPersonController>();
                if (hunter) noise = hunter.GetComponent<PlayerNoise>();
                if (!hunter) return;
            }
            Vector3 me = transform.position, hp = hunter.transform.position;
            float d = Vector3.Distance(me, hp);
            DistanceToHunter = d;

            Hearing = HearingGain(d);
            Smell = SmellGain(me, hp, d);
            Sight = SightGain(me, hp);
            float proximity = d < settings.panicDistance ? 2f : 0f;

            // Suspicion moves by what it senses minus a steady calming rate: faint signals (a hunter standing still,
            // sneaking crouched far away) never add up, clear ones (walking in the open, scent, loud steps) do.
            float gain = Hearing + Smell + Sight + proximity;
            Suspicion = Mathf.Clamp01(Suspicion + (gain - settings.suspicionDecay) * Time.deltaTime);
            if (gain > settings.suspicionDecay)
            {
                LastStimulusPosition = hp;
                LastStimulus = proximity > 0f ? Stimulus.TooClose
                             : Smell >= Hearing && Smell >= Sight ? Stimulus.Scent
                             : Hearing >= Sight ? Stimulus.Sound : Stimulus.Sight;
            }
        }

        float HearingGain(float d)
        {
            if (!noise) return 0f;
            float radius = noise.HearingRadius * settings.hearingMultiplier;
            return radius > 0.01f && d < radius ? settings.hearingRate * (1f - d / radius) : 0f;
        }

        float SmellGain(Vector3 me, Vector3 hp, float d)
        {
            var wind = WindSystem.Active;
            if (!wind) return 0f;
            float range = settings.smellRange * Mathf.Lerp(0.5f, 1.2f, wind.Strength);
            if (d >= range) return 0f;
            Vector3 toMe = me - hp;
            toMe.y = 0f;
            // the scent drifts downwind from the hunter: smelt when the animal sits inside that cone
            if (Vector3.Angle(wind.Direction, toMe) > settings.scentHalfAngle) return 0f;
            return settings.smellRate * (1f - d / range);
        }

        float SightGain(Vector3 me, Vector3 hp)
        {
            Vector3 eye = me + Vector3.up * settings.eyeHeight;
            Vector3 target = hp + Vector3.up * (hunter.IsCrouched ? 0.9f : 1.5f);
            Vector3 dir = target - eye;
            float dist = dir.magnitude;
            if (dist >= settings.sightRange) return 0f;
            if (Vector3.Angle(transform.forward, dir) > settings.fieldOfView * 0.5f) return 0f;
            float cover = LineOfSight(eye, dir / dist, dist);
            if (cover <= 0f) return 0f;
            float visibility = Mathf.Lerp(settings.stillVisibility, 1f, hunter.SpeedFraction) * cover;
            if (hunter.IsCrouched) visibility *= settings.crouchVisibility;
            return settings.sightRate * visibility * (1f - dist / settings.sightRange);
        }

        /// <summary>
        /// 0 when solid cover (ground, rock, trunk) blocks the view; otherwise the product of every concealing layer
        /// (bush, young fir) the line passes through, 1 in the open.
        /// </summary>
        float LineOfSight(Vector3 from, Vector3 dir, float dist)
        {
            int n = Physics.RaycastNonAlloc(from, dir, hits, dist - 0.3f, ~0, QueryTriggerInteraction.Collide);
            float visibility = 1f;
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c is CharacterController || c.transform.IsChildOf(transform)) continue;
                if (c.isTrigger)
                {
                    if (c.TryGetComponent(out Concealment soft)) visibility *= soft.visibility;
                    continue;
                }
                return 0f;
            }
            return visibility;
        }

        /// <summary>Clears suspicion (for example when the animal comes back later).</summary>
        public void Calm() => Suspicion = 0f;
    }
}
