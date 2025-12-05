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

        // 1) Cache old metadata by GUID so we can preserve tags and categories
        Dictionary<string, AssetMetadata> oldMetaDataByGuid = new Dictionary<string, AssetMetadata>();

        if (db.assets != null)
        {
            for (int i = 0; i < db.assets.Count; i++)
            {
                AssetMetadata meta = db.assets[i];
                if (meta != null && !string.IsNullOrEmpty(meta.guid))
                {
                    oldMetaDataByGuid[meta.guid] = meta;
                }
            }
        }

        // 2-Start fresh asset list
        db.assets.Clear();
        
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
                searchInFolders = new string[] { "Assets" };
            }
        }
        else
        {
            searchInFolders = new string[] { "Assets" };
        }

        string[] guids = AssetDatabase.FindAssets("", searchInFolders);

        for (int i = 0; i < guids.Length; i++)
        {
            string guid = guids[i];
            string path = AssetDatabase.GUIDToAssetPath(guid);

            // Skip folders
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
            if (obj != null)
            {
                meta.assetName = obj.name;
            }
            else
            {
                string fileName = Path.GetFileNameWithoutExtension(path);
                meta.assetName = fileName;
            }
            meta.assetType = typeName;
            meta.fileSizeBytes = fileSizeBytes;
            meta.audioLengthSeconds = audioLengthSeconds;
            meta.LastIndexed = DateTime.Now;

            // 4-If we had metadata for this GUID before, copy over user-editable fields
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
                
                meta.customField1Label = old.customField1Label;
                meta.customField1Value = old.customField1Value;
                meta.customField2Label = old.customField2Label;
                meta.customField2Value = old.customField2Value;
                
                meta.vcsStatus = old.vcsStatus;
                meta.vcsSystem = old.vcsSystem;
            }
            else
            {
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

            string[] dependencies = AssetDatabase.GetDependencies(assetPath, false);
            if (dependencies == null)
            {
                continue;
            }

            for (int d = 0; d < dependencies.Length; d++)
            {
                string dependencyPath = dependencies[d];

                // Only care about dependencies that are also inside Assets
                if (!dependencyPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string dependencyGuid = AssetDatabase.AssetPathToGUID(dependencyPath);
                if (string.IsNullOrEmpty(dependencyGuid))
                {
                    continue;
                }

                // Skip self
                if (dependencyGuid == meta.guid)
                {
                    continue;
                }

                if (!meta.directDependencies.Contains(dependencyGuid))
                {
                    meta.directDependencies.Add(dependencyGuid);
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
                string directDependencyGuid = meta.directDependencies[d];
                AssetMetadata dependencyMetaData;

                if (!guidToMeta.TryGetValue(directDependencyGuid, out dependencyMetaData) || dependencyMetaData == null)
                {
                    continue;
                }

                if (dependencyMetaData.directDependants == null)
                {
                    dependencyMetaData.directDependants = new List<string>();
                }

                if (!dependencyMetaData.directDependants.Contains(meta.guid))
                {
                    dependencyMetaData.directDependants.Add(meta.guid);
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
