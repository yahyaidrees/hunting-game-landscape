using UnityEngine;

namespace Game.UI
{
    /// <summary>Fits this RectTransform to the device safe area (notches, rounded corners). Plan section 12, Layout.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        RectTransform rect;
        Rect applied;
        Vector2Int screen;

        void Awake() => rect = (RectTransform)transform;

        void OnEnable() => Apply();

        void Update()
        {
            if (Screen.safeArea != applied || screen.x != Screen.width || screen.y != Screen.height) Apply();
        }

        void Apply()
        {
            if (!rect) rect = (RectTransform)transform;
            applied = Screen.safeArea;
            screen = new Vector2Int(Screen.width, Screen.height);
            if (screen.x <= 0 || screen.y <= 0) return;
            rect.anchorMin = new Vector2(applied.xMin / screen.x, applied.yMin / screen.y);
            rect.anchorMax = new Vector2(applied.xMax / screen.x, applied.yMax / screen.y);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
