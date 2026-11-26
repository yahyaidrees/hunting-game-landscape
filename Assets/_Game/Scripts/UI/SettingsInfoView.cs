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
            float gb = SystemInfo.systemMemorySize / 1024f;
            label.text = $"In use: {QualityTiers.Current}, {Application.targetFrameRate} fps cap, now {fps:0} fps.   "
                       + $"Auto picks {GameSettings.DetectedTier} for this device ({gb:0.0} GB).";
        }
    }
}
