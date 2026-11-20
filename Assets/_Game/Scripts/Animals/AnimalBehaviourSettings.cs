using UnityEngine;

namespace Game.Animals
{
    /// <summary>
    /// How one species moves and senses the hunter (plan section 6, "Animal behavior" and "Senses and environment").
    /// Starting values for the wolf; tune in play tests.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Animals/Animal Behaviour", fileName = "Behaviour")]
    public sealed class AnimalBehaviourSettings : ScriptableObject
    {
        public string displayName = "Wolf";

        [Header("Movement")]
        public float walkSpeed = 1.4f;
        public float fleeSpeed = 9f;
        [Tooltip("Roams within this distance of its home (the kill site).")]
        public float roamRadius = 16f;
        [Tooltip("Pause between roam targets, seconds (min, max).")]
        public Vector2 roamPause = new Vector2(3f, 8f);
        [Tooltip("Seconds spent feeding before it starts to roam.")]
        public float feedSeconds = 25f;
        public float fleeDistance = 80f;

        [Header("Hearing (uses the hunter's noise)")]
        [Tooltip("Multiplies the distance at which the hunter's noise is heard.")]
        public float hearingMultiplier = 1.2f;
        [Tooltip("Suspicion per second when the noise is heard right next to it.")]
        public float hearingRate = 0.9f;

        [Header("Smell (needs the wind blowing from the hunter toward it)")]
        public float smellRange = 45f;
        [Tooltip("Half-angle of the scent cone that drifts downwind of the hunter, degrees.")]
        public float scentHalfAngle = 25f;
        public float smellRate = 1.5f;

        [Header("Sight")]
        public float sightRange = 55f;
        public float fieldOfView = 220f;
        public float sightRate = 1.2f;
        [Tooltip("How visible a hunter standing still is, compared with one walking at full speed (1).")]
        [Range(0f, 1f)] public float stillVisibility = 0.1f;
        [Tooltip("Crouching multiplies visibility by this.")]
        [Range(0f, 1f)] public float crouchVisibility = 0.45f;
        public float eyeHeight = 0.7f;

        [Header("Awareness")]
        [Tooltip("Suspicion (0..1) at which it freezes and listens. At 1 it flees.")]
        [Range(0f, 1f)] public float alertThreshold = 0.35f;
        [Tooltip("Steady calming rate per second. Signals weaker than this never build suspicion; with none, it fades at this rate.")]
        public float suspicionDecay = 0.12f;
        [Tooltip("Minimum seconds it stays alert before calming down.")]
        public float alertListenSeconds = 6f;
        [Tooltip("Closer than this, it notices the hunter whatever the senses say.")]
        public float panicDistance = 6f;

        [Header("Health and wounds")]
        public float maxHealth = 100f;
        [Tooltip("Damage of a body (gut, haunch) hit. A second body hit usually kills.")]
        public float bodyDamage = 60f;
        public float legDamage = 35f;
        [Tooltip("A leg wound multiplies its speed by this (it limps).")]
        [Range(0.2f, 1f)] public float legWoundSpeed = 0.6f;
        [Tooltip("How far a wounded animal runs before it lies down to rest.")]
        public float woundedFleeDistance = 45f;
        [Tooltip("Health regained per second while resting. Fully recovered, it gets up and escapes.")]
        public float recoverPerSecond = 0.6f;

        [Header("Charge (wounded and cornered)")]
        [Tooltip("A resting wounded animal charges a hunter who comes this close.")]
        public float chargeDistance = 9f;
        public float chargeSpeed = 11f;
        [Tooltip("Push given to the hunter when the charge connects.")]
        public float chargeKnockback = 7f;
        public float chargeGiveUpSeconds = 6f;
    }
}
