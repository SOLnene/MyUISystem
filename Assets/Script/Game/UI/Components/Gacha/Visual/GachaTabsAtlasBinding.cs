// [UNITY-SKILL:SPRITEATLAS]
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.U2D;

internal static class GachaTabsAtlasBinding
{
    const string AtlasTag = "GachaTabs";
    const string AtlasAddress = "ui/gacha/atlas/tabs";
    static readonly List<Action<SpriteAtlas>> pendingBindings = new();
    static bool loading;
    static int generation;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        ++generation;
        loading = false;
        pendingBindings.Clear();
        SpriteAtlasManager.atlasRequested -= RequestAtlas;
        Application.quitting -= Reset;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        SpriteAtlasManager.atlasRequested -= RequestAtlas;
        SpriteAtlasManager.atlasRequested += RequestAtlas;
        Application.quitting -= Reset;
        Application.quitting += Reset;
    }

    static void RequestAtlas(string tag, Action<SpriteAtlas> bind)
    {
        if (tag != AtlasTag) return;
        pendingBindings.Add(bind);
        if (loading) return;
        loading = true;
        LoadAndBind(generation).Forget();
    }

    static async UniTask LoadAndBind(int requestGeneration)
    {
        SpriteAtlas atlas;
        try
        {
            // ResourceManager owns the shared handle; don't retain an atlas after its session cache is released.
            atlas = await ResourceManager.Instance.LoadAssetAsync<SpriteAtlas>(AtlasAddress);
            if (atlas == null) throw new InvalidOperationException("GachaTabs atlas failed to load.");
        }
        catch (Exception exception)
        {
            if (requestGeneration != generation) return;
            loading = false;
            pendingBindings.Clear();
            Debug.LogException(exception);
            return;
        }

        // An earlier Play session must not bind into a later one when domain reload is disabled.
        if (requestGeneration != generation) return;
        var callbacks = pendingBindings.ToArray();
        pendingBindings.Clear();
        loading = false;
        foreach (var callback in callbacks) callback(atlas);
    }
}
