using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public partial class AssetManagerWindow : EditorWindow
{
    private AssetDatabaseAsset databaseAsset;

    // Column widths
    private const float NameColumnWidth     = 220f;
    private const float TypeColumnWidth     = 220f;
    private const float CategoryColumnWidth = 220f;

    // UI state
    private string renameBuffer        = string.Empty;
    private string searchText          = "";
    private string tagFilter           = "";
    private string typeFilter          = "";
    private Vector2 listScroll         = Vector2.zero;
    private Vector2 detailsScroll      = Vector2.zero;
    private AssetMetadata selectedAsset;

    // Multi-selection
    private HashSet<string> selectedGuids = new HashSet<string>();

    // Batch edit (selected assets)
    private string batchCategoryInput = "";
    private string batchTagInput      = "";

    // Cached filtered list
    private List<AssetMetadata> filteredAssets = new List<AssetMetadata>();
    private bool filtersDirty = true;

    // Pagination
    private const int RowCountPerPage = 100;
    private int currentPage = 0;

    [MenuItem("Tools/Asset Manager")]
    public static void ShowWindow()
    {
        AssetManagerWindow window = GetWindow<AssetManagerWindow>("Asset Manager");
        window.Show();
    }

    private void OnEnable()
    {
        databaseAsset = AssetDatabaseUtility.LoadOrCreateDatabase();
        filtersDirty = true;
        currentPage  = 0;
    }

    private void OnGUI()
    {
        if (databaseAsset == null)
        {
            databaseAsset = AssetDatabaseUtility.LoadOrCreateDatabase();
            filtersDirty = true;
            currentPage  = 0;
        }

        DrawToolbar();

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        DrawAssetList();
        DrawDetailsPanel();
        EditorGUILayout.EndHorizontal();
    }

    // ---------- Utility helpers shared by partials ----------

    private static string FormatFileSize(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size    = bytes;
        int unitIndex  = 0;

        while (size >= 1024.0 && unitIndex < units.Length - 1)
        {
            size     /= 1024.0;
            unitIndex = unitIndex + 1;
        }

        return string.Format("{0:0.##} {1}", size, units[unitIndex]);
    }

    private bool IsGameObjectAsset(AssetMetadata meta)
    {
        if (meta == null)
        {
            return false;
        }

        if (string.IsNullOrEmpty(meta.assetPath))
        {
            return false;
        }

        Type t = AssetDatabase.GetMainAssetTypeAtPath(meta.assetPath);
        if (t == null)
        {
            return false;
        }

        return typeof(GameObject).IsAssignableFrom(t);
    }

    private void MarkDatabaseDirtyAndSave()
    {
        if (databaseAsset != null)
        {
            EditorUtility.SetDirty(databaseAsset);
            AssetDatabase.SaveAssets();
        }
    }
    private void DrawHoverHighlight(Rect rect)
    {
        if (rect.Contains(Event.current.mousePosition))
        {
            Color old = GUI.color;
            GUI.color = new Color(0.25f, 0.35f, 0.55f, 0.25f); // light blue-ish hover
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}