#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SeaOfUncertainty.Editor
{
    public static class GitHubReleaseBuilder
    {
        public static void BuildWindowsRelease()
        {
            string version = Environment.GetEnvironmentVariable("SEA_RELEASE_VERSION");
            string output = Environment.GetEnvironmentVariable("SEA_RELEASE_OUTPUT");
            if (!System.Text.RegularExpressions.Regex.IsMatch(version ?? "", @"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$") || string.IsNullOrEmpty(output))
                throw new InvalidOperationException("Set SEA_RELEASE_VERSION and SEA_RELEASE_OUTPUT using tools/package-release.ps1.");
            string previous = PlayerSettings.bundleVersion;
            try
            {
                PlayerSettings.bundleVersion = version;
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Scenes/Prototype.unity" },
                    locationPathName = System.IO.Path.Combine(output, "Sea of Uncertainty.exe"),
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows release build failed: " + report.summary.result);
                Debug.Log("GitHub Windows release built: " + version);
            }
            finally { PlayerSettings.bundleVersion = previous; AssetDatabase.SaveAssets(); }
        }
    }
}
#endif
