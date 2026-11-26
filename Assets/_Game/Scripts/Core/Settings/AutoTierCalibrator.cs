using System.Collections.Generic;
using Game.Core.Analytics;
using UnityEngine;

namespace Game.Core.Settings
{
    /// <summary>
    /// Auto graphics quality by measurement (plan section 13: "the game picks a tier on first launch"). Memory alone
    /// guessed wrong on the test phones: two phones with the same GPU and driver differed by half again in speed.
    ///
    /// Calibration (once per device, while quality is on Auto and the player is really hunting): the 30 fps cap is
    /// lifted for a few seconds and the typical frame time measured on the current tier. A tier is kept only with
    /// clear headroom (about 15% for the 30 fps tiers), a faster phone tries the next tier up and measures that
    /// too, a slower one steps down; a tier it stepped down from is not tried again. The result is saved per device.
    ///
    /// Safety net afterwards: if the average frame rate at the cap stays under the limit for a long stretch (a hot
    /// phone, a heavier scene), Auto steps down one tier for good on this device. Never steps up on its own.
    /// Numbers are starting values from the Galaxy A30s, Galaxy A06 and VGO TEL New 26 Lite (2026-11-26).
    /// </summary>
    [RequireComponent(typeof(QualityTierApplier))]
    public sealed class AutoTierCalibrator : MonoBehaviour
    {
        [Header("Measuring")]
        [Tooltip("Seconds after a tier change before measuring (first frames load shaders and textures).")]
        [SerializeField] float settleSeconds = 2.5f;
        [Tooltip("Seconds of frames measured per tier.")]
        [SerializeField] float measureSeconds = 6f;
        [Tooltip("Cap while measuring: high enough to see the headroom above 30.")]
        [SerializeField] int measureCap = 60;

        [Header("Thresholds (uncapped fps, typical frame)")]
        [Tooltip("Medium is kept only at or above this (30 fps cap with about 15% headroom).")]
        [SerializeField] float keepMedium = 36f;
        [Tooltip("High is kept only at or above this (High runs at a 60 fps cap).")]
        [SerializeField] float keepHigh = 57f;
        [Tooltip("Low tries Medium at or above this.")]
        [SerializeField] float tryMediumFromLow = 48f;
        [Tooltip("Medium tries High at or above this (with a 60 Hz screen, Medium at the 60 cap).")]
        [SerializeField] float tryHighFromMedium = 58f;
        [Tooltip("High is only tried on phones with at least this much memory (MB).")]
        [SerializeField] int highMinMemoryMB = 6000;

        [Header("Safety net")]
        [Tooltip("Seconds of play averaged (at the cap) before deciding the tier is too heavy.")]
        [SerializeField] float watchSeconds = 45f;
        [Tooltip("Steps down when the average stays under this fraction of the cap (0.92 of 30 = 27.6 fps).")]
        [SerializeField] float watchFraction = 0.92f;

        enum Phase { Idle, Settling, Measuring, Watching }

        QualityTierApplier applier;
        Phase phase;
        float phaseTime, watchTime, watchFrames;
        int measuredCount;
        readonly List<float> frameTimes = new List<float>(1024);
        readonly HashSet<QualityTier> steppedDownFrom = new HashSet<QualityTier>();

        void Awake() => applier = GetComponent<QualityTierApplier>();

        void OnEnable()
        {
            GameSettings.Changed += OnSettingsChanged;
            phase = Phase.Idle;
        }

        void OnDisable()
        {
            GameSettings.Changed -= OnSettingsChanged;
            if (phase == Phase.Measuring || phase == Phase.Settling) applier.Reapply();   // put the cap back
        }

        bool OnAuto => GameSettings.QualityTierChoice == GameSettings.AutoTier;

        void OnSettingsChanged()
        {
            // the player chose a tier by hand: stop measuring and give the cap back
            if (!OnAuto && (phase == Phase.Settling || phase == Phase.Measuring))
            {
                phase = Phase.Idle;
                applier.Reapply();
            }
        }

        void Update()
        {
            if (!OnAuto) { phase = Phase.Idle; return; }
            bool playing = QualityTiers.MeasurementAllowed && Time.timeScale > 0.99f && Application.isFocused;
            float dt = Time.unscaledDeltaTime;

            switch (phase)
            {
                case Phase.Idle:
                    if (!playing) return;
                    if (!GameSettings.TierMeasured) Begin(Phase.Settling);
                    else { Begin(Phase.Watching); watchTime = watchFrames = 0f; }
                    break;

                case Phase.Settling:
                    Application.targetFrameRate = measureCap;   // other settings changes may put the cap back
                    if (!playing) { phaseTime = 0f; return; }   // wait out menus and pauses
                    phaseTime += dt;
                    if (phaseTime >= settleSeconds) { Begin(Phase.Measuring); frameTimes.Clear(); }
                    break;

                case Phase.Measuring:
                    Application.targetFrameRate = measureCap;
                    if (!playing) return;                         // paused: keep what was measured, go on after
                    frameTimes.Add(dt);
                    phaseTime += dt;
                    if (phaseTime >= measureSeconds) Decide();
                    break;

                case Phase.Watching:
                    if (!playing) return;
                    watchTime += dt;
                    watchFrames++;
                    if (watchTime < watchSeconds) return;
                    float fps = watchFrames / watchTime;
                    float limit = Application.targetFrameRate > 0 ? Application.targetFrameRate * watchFraction : 27.6f;
                    var tier = QualityTiers.Current;
                    if (fps < limit && tier > QualityTier.Low)
                    {
                        var lower = tier - 1;
                        AnalyticsEvents.Log("gfx_auto_step_down", ("from", tier), ("to", lower), ("fps", Mathf.Round(fps * 10f) / 10f));
                        Debug.Log($"[perf] auto: {tier} averaged {fps:0.0} fps over {watchTime:0} s, stepping down to {lower}");
                        GameSettings.SetMeasuredTier(lower, true);
                    }
                    watchTime = watchFrames = 0f;
                    break;
            }
        }

        void Begin(Phase next)
        {
            phase = next;
            phaseTime = 0f;
        }

        void Decide()
        {
            var tier = QualityTiers.Current;
            float fps = TypicalFps();
            measuredCount++;
            Debug.Log($"[perf] auto: measured {tier} at {fps:0.0} fps uncapped");

            QualityTier? next = null;
            if (tier == QualityTier.Medium && fps < keepMedium) next = QualityTier.Low;
            else if (tier == QualityTier.High && fps < keepHigh) next = QualityTier.Medium;
            else if (tier == QualityTier.Low && fps >= tryMediumFromLow && !steppedDownFrom.Contains(QualityTier.Medium)) next = QualityTier.Medium;
            else if (tier == QualityTier.Medium && fps >= tryHighFromMedium && SystemInfo.systemMemorySize >= highMinMemoryMB
                     && !steppedDownFrom.Contains(QualityTier.High)) next = QualityTier.High;

            if (next.HasValue && measuredCount < 4)
            {
                if (next.Value < tier) steppedDownFrom.Add(tier);
                GameSettings.SetMeasuredTier(next.Value, false);   // switches the tier; measuring goes on
                Begin(Phase.Settling);
                return;
            }

            var final = next.HasValue && next.Value < tier ? next.Value : tier;   // out of steps: only a step down is kept
            GameSettings.SetMeasuredTier(final, true);
            AnalyticsEvents.Log("gfx_auto_measured", ("tier", final), ("fps", Mathf.Round(fps * 10f) / 10f), ("steps", measuredCount),
                ("gpu", SystemInfo.graphicsDeviceName), ("ram_mb", SystemInfo.systemMemorySize));
            Debug.Log($"[perf] auto: settled on {final} after {measuredCount} measurement(s)");
            applier.Reapply();   // the tier's own frame rate cap again
            Begin(Phase.Watching);
            watchTime = watchFrames = 0f;
        }

        // The average frame rate with the slowest 3% of frames left out (a loading hitch must not swing it). Not the
        // median: on a 60 Hz screen frames last 16.7 or 33.3 ms, so a median snaps to 60 or 30 while a phone that
        // mixes both (35 fps on the Galaxy A30s) is in between.
        float TypicalFps()
        {
            if (frameTimes.Count == 0) return 0f;
            frameTimes.Sort();
            int keep = Mathf.Max(1, Mathf.FloorToInt(frameTimes.Count * 0.97f));
            float total = 0f;
            for (int i = 0; i < keep; i++) total += frameTimes[i];
            return total > 0f ? keep / total : 0f;
        }
    }
}
