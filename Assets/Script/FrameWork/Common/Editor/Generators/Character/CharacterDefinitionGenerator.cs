using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Game.Domain.Character;
using UnityEditor;
using UnityEngine;

public class CharacterDefinitionGenerator
{
    bool overwrite;
    bool markAddressable;
    string addressPrefix;
    string label;
    
    Dictionary<string, CharacterDefinition> map = new();
    
    public CharacterDefinitionGenerator(
        bool overwrite = true,
        bool markAddressable = true,
        string addressPrefix = "Assets/Res/Character/",
        string label = "Character")
    {
        this.overwrite = overwrite;
        this.markAddressable = markAddressable;
        this.addressPrefix = addressPrefix;
        this.label = label;
    }
    
    public GenerateResult Generate(string spriteFolder,string outputFolder)
    {
        map.Clear();

        var generateResult = new GenerateResult();
        var builder = new CharacterDefinitionBuilder();
        foreach (var result in SpriteScanUtil.ScanFolder(spriteFolder))
        {
            if (!GachaSpriteNameParser.TryParse(result.fileName, out var key, out var type))
            {
                continue;
            }

            var def = GetOrCreate(key,outputFolder);
            
            builder.Build(def);
            MarkAddressable(result.assetPath,
                $"{addressPrefix}_{key}"
                );
        }
        foreach (var kv in map)
        {
            if (kv.Value == null)
            {
                Debug.LogWarning($"CharacterDefinitionGenerator {kv.Key} 是 null，跳过生成");
                continue;
            }
            
            var assetPath = Path.Combine(
                outputFolder,
                $"{kv.Key}.asset");

            var createResult = EditorAssetUtil.CreateOrReplaceAsset(
                kv.Value,
                assetPath,
                overwrite);
            
            generateResult.Apply(createResult);
        }
        return generateResult;
    }

    CharacterDefinition GetOrCreate(string key,string outputFolder)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("key 不能为空");
        
        var assetPath = Path.Combine(outputFolder, $"{key}.asset");

        // ① 先尝试加载已有 asset
        var existDef = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(assetPath);
        if (existDef != null)
        {
            map[key] = existDef;
            return existDef;
        }
        
        if(!map.TryGetValue(key,out var def))
        {
            def = ScriptableObject.CreateInstance<CharacterDefinition>();
            if (def == null)
                throw new Exception($"无法创建 CharacterDefinitionGenerator: {key}");
            def.key = key;
            map[key] = def;
        }
        return def;
    }
    
    void MarkAddressable(string assetPath, string address)
    {
        if (!markAddressable) return;

        AddressableEditorService.Register(
            assetPath,
            address,
            label);
    }
}
