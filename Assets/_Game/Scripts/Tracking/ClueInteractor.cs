using System;
using Game.Player;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.Tracking
{
    /// <summary>
    /// On the hunter: finds what is being looked at and inspects it (plan section 6: "Tap a print to inspect").
    /// Clues come first; otherwise any print on the ground can be read: the trail's prints and the live ones animals
    /// leave. The Inspect button shows while an unread clue or print is in reach near the view centre, and a small
    /// ring marks the aimed print. A tap near a clue or print on screen inspects it too. Works on screen positions,
    /// so nothing needs a collider.
    /// </summary>
    public sealed class ClueInteractor : MonoBehaviour
    {
        [SerializeField] Camera viewCamera;
        [Tooltip("Diameter of the ring that marks the aimed print, in metres.")]
        [SerializeField] float printMarkerSize = 0.38f;

        DecalProjector printMarker;

        /// <summary>The clue near the view centre (found or not), or null.</summary>
        public Clue Target { get; private set; }
        /// <summary>The unread print near the view centre when no unread clue is aimed at, or null.</summary>
        public Footprint PrintTarget { get; private set; }
        /// <summary>Something can be inspected right now (the Inspect button).</summary>
        public bool HasInspectable => (Target && !Target.Discovered) || PrintTarget != null;

        public event Action<Clue> Inspected;
        public event Action<Footprint> PrintInspected;

        void Awake()
        {
            if (!viewCamera) viewCamera = GetComponentInChildren<Camera>();
        }

        void OnEnable() => TouchLookArea.Tapped += OnTap;

        void OnDisable()
        {
            TouchLookArea.Tapped -= OnTap;
            if (printMarker) printMarker.gameObject.SetActive(false);
        }

        void Update()
        {
            var sys = TrackingSystem.Active;
            Target = sys && viewCamera ? FindAimed(sys) : null;
            PrintTarget = sys && viewCamera && !(Target && !Target.Discovered)
                ? sys.FindAimedPrint(transform.position, viewCamera.transform.position, viewCamera.transform.forward)
                : null;
            UpdateMarker(sys);
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

        // the glow ring material, smaller, on the print the Inspect button would read
        void UpdateMarker(TrackingSystem sys)
        {
            bool show = PrintTarget != null && sys && sys.Settings.glowMaterial;
            if (show && !printMarker)
            {
                var go = new GameObject("PrintMarker");
                printMarker = go.AddComponent<DecalProjector>();
                printMarker.material = sys.Settings.glowMaterial;
                printMarker.size = new Vector3(printMarkerSize, printMarkerSize, 0.5f);
                printMarker.pivot = Vector3.zero;
                printMarker.drawDistance = 20f;
                printMarker.startAngleFade = 50f; printMarker.endAngleFade = 75f;
            }
            if (!printMarker) return;
            if (printMarker.gameObject.activeSelf != show) printMarker.gameObject.SetActive(show);
            if (!show) return;
            printMarker.transform.SetPositionAndRotation(PrintTarget.position, Quaternion.LookRotation(Vector3.down, PrintTarget.direction));
            printMarker.fadeFactor = 0.55f + 0.25f * Mathf.Sin(Time.time * 5f);
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
            if (best) { Inspect(best); return; }
            // prints are small: a tighter radius, so a tap meant for looking around does not open a card
            var print = sys.FindTappedPrint(viewCamera, screenPosition, transform.position, radius * 0.6f);
            if (print != null) InspectPrint(print);
        }

        public void InspectTarget()
        {
            if (Target && !Target.Discovered) Inspect(Target);
            else if (PrintTarget != null) InspectPrint(PrintTarget);
            else if (Target) Inspect(Target);
        }

        public void Inspect(Clue clue)
        {
            var sys = TrackingSystem.Active;
            if (!sys || !clue) return;
            sys.Discover(clue);
            Inspected?.Invoke(clue);
        }

        public void InspectPrint(Footprint print)
        {
            var sys = TrackingSystem.Active;
            if (!sys || print == null) return;
            sys.ReadPrint(print);
            PrintInspected?.Invoke(print);
        }
    }
}
