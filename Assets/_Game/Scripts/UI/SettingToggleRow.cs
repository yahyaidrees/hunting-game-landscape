using Game.Core.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>An on/off row: one button that flips the option and shows On or Off.</summary>
    public sealed class SettingToggleRow : MonoBehaviour
    {
        [SerializeField] SettingKey key;
        [SerializeField] Button button;
        [SerializeField] Text stateLabel;
        [SerializeField] Image background;
        [SerializeField] Color onColor = new Color(0.91f, 0.64f, 0.23f), offColor = new Color(0.37f, 0.49f, 0.54f);

        void Awake() => button.onClick.AddListener(() => SettingAccess.SetBool(key, !SettingAccess.GetBool(key)));
        void OnEnable() { GameSettings.Changed += Refresh; Refresh(); }
        void OnDisable() => GameSettings.Changed -= Refresh;

        void Refresh()
        {
            bool on = SettingAccess.GetBool(key);
            stateLabel.text = on ? "On" : "Off";
            background.color = on ? onColor : offColor;
        }
    }
}
