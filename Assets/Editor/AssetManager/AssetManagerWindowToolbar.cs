using UnityEditor;
using UnityEngine;

public partial class AssetManagerWindow
{
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        // Rebuild full index
        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
        {
            AssetIndexer.RebuildIndex(false);
            filtersDirty = true;
            currentPage  = 0;

            VersionControlIntegration.RefreshVersionControlInfo(databaseAsset);
            MarkDatabaseDirtyAndSave();
        }

        GUILayout.FlexibleSpace();

        DrawSearchField();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawSearchField()
    {
        float searchHeight = 20f;
        float searchWidth = 220f;

        Rect searchRect = GUILayoutUtility.GetRect(
            searchWidth,
            searchHeight,
            GUILayout.ExpandWidth(false)
        );

        GUIStyle searchStyle = new GUIStyle(GUI.skin.textField);
        searchStyle.fontSize = 12;
        searchStyle.fixedHeight = searchHeight;
        searchStyle.padding = new RectOffset(6, 6, 4, 4);

        GUI.SetNextControlName("AssetManagerSearchField");
        string newSearch = GUI.TextField(searchRect, searchText, searchStyle);

        if (newSearch != searchText)
        {
            searchText = newSearch;
            filtersDirty = true;
            currentPage = 0;
        }

        if (string.IsNullOrEmpty(searchText) &&
            GUI.GetNameOfFocusedControl() != "AssetManagerSearchField" &&
            Event.current.type == EventType.Repaint)
        {
            GUIStyle placeholderStyle = new GUIStyle(searchStyle);
            Color c = placeholderStyle.normal.textColor;
            placeholderStyle.normal.textColor = new Color(c.r, c.g, c.b, 0.35f);

            GUI.Label(searchRect, "Search...", placeholderStyle);
        }

        // Clear button
        if (GUILayout.Button("X", EditorStyles.toolbarButton, GUILayout.Width(20)))
        {
            if (!string.IsNullOrEmpty(searchText))
            {
                searchText   = "";
                filtersDirty = true;
                currentPage  = 0;
            }

            GUI.FocusControl(null);
        }
    }
}
