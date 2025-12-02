using UnityEngine;
using System.IO;

public static class AssetDatabaseUtility
{
    private const string DatabasePath = "Assets/Editor/AssetManager/DatabaseAsset.asset";

    public static AssetDatabaseAsset LoadOrCreateDatabase()
    {
        AssetDatabaseAsset databaseAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<AssetDatabaseAsset>(DatabasePath);
        if (databaseAsset == null)
        {
            if (!Directory.Exists("Assets/Editor/AssetManager"))
            {
                Directory.CreateDirectory("Assets/Editor/AssetManager");
            }

            databaseAsset = ScriptableObject.CreateInstance<AssetDatabaseAsset>();
            UnityEditor.AssetDatabase.CreateAsset(databaseAsset, DatabasePath);
            UnityEditor.AssetDatabase.SaveAssets();
        }
        return databaseAsset;
    }
}