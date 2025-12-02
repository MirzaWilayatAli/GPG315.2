using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using System.IO;
using UnityEngine;

public static class AssetIndexer
{
    public static void RebuildIndex(bool onlySelectedFolders = false)
    {
        var db = AssetDatabaseUtility.LoadOrCreateDatabase();

        // 1) Cache old metadata by GUID so we can preserve tags etc.
        var oldByGuid = new Dictionary<string, AssetMetadata>();
        if (db.assets != null)
        {
            foreach (var meta in db.assets)
            {
                if (meta != null && !string.IsNullOrEmpty(meta.guid))
                {
                    oldByGuid[meta.guid] = meta;
                }
            }
        }

        // 2) Start fresh asset list
        db.assets.Clear();

        // Decide where to search
        string[] searchInFolders;

        if (onlySelectedFolders)
        {
            var selected = GetSelectedFolders();
            searchInFolders = (selected != null && selected.Length > 0)
                ? selected
                : new[] { "Assets" }; // fallback
        }
        else
        {
            // Hard-lock to Assets root only
            searchInFolders = new[] { "Assets" };
        }

        string[] guids = AssetDatabase.FindAssets("", searchInFolders);

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            
            // Skip folders entirely (do not index them)
            if (AssetDatabase.IsValidFolder(path))
                continue;

            // Only care about stuff inside Assets
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                continue;

            // Skip dlls if you want
            if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                continue;

            UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);

            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();


            string typeName;
            bool isAudioExt = ext == ".wav" || ext == ".mp3" || ext == ".ogg" || ext == ".aiff" || ext == ".flac";

            if (isAudioExt)
            {
                typeName = ext.TrimStart('.').ToUpper();
            }
            else
            {
                typeName = obj != null
                    ? obj.GetType().Name
                    : ext.TrimStart('.');
            }


            long fileSizeBytes = 0;
            try
            {
                var fi = new FileInfo(path);   // path is "Assets/..." and project root is current dir
                if (fi.Exists)
                    fileSizeBytes = fi.Length;
            }
            catch
            {
                // ignore IO errors, keep as 0
            }


            float audioLengthSeconds = 0f;
            if (isAudioExt)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null)
                {
                    audioLengthSeconds = clip.length;
                }
            }

            var meta = new AssetMetadata
            {
                guid = guid,
                assetPath = path,
                assetName = obj != null
                    ? obj.name
                    : System.IO.Path.GetFileNameWithoutExtension(path),
                assetType = typeName,
                fileSizeBytes = fileSizeBytes,
                audioLengthSeconds = audioLengthSeconds,
                lastIndexed = DateTime.Now
            };



            // 4) If we had metadata for this GUID before, copy over user-editable fields
            if (oldByGuid.TryGetValue(guid, out var old))
            {
                // Tags & category
                meta.tags = old.tags != null ? new List<string>(old.tags) : new List<string>();
                meta.category = old.category;

                // Custom fields
                meta.customField1Label = old.customField1Label;
                meta.customField1Value = old.customField1Value;
                meta.customField2Label = old.customField2Label;
                meta.customField2Value = old.customField2Value;

                // Version control info
                meta.vcsStatus = old.vcsStatus;
                meta.vcsSystem = old.vcsSystem;
            }

            db.assets.Add(meta);
        }

        // 5) Rebuild dependencies for the new list
        BuildDependencies(db);

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
    }

    private static void BuildDependencies(AssetDatabaseAsset db)
    {
        var guidToMeta = db.assets.ToDictionary(a => a.guid, a => a);

        // Clear existing
        foreach (var meta in db.assets)
        {
            meta.directDependencies.Clear();
            meta.directDependants.Clear();
        }

        // Forward deps
        foreach (var meta in db.assets)
        {
            string[] deps = AssetDatabase.GetDependencies(meta.assetPath, false);
            foreach (var depPath in deps)
            {
                // 🔒 Only care about dependencies that are also inside Assets/
                if (!depPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    continue;

                string depGuid = AssetDatabase.AssetPathToGUID(depPath);
                if (string.IsNullOrEmpty(depGuid) || depGuid == meta.guid)
                    continue;

                if (!meta.directDependencies.Contains(depGuid))
                    meta.directDependencies.Add(depGuid);
            }
        }

        // Reverse deps (only between Assets/ entries)
        foreach (var meta in db.assets)
        {
            foreach (var depGuid in meta.directDependencies)
            {
                if (guidToMeta.TryGetValue(depGuid, out var depMeta))
                {
                    if (!depMeta.directDependants.Contains(meta.guid))
                        depMeta.directDependants.Add(meta.guid);
                }
            }
        }
    }

    private static string[] GetSelectedFolders()
    {
        var selection = Selection.GetFiltered<UnityEngine.Object>(SelectionMode.Assets);
        var folders = new List<string>();
        foreach (var obj in selection)
        {
            string path = AssetDatabase.GetAssetPath(obj);

            // Only keep folders that are under Assets/
            if (AssetDatabase.IsValidFolder(path) &&
                path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                folders.Add(path);
            }
        }
        return folders.ToArray();
    }
}
