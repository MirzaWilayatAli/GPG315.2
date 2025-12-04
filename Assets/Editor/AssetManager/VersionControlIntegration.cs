using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;

public static class VersionControlIntegration
{
    public static void RefreshVersionControlInfo(AssetDatabaseAsset db)
    {
        if (db == null || db.assets == null) return;

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        
        foreach (var meta in db.assets)
        {
            meta.vcsSystem = "None";
            meta.vcsStatus = "Unknown";
        }

        if (Directory.Exists(Path.Combine(projectRoot, ".git")))
        {
            UpdateFromGit(db, projectRoot);
        }
    }

    private static void UpdateFromGit(AssetDatabaseAsset db, string projectRoot)
    {
        // git status --porcelain gives us a 2-char code + path per line
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "status --porcelain",
            WorkingDirectory = projectRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            // Build lookup: "Assets/..." -> (code, path)
            var map = new Dictionary<string, string>();

            using (var reader = new StringReader(output))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length < 4) continue;
                    // Format: "XY path"
                    string code = line.Substring(0, 2);
                    string path = line.Substring(3).Replace('\\', '/'); // normalize

                    // Want Unity-style path, relative to project root
                    if (!path.StartsWith("Assets/"))
                        continue;

                    map[path] = code.Trim();
                }
            }

            // Apply to metadata
            foreach (var meta in db.assets)
            {
                if (meta == null || string.IsNullOrEmpty(meta.assetPath))
                    continue;

                meta.vcsSystem = "Git";

                if (map.TryGetValue(meta.assetPath, out string code))
                {
                    meta.vcsStatus = ParseGitStatusCode(code);
                }
                else
                {
                    // Not in status output => tracked & clean (most of the time)
                    meta.vcsStatus = "Up to date";
                }
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogWarning("Git status failed: " + ex.Message);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
            process.Dispose();
        }
    }

    private static string ParseGitStatusCode(string code)
    {
        switch (code)
        {
            case "??": return "Untracked";
            case "A": 
            case "A?":
            case "AM":
            case "M?": return "Added";
            case "M":
            case " M":
            case "MM":
            case "M.": return "Modified";
            case "D":
            case " D": return "Deleted";
            case "R":
            case " R": return "Renamed";
            case "C": return "Copied";
            default:   return "Changed"; 
        }
    }
}
