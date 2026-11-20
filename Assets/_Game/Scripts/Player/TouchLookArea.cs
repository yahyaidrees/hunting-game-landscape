using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Player
{
    /// <summary>
    /// Drag anywhere on this UI area to look (plan section 12: the right half of the screen).
    /// Tracks one finger at a time, so the move stick on the left keeps working with a second finger.
    /// Works with a mouse drag in the editor too.
    /// </summary>
    public sealed class TouchLookArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        const int NoPointer = int.MinValue;
        static Vector2 pending;
        int activePointer = NoPointer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => pending = Vector2.zero;

        /// <summary>Screen-pixel drag since the last call.</summary>
        public static Vector2 ConsumeDelta()
        {
            var d = pending;
            pending = Vector2.zero;
            return d;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (activePointer == NoPointer) activePointer = e.pointerId;
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId == activePointer) pending += e.delta;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == activePointer) activePointer = NoPointer;
        }

        void OnDisable() => activePointer = NoPointer;
    }
}
