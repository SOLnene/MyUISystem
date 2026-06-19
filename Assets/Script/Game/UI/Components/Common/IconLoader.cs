using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public static class IconLoader
{
    public static CancellationTokenSource LoadSpriteAsync(
        Image target,
        string iconPath,
        MonoBehaviour owner,
        CancellationTokenSource currentCts)
    {
        Cancel(currentCts);
        target.sprite = null;

        if (string.IsNullOrEmpty(iconPath))
        {
            return null;
        }

        var requestCts = CancellationTokenSource.CreateLinkedTokenSource(
            owner.GetCancellationTokenOnDestroy());
        LoadSpriteAsync(target, iconPath, requestCts).Forget();
        return requestCts;
    }

    public static void Cancel(CancellationTokenSource cts)
    {
        try
        {
            cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    static async UniTask LoadSpriteAsync(
        Image target,
        string iconPath,
        CancellationTokenSource requestCts)
    {
        try
        {
            Sprite sprite = await ResourceManager.Instance.LoadAssetAsync<Sprite>(
                iconPath,
                requestCts.Token);
            if (requestCts.IsCancellationRequested)
            {
                return;
            }

            if (target == null)
            {
                return;
            }

            target.sprite = sprite;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            requestCts.Dispose();
        }
    }
}
