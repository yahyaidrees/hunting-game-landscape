using Game.Core.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>A row of option buttons (each child has a SettingChoiceOption with its value); the chosen one is lit.</summary>
    public sealed class SettingChoiceRow : MonoBehaviour
    {
        [SerializeField] SettingKey key;
        [SerializeField] Color chosenColor = new Color(0.91f, 0.64f, 0.23f), otherColor = new Color(0.37f, 0.49f, 0.54f);

        SettingChoiceOption[] options;

        void Awake()
        {
            options = GetComponentsInChildren<SettingChoiceOption>(true);
            foreach (var o in options)
            {
                int value = o.Value;
                o.Button.onClick.AddListener(() => SettingAccess.SetInt(key, value));
            }
        }

        void OnEnable() { GameSettings.Changed += Refresh; Refresh(); }
        void OnDisable() => GameSettings.Changed -= Refresh;

        void Refresh()
        {
            int current = SettingAccess.GetInt(key);
            foreach (var o in options) o.Background.color = o.Value == current ? chosenColor : otherColor;
        }
    }
}
