using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public readonly struct SpriteScanResult
{
    public readonly string assetPath;
    public readonly string fileName;
    public readonly Sprite sprite;

    public SpriteScanResult(string assetPath,
        string fileName,
        Sprite sprite)
    {
        this.assetPath = assetPath;
        this.fileName = fileName;
        this.sprite = sprite;
    }
}

public static class SpriteScanUtil
{
    public static IEnumerable<SpriteScanResult> ScanFolder(string spriteFolder)
    {
        if (!Directory.Exists(spriteFolder))
            yield break;

        var files = Directory.GetFiles(spriteFolder, "*.*", SearchOption.AllDirectories)
            .Where(f =>
                    f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                );

        foreach (var path in files)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (!sprite) continue;

            yield return new SpriteScanResult(
                assetPath: path,
                fileName: Path.GetFileNameWithoutExtension(path),
                sprite: sprite
                );
        }
    }
}
