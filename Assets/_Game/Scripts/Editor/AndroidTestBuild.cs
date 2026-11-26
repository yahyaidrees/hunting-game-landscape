using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Phone test builds (plan section 19, Gate B: 30 fps on the Low tier phone). A release-speed APK with the
    /// performance lab compiled in (GAME_PERFLAB), signed with the debug key, for installing over USB. Store
    /// builds (AAB, upload key, no lab) come with the Gate A upload. The result is also written to
    /// Temp/build_result.txt so a script can wait for it.
    /// </summary>
    public static class AndroidTestBuild
    {
        public const string OutputPath = "Builds/Android/FarmerOfTheJungle-test.apk";
        const string ResultPath = "Temp/build_result.txt";
        const string PendingKey = "Game.AndroidTestBuild.Pending";   // 1 release, 2 development; survives script reloads

        [MenuItem("Game/Build/Android Test Build (release speed + perf lab)")]
        public static void BuildRelease() => Build(false);

        [MenuItem("Game/Build/Android Test Build (development, profiler)")]
        public static void BuildDevelopment() => Build(true);

        /// <summary>
        /// Starts the build on the next editor tick, so the caller returns at once. Remembered across script reloads
        /// (a reload right after scheduling used to drop the request).
        /// </summary>
        public static void Schedule(bool development)
        {
            if (File.Exists(ResultPath)) File.Delete(ResultPath);
            SessionState.SetInt(PendingKey, development ? 2 : 1);
            EditorApplication.delayCall += RunPending;
        }

        [InitializeOnLoadMethod]
        static void ResumeAfterReload()
        {
            if (SessionState.GetInt(PendingKey, 0) != 0) EditorApplication.delayCall += RunPending;
        }

        static void RunPending()
        {
            int pending = SessionState.GetInt(PendingKey, 0);
            if (pending == 0 || BuildPipeline.isBuildingPlayer) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += RunPending; return; }
            SessionState.EraseInt(PendingKey);
            Build(pending == 2);
        }

        static void Build(bool development)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Game/Scenes/Graybox/R01_Meadow_Graybox.unity" },
                locationPathName = development ? OutputPath.Replace("-test", "-dev") : OutputPath,
                target = BuildTarget.Android,
                options = development ? BuildOptions.Development : BuildOptions.None,
                extraScriptingDefines = new[] { "GAME_PERFLAB" },
            };
            var text = new StringBuilder();
            try
            {
                var report = BuildPipeline.BuildPlayer(opts);
                var s = report.summary;
                text.AppendLine($"result={s.result} seconds={s.totalTime.TotalSeconds:0} path={opts.locationPathName} errors={s.totalErrors} warnings={s.totalWarnings}");
                foreach (var step in report.steps)
                foreach (var m in step.messages)
                    if (m.type == LogType.Error || m.type == LogType.Exception) text.AppendLine("ERR " + m.content);
            }
            catch (System.Exception e) { text.AppendLine("exception " + e); }
            File.WriteAllText(ResultPath, text.ToString());
            Debug.Log("[build] " + text);
        }
    }
}
