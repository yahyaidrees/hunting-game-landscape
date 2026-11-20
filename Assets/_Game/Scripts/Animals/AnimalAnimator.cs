using UnityEngine;
using UnityEngine.AI;

namespace Game.Animals
{
    /// <summary>
    /// Drives an animal's Animator from its brain (plan section 15: blend trees for speed changes hide the joins).
    /// Speed feeds the locomotion blend (idle, walk, trot, run); each brain state cross-fades to its animation;
    /// a hit plays a short flinch first; a leg wound switches to the limping set.
    /// </summary>
    public sealed class AnimalAnimator : MonoBehaviour
    {
        public const string Locomotion = "Locomotion", InjuredLocomotion = "InjuredLocomotion", Eat = "Eat",
            Alert = "Alert", Rest = "Rest", Death = "Death", Attack = "Attack", Hit = "Hit";
        public const string MoveScaleParam = "MoveScale";
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int MoveScaleId = Animator.StringToHash(MoveScaleParam);

        [SerializeField] Animator animator;
        [SerializeField] AnimalBrain brain;
        [Tooltip("Ground speed (m/s) of the fastest locomotion clip. Faster than this, the legs cycle faster instead of sliding.")]
        [SerializeField] float runClipSpeed = 5.6f;
        [Tooltip("Upper limit of that speed-up; past it the feet slide a little rather than look frantic.")]
        [SerializeField] float maxPlaybackScale = 1.7f;
        [SerializeField] float crossFadeSeconds = 0.25f;
        [Tooltip("Seconds the hit flinch plays before the next animation.")]
        [SerializeField] float hitSeconds = 0.45f;

        NavMeshAgent agent;
        AnimalHealth health;
        float speed;
        string pending;
        float pendingAt;

        void Awake()
        {
            if (!brain) brain = GetComponent<AnimalBrain>();
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<AnimalHealth>();
            brain.StateChanged += OnState;
            brain.Attacked += OnAttack;
            if (health) health.Hit += OnHit;
            // the navigation agent moves the animal; a RootMotionSink next to the Animator drops the clips' own travel
            if (!animator.TryGetComponent<RootMotionSink>(out _)) animator.gameObject.AddComponent<RootMotionSink>();
        }

        void OnDestroy()
        {
            if (brain) { brain.StateChanged -= OnState; brain.Attacked -= OnAttack; }
            if (health) health.Hit -= OnHit;
        }

        void Update()
        {
            float target = agent && agent.enabled ? agent.velocity.magnitude : 0f;
            speed = Mathf.Lerp(speed, target, 1f - Mathf.Exp(-8f * Time.deltaTime));
            animator.SetFloat(SpeedId, speed);
            animator.SetFloat(MoveScaleId, Mathf.Clamp(speed / Mathf.Max(0.1f, runClipSpeed), 1f, maxPlaybackScale));
            if (pending != null && Time.time >= pendingAt)
            {
                animator.CrossFadeInFixedTime(pending, crossFadeSeconds);
                pending = null;
            }
        }

        void OnState(AnimalState state)
        {
            string target = StateFor(state);
            if (target == null) return;
            if (pending != null && state != AnimalState.Dead) { pending = target; return; }   // let the flinch finish
            pending = null;
            animator.CrossFadeInFixedTime(target, state == AnimalState.Dead ? 0.1f : crossFadeSeconds);
        }

        string StateFor(AnimalState state)
        {
            switch (state)
            {
                case AnimalState.Feed: return Eat;
                case AnimalState.Alert: return Alert;
                case AnimalState.Rest: return Rest;
                case AnimalState.Dead: return Death;
                case AnimalState.WoundedFlee: return health && health.LegWound ? InjuredLocomotion : Locomotion;
                case AnimalState.Gone: return null;
                default: return health && health.LegWound ? InjuredLocomotion : Locomotion;
            }
        }

        void OnHit(HitOutcome outcome)
        {
            if (health.IsDead) return;   // the Dead state plays the death instead
            animator.CrossFadeInFixedTime(Hit, 0.08f);
            pending = StateFor(brain.State) ?? Locomotion;
            pendingAt = Time.time + hitSeconds;
        }

        void OnAttack()
        {
            animator.CrossFadeInFixedTime(Attack, 0.05f);
            pending = StateFor(brain.State) ?? Locomotion;
            pendingAt = Time.time + 0.6f;
        }
    }
}
