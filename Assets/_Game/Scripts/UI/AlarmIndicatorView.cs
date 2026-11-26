using Game.Core.Alarms;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The compass pulse for alarm calls (plan section 6: "a sound with a direction, plus a small pulse on the
    /// compass for players with sound off"). Under the wind panel, a marker slides left or right to the call's
    /// bearing (centre = straight ahead; it waits at the edge when the call is behind), its arrow points the way
    /// (up = ahead), and it pulses, then fades.
    /// </summary>
    public sealed class AlarmIndicatorView : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] RectTransform marker;
        [SerializeField] RectTransform arrow;
        [SerializeField] RectTransform ring;
        [SerializeField] Text label;
        [Tooltip("How far the marker may slide from the centre (canvas units); 90 degrees off reaches the edge.")]
        [SerializeField] float halfWidth = 430f;
        [SerializeField] float showSeconds = 4.5f;

        Vector3 source;
        float shownAt = -99f;
        Transform view;

        void OnEnable() => AlarmEvents.Raised += Show;
        void OnDisable() => AlarmEvents.Raised -= Show;

        void Awake() => group.alpha = 0f;

        void Show(AlarmCall call)
        {
            source = call.position;
            shownAt = Time.time;
            label.text = call.kind == AlarmKind.BirdFlush ? (call.causedByHunter ? "You flushed birds" : "Birds flew up") : "Bird alarm call";
        }

        void Update()
        {
            float age = Time.time - shownAt;
            if (age > showSeconds) { if (group.alpha > 0f) group.alpha = 0f; return; }
            if (!view && Camera.main) view = Camera.main.transform;
            if (!view) return;

            Vector3 fwd = view.forward; fwd.y = 0f;
            Vector3 to = source - view.position; to.y = 0f;
            float bearing = fwd.sqrMagnitude > 1e-4f && to.sqrMagnitude > 1e-4f ? Vector3.SignedAngle(fwd, to, Vector3.up) : 0f;
            marker.anchoredPosition = new Vector2(Mathf.Clamp(bearing / 90f, -1f, 1f) * halfWidth, 0f);
            arrow.localEulerAngles = new Vector3(0f, 0f, -bearing);

            // two quick pulses of the ring, then a steady marker that fades out over the last second
            float pulse = Mathf.Repeat(age, 0.8f) / 0.8f;
            bool pulsing = age < 1.6f;
            ring.localScale = Vector3.one * (pulsing ? Mathf.Lerp(1f, 2.2f, pulse) : 1f);
            var ringGraphic = ring.GetComponent<Graphic>();
            if (ringGraphic) { var c = ringGraphic.color; c.a = pulsing ? 1f - pulse : 0f; ringGraphic.color = c; }
            group.alpha = Mathf.Clamp01(showSeconds - age);
        }
    }
}
