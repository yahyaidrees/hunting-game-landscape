using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Player
{
    /// <summary>
    /// Drag anywhere on this UI area to look (plan section 12: the right half of the screen). A short touch that
    /// barely moves is a tap instead (used to inspect clues). Tracks one finger at a time, so the move stick on the
    /// left keeps working with a second finger. Works with a mouse in the editor too.
    /// </summary>
    public sealed class TouchLookArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        const int NoPointer = int.MinValue;
        [Tooltip("A touch shorter than this (seconds)...")]
        [SerializeField] float tapMaxSeconds = 0.3f;
        [Tooltip("...that moved less than this (pixels at a 1080p screen) is a tap.")]
        [SerializeField] float tapMaxReferencePixels = 22f;

        static Vector2 pending;
        int activePointer = NoPointer;
        Vector2 downPosition;
        float downTime;
        float travelled;

        /// <summary>Screen position of a tap on the look area.</summary>
        public static event Action<Vector2> Tapped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            pending = Vector2.zero;
            Tapped = null;
        }

        /// <summary>Screen-pixel drag since the last call.</summary>
        public static Vector2 ConsumeDelta()
        {
            var d = pending;
            pending = Vector2.zero;
            return d;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (activePointer != NoPointer) return;
            activePointer = e.pointerId;
            downPosition = e.position;
            downTime = Time.unscaledTime;
            travelled = 0f;
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != activePointer) return;
            pending += e.delta;
            travelled += e.delta.magnitude;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != activePointer) return;
            activePointer = NoPointer;
            float maxPx = tapMaxReferencePixels * Screen.height / 1080f;
            if (Time.unscaledTime - downTime <= tapMaxSeconds && travelled <= maxPx)
                Tapped?.Invoke(downPosition);
        }

        void OnDisable() => activePointer = NoPointer;
    }
}
