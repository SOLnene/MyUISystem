using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class EditorAssetUtil 
{
    public static AssetCreateResult CreateOrReplaceAsset(
        ScriptableObject asset,
        string assetPath,
        bool overwrite = true)
    {
        var dir = Path.GetDirectoryName(assetPath);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var existAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
        if (existAsset != null)
        {
            if (!overwrite)
            {
                return AssetCreateResult.Skipped;
            }
            EditorUtility.CopySerialized(asset, existAsset);
            EditorUtility.SetDirty(existAsset);
            return AssetCreateResult.Replaced;
        }
        
        AssetDatabase.CreateAsset(asset,assetPath);
        return AssetCreateResult.Created;
    }
}

public enum AssetCreateResult
{
    Created,
    Replaced,
    Skipped
}