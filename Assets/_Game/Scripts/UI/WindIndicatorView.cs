using Game.Core.Wind;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Top-centre wind arrow (plan section 12, Hunt HUD): points where the wind blows, relative to where the hunter
    /// faces (up = straight ahead), with a strength word. Approach an animal with the arrow pointing at you.
    /// </summary>
    public sealed class WindIndicatorView : MonoBehaviour
    {
        [SerializeField] RectTransform arrow;
        [SerializeField] Text label;

        Transform view;
        int shownBand = -1;

        void Update()
        {
            var wind = WindSystem.Active;
            if (!wind) return;
            if (!view && Camera.main) view = Camera.main.transform;
            float facing = view ? view.eulerAngles.y : 0f;
            arrow.localEulerAngles = new Vector3(0f, 0f, -(wind.DirectionDegrees - facing));

            int band = wind.Strength < 0.33f ? 0 : wind.Strength < 0.66f ? 1 : 2;
            if (band == shownBand) return;
            shownBand = band;
            label.text = band == 0 ? "Wind: light" : band == 1 ? "Wind: moderate" : "Wind: strong";
        }
    }
}
