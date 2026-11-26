using Game.Core.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>One button of a SettingChoiceRow.</summary>
    [RequireComponent(typeof(Button), typeof(Image))]
    public sealed class SettingChoiceOption : MonoBehaviour
    {
        [SerializeField] int value;
        public int Value => value;
        public Button Button => GetComponent<Button>();
        public Image Background => GetComponent<Image>();
    }
}
