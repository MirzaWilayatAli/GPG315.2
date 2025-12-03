using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class AssetManagerWindow
{
    private void DrawBatchEditSelected()
    {
        EditorGUILayout.LabelField("Batch Edit (Selected Assets)", EditorStyles.boldLabel);

        // Category batch field
        EditorGUILayout.BeginHorizontal();
        batchCategoryInput = EditorGUILayout.TextField("Category", batchCategoryInput);
        bool canApplyCategory = selectedGuids.Count > 0 && !string.IsNullOrEmpty(batchCategoryInput);
        GUI.enabled = canApplyCategory;
        if (GUILayout.Button("Apply Category", GUILayout.Width(130)))
        {
            ApplyCategoryToSelected(batchCategoryInput);
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        // Tag batch field
        EditorGUILayout.BeginHorizontal();
        batchTagInput = EditorGUILayout.TextField("Tag", batchTagInput);
        bool canApplyTag = selectedGuids.Count > 0 && !string.IsNullOrEmpty(batchTagInput);
        GUI.enabled = canApplyTag;
        if (GUILayout.Button("Add Tag", GUILayout.Width(130)))
        {
            ApplyTagToSelected(batchTagInput);
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField("Selected: " + selectedGuids.Count);
    }

    private void ApplyCategoryToSelected(string category)
    {
        if (databaseAsset == null || databaseAsset.assets == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(category))
        {
            return;
        }

        for (int i = 0; i < databaseAsset.assets.Count; i++)
        {
            AssetMetadata meta = databaseAsset.assets[i];
            if (meta == null)
            {
                continue;
            }

            if (!selectedGuids.Contains(meta.guid))
            {
                continue;
            }

            meta.category = category;
        }

        MarkDatabaseDirtyAndSave();
        filtersDirty = true;
    }

    private void ApplyTagToSelected(string tag)
    {
        if (databaseAsset == null || databaseAsset.assets == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(tag))
        {
            return;
        }

        string tagLower = tag.ToLowerInvariant();

        for (int i = 0; i < databaseAsset.assets.Count; i++)
        {
            AssetMetadata meta = databaseAsset.assets[i];
            if (meta == null)
            {
                continue;
            }

            if (!selectedGuids.Contains(meta.guid))
            {
                continue;
            }

            if (meta.tags == null)
            {
                meta.tags = new List<string>();
            }

            bool already = false;
            for (int t = 0; t < meta.tags.Count; t++)
            {
                string existing = meta.tags[t];
                if (!string.IsNullOrEmpty(existing) &&
                    existing.ToLowerInvariant() == tagLower)
                {
                    already = true;
                    break;
                }
            }

            if (!already)
            {
                meta.tags.Add(tag);
            }
        }

        MarkDatabaseDirtyAndSave();
        filtersDirty = true;
    }
}
