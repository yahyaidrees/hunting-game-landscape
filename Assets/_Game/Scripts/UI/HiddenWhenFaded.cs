using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Stops drawing a faded-out panel. A CanvasGroup at alpha 0 still sends its graphics to the GPU, and on a
    /// low-end phone every full-screen layer costs (the scope overlay alone covers the whole screen). This panel
    /// gets its own nested Canvas, switched off while the group is fully transparent.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup), typeof(Canvas))]
    public sealed class HiddenWhenFaded : MonoBehaviour
    {
        CanvasGroup group;
        Canvas canvas;

        void Awake()
        {
            group = GetComponent<CanvasGroup>();
            canvas = GetComponent<Canvas>();
        }

        void LateUpdate()
        {
            bool show = group.alpha > 0.001f;
            if (canvas.enabled != show) canvas.enabled = show;
        }
    }
}
