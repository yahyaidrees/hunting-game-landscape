using Game.Core.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>A line under the graphics options: the tier in use, what Auto picked for this phone, and the frame rate.</summary>
    public sealed class SettingsInfoView : MonoBehaviour
    {
        [SerializeField] Text label;
        float nextUpdate;
        int frames;
        float frameTime;

        void Update()
        {
            frames++;
            frameTime += Time.unscaledDeltaTime;
            if (Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + 0.5f;
            float fps = frameTime > 0f ? frames / frameTime : 0f;
            frames = 0; frameTime = 0f;
            label.text = $"In use: {QualityTiers.Current}, {Application.targetFrameRate} fps cap, now {fps:0} fps.   "
                       + (GameSettings.TierMeasured ? $"Auto picks {GameSettings.DetectedTier} for this device (measured)."
                                                     : $"Auto starts on {GameSettings.DetectedTier} and measures this device during the first hunt.");
        }
    }
}
