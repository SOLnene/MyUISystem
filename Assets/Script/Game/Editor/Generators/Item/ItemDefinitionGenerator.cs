using System;
using System.Text;
using UnityEditor;
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
        string configPath = Path.Combine(outputFolder, ItemConfigJson.FileName);
        var items = File.Exists(configPath)
            ? ItemConfigJson.Parse(File.ReadAllText(configPath, Encoding.UTF8)).allItems
            : new List<ItemDefinition>();
        foreach (var result in SpriteScanUtil.ScanFolder(spriteFolder))
        {
            if (!ItemSpriteNameParser.TryParse(result.fileName, out var parse))
            {
                continue;
            }

            var def = ItemDefinitionFactory.Create(parse);
            
            int existingIndex = items.FindIndex(item => item.key == def.key);
            var createResult = existingIndex < 0 ? AssetCreateResult.Created
                : overwrite ? AssetCreateResult.Replaced : AssetCreateResult.Skipped;

            if (!generateResult.Apply(createResult))
            {
                continue;
            }

            if (existingIndex < 0)
                items.Add(def);
            else
            {
                // JSON updates must preserve IDs already used by inventory saves.
                def.id = items[existingIndex].id;
                items[existingIndex] = def;
            }

            if (markAddressable)
            {
                def.iconPath = addressPrefix + parse.addressKey;
                AddressableEditorService.Register(
                    result.assetPath,
                    addressPrefix + parse.addressKey,
                    label
                    );
            }
        }
        if (generateResult.Total > 0)
        {
            string json = ItemConfigJson.Serialize(items);
            Directory.CreateDirectory(outputFolder);
            File.WriteAllText(configPath, json, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(configPath.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
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

