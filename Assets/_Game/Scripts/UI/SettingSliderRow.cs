using Game.Core.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>A slider row: label, slider and the value as text.</summary>
    public sealed class SettingSliderRow : MonoBehaviour
    {
        [SerializeField] SettingKey key;
        [SerializeField] Slider slider;
        [SerializeField] Text valueLabel;

        void Awake()
        {
            var (min, max) = SettingAccess.Range(key);
            slider.minValue = min;
            slider.maxValue = max;
            slider.onValueChanged.AddListener(v => { SettingAccess.SetFloat(key, v); Refresh(); });
        }

        void OnEnable() { GameSettings.Changed += Refresh; Refresh(); }
        void OnDisable() => GameSettings.Changed -= Refresh;

        void Refresh()
        {
            float v = SettingAccess.GetFloat(key);
            slider.SetValueWithoutNotify(v);
            if (valueLabel) valueLabel.text = SettingAccess.Format(key, v);
        }
    }
}
