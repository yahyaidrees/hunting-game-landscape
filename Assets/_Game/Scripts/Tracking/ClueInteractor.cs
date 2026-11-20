using System;
using Game.Player;
using UnityEngine;

namespace Game.Tracking
{
    /// <summary>
    /// On the hunter: finds the clue being looked at (shows the Inspect button) and inspects a clue when the player
    /// taps near it on screen (plan section 6: "Tap a print to inspect"). Works on screen positions, so clues need no colliders.
    /// </summary>
    public sealed class ClueInteractor : MonoBehaviour
    {
        [SerializeField] Camera viewCamera;

        public Clue Target { get; private set; }
        public event Action<Clue> Inspected;

        void Awake()
        {
            if (!viewCamera) viewCamera = GetComponentInChildren<Camera>();
        }

        void OnEnable() => TouchLookArea.Tapped += OnTap;
        void OnDisable() => TouchLookArea.Tapped -= OnTap;

        void Update()
        {
            var sys = TrackingSystem.Active;
            Target = sys && viewCamera ? FindAimed(sys) : null;
        }

        Clue FindAimed(TrackingSystem sys)
        {
            var s = sys.Settings;
            Vector3 eye = viewCamera.transform.position, fwd = viewCamera.transform.forward;
            Clue best = null;
            float bestAngle = s.aimAngle;
            foreach (var c in sys.Clues)
            {
                if ((c.Position - transform.position).sqrMagnitude > s.inspectRange * s.inspectRange) continue;
                float angle = Vector3.Angle(fwd, c.Position - eye);
                if (angle < bestAngle) { bestAngle = angle; best = c; }
            }
            return best;
        }

        void OnTap(Vector2 screenPosition)
        {
            var sys = TrackingSystem.Active;
            if (!sys || !viewCamera) return;
            var s = sys.Settings;
            float radius = s.tapRadiusReferencePixels * Screen.height / 1080f;
            Clue best = null;
            float bestPx = radius * radius;
            foreach (var c in sys.Clues)
            {
                if ((c.Position - transform.position).sqrMagnitude > s.inspectRange * s.inspectRange) continue;
                Vector3 sp = viewCamera.WorldToScreenPoint(c.Position);
                if (sp.z <= 0f) continue;
                float d = ((Vector2)sp - screenPosition).sqrMagnitude;
                if (d < bestPx) { bestPx = d; best = c; }
            }
            if (best) Inspect(best);
        }

        public void InspectTarget()
        {
            if (Target) Inspect(Target);
        }

        public void Inspect(Clue clue)
        {
            var sys = TrackingSystem.Active;
            if (!sys || !clue) return;
            sys.Discover(clue);
            Inspected?.Invoke(clue);
        }
    }
}
