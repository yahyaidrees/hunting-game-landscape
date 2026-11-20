using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Top-right map button toggling the hunter's map panel; redraws the map while it is open.</summary>
    public sealed class MapPanelView : MonoBehaviour
    {
        [SerializeField] Button toggleButton;
        [SerializeField] GameObject panel;
        [SerializeField] TrailMapGraphic map;

        void Awake()
        {
            toggleButton.onClick.AddListener(() => panel.SetActive(!panel.activeSelf));
            panel.SetActive(false);
        }

        void Update()
        {
            if (panel.activeSelf) map.SetVerticesDirty();
        }
    }
}
