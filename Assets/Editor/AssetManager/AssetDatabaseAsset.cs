using System.Collections.Generic;
using UnityEngine;

public class AssetDatabaseAsset : ScriptableObject
{
    public List<AssetMetadata> assets = new List<AssetMetadata>();
    
    public Dictionary<string, int> BuildIndex()
    {
        var dictionary = new Dictionary<string, int>();
        for (int i = 0; i < assets.Count; i++)
        {
            if (!string.IsNullOrEmpty(assets[i].guid))
            {
                dictionary[assets[i].guid] = i;
            }
        }
        return dictionary;
    }
}
