using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AssetIndexer
{
    public static void RebuildIndex(bool onlySelectedFolders = false)
    {
        AssetDatabaseAsset db = AssetDatabaseUtility.LoadOrCreateDatabase();

        // 1) Cache old metadata by GUID so we can preserve tags etc.
        Dictionary<string, AssetMetadata> oldMetaDataByGuid = new Dictionary<string, AssetMetadata>();

        if (db.assets != null)
        {
            for (int i = 0; i < db.assets.Count; i++)
            {
                AssetMetadata meta = db.assets[i];
                if (meta != null && !string.IsNullOrEmpty(meta.guid))
                {
                    // last wins, but that should not matter
                    oldMetaDataByGuid[meta.guid] = meta;
                }
            }
        }

        // 2) Start fresh asset list
        db.assets.Clear();

        // Decide where to search
        string[] searchInFolders;

        if (onlySelectedFolders)
        {
            string[] selected = GetSelectedFolders();
            if (selected != null && selected.Length > 0)
            {
                searchInFolders = selected;
            }
            else
            {
                // Fallback
                searchInFolders = new string[] { "Assets" };
            }
        }
        else
        {
            // Hard-lock to Assets root only
            searchInFolders = new string[] { "Assets" };
        }

        string[] guids = AssetDatabase.FindAssets("", searchInFolders);

        for (int i = 0; i < guids.Length; i++)
        {
            string guid = guids[i];
            string path = AssetDatabase.GUIDToAssetPath(guid);

            // Skip folders entirely
            if (AssetDatabase.IsValidFolder(path))
            {
                continue;
            }

            // Only care about stuff inside Assets
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);

            string ext = Path.GetExtension(path).ToLowerInvariant();
            string typeName;
            float audioLengthSeconds = 0f;
            
            if (ext == ".wav" || ext == ".mp3" || ext == ".ogg" || ext == ".aiff" || ext == ".flac")
            {
                typeName = ext.TrimStart('.').ToUpper();
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null)
                {
                    audioLengthSeconds = clip.length;
                }
            }
            else
            {
                if (obj != null)
                {
                    typeName = obj.GetType().Name;
                }
                else
                {
                    typeName = ext.TrimStart('.');
                }
            }
            
            long fileSizeBytes = 0;
            try
            {
                FileInfo fileInfo = new FileInfo(path);
                if (fileInfo.Exists)
                {
                    fileSizeBytes = fileInfo.Length;
                }
            }
            catch
            {
                // ignore
            }

            // Build new metadata
            AssetMetadata meta = new AssetMetadata();
            meta.guid = guid;
            meta.assetPath = path;
            meta.assetName = (obj != null)
                                      ? obj.name
                                      : Path.GetFileNameWithoutExtension(path);
            meta.assetType = typeName;
            meta.fileSizeBytes = fileSizeBytes;
            meta.audioLengthSeconds = audioLengthSeconds;
            meta.LastIndexed = DateTime.Now;

            // 4) If we had metadata for this GUID before, copy over user-editable fields
            AssetMetadata old;
            if (oldMetaDataByGuid.TryGetValue(guid, out old) && old != null)
            {
                // Tags & category
                if (old.tags != null)
                {
                    meta.tags = new List<string>(old.tags);
                }
                else
                {
                    meta.tags = new List<string>();
                }

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
            else
            {
                // Make sure tags is at least an empty list so you don't get null refs
                meta.tags = new List<string>();
            }

            db.assets.Add(meta);
        }
        
        BuildDependencies(db);
        VersionControlIntegration.RefreshVersionControlInfo(db);
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
    }

    private static void BuildDependencies(AssetDatabaseAsset db)
    {
        if (db == null || db.assets == null)
        {
            return;
        }

        // Build a lookup from GUID to metadata
        Dictionary<string, AssetMetadata> guidToMeta = new Dictionary<string, AssetMetadata>();

        for (int i = 0; i < db.assets.Count; i++)
        {
            AssetMetadata meta = db.assets[i];
            if (meta != null && !string.IsNullOrEmpty(meta.guid))
            {
                guidToMeta[meta.guid] = meta;
            }
        }

        // Clear existing dependencies
        for (int i = 0; i < db.assets.Count; i++)
        {
            AssetMetadata meta = db.assets[i];
            if (meta == null)
            {
                continue;
            }

            if (meta.directDependencies == null)
            {
                meta.directDependencies = new List<string>();
            }
            else
            {
                meta.directDependencies.Clear();
            }

            if (meta.directDependants == null)
            {
                meta.directDependants = new List<string>();
            }
            else
            {
                meta.directDependants.Clear();
            }
        }

        // Forward dependencies
        for (int i = 0; i < db.assets.Count; i++)
        {
            AssetMetadata meta = db.assets[i];
            if (meta == null)
            {
                continue;
            }

            string assetPath = meta.assetPath;
            if (string.IsNullOrEmpty(assetPath))
            {
                continue;
            }

            string[] deps = AssetDatabase.GetDependencies(assetPath, false);
            if (deps == null)
            {
                continue;
            }

            for (int d = 0; d < deps.Length; d++)
            {
                string depPath = deps[d];

                // Only care about dependencies that are also inside Assets/
                if (!depPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string depGuid = AssetDatabase.AssetPathToGUID(depPath);
                if (string.IsNullOrEmpty(depGuid))
                {
                    continue;
                }

                // Skip self
                if (depGuid == meta.guid)
                {
                    continue;
                }

                if (!meta.directDependencies.Contains(depGuid))
                {
                    meta.directDependencies.Add(depGuid);
                }
            }
        }

        // Reverse dependencies (dependants)
        for (int i = 0; i < db.assets.Count; i++)
        {
            AssetMetadata meta = db.assets[i];
            if (meta == null)
            {
                continue;
            }

            if (meta.directDependencies == null)
            {
                continue;
            }

            for (int d = 0; d < meta.directDependencies.Count; d++)
            {
                string depGuid = meta.directDependencies[d];
                AssetMetadata depMeta;

                if (!guidToMeta.TryGetValue(depGuid, out depMeta) || depMeta == null)
                {
                    continue;
                }

                if (depMeta.directDependants == null)
                {
                    depMeta.directDependants = new List<string>();
                }

                if (!depMeta.directDependants.Contains(meta.guid))
                {
                    depMeta.directDependants.Add(meta.guid);
                }
            }
        }
    }

    private static string[] GetSelectedFolders()
    {
        UnityEngine.Object[] selection = Selection.GetFiltered<UnityEngine.Object>(SelectionMode.Assets);
        List<string> folders = new List<string>();

        if (selection != null)
        {
            for (int i = 0; i < selection.Length; i++)
            {
                UnityEngine.Object obj = selection[i];
                if (obj == null)
                {
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(obj);
                if (AssetDatabase.IsValidFolder(path) &&
                    path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                {
                    folders.Add(path);
                }
            }
        }

        return folders.ToArray();
    }
}
