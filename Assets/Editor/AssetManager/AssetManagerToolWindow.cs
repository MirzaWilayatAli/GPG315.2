using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using System.IO;

public class AssetManagerWindow : EditorWindow
{
    private AssetDatabaseAsset db;
    private const float NameColumnWidth     = 220f;
    private const float TypeColumnWidth     = 220f;
    private const float CategoryColumnWidth = 220f;
    
    private string renameBuffer = string.Empty;

    // UI state
    private string searchText = "";
    private string tagFilter = "";
    private string typeFilter = "";
    private Vector2 listScroll;
    private Vector2 detailsScroll;
    private AssetMetadata selectedAsset;

    // Cached filtered list
    private List<AssetMetadata> filteredAssets = new List<AssetMetadata>();
    private bool filtersDirty = true;

    // Pagination
    private const int rowCountPerPage = 100; 
    private int currentPage = 0;

    [MenuItem("Tools/Asset Manager")]
    public static void ShowWindow()
    {
        var window = GetWindow<AssetManagerWindow>("Asset Manager");
        window.Show();
    }

    private void OnEnable()
    {
        db = AssetDatabaseUtility.LoadOrCreateDatabase();
        filtersDirty = true;
        currentPage = 0;
    }

    private void OnGUI()
    {
        if (db == null)
        {
            db = AssetDatabaseUtility.LoadOrCreateDatabase();
            filtersDirty = true;
            currentPage = 0;
        }

        DrawToolbar();
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();

        DrawAssetList();
        DrawDetailsPanel();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
        {
            AssetIndexer.RebuildIndex(false);
            filtersDirty = true;
            currentPage = 0;
        }

        if (GUILayout.Button("Refresh Selected Folders", EditorStyles.toolbarButton))
        {
            AssetIndexer.RebuildIndex(true);
            filtersDirty = true;
            currentPage = 0;
        }

        GUILayout.FlexibleSpace();

        // --- Search field with placeholder (custom height) ---
        float searchHeight = 20f; // ⬅️ increase this to whatever you like (22–28 looks good)
        float searchWidth  = 220f;

        Rect searchRect = GUILayoutUtility.GetRect(
            searchWidth,
            searchHeight,
            GUILayout.ExpandWidth(false)
        );

// Custom style NOT forced by toolbar
        GUIStyle searchStyle = new GUIStyle(GUI.skin.textField);
        searchStyle.fontSize = 12;
        searchStyle.fixedHeight = searchHeight;
        searchStyle.padding = new RectOffset(6, 6, 4, 4);

// Draw actual input box
        GUI.SetNextControlName("AssetManagerSearchField");
        string newSearch = GUI.TextField(searchRect, searchText, searchStyle);

        if (newSearch != searchText)
        {
            searchText = newSearch;
            filtersDirty = true;
            currentPage = 0;
        }

// Placeholder
        if (string.IsNullOrEmpty(searchText) &&
            GUI.GetNameOfFocusedControl() != "AssetManagerSearchField" &&
            Event.current.type == EventType.Repaint)
        {
            GUIStyle placeholderStyle = new GUIStyle(searchStyle);
            placeholderStyle.normal.textColor = new Color(1f, 1f, 1f, 0.35f);

            GUI.Label(searchRect, "Search...", placeholderStyle);
        }


        // Clear button
        if (GUILayout.Button("X", EditorStyles.toolbarButton, GUILayout.Width(20)))
        {
            if (!string.IsNullOrEmpty(searchText))
            {
                searchText = "";
                filtersDirty = true;
                currentPage = 0;
            }
            GUI.FocusControl(null);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawAssetList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.55f));

        EditorGUILayout.LabelField("Filters", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        // ----- Group 1: Filter By Tag -----
        EditorGUILayout.BeginHorizontal(GUILayout.Width(250)); // control group width as needed
        EditorGUILayout.LabelField("Filter By Tag", GUILayout.Width(90));
        string newTagFilter = EditorGUILayout.TextField(tagFilter, GUILayout.Width(150));
        if (newTagFilter != tagFilter)
        {
            tagFilter = newTagFilter;
            filtersDirty = true;
            currentPage = 0;
        }
        EditorGUILayout.EndHorizontal();

        // Spacing between groups
        GUILayout.Space(30);

        // ----- Group 2: Filter By Type -----
        EditorGUILayout.BeginHorizontal(GUILayout.Width(250));
        EditorGUILayout.LabelField("Filter By Type", GUILayout.Width(90));
        string newTypeFilter = EditorGUILayout.TextField(typeFilter, GUILayout.Width(150));
        if (newTypeFilter != typeFilter)
        {
            typeFilter = newTypeFilter;
            filtersDirty = true;
            currentPage = 0;
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // Update filteredAssets if needed
        EnsureFilteredAssets();

        int total      = filteredAssets.Count;
        int totalPages = Mathf.Max(1, Mathf.CeilToInt(total / (float)rowCountPerPage));
        currentPage    = Mathf.Clamp(currentPage, 0, totalPages - 1);

        // Pagination controls
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Results: {total} (Page {currentPage + 1}/{totalPages})");

        GUI.enabled = currentPage > 0;
        if (GUILayout.Button("Prev", GUILayout.Width(50)))
            currentPage--;
        GUI.enabled = currentPage < totalPages - 1;
        if (GUILayout.Button("Next", GUILayout.Width(50)))
            currentPage++;
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // Column headers – use same widths as rows
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        GUILayout.Space(20); // checkbox column
        GUILayout.Label("Name",     EditorStyles.boldLabel, GUILayout.Width(NameColumnWidth));
        GUILayout.Label("Type",     EditorStyles.boldLabel, GUILayout.Width(TypeColumnWidth));
        GUILayout.Label("Category", EditorStyles.boldLabel, GUILayout.Width(CategoryColumnWidth));
        GUILayout.Label("Tags",     EditorStyles.boldLabel); // expands
        EditorGUILayout.EndHorizontal();

        listScroll = EditorGUILayout.BeginScrollView(listScroll);

        int startIndex = currentPage * rowCountPerPage;
        int endIndex   = Mathf.Min(total, startIndex + rowCountPerPage);

        for (int i = startIndex; i < endIndex; i++)
        {
            DrawAssetListItem(filteredAssets[i]);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }


    private void EnsureFilteredAssets()
    {
        if (db == null || db.assets == null)
        {
            filteredAssets.Clear();
            return;
        }

        // If nothing changed and we already have results, reuse
        if (!filtersDirty && filteredAssets.Count > 0)
            return;

        filteredAssets.Clear();

        string searchLower = string.IsNullOrEmpty(searchText) ? null : searchText.ToLowerInvariant();
        string tagLower = string.IsNullOrEmpty(tagFilter) ? null : tagFilter.ToLowerInvariant();
        string typeLower = string.IsNullOrEmpty(typeFilter) ? null : typeFilter.ToLowerInvariant();

        foreach (var a in db.assets)
        {
            if (a == null) continue;

            // Search filter
            if (searchLower != null)
            {
                bool match =
                    (!string.IsNullOrEmpty(a.assetName) && a.assetName.ToLowerInvariant().Contains(searchLower)) ||
                    (!string.IsNullOrEmpty(a.assetPath) && a.assetPath.ToLowerInvariant().Contains(searchLower));

                if (!match && a.tags != null)
                {
                    foreach (var t in a.tags)
                    {
                        if (!string.IsNullOrEmpty(t) && t.ToLowerInvariant().Contains(searchLower))
                        {
                            match = true;
                            break;
                        }
                    }
                }

                if (!match)
                    continue;
            }

            // Tag filter
            if (tagLower != null)
            {
                bool hasTag = false;
                if (a.tags != null)
                {
                    foreach (var t in a.tags)
                    {
                        if (!string.IsNullOrEmpty(t) && t.ToLowerInvariant().Contains(tagLower))
                        {
                            hasTag = true;
                            break;
                        }
                    }
                }

                if (!hasTag)
                    continue;
            }

            // Type filter
            if (typeLower != null)
            {
                if (string.IsNullOrEmpty(a.assetType) || !a.assetType.ToLowerInvariant().Contains(typeLower))
                    continue;
            }

            filteredAssets.Add(a);
        }

        filtersDirty = false;
        // when filters change we automatically reset to first page
        currentPage = 0;
    }

    private void DrawAssetListItem(AssetMetadata meta)
    {
        EditorGUILayout.BeginHorizontal();

        bool wasSelected = (selectedAsset == meta);
        bool nowSelected = GUILayout.Toggle(wasSelected, GUIContent.none, GUILayout.Width(20));

        if (nowSelected && !wasSelected)
        {
            selectedAsset = meta;
            renameBuffer = selectedAsset.assetName;  // sync name into buffer
        }
        else if (!nowSelected && wasSelected)
        {
            if (selectedAsset == meta)
                selectedAsset = null;
        }
        // Name column
        if (GUILayout.Button(meta.assetName, EditorStyles.label, GUILayout.Width(NameColumnWidth)))
        {
            selectedAsset = meta;
            renameBuffer = selectedAsset.assetName;  // NEW
            var obj = UnityEditor.AssetDatabase.LoadMainAssetAtPath(meta.assetPath);
            Selection.activeObject = obj;
        }

        // Type / Category / Tags columns, fixed widths for alignment
        GUILayout.Label(meta.assetType,           GUILayout.Width(TypeColumnWidth));
        GUILayout.Label(meta.category ?? "",      GUILayout.Width(CategoryColumnWidth));
        GUILayout.Label(string.Join(", ", meta.tags), GUILayout.ExpandWidth(true));

        EditorGUILayout.EndHorizontal();
    }


    private void DrawDetailsPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.45f));
        EditorGUILayout.LabelField("Details", EditorStyles.boldLabel);

        if (selectedAsset == null)
        {
            EditorGUILayout.HelpBox("Select an asset to view and edit metadata.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        detailsScroll = EditorGUILayout.BeginScrollView(detailsScroll);

        // Basic info
        EditorGUILayout.LabelField("Path", selectedAsset.assetPath);
        EditorGUILayout.LabelField("Type", selectedAsset.assetType);
        EditorGUILayout.LabelField("GUID", selectedAsset.guid);

// --- File info: size + audio length ---
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("File Info", EditorStyles.boldLabel);

        string sizeText = FormatFileSize(selectedAsset.fileSizeBytes);
        EditorGUILayout.LabelField("File Size", sizeText);

        if (selectedAsset.audioLengthSeconds > 0.01f)
        {
            TimeSpan ts = TimeSpan.FromSeconds(selectedAsset.audioLengthSeconds);
            string timeFormatted = ts.Hours > 0
                ? ts.ToString(@"h\:mm\:ss")
                : ts.ToString(@"m\:ss");
            EditorGUILayout.LabelField("Audio Length", timeFormatted);
        }
        else
        {
            EditorGUILayout.LabelField("Audio Length", "N/A");
        }

        // --- Rename section ---
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Rename", EditorStyles.boldLabel);

        renameBuffer = EditorGUILayout.TextField("Name", renameBuffer);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply Rename"))
        {
            RenameSelectedAsset();
        }

        if (GUILayout.Button("Open in OS"))
        {
            OpenSelectedInOS();
        }

        GUI.enabled = IsGameObjectAsset(selectedAsset);
        if (GUILayout.Button("Add to Scene"))
        {
            AddSelectedPrefabToScene();
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // --- Metadata below as before ---
        selectedAsset.category = EditorGUILayout.TextField("Category", selectedAsset.category);


        // Tags as comma-separated
        string tagsJoined = string.Join(", ", selectedAsset.tags);
        string newTags = EditorGUILayout.TextField("Tags", tagsJoined);
        if (newTags != tagsJoined)
        {
            selectedAsset.tags = newTags
                .Split(',')
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .Distinct()
                .ToList();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Custom Fields", EditorStyles.boldLabel);
        selectedAsset.customField1Value = EditorGUILayout.TextField("Designer", selectedAsset.customField1Value);
        selectedAsset.customField2Value = EditorGUILayout.TextField("Notes", selectedAsset.customField2Value);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Version Control", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("System", selectedAsset.vcsSystem ?? "Unknown");
        EditorGUILayout.LabelField("Status", selectedAsset.vcsStatus ?? "Unknown");
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Dependencies", EditorStyles.boldLabel);

        if (selectedAsset.directDependencies != null && selectedAsset.directDependencies.Count > 0)
        {
            foreach (var depGuid in selectedAsset.directDependencies)
            {
                string path = AssetDatabase.GUIDToAssetPath(depGuid);

                EditorGUILayout.BeginHorizontal();

                // Show the path as plain non-clickable text
                EditorGUILayout.LabelField(path, GUILayout.Width(300));

                // Button: select + ping asset inside Unity Editor
                if (GUILayout.Button("Open File Location", GUILayout.Width(120)))
                {
                    var obj = AssetDatabase.LoadMainAssetAtPath(path);
                    if (obj != null)
                    {
                        Selection.activeObject = obj;
                        EditorGUIUtility.PingObject(obj);   // highlight inside Project window
                    }
                }

                EditorGUILayout.EndHorizontal();
            }
        }
        else
        {
            EditorGUILayout.LabelField("None.");
        }


        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Used By (Dependants)", EditorStyles.boldLabel);
        if (selectedAsset.directDependants != null && selectedAsset.directDependants.Count > 0)
        {
            foreach (var depGuid in selectedAsset.directDependants)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(depGuid);
                if (GUILayout.Button(path, EditorStyles.miniButton))
                {
                    var obj = UnityEditor.AssetDatabase.LoadMainAssetAtPath(path);
                    if (obj != null)
                        Selection.activeObject = obj;
                }
            }
        }
        else
        {
            EditorGUILayout.LabelField("Not referenced by other indexed assets.");
        }

        if (GUILayout.Button("Save Metadata"))
        {
            EditorUtility.SetDirty(db);
            UnityEditor.AssetDatabase.SaveAssets();
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }
    
    private static string FormatFileSize(long bytes)
    {
        if (bytes <= 0) return "0 B";

        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        int unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }
    private void RenameSelectedAsset()
    {
        if (selectedAsset == null)
            return;

        string newName = renameBuffer?.Trim();
        if (string.IsNullOrEmpty(newName) || newName == selectedAsset.assetName)
            return;

        // AssetDatabase.RenameAsset uses file name without extension
        string error = AssetDatabase.RenameAsset(selectedAsset.assetPath, newName);
        if (!string.IsNullOrEmpty(error))
        {
            EditorUtility.DisplayDialog("Rename Failed", error, "OK");
            return;
        }

        // Refresh metadata path & name from GUID
        selectedAsset.assetName = newName;
        selectedAsset.assetPath = AssetDatabase.GUIDToAssetPath(selectedAsset.guid);

        // Mark DB dirty and refresh filters so list updates
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        filtersDirty = true;
    }

    private void OpenSelectedInOS()
    {
        if (selectedAsset == null || string.IsNullOrEmpty(selectedAsset.assetPath))
            return;

        // Convert "Assets/..." to full system path
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string fullPath = Path.Combine(projectRoot, selectedAsset.assetPath);

        if (File.Exists(fullPath))
        {
            EditorUtility.OpenWithDefaultApp(fullPath);
        }
        else
        {
            EditorUtility.DisplayDialog("Open Failed",
                "File not found on disk:\n" + fullPath,
                "OK");
        }
    }

    private bool IsGameObjectAsset(AssetMetadata meta)
    {
        if (meta == null || string.IsNullOrEmpty(meta.assetPath))
            return false;

        Type t = AssetDatabase.GetMainAssetTypeAtPath(meta.assetPath);
        return t != null && typeof(GameObject).IsAssignableFrom(t);
    }

    private void AddSelectedPrefabToScene()
    {
        if (selectedAsset == null)
            return;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(selectedAsset.assetPath);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Add to Scene",
                "Selected asset is not a GameObject/Prefab.",
                "OK");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (instance == null)
            return;

        Undo.RegisterCreatedObjectUndo(instance, "Instantiate " + instance.name);

        // Place roughly at the scene view pivot, or at origin as fallback
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            instance.transform.position = sceneView.pivot;
        }
        else
        {
            instance.transform.position = Vector3.zero;
        }

        Selection.activeGameObject = instance;
    }
}
