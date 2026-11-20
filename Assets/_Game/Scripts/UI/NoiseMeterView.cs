using Game.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Bottom-centre noise meter (plan section 12, Hunt HUD): a bar and a word (Quiet, Noticeable, Loud).
    /// Colours follow the style guide: slate for quiet, lantern amber for noticeable, alert red for loud.
    /// </summary>
    public sealed class NoiseMeterView : MonoBehaviour
    {
        [SerializeField] PlayerNoise noise;
        [SerializeField] RectTransform fill;
        [SerializeField] Graphic fillGraphic;
        [SerializeField] Text label;
        [SerializeField] Color quietColor = new Color32(0x5F, 0x7C, 0x8A, 0xFF);
        [SerializeField] Color noticeableColor = new Color32(0xE9, 0xA2, 0x3B, 0xFF);
        [SerializeField] Color loudColor = new Color32(0xC8, 0x45, 0x2F, 0xFF);

        int band = -1;

        void Update()
        {
            if (!noise)
            {
                noise = FindFirstObjectByType<PlayerNoise>();
                if (!noise) return;
            }
            float level = noise.Level;
            fill.anchorMax = new Vector2(Mathf.Clamp01(level), 1f);

            var s = noise.Settings;
            int newBand = level < s.quietBelow ? 0 : level < s.loudFrom ? 1 : 2;
            if (newBand == band) return;
            band = newBand;
            Color c = band == 0 ? quietColor : band == 1 ? noticeableColor : loudColor;
            fillGraphic.color = c;
            label.text = band == 0 ? "Quiet" : band == 1 ? "Noticeable" : "Loud";
        }
    }
}
