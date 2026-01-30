using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor.AddressableAssets;
using UnityEngine;

public class ItemDefinitionGenerator
{
    bool overwrite;
    bool markAddressable;
    string addressPrefix;
    string label;
    
    public ItemDefinitionGenerator(
        bool overwrite = true,
        bool markAddressable = true,
        string addressPrefix = "Assets/Res/Item/",
        string label = "item")
    {
        this.overwrite = overwrite;
        this.markAddressable = markAddressable;
        this.addressPrefix = addressPrefix;
        this.label = label;
    }
    
    public GenerateResult Generate(string spriteFolder,string outputFolder)
    {
        var generateResult = new GenerateResult();
        foreach (var result in SpriteScanUtil.ScanFolder(spriteFolder))
        {
            if (!ItemSpriteNameParser.TryParse(result.fileName, out var parse))
            {
                continue;
            }

            var def = ItemDefinitionFactory.Create(parse);
            
            var assetPath= Path.Combine(
                outputFolder,
                parse.category.ToString(),
                $"{parse.assetName}.asset");

            var createResult = EditorAssetUtil.CreateOrReplaceAsset(def, assetPath, overwrite);

            if (!generateResult.Apply(createResult))
            {
                continue;
            }

            if (markAddressable)
            {
                AddressableEditorService.Register(
                    result.assetPath,
                    addressPrefix + parse.addressKey,
                    label
                    );
            }
        }
        return generateResult;
    }
}

public class GenerateResult
{
    public int createdCount;
    public int replacedCount;
    public int skippedCount;

    public int Total => createdCount + replacedCount;

    public bool Apply(AssetCreateResult result)
    {
        switch (result)
        {
            case AssetCreateResult.Created:
                createdCount++;
                return true;

            case AssetCreateResult.Replaced:
                replacedCount++;
                return true;

            case AssetCreateResult.Skipped:
                skippedCount++;
                return false;
        }
        return false;
    }
}

