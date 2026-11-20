using Game.Tracking;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Left-edge Hunter Sense button; a dark fill shows the cooldown draining, with the seconds left.</summary>
    public sealed class HunterSenseButtonView : MonoBehaviour
    {
        [SerializeField] Button button;
        [Tooltip("Filled image over the button (Filled, Vertical, from the top).")]
        [SerializeField] Image cooldownFill;
        [SerializeField] Text label;

        void Awake() => button.onClick.AddListener(OnClick);

        void OnClick()
        {
            var sys = TrackingSystem.Active;
            if (sys) sys.ActivateSense();
        }

        void Update()
        {
            var sys = TrackingSystem.Active;
            float f = sys ? sys.SenseCooldownFraction : 0f;
            cooldownFill.fillAmount = f;
            bool ready = f <= 0f;
            if (button.interactable != ready) button.interactable = ready;
            if (label) label.text = ready || !sys ? "Sense" : Mathf.CeilToInt(sys.SenseCooldownRemaining).ToString();
        }
    }
}
