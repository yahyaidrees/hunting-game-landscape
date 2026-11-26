using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace Game.Core.Settings
{
    /// <summary>
    /// Writes the device and its frame rate to the log, in every build (plan section 19: 30 fps on the Low tier
    /// phone is a Gate B condition). Once at start: model, chip, GPU, memory, screen, and the tier Auto picked.
    /// Then every 10 seconds: average fps, the worst 1% of frames, frame time and memory. Read on a phone with
    /// "adb logcat -s Unity" (lines start with [perf]). Cheap: one array write per frame.
    /// </summary>
    public sealed class PerfProbe : MonoBehaviour
    {
        const float WindowSeconds = 10f;
        readonly float[] frames = new float[2048];
        int count;
        float windowStart;
        // CPU and GPU time per frame (Player Settings: Frame Timing Stats), and what the frame draws
        readonly FrameTiming[] timing = new FrameTiming[1];
        double cpuMain, cpuRender, gpu;
        int timed;
        ProfilerRecorder batches, setPass, triangles;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (Application.isEditor) return;   // the editor has its own profiler and stats
            var go = new GameObject("PerfProbe") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<PerfProbe>();
        }

        void Start()
        {
            Debug.Log($"[perf] device model={SystemInfo.deviceModel} cpu={SystemInfo.processorType} cores={SystemInfo.processorCount} " +
                      $"ram={SystemInfo.systemMemorySize}MB gpu={SystemInfo.graphicsDeviceName} gpuMem={SystemInfo.graphicsMemorySize}MB " +
                      $"api={SystemInfo.graphicsDeviceType} shaderLevel={SystemInfo.graphicsShaderLevel} os={SystemInfo.operatingSystem} " +
                      $"screen={Screen.width}x{Screen.height}@{Screen.currentResolution.refreshRateRatio.value:0} " +
                      $"autoTier={GameSettings.DetectedTier} choice={GameSettings.QualityTierChoice}");
            windowStart = Time.unscaledTime;
            batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        }

        void OnDestroy() { batches.Dispose(); setPass.Dispose(); triangles.Dispose(); }

        void Update()
        {
            if (count < frames.Length) frames[count++] = Time.unscaledDeltaTime;
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timing) > 0)
            {
                cpuMain += timing[0].cpuMainThreadFrameTime;
                cpuRender += timing[0].cpuRenderThreadFrameTime;
                gpu += timing[0].gpuFrameTime;
                timed++;
            }
            float now = Time.unscaledTime;
            if (now - windowStart < WindowSeconds || count == 0) return;

            float total = 0f;
            for (int i = 0; i < count; i++) total += frames[i];
            System.Array.Sort(frames, 0, count);
            int worstStart = Mathf.Clamp(Mathf.FloorToInt(count * 0.99f), 0, count - 1);
            float worst = 0f;
            for (int i = worstStart; i < count; i++) worst += frames[i];
            worst /= count - worstStart;

            var rp = QualitySettings.renderPipeline;
            var sb = new StringBuilder("[perf] ");
            sb.Append($"tier={QualityTiers.Current} pipeline={(rp ? rp.name : "default")} cap={Application.targetFrameRate} ");
            sb.Append($"fps={count / total:0.0} low1%={1f / Mathf.Max(1e-4f, worst):0.0} ms={total / count * 1000f:0.0} ");
            sb.Append($"maxMs={frames[count - 1] * 1000f:0} frames={count} ");
            if (timed > 0) sb.Append($"cpuMain={cpuMain / timed:0.0} cpuRender={cpuRender / timed:0.0} gpu={gpu / timed:0.0} ");
            if (batches.Valid) sb.Append($"batches={batches.LastValue} setpass={setPass.LastValue} tris={triangles.LastValue / 1000}k ");
            sb.Append($"mono={UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / 1048576}MB ");
            sb.Append($"alloc={UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576}MB");
            Debug.Log(sb.ToString());
            count = 0;
            cpuMain = cpuRender = gpu = 0;
            timed = 0;
            windowStart = now;
        }
    }
}
