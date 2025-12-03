using System;
using System.Collections.Generic;

[Serializable]
public class AssetMetadata
{
    public string guid;
    public string assetPath;
    public string assetName;
    public string assetType;

    public long fileSizeBytes;
    public float audioLengthSeconds;

    // Tagging & categorisation
    public List<string> tags = new List<string>();
    public string category;

    // Some Custom fields
    public string customField1Label = "Designer";
    public string customField1Value;
    public string customField2Label = "Notes";
    public string customField2Value;

    // Dependency info
    public List<string> directDependencies = new List<string>();
    public List<string> directDependants = new List<string>(); // who references this

    // Version control info
    public string vcsStatus;
    public string vcsSystem;
    
    public DateTime lastIndexed;
}