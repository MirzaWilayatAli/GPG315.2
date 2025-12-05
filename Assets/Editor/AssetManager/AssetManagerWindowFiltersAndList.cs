using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class AssetManagerWindow
{
    private void DrawAssetList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.55f));

        DrawFilterSection();

        EditorGUILayout.Space();

        DrawBatchToolsForVisible();   // batch on filtered list
        EditorGUILayout.Space();
        DrawBatchEditSelected();      // batch on selected assets

        EditorGUILayout.Space();

        EnsureFilteredAssets();
        DrawPaginationSection();

        EditorGUILayout.Space();

        DrawListHeaders();
        DrawListItems();

        EditorGUILayout.EndVertical();
    }

    private void DrawFilterSection()
    {
        EditorGUILayout.LabelField("Filters", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        // Filter by Tag
        EditorGUILayout.BeginHorizontal(GUILayout.Width(250));
        EditorGUILayout.LabelField("Filter By Tag", GUILayout.Width(90));
        string newTagFilter = EditorGUILayout.TextField(tagFilter, GUILayout.Width(150));
        if (newTagFilter != tagFilter)
        {
            tagFilter   = newTagFilter;
            filtersDirty = true;
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(30);

        // Filter by Type
        EditorGUILayout.BeginHorizontal(GUILayout.Width(250));
        EditorGUILayout.LabelField("Filter By Type", GUILayout.Width(90));
        string newTypeFilter = EditorGUILayout.TextField(typeFilter, GUILayout.Width(150));
        if (newTypeFilter != typeFilter)
        {
            typeFilter   = newTypeFilter;
            filtersDirty = true;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndHorizontal();
    }

    // Filtering logic here

    private void EnsureFilteredAssets()
    {
        if (databaseAsset == null || databaseAsset.assets == null)
        {
            filteredAssets.Clear();
            return;
        }

        if (!filtersDirty && filteredAssets.Count > 0)
        {
            return;
        }

        filteredAssets.Clear();

        string searchLower = null;
        string tagLower    = null;
        string typeLower   = null;

        if (!string.IsNullOrEmpty(searchText))
        {
            searchLower = searchText.ToLowerInvariant();
        }
        if (!string.IsNullOrEmpty(tagFilter))
        {
            tagLower = tagFilter.ToLowerInvariant();
        }
        if (!string.IsNullOrEmpty(typeFilter))
        {
            typeLower = typeFilter.ToLowerInvariant();
        }

        for (int i = 0; i < databaseAsset.assets.Count; i++)
        {
            AssetMetadata a = databaseAsset.assets[i];
            if (a == null)
            {
                continue;
            }

            // Search filter
            if (searchLower != null)
            {
                bool match = false;

                if (!string.IsNullOrEmpty(a.assetName) &&
                    a.assetName.ToLowerInvariant().Contains(searchLower))
                {
                    match = true;
                }

                if (!match && !string.IsNullOrEmpty(a.assetPath) &&
                    a.assetPath.ToLowerInvariant().Contains(searchLower))
                {
                    match = true;
                }

                if (!match && a.tags != null)
                {
                    for (int t = 0; t < a.tags.Count; t++)
                    {
                        string tagValue = a.tags[t];
                        if (!string.IsNullOrEmpty(tagValue) &&
                            tagValue.ToLowerInvariant().Contains(searchLower))
                        {
                            match = true;
                            break;
                        }
                    }
                }

                if (!match)
                {
                    continue;
                }
            }

            // Tag filter
            if (tagLower != null)
            {
                bool hasTag = false;
                if (a.tags != null)
                {
                    for (int t = 0; t < a.tags.Count; t++)
                    {
                        string tagValue = a.tags[t];
                        if (!string.IsNullOrEmpty(tagValue) &&
                            tagValue.ToLowerInvariant().Contains(tagLower))
                        {
                            hasTag = true;
                            break;
                        }
                    }
                }

                if (!hasTag)
                {
                    continue;
                }
            }

            // Type filter
            if (typeLower != null)
            {
                if (string.IsNullOrEmpty(a.assetType) ||
                    !a.assetType.ToLowerInvariant().Contains(typeLower))
                {
                    continue;
                }
            }

            filteredAssets.Add(a);
        }

        // Remove selections no longer visible
        HashSet<string> visibleGuids = new HashSet<string>();
        for (int i = 0; i < filteredAssets.Count; i++)
        {
            visibleGuids.Add(filteredAssets[i].guid);
        }

        List<string> toRemove = new List<string>();
        foreach (string guid in selectedGuids)
        {
            if (!visibleGuids.Contains(guid))
            {
                toRemove.Add(guid);
            }
        }
        for (int i = 0; i < toRemove.Count; i++)
        {
            selectedGuids.Remove(toRemove[i]);
        }

        filtersDirty = false;
        currentPage  = 0;
    }

    // Pagination

    private void DrawPaginationSection()
    {
        int total = filteredAssets.Count;
        int totalPages = 1;

        if (total > 0)
        {
            float pages = total / (float)RowCountPerPage;
            totalPages = Mathf.CeilToInt(pages);
        }

        if (totalPages < 1)
        {
            totalPages = 1;
        }

        if (currentPage < 0)
        {
            currentPage = 0;
        }
        if (currentPage > totalPages - 1)
        {
            currentPage = totalPages - 1;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Results: " + total + " (Page " + (currentPage + 1) + "/" + totalPages + ")");

        GUI.enabled = currentPage > 0;
        if (GUILayout.Button("Prev", GUILayout.Width(50)))
        {
            currentPage--;
        }

        GUI.enabled = currentPage < totalPages - 1;
        if (GUILayout.Button("Next", GUILayout.Width(50)))
        {
            currentPage++;
        }
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();
    }
    

    private void DrawListHeaders()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        GUILayout.Space(20); // checkbox column
        GUILayout.Label("Name",     EditorStyles.boldLabel, GUILayout.Width(NameColumnWidth));
        GUILayout.Label("Type",     EditorStyles.boldLabel, GUILayout.Width(TypeColumnWidth));
        GUILayout.Label("Category", EditorStyles.boldLabel, GUILayout.Width(CategoryColumnWidth));
        GUILayout.Label("Tags",     EditorStyles.boldLabel);

        EditorGUILayout.EndHorizontal();
    }

    private void DrawListItems()
    {
        listScroll = EditorGUILayout.BeginScrollView(listScroll);

        int total = filteredAssets.Count;
        int startIndex = currentPage * RowCountPerPage;
        int endIndex = startIndex + RowCountPerPage;

        if (startIndex < 0)
        {
            startIndex = 0;
        }
        if (endIndex > total)
        {
            endIndex = total;
        }

        for (int i = startIndex; i < endIndex; i++)
        {
            AssetMetadata meta = filteredAssets[i];
            DrawAssetListItem(meta);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawAssetListItem(AssetMetadata meta)
    {
        if (meta == null)
        {
            return;
        }

        Rect rowRect = EditorGUILayout.BeginHorizontal();

        DrawHoverHighlight(rowRect);
        
        // Multi-select checkbox
        bool wasSelected = selectedGuids.Contains(meta.guid);
        bool nowSelected = GUILayout.Toggle(wasSelected, GUIContent.none, GUILayout.Width(20));

        if (nowSelected != wasSelected)
        {
            if (nowSelected)
            {
                selectedGuids.Add(meta.guid);
                selectedAsset = meta;
                renameBuffer = meta.assetName;
            }
            else
            {
                selectedGuids.Remove(meta.guid);
                if (selectedAsset == meta)
                {
                    selectedAsset = null;
                }
            }
        }
        
        if (GUILayout.Button(meta.assetName, EditorStyles.label, GUILayout.Width(NameColumnWidth)))
        {
            selectedAsset = meta;
            renameBuffer = meta.assetName;

            Object obj = AssetDatabase.LoadMainAssetAtPath(meta.assetPath);
            if (obj != null)
            {
                Selection.activeObject = obj;
            }
        }

        GUILayout.Label(meta.assetType,      GUILayout.Width(TypeColumnWidth));
        GUILayout.Label(meta.category ?? "", GUILayout.Width(CategoryColumnWidth));

        string tagsDisplay = "";
        if (meta.tags != null && meta.tags.Count > 0)
        {
            for (int i = 0; i < meta.tags.Count; i++)
            {
                if (i > 0)
                {
                    tagsDisplay += ", ";
                }
                tagsDisplay += meta.tags[i];
            }
        }

        GUILayout.Label(tagsDisplay);

        EditorGUILayout.EndHorizontal();
    }
}
