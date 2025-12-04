using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public partial class AssetManagerWindow : EditorWindow
{
    private AssetDatabaseAsset databaseAsset;
    
    private const float NameColumnWidth     = 220f;
    private const float TypeColumnWidth     = 220f;
    private const float CategoryColumnWidth = 220f;
    
    private string renameBuffer = string.Empty;
    private string searchText = "";
    private string tagFilter = "";
    private string typeFilter = "";
    private Vector2 listScroll = Vector2.zero;
    private Vector2 detailsScroll = Vector2.zero;
    private AssetMetadata selectedAsset;
    
    private HashSet<string> selectedGuids = new HashSet<string>();
    
    private string batchCategoryInput = "";
    private string batchTagInput = "";
    
    private List<AssetMetadata> filteredAssets = new List<AssetMetadata>();
    private bool filtersDirty = true;
    
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
            size /= 1024.0;
            unitIndex++;
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

        Type type = AssetDatabase.GetMainAssetTypeAtPath(meta.assetPath);
        if (type == null)
        {
            return false;
        }

        return typeof(GameObject).IsAssignableFrom(type);
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
            Color originalColor = GUI.color;
            GUI.color = new Color(0.25f, 0.35f, 0.55f, 0.25f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = originalColor;
        }
    }
}