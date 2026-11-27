using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Game.Editor
{
    /// <summary>
    /// Keeps the Unity MCP plugin (the coding agent's editor bridge) out of every player build (Gate A must-fix).
    /// Its runtime assembly is compiled out with the package's own define (passed by every build script here), and
    /// the support DLLs its dependency resolver keeps in Assets/Plugins/NuGet (networking, Roslyn, reflection; about
    /// 18 MB) are filtered out of the build's assemblies. Their import settings cannot be used for this: the resolver
    /// sets them back to every platform after each script reload. A build without the define stops with a message.
    /// </summary>
    public sealed class McpBuildExclusion : IFilterBuildAssemblies, IPreprocessBuildWithReport
    {
        /// <summary>Scripting define that drops the plugin's runtime assemblies from a player build.</summary>
        public const string Define = "UNITY_MCP_EXCLUDE_FROM_COMPILATION";
        const string NuGetDir = "Assets/Plugins/NuGet";

        public int callbackOrder => 0;

        /// <summary>The defines every player build of this project passes (with any extra ones).</summary>
        public static string[] BuildDefines(params string[] extra) => new[] { Define }.Concat(extra).Distinct().ToArray();

        /// <summary>Set by our build scripts while they build; a build started any other way would ship the plugin.</summary>
        public static bool BuildingWithDefine { get; set; }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!BuildingWithDefine)
                throw new BuildFailedException("Build from the Game/Build menu: other builds would ship the MCP plugin (they lack " + Define + ").");
        }

        public string[] OnFilterAssemblies(BuildOptions buildOptions, string[] assemblies)
        {
            var plugin = Directory.Exists(NuGetDir)
                ? new System.Collections.Generic.HashSet<string>(Directory.GetFiles(NuGetDir, "*.dll").Select(Path.GetFileName), System.StringComparer.OrdinalIgnoreCase)
                : new System.Collections.Generic.HashSet<string>();
            var kept = assemblies.Where(a =>
            {
                string name = Path.GetFileName(a);
                return !plugin.Contains(name) && !name.StartsWith("com.IvanMurzak.", System.StringComparison.OrdinalIgnoreCase);
            }).ToArray();
            UnityEngine.Debug.Log($"[build] MCP plugin assemblies left out: {assemblies.Length - kept.Length}");
            return kept;
        }
    }
}
