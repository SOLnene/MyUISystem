// [UNITY-SKILL:SPRITEATLAS]
using System;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
internal sealed class GachaTabsAtlasAddressablesBuild : IPostprocessBuildWithReport
{
    static readonly Func<AddressableAssetSettings, AddressablesPlayerBuildResult> previousBuilder;
    static bool preparedForPlayer;
    static Hash128 preparedAtlasHash;

    static GachaTabsAtlasAddressablesBuild()
    {
        previousBuilder = AddressablesPlayerBuildProcessor.BuildAddressablesOverride;
        AddressablesPlayerBuildProcessor.BuildAddressablesOverride = BuildForPlayer;
    }

    public int callbackOrder => 1000;

    static AddressablesPlayerBuildResult BuildForPlayer(AddressableAssetSettings settings)
    {
        GachaTabsAtlasPrebuild.Synchronize();
        AddressablesPlayerBuildResult result;
        if (previousBuilder != null) result = previousBuilder(settings);
        else AddressableAssetSettings.BuildPlayerContent(out result);
        preparedForPlayer = result != null && string.IsNullOrEmpty(result.Error);
        if (preparedForPlayer) preparedAtlasHash = AssetDatabase.GetAssetDependencyHash(GachaTabsAtlasPrebuild.AtlasPath);
        return result;
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        // StreamingAssets are collected before this callback. Rebuilding here would leave the Player with old bundles.
        // Native Addressables preparation invokes BuildForPlayer before collecting them; don't build a second time.
        if (!preparedForPlayer) return;
        preparedForPlayer = false;
        if (preparedAtlasHash != AssetDatabase.GetAssetDependencyHash(GachaTabsAtlasPrebuild.AtlasPath))
            throw new BuildFailedException("GachaTabs sources changed after the content build; rebuild the Player.");
    }
}
