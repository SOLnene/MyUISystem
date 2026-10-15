using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

internal sealed class GameStartupData
{
    internal static GameStartupData Current { get; private set; }
    internal StartupDataEntry<GachaConfigSnapshot> Gacha { get; private set; }
    readonly CloudGameSaveSession cloud;
    readonly CancellationTokenSource lifetime;
    readonly Dictionary<string, Func<UniTask>> preloaders = new Dictionary<string, Func<UniTask>>(StringComparer.Ordinal);
    bool disposed;

    GameStartupData(CloudGameSaveSession session, CancellationToken cancellationToken)
    {
        cloud = session;
        lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    }

    internal static GameStartupData StartForCloud(CloudGameSaveSession session, CancellationToken cancellationToken)
    {
        if (Current != null && !Current.disposed && ReferenceEquals(Current.cloud, session))
            return Current;
        Clear();
        var data = new GameStartupData(session, cancellationToken);
        Current = data;
        data.RegisterCloudRequests();
        // Prefetch belongs to the game session, not the login view that is about to close.
        data.PreloadAsync().Forget(error => Debug.LogWarning("Startup data prefetch failed: " + error.Message));
        return data;
    }

    internal static GameStartupData ForCloud(CloudGameSaveSession session)
    {
        var data = StartForCloud(session, CancellationToken.None);
        data.RequireCurrentSession();
        return data;
    }

    internal static void Clear()
    {
        var previous = Current;
        Current = null;
        if (previous == null) return;
        previous.disposed = true;
        previous.lifetime.Cancel();
        previous.lifetime.Dispose();
    }

    void RegisterCloudRequests()
    {
        // Register future startup data here; each entry retains its own cache and deduplicates concurrent callers.
        Gacha = Register("gacha-configuration",
            token => CloudSaveHttpClient.Default.LoadGachaConfigAsync(cloud.PlayerId, token),
            snapshot =>
            {
                // Cache DTOs rather than ScriptableObjects owned/disposed by a particular GachaService.
                using var checkedConfiguration = RemoteGachaConfiguration.Create(snapshot,
                    GameDatabase.GachaPoolUIConfigDatabase, GameDatabase.CharacterDatabase, GameDatabase.ItemDatabase);
            });
    }

    internal StartupDataEntry<T> Register<T>(string key, Func<CancellationToken, UniTask<T>> loader, Action<T> validate = null)
    {
        RequireCurrentSession();
        if (string.IsNullOrEmpty(key) || preloaders.ContainsKey(key))
            throw new ArgumentException("Startup data key is empty or already registered.", nameof(key));
        var entry = new StartupDataEntry<T>(loader, validate, lifetime.Token, RequireCurrentSession);
        preloaders.Add(key, async () => { await entry.RefreshAsync(CancellationToken.None); });
        return entry;
    }

    internal UniTask PreloadAsync() => UniTask.WhenAll(preloaders.Select(pair => PreloadEntryAsync(pair.Key, pair.Value)));

    async UniTask PreloadEntryAsync(string key, Func<UniTask> preload)
    {
        try { await preload(); }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            // Optional startup data failing must not prevent login or unrelated startup requests from completing.
            Debug.LogWarning("Startup data unavailable (" + key + "): " + error.Message);
        }
    }

    void RequireCurrentSession()
    {
        if (disposed || Current != this || CloudGameSaveSession.Current != cloud ||
            !cloud.IsReady || !AccountSession.Current.IsAuthorized || AccountSession.Current.PlayerId != cloud.PlayerId)
            throw new OperationCanceledException("Startup data belongs to an inactive account session.");
        lifetime.Token.ThrowIfCancellationRequested();
    }
}

internal sealed class StartupDataEntry<T>
{
    readonly Func<CancellationToken, UniTask<T>> loader;
    readonly Action<T> validate;
    readonly CancellationToken lifetime;
    readonly Action requireOwner;
    Task<T> inFlight;
    T cached;
    bool hasCached;

    internal StartupDataEntry(Func<CancellationToken, UniTask<T>> fetch, Action<T> validator,
        CancellationToken cancellationToken, Action ownerCheck)
    {
        loader = fetch ?? throw new ArgumentNullException(nameof(fetch));
        validate = validator;
        lifetime = cancellationToken;
        requireOwner = ownerCheck;
    }

    internal bool TryGetCached(out T value)
    {
        requireOwner?.Invoke();
        lifetime.ThrowIfCancellationRequested();
        value = cached;
        return hasCached;
    }

    internal UniTask<T> RefreshAsync(CancellationToken callerCancellation)
    {
        requireOwner?.Invoke();
        lifetime.ThrowIfCancellationRequested();
        callerCancellation.ThrowIfCancellationRequested();
        if (inFlight == null || inFlight.IsCompleted)
            inFlight = FetchAndCacheAsync().AsTask();
        // A page closing cancels only its wait; the shared session request can still warm the cache for a later page.
        return inFlight.AsUniTask().AttachExternalCancellation(callerCancellation);
    }

    async UniTask<T> FetchAndCacheAsync()
    {
        var value = await loader(lifetime);
        lifetime.ThrowIfCancellationRequested();
        requireOwner?.Invoke();
        validate?.Invoke(value);
        cached = value;
        hasCached = true;
        return value;
    }
}
