using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public static class IconLoader
{
    public static async UniTask SetSpriteAsync(
        Image target,
        string iconPath,
        CancellationToken cancellationToken)
    {
        target.sprite = null;
        if (string.IsNullOrEmpty(iconPath))
        {
            return;
        }

        try
        {
            Sprite sprite = await ResourceManager.Instance.LoadAssetAsync<Sprite>(
                iconPath,
                cancellationToken);
            if (cancellationToken.IsCancellationRequested)
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
    }
}
