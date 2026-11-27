using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The Google Play build (Gate A): an Android App Bundle signed with the owner's upload key, Addressables built
    /// into asset packs (Play Asset Delivery), no performance lab, no MCP plugin, version code above the old game's
    /// 99 and one higher on every build. The signing key comes from the owner's Windows user environment variables,
    /// never from the repository: FOTJ_KEYSTORE (path), FOTJ_KEYSTORE_PASS, FOTJ_KEY_ALIAS, FOTJ_KEY_PASS. Keystore
    /// settings are put back after the build. Writes Temp/store_build_result.txt.
    /// </summary>
    public static class StoreBuild
    {
        const string ResultPath = "Temp/store_build_result.txt";
        const int FirstVersionCode = 100;   // the old game shipped 99

        [MenuItem("Game/Build/Google Play Bundle (AAB)")]
        public static void BuildMenu() => Debug.Log("[build] " + Build());

        /// <summary>
        /// The same bundle signed with the debug key, version code unchanged, for testing asset packs on a phone over
        /// USB (bundletool --local-testing) before anything goes to Play.
        /// </summary>
        [MenuItem("Game/Build/Local Test Bundle (AAB, debug key)")]
        public static void BuildLocalTestMenu() => Debug.Log("[build] " + Build(forUpload: false));

        static string Env(string name) =>
            Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User) ?? Environment.GetEnvironmentVariable(name);

        public static string Build(bool forUpload = true)
        {
            var text = new StringBuilder();
            if (!forUpload) return BuildBundle(null, null, null, null, text);
            string keystore = Env("FOTJ_KEYSTORE"), storePass = Env("FOTJ_KEYSTORE_PASS"), alias = Env("FOTJ_KEY_ALIAS"), keyPass = Env("FOTJ_KEY_PASS");
            if (string.IsNullOrEmpty(keystore) || string.IsNullOrEmpty(storePass) || string.IsNullOrEmpty(alias) || string.IsNullOrEmpty(keyPass))
                return Finish("result=Failed missing signing variables (FOTJ_KEYSTORE, FOTJ_KEYSTORE_PASS, FOTJ_KEY_ALIAS, FOTJ_KEY_PASS)");
            if (!File.Exists(keystore)) return Finish("result=Failed keystore file not found at FOTJ_KEYSTORE");
            return BuildBundle(keystore, storePass, alias, keyPass, text);
        }

        static string BuildBundle(string keystore, string storePass, string alias, string keyPass, StringBuilder text)
        {
            bool forUpload = keystore != null;

            bool keepCustom = PlayerSettings.Android.useCustomKeystore;
            string keepName = PlayerSettings.Android.keystoreName, keepAlias = PlayerSettings.Android.keyaliasName;
            bool keepBundle = EditorUserBuildSettings.buildAppBundle, keepSplit = PlayerSettings.Android.splitApplicationBinary;
            int keepCode = PlayerSettings.Android.bundleVersionCode;
            int code = forUpload ? Math.Max(keepCode + 1, FirstVersionCode) : Math.Max(keepCode, FirstVersionCode);
            bool succeeded = false;
            try
            {
                PlayerSettings.Android.useCustomKeystore = forUpload;
                if (forUpload)
                {
                    PlayerSettings.Android.keystoreName = keystore;
                    PlayerSettings.Android.keystorePass = storePass;
                    PlayerSettings.Android.keyaliasName = alias;
                    PlayerSettings.Android.keyaliasPass = keyPass;
                }
                EditorUserBuildSettings.buildAppBundle = true;
                PlayerSettings.Android.splitApplicationBinary = true;   // asset packs need it
                PlayerSettings.Android.bundleVersionCode = code;
                text.AppendLine(ContentDeliverySetup.SetUp().Trim());

                string output = forUpload ? $"Builds/Android/FarmerOfTheJungle-{code}.aab" : "Builds/Android/FarmerOfTheJungle-localtest.aab";
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                McpBuildExclusion.BuildingWithDefine = true;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/_Game/Scenes/Graybox/R01_Meadow_Graybox.unity" },
                    locationPathName = output,
                    target = BuildTarget.Android,
                    options = BuildOptions.None,
                    extraScriptingDefines = McpBuildExclusion.BuildDefines(),
                });
                var s = report.summary;
                succeeded = s.result == BuildResult.Succeeded;
                text.AppendLine($"result={s.result} versionCode={code} seconds={s.totalTime.TotalSeconds:0} path={output} sizeMB={(File.Exists(output) ? new FileInfo(output).Length / 1048576f : 0f):0.0} errors={s.totalErrors} warnings={s.totalWarnings}");
                foreach (var step in report.steps)
                foreach (var m in step.messages)
                    if (m.type == LogType.Error || m.type == LogType.Exception) text.AppendLine("ERR " + m.content);
            }
            catch (Exception e) { text.AppendLine("result=Failed exception " + e.Message); }
            finally
            {
                McpBuildExclusion.BuildingWithDefine = false;
                PlayerSettings.Android.keystorePass = "";
                PlayerSettings.Android.keyaliasPass = "";
                PlayerSettings.Android.useCustomKeystore = keepCustom;
                PlayerSettings.Android.keystoreName = keepName;
                PlayerSettings.Android.keyaliasName = keepAlias;
                EditorUserBuildSettings.buildAppBundle = keepBundle;
                PlayerSettings.Android.splitApplicationBinary = keepSplit;
                if (!succeeded || !forUpload) PlayerSettings.Android.bundleVersionCode = keepCode;   // only uploads use up version codes
            }
            return Finish(text.ToString());
        }

        static string Finish(string text)
        {
            File.WriteAllText(ResultPath, text);
            return text;
        }
    }
}
