using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class AssetManagerWindow
{
    // Optional separate fields for visible batch edits
    private string visibleCategoryInput = "";
    private string visibleTagInput      = "";

    private void DrawBatchToolsForVisible()
    {
        EditorGUILayout.LabelField("Batch Tools (Visible Assets)", EditorStyles.boldLabel);

        // --- Reimport all visible assets ---
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Reimport Visible", GUILayout.Width(140)))
        {
            BatchReimportVisible();
        }
        EditorGUILayout.EndHorizontal();

        // --- Category for all visible assets ---
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Category For Visible", GUILayout.Width(140));
        visibleCategoryInput = EditorGUILayout.TextField(visibleCategoryInput, GUILayout.Width(180));

        bool canApplyCategory = !string.IsNullOrEmpty(visibleCategoryInput) && filteredAssets.Count > 0;
        bool previousEnabled  = GUI.enabled;
        GUI.enabled = canApplyCategory;

        if (GUILayout.Button("Apply", GUILayout.Width(60)))
        {
            BatchSetCategoryVisible(visibleCategoryInput);
        }

        GUI.enabled = previousEnabled;
        EditorGUILayout.EndHorizontal();

        // --- Tags for all visible assets ---
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Tag For Visible", GUILayout.Width(140));
        visibleTagInput = EditorGUILayout.TextField(visibleTagInput, GUILayout.Width(180));

        bool canApplyTag = !string.IsNullOrEmpty(visibleTagInput) && filteredAssets.Count > 0;
        GUI.enabled = canApplyTag;

        if (GUILayout.Button("Add Tag", GUILayout.Width(80)))
        {
            BatchAddTagVisible(visibleTagInput);
        }

        GUI.enabled = filteredAssets.Count > 0;

        if (GUILayout.Button("Clear Tags", GUILayout.Width(80)))
        {
            BatchClearTagsVisible();
        }

        GUI.enabled = previousEnabled;
        EditorGUILayout.EndHorizontal();
    }

    private void BatchReimportVisible()
    {
        EnsureFilteredAssets();

        int count = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < filteredAssets.Count; i++)
            {
                AssetMetadata meta = filteredAssets[i];
                if (meta == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(meta.assetPath))
                {
                    continue;
                }

                AssetDatabase.ImportAsset(meta.assetPath, ImportAssetOptions.ForceUpdate);
                count = count + 1;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
        }

        Debug.Log("Reimported " + count + " assets from Asset Manager.");
    }

    private void BatchSetCategoryVisible(string category)
    {
        EnsureFilteredAssets();

        if (string.IsNullOrEmpty(category))
        {
            return;
        }

        for (int i = 0; i < filteredAssets.Count; i++)
        {
            AssetMetadata meta = filteredAssets[i];
            if (meta == null)
            {
                continue;
            }

            meta.category = category;
        }

        MarkDatabaseDirtyAndSave();
        filtersDirty = true;
    }

    private void BatchAddTagVisible(string tag)
    {
        EnsureFilteredAssets();

        if (string.IsNullOrEmpty(tag))
        {
            return;
        }

        string newTagLower = tag.ToLowerInvariant();

        for (int i = 0; i < filteredAssets.Count; i++)
        {
            AssetMetadata meta = filteredAssets[i];
            if (meta == null)
            {
                continue;
            }

            if (meta.tags == null)
            {
                meta.tags = new List<string>();
            }

            bool hasAlready = false;
            for (int t = 0; t < meta.tags.Count; t++)
            {
                string existing = meta.tags[t];
                if (!string.IsNullOrEmpty(existing) &&
                    existing.ToLowerInvariant() == newTagLower)
                {
                    hasAlready = true;
                    break;
                }
            }

            if (!hasAlready)
            {
                meta.tags.Add(tag);
            }
        }

        MarkDatabaseDirtyAndSave();
        filtersDirty = true;
    }

    private void BatchClearTagsVisible()
    {
        EnsureFilteredAssets();

        for (int i = 0; i < filteredAssets.Count; i++)
        {
            AssetMetadata meta = filteredAssets[i];
            if (meta == null)
            {
                continue;
            }

            if (meta.tags != null)
            {
                meta.tags.Clear();
            }
        }

        MarkDatabaseDirtyAndSave();
        filtersDirty = true;
    }

    // Optional: later you can use this for texture import settings
    private void BatchApplyTextureSettingsVisible(int maxSize, TextureImporterCompression compression)
    {
        EnsureFilteredAssets();

        try
        {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < filteredAssets.Count; i++)
            {
                AssetMetadata meta = filteredAssets[i];
                if (meta == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(meta.assetPath))
                {
                    continue;
                }

                TextureImporter importer = AssetImporter.GetAtPath(meta.assetPath) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                importer.maxTextureSize     = maxSize;
                importer.textureCompression = compression;

                EditorUtility.SetDirty(importer);
                AssetDatabase.ImportAsset(meta.assetPath, ImportAssetOptions.ForceUpdate);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
        }
    }
}
