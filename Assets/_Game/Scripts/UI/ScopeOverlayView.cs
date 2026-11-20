using Game.Weapons;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>The scope picture (dark surround and reticle) faded in while aiming, plus a hold-breath bar.</summary>
    public sealed class ScopeOverlayView : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] RectTransform breathFill;
        [SerializeField] Graphic breathFillGraphic;
        [SerializeField] GameObject breathBar;
        [SerializeField] Color breathColor = new Color32(0xEF, 0xE4, 0xCC, 0xFF);
        [SerializeField] Color recoverColor = new Color32(0xC8, 0x45, 0x2F, 0xFF);

        WeaponController weapon;

        void Update()
        {
            if (!weapon) weapon = FindFirstObjectByType<WeaponController>();
            float a = weapon ? Mathf.InverseLerp(0.6f, 0.95f, weapon.ScopeAmount) : 0f;
            group.alpha = a;
            bool showBreath = a > 0.5f;
            if (breathBar.activeSelf != showBreath) breathBar.SetActive(showBreath);
            if (!showBreath) return;
            breathFill.anchorMax = new Vector2(weapon.BreathFraction, 1f);
            breathFillGraphic.color = weapon.RecoveringBreath ? recoverColor : breathColor;
        }
    }
}
