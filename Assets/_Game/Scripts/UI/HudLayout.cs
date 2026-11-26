using System.Collections.Generic;
using Game.Core.Settings;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Applies the player's HUD layout options to the hunt HUD (plan section 12): the left-handed layout mirrors
    /// every element left to right (move stick on the right, look and fire on the left), and the button size option
    /// scales the thumb controls and moves them a little toward or away from their screen corner, so a cluster
    /// grows or shrinks together without overlapping. Works from the layout the HUD was built with, so the options can change at any time.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class HudLayout : MonoBehaviour
    {
        [Tooltip("Thumb controls (stick, action buttons): scaled by the button size option.")]
        [SerializeField] RectTransform[] thumbControls;

        struct Placement
        {
            public RectTransform rect;
            public Vector2 anchorMin, anchorMax, pivot, position;
            public Vector3 scale;
            public bool thumb;
        }

        readonly List<Placement> placements = new List<Placement>();
        bool appliedLeft;
        float appliedScale = -1f;

        void Awake()
        {
            var thumbs = new HashSet<RectTransform>(thumbControls ?? new RectTransform[0]);
            foreach (Transform child in transform)
            {
                var r = (RectTransform)child;
                placements.Add(new Placement
                {
                    rect = r, anchorMin = r.anchorMin, anchorMax = r.anchorMax, pivot = r.pivot,
                    position = r.anchoredPosition, scale = r.localScale, thumb = thumbs.Contains(r)
                });
            }
        }

        void OnEnable()
        {
            GameSettings.Changed += Apply;
            Apply();
        }

        void OnDisable() => GameSettings.Changed -= Apply;

        void Apply()
        {
            bool left = GameSettings.LeftHanded;
            float k = GameSettings.ButtonScale;
            if (left == appliedLeft && Mathf.Approximately(k, appliedScale)) return;
            appliedLeft = left;
            appliedScale = k;
            foreach (var p in placements)
            {
                if (!p.rect) continue;
                Vector2 pos = p.position;
                Vector3 scale = p.scale;
                // anchored at a corner or edge: positions move out by the square root of the size change, so a
                // cluster spreads a little less than its buttons grow and stays clear of the centre (noise meter)
                if (p.thumb) { pos *= Mathf.Sqrt(k); scale *= k; }
                Vector2 aMin = p.anchorMin, aMax = p.anchorMax, pivot = p.pivot;
                if (left)
                {
                    aMin.x = 1f - p.anchorMax.x;
                    aMax.x = 1f - p.anchorMin.x;
                    pivot.x = 1f - p.pivot.x;
                    pos.x = -pos.x;
                }
                p.rect.anchorMin = aMin;
                p.rect.anchorMax = aMax;
                p.rect.pivot = pivot;
                p.rect.anchoredPosition = pos;
                p.rect.localScale = scale;
            }
        }
    }
}
