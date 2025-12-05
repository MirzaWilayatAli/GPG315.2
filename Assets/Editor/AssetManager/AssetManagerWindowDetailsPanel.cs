using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public partial class AssetManagerWindow
{
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

        DrawBasicInfoSection();
        EditorGUILayout.Space();
        DrawFileInfoSection();
        EditorGUILayout.Space();
        DrawRenameSection();
        EditorGUILayout.Space();
        DrawMetadataSection();
        EditorGUILayout.Space();
        DrawVersionControlSection();
        EditorGUILayout.Space();
        DrawDependenciesSection();
        EditorGUILayout.Space();
        DrawDependantsSection();
        EditorGUILayout.Space();

        if (GUILayout.Button("Save Metadata"))
        {
            MarkDatabaseDirtyAndSave();
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawBasicInfoSection()
    {
        EditorGUILayout.LabelField("Path", selectedAsset.assetPath);
        EditorGUILayout.LabelField("Type", selectedAsset.assetType);
        EditorGUILayout.LabelField("GUID", selectedAsset.guid);
    }

    private void DrawFileInfoSection()
    {
        EditorGUILayout.LabelField("File Info", EditorStyles.boldLabel);

        string sizeText = FormatFileSize(selectedAsset.fileSizeBytes);
        EditorGUILayout.LabelField("File Size", sizeText);

        if (selectedAsset.audioLengthSeconds > 0.01f)
        {
            TimeSpan ts = TimeSpan.FromSeconds(selectedAsset.audioLengthSeconds);
            string timeFormatted;

            if (ts.Hours > 0)
            {
                timeFormatted = ts.ToString(@"h\:mm\:ss");
            }
            else
            {
                timeFormatted = ts.ToString(@"m\:ss");
            }

            EditorGUILayout.LabelField("Audio Length", timeFormatted);
        }
        else
        {
            EditorGUILayout.LabelField("Audio Length", "N/A");
        }
    }

    private void DrawRenameSection()
    {
        EditorGUILayout.LabelField("Rename / Actions", EditorStyles.boldLabel);

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

        bool wasEnabled = GUI.enabled;
        GUI.enabled = IsGameObjectAsset(selectedAsset);

        if (GUILayout.Button("Add to Scene"))
        {
            AddSelectedPrefabToScene();
        }

        GUI.enabled = wasEnabled;

        EditorGUILayout.EndHorizontal();
    }

    private void DrawMetadataSection()
    {
        EditorGUILayout.LabelField("Metadata", EditorStyles.boldLabel);

        selectedAsset.category = EditorGUILayout.TextField("Category", selectedAsset.category);

        // Tags as comma-separated
        string tagsJoined = "";
        if (selectedAsset.tags != null && selectedAsset.tags.Count > 0)
        {
            for (int i = 0; i < selectedAsset.tags.Count; i++)
            {
                if (i > 0)
                {
                    tagsJoined += ", ";
                }
                tagsJoined += selectedAsset.tags[i];
            }
        }

        string newTags = EditorGUILayout.TextField("Tags", tagsJoined);
        if (newTags != tagsJoined)
        {
            List<string> newList = new List<string>();

            string[] parts = newTags.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (!string.IsNullOrEmpty(part))
                {
                    bool exists = false;
                    for (int j = 0; j < newList.Count; j++)
                    {
                        if (string.Equals(newList[j], part, StringComparison.OrdinalIgnoreCase))
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists)
                    {
                        newList.Add(part);
                    }
                }
            }

            selectedAsset.tags = newList;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Custom Fields", EditorStyles.boldLabel);

        selectedAsset.customField1Value = EditorGUILayout.TextField("Designer", selectedAsset.customField1Value);
        selectedAsset.customField2Value = EditorGUILayout.TextField("Notes", selectedAsset.customField2Value);
    }

    private void DrawVersionControlSection()
    {
        EditorGUILayout.LabelField("Version Control", EditorStyles.boldLabel);

        string vcsSystem = selectedAsset.vcsSystem;
        if (string.IsNullOrEmpty(vcsSystem))
        {
            vcsSystem = "Unknown";
        }

        string vcsStatus = selectedAsset.vcsStatus;
        if (string.IsNullOrEmpty(vcsStatus))
        {
            vcsStatus = "Unknown";
        }

        EditorGUILayout.LabelField("System", vcsSystem);
        EditorGUILayout.LabelField("Status", vcsStatus);
    }

    private void DrawDependenciesSection()
    {
        EditorGUILayout.LabelField("Dependencies", EditorStyles.boldLabel);

        if (selectedAsset.directDependencies != null &&
            selectedAsset.directDependencies.Count > 0)
        {
            for (int i = 0; i < selectedAsset.directDependencies.Count; i++)
            {
                string depGuid = selectedAsset.directDependencies[i];
                string path    = AssetDatabase.GUIDToAssetPath(depGuid);

                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField(path, GUILayout.Width(300));

                if (GUILayout.Button("Open File Location", GUILayout.Width(140)))
                {
                    UnityEngine.Object obj = AssetDatabase.LoadMainAssetAtPath(path);
                    if (obj != null)
                    {
                        Selection.activeObject = obj;
                        EditorGUIUtility.PingObject(obj);
                    }
                }

                EditorGUILayout.EndHorizontal();
            }
        }
        else
        {
            EditorGUILayout.LabelField("None.");
        }
    }

    private void DrawDependantsSection()
    {
        EditorGUILayout.LabelField("Used By (Dependants)", EditorStyles.boldLabel);

        if (selectedAsset.directDependants != null &&
            selectedAsset.directDependants.Count > 0)
        {
            for (int i = 0; i < selectedAsset.directDependants.Count; i++)
            {
                string depGuid = selectedAsset.directDependants[i];
                string path    = AssetDatabase.GUIDToAssetPath(depGuid);

                if (GUILayout.Button(path, EditorStyles.miniButton))
                {
                    UnityEngine.Object obj = AssetDatabase.LoadMainAssetAtPath(path);
                    if (obj != null)
                    {
                        Selection.activeObject = obj;
                    }
                }
            }
        }
        else
        {
            EditorGUILayout.LabelField("Not referenced by other indexed assets.");
        }
    }

    // Helpers methods

    private void RenameSelectedAsset()
    {
        if (selectedAsset == null)
        {
            return;
        }

        string newName = renameBuffer;
        if (newName == null)
        {
            return;
        }

        newName = newName.Trim();
        if (string.IsNullOrEmpty(newName) || newName == selectedAsset.assetName)
        {
            return;
        }

        string error = AssetDatabase.RenameAsset(selectedAsset.assetPath, newName);
        if (!string.IsNullOrEmpty(error))
        {
            EditorUtility.DisplayDialog("Rename Failed", error, "OK");
            return;
        }

        selectedAsset.assetName = newName;
        selectedAsset.assetPath = AssetDatabase.GUIDToAssetPath(selectedAsset.guid);

        MarkDatabaseDirtyAndSave();
        filtersDirty = true;
    }

    private void OpenSelectedInOS()
    {
        if (selectedAsset == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(selectedAsset.assetPath))
        {
            return;
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string fullPath = Path.Combine(projectRoot, selectedAsset.assetPath);

        if (File.Exists(fullPath))
        {
            EditorUtility.OpenWithDefaultApp(fullPath);
        }
        else
        {
            EditorUtility.DisplayDialog("Open Failed", "File not found on disk:\n" + fullPath, "OK");
        }
    }

    private void AddSelectedPrefabToScene()
    {
        if (selectedAsset == null)
        {
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(selectedAsset.assetPath);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Add to Scene", "Selected asset is not a GameObject/Prefab.", "OK");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (instance == null)
        {
            return;
        }

        Undo.RegisterCreatedObjectUndo(instance, "Instantiate " + instance.name);

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
