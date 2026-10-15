// [UNITY-SKILL:SPRITEATLAS]
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;
using Object = UnityEngine.Object;

internal sealed class GachaTabsAtlasPrebuild : IPreprocessBuildWithReport
{
    internal const string AtlasPath = "Assets/Art/Sprite/Gacha/Tab/GachaTabs.spriteatlasv2";
    internal const string AtlasAddress = "ui/gacha/atlas/tabs";
    internal const string DatabasePath = "Assets/GameData/Database/GachaPoolUIConfigDatabase.asset";
    internal const string TabPrefabPath = "Assets/AssetsPackage/UI/Prefab/Gacha/GachaPoolTabBtn.prefab";
    static bool synchronizationQueued;

    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report)
    {
        Synchronize();
    }

    [InitializeOnLoadMethod]
    internal static void QueueSynchronization()
    {
        if (synchronizationQueued) return;
        synchronizationQueued = true;
        EditorApplication.delayCall += SynchronizeWhenReady;
    }

    static void SynchronizeWhenReady()
    {
        synchronizationQueued = false;
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            QueueSynchronization();
            return;
        }

        try { Synchronize(); }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    internal static void Synchronize()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var group = settings.FindGroup("GachaUI");
        if (group == null) throw new BuildFailedException("GachaUI Addressables group is missing.");
        var database = AssetDatabase.LoadAssetAtPath<GachaPoolUIConfigDatabase>(DatabasePath);
        var entries = settings.groups.Where(g => g != null).SelectMany(g => g.entries).ToArray();
        var paths = new HashSet<string>(StringComparer.Ordinal);

        foreach (var config in database.Configs)
        {
            // Configured candidates belong here even while closed; the server still controls visibility.
            if (!config.tabIconAddress.StartsWith("ui/gacha/tab/", StringComparison.Ordinal))
                throw new BuildFailedException("Unexpected gacha tab address: " + config.tabIconAddress);
            var matches = entries.Where(e => e.address == config.tabIconAddress).ToArray();
            if (matches.Length != 1)
                throw new BuildFailedException("Gacha tab address must resolve once: " + config.tabIconAddress);
            paths.Add(matches[0].AssetPath);
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TabPrefabPath);
        var serializedTab = new SerializedObject(prefab.GetComponent<GachaPoolTabView>());
        foreach (var propertyName in new[] { "selectBg", "normalBg" })
        {
            var image = (Image)serializedTab.FindProperty(propertyName).objectReferenceValue;
            paths.Add(AssetDatabase.GetAssetPath(image.sprite));
        }

        var orderedPaths = paths.OrderBy(p => p, StringComparer.Ordinal).ToArray();
        var sprites = orderedPaths.Select(path =>
        {
            if (!path.StartsWith("Assets/Art/Sprite/", StringComparison.Ordinal))
                throw new BuildFailedException("Only project art can enter GachaTabs: " + path);
            var sourceImporter = (TextureImporter)AssetImporter.GetAtPath(path);
            if (sourceImporter.textureCompression != TextureImporterCompression.Uncompressed)
                throw new BuildFailedException("Set gacha atlas source compression to Uncompressed: " + path);
            return (Object)(AssetDatabase.LoadAssetAtPath<Sprite>(path)
                ?? throw new BuildFailedException("Gacha tab Sprite is missing: " + path));
        }).ToArray();

        bool changed = EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2;
        if (changed) EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
        if (EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2)
            throw new BuildFailedException("Sprite Atlas V2 packing could not be enabled.");

        var runtimeAtlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath);
        var currentPackables = runtimeAtlas == null ? Array.Empty<Object>() : runtimeAtlas.GetPackables();
        var currentPaths = currentPackables.Select(AssetDatabase.GetAssetPath).OrderBy(p => p, StringComparer.Ordinal);
        if (runtimeAtlas == null || !currentPaths.SequenceEqual(orderedPaths, StringComparer.Ordinal))
        {
            var atlasAsset = runtimeAtlas == null ? new SpriteAtlasAsset() : SpriteAtlasAsset.Load(AtlasPath);
            atlasAsset.name = "GachaTabs";
            atlasAsset.Remove(currentPackables);
            atlasAsset.Add(sprites);
            SpriteAtlasAsset.Save(atlasAsset, AtlasPath);
            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport);
            changed = true;
        }

        var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(AtlasPath);
        var packing = importer.packingSettings;
        var texture = importer.textureSettings;
        var platform = importer.GetPlatformSettings("DefaultTexturePlatform");
        if (importer.includeInBuild || packing.enableRotation || packing.enableTightPacking || packing.padding != 4 ||
            texture.generateMipMaps || texture.readable || texture.filterMode != FilterMode.Bilinear ||
            platform.maxTextureSize != 1024)
        {
            // UGUI images and sliced backgrounds keep rectangular, unrotated UVs.
            packing.enableRotation = false;
            packing.enableTightPacking = false;
            packing.padding = 4;
            texture.generateMipMaps = false;
            texture.readable = false;
            texture.filterMode = FilterMode.Bilinear;
            platform.maxTextureSize = 1024;
            importer.packingSettings = packing;
            importer.textureSettings = texture;
            importer.SetPlatformSettings(platform);
            importer.includeInBuild = false;
            importer.SaveAndReimport();
            changed = true;
        }

        string atlasGuid = AssetDatabase.AssetPathToGUID(AtlasPath);
        if (entries.Any(e => e.address == AtlasAddress && e.guid != atlasGuid))
            throw new BuildFailedException("GachaTabs atlas address is already used by another asset.");
        var atlasEntry = settings.FindAssetEntry(atlasGuid);
        if (atlasEntry == null || atlasEntry.parentGroup != group)
        {
            atlasEntry = settings.CreateOrMoveEntry(atlasGuid, group);
            changed = true;
        }
        if (atlasEntry.address != AtlasAddress)
        {
            atlasEntry.SetAddress(AtlasAddress);
            changed = true;
        }
        if (changed) AssetDatabase.SaveAssets();
    }
}

internal sealed class GachaTabsAtlasSourceWatcher : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        // Keep standalone Addressables builds current too, without scanning an entire art folder into the atlas.
        if (imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(path =>
            path == GachaTabsAtlasPrebuild.DatabasePath || path == GachaTabsAtlasPrebuild.TabPrefabPath ||
            path.StartsWith("Assets/GameData/Gacha/", StringComparison.Ordinal) ||
            path.StartsWith("Assets/AddressableAssetsData/AssetGroups/", StringComparison.Ordinal)))
            GachaTabsAtlasPrebuild.QueueSynchronization();
    }
}
