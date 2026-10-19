using System;
using System.IO;
using UnityEngine;

namespace SeaOfUncertainty.Prototype
{
    public static class GitHubUpdates
    {
        public const string ReleasesUrl = "https://github.com/murdockpeter/seaofuncertainty/releases";

        public static string CheckForUpdates()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                // Packaged layout: root/versions/vX.Y.Z/Sea of Uncertainty_Data.
                string versionDirectory = Path.GetDirectoryName(Application.dataPath);
                string versionsDirectory = Path.GetDirectoryName(versionDirectory);
                string root = Path.GetDirectoryName(versionsDirectory);
                string launcher = Path.Combine(root, "SeaLauncher.ps1");
                if (Path.GetFileName(versionsDirectory) == "versions" && File.Exists(launcher))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe"),
                        Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + launcher + "\" -NoLaunch",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    return "The update checker has opened. You can keep playing; installed updates take effect when you exit and start the game through its launcher.";
                }
            }
            catch (Exception exception) { return "Could not open the update checker: " + exception.Message; }
#endif
            Application.OpenURL(ReleasesUrl);
            return "The GitHub Releases page has opened. Download and extract a launcher package to enable automatic update checks.";
        }
    }
}
