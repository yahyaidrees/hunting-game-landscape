using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.Tracking
{
    /// <summary>
    /// One readable sign on a trail (a print, scat, fur, a kill site). Inspecting it discovers it, which draws the
    /// trail up to it on the hunter's map. Placed as a child of its Trail.
    /// </summary>
    public sealed class Clue : MonoBehaviour
    {
        public ClueKind kind;
        [Tooltip("Hours on top of the trail's age (scat can be older than the prints).")]
        public float extraAgeHours;
        [Tooltip("Replaces the general text on the clue card when set.")]
        [TextArea(2, 5)] public string hintOverride;
        [Tooltip("Glow ring under the clue (decal), switched on by the tracking system.")]
        [SerializeField] DecalProjector glow;

        float highlightUntil = -1f;

        public Trail Trail { get; private set; }
        public float PathDistance { get; private set; }
        public bool Discovered { get; private set; }
        public float AgeHours => (Trail ? Trail.ageHours : 0f) + extraAgeHours;
        public Vector3 Position => transform.position;
        public bool IsHighlighted => Time.time < highlightUntil;

        public void Bind(Trail trail, float pathDistance)
        {
            Trail = trail;
            PathDistance = pathDistance;
        }

        /// <summary>Marks it found. Called through TrackingSystem.Discover so counts and events stay in step.</summary>
        internal void MarkDiscovered()
        {
            Discovered = true;
            highlightUntil = -1f;
            if (Trail) Trail.Reveal(PathDistance);
        }

        public void Highlight(float seconds) => highlightUntil = Time.time + seconds;

        public void SetGlow(float strength)
        {
            if (!glow) return;
            bool on = strength > 0.01f;
            if (glow.gameObject.activeSelf != on) glow.gameObject.SetActive(on);
            if (on) glow.fadeFactor = Mathf.Clamp01(strength);
        }
    }
}
