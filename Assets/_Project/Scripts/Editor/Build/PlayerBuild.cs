using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Pathfinder.Editor.Build
{
    /// <summary>
    /// Standalone Windows builds for playtests (GDD §17, M6). The playtest build has no dev hotkeys; the dev build keeps
    /// them. Output goes to Builds/ (git-ignored). Batch: <c>-executeMethod Pathfinder.Editor.Build.PlayerBuild.BuildBatch</c>.
    /// </summary>
    public static class PlayerBuild
    {
        const string Version = "0.6.0-slice";

        [MenuItem("Pathfinder/Build/Windows Playtest Build")]
        public static void BuildPlaytest() => Build("Builds/Windows", BuildOptions.None);

        [MenuItem("Pathfinder/Build/Windows Dev Build (hotkeys on)")]
        public static void BuildDev() => Build("Builds/WindowsDev", BuildOptions.Development);

        /// <summary>For the command line: builds the playtest build and exits with 0 on success.</summary>
        public static void BuildBatch()
        {
            var report = Build("Builds/Windows", BuildOptions.None);
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static BuildReport Build(string folder, BuildOptions options)
        {
            PlayerSettings.bundleVersion = Version;
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(folder, "ProjectPathfinder.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = options,
            });

            var summary = report.summary;
            Debug.Log($"[PlayerBuild] {summary.result}: {summary.outputPath}, {summary.totalSize / (1024 * 1024)} MB, " +
                $"{summary.totalErrors} errors, {summary.totalTime.TotalSeconds:F0} s");
            return report;
        }
    }
}
