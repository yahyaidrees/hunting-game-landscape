using Game.Core.Audio;
using UnityEngine;

namespace Game.Animals
{
    /// <summary>
    /// An animal's calls, placed in the world so the hunter can find it by ear (plan section 14, "3D sound").
    /// A calm wolf howls now and then (a far, directional hint), barks when it is unsure, barks hard as it charges,
    /// and whines when it is hit or dies.
    /// </summary>
    public sealed class AnimalVoice : MonoBehaviour
    {
        [SerializeField] AnimalBrain brain;
        [SerializeField] SoundCue howl;
        [SerializeField] SoundCue alarm;
        [SerializeField] SoundCue charge;
        [SerializeField] SoundCue hurt;
        [Tooltip("Seconds before the first howl of the hunt (min, max): an early hint of where the animal is.")]
        [SerializeField] Vector2 firstHowl = new Vector2(6f, 14f);
        [Tooltip("Seconds between howls while it is calm (min, max).")]
        [SerializeField] Vector2 howlInterval = new Vector2(45f, 100f);
        [Tooltip("Chance it barks when it turns to listen.")]
        [SerializeField, Range(0f, 1f)] float alarmChance = 0.6f;
        [SerializeField] float mouthHeight = 0.7f;

        AnimalHealth health;
        float nextHowl;

        void Awake()
        {
            if (!brain) brain = GetComponent<AnimalBrain>();
            health = GetComponent<AnimalHealth>();
            brain.StateChanged += OnState;
            brain.Attacked += OnAttack;
            if (health) health.Hit += OnHit;
            nextHowl = Time.time + Random.Range(firstHowl.x, firstHowl.y);
        }

        void OnDestroy()
        {
            if (brain) { brain.StateChanged -= OnState; brain.Attacked -= OnAttack; }
            if (health) health.Hit -= OnHit;
        }

        void Update()
        {
            if (Time.time < nextHowl) return;
            var s = brain.State;
            bool calm = s == AnimalState.Feed || s == AnimalState.Roam || (s == AnimalState.Rest && !(health && health.IsWounded));
            if (calm) Call(howl);
            nextHowl = Time.time + Random.Range(howlInterval.x, howlInterval.y);
        }

        void OnState(AnimalState state)
        {
            switch (state)
            {
                case AnimalState.Alert: if (Random.value < alarmChance) Call(alarm); break;
                case AnimalState.Charge: Call(charge); break;
            }
        }

        void OnAttack() => Call(charge);

        void OnHit(HitOutcome outcome) => Call(hurt);

        void Call(SoundCue cue) => AudioService.Play(cue, transform.position + Vector3.up * mouthHeight);
    }
}
