using System;
using System.Threading;
using Cysharp.Threading.Tasks;

public readonly struct VersionedAssetLoadResult<T> where T : class
{
    public T Asset { get; }
    public bool IsCurrent { get; }

    internal VersionedAssetLoadResult(T asset, bool isCurrent)
    {
        Asset = asset;
        IsCurrent = isCurrent;
    }
}

public sealed class VersionedAssetLoader<T> : IDisposable where T : class
{
    CancellationTokenSource requestCancellation;
    int version;

    public int Version => version;

    public async UniTask<VersionedAssetLoadResult<T>> LoadAsync(
        string address,
        CancellationToken cancellationToken = default)
    {
        CancelCurrentRequest();

        int requestVersion = ++version;
        requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationToken requestToken = requestCancellation.Token;

        try
        {
            T asset = await ResourceManager.Instance.LoadAssetAsync<T>(address, requestToken);
            bool isCurrent = requestVersion == version &&
                             !requestToken.IsCancellationRequested &&
                             asset != null;
            return new VersionedAssetLoadResult<T>(asset, isCurrent);
        }
        catch (OperationCanceledException)
        {
            return default;
        }
    }

    public void Cancel()
    {
        ++version;
        CancelCurrentRequest();
    }

    public void Dispose()
    {
        Cancel();
    }

    void CancelCurrentRequest()
    {
        requestCancellation?.Cancel();
        requestCancellation?.Dispose();
        requestCancellation = null;
    }
}
