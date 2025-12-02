using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AssetMetadata
{
    public string guid;            // Unity GUID, stable even if path changes
    public string assetPath;       // For convenience
    public string assetName;
    public string assetType;       // e.g. "Texture2D", "Prefab"

    // NEW: file info
    public long fileSizeBytes;         // raw size in bytes
    public float audioLengthSeconds;   // > 0 for audio clips only

    // Tagging & categorisation
    public List<string> tags = new List<string>();
    public string category;

    // Custom fields (simple version)
    public string customField1Label = "Designer";
    public string customField1Value;
    public string customField2Label = "Notes";
    public string customField2Value;

    // Dependency info (cached)
    public List<string> directDependencies = new List<string>();
    public List<string> directDependants = new List<string>(); // who references this

    // Version control info (simple)
    public string vcsStatus;   // "Modified", "Unversioned", "Clean", etc.
    public string vcsSystem;   // "Git", "Perforce", "None"

    // Perf / housekeeping
    public DateTime lastIndexed;
}