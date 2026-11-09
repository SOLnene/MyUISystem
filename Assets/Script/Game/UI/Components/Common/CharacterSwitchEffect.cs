using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

internal static class CharacterSwitchEffect
{
    static readonly int ModelFadeId = Shader.PropertyToID("_ModelFade");
    static MaterialPropertyBlock properties;

    internal static async UniTask FadeAsync(Renderer[] renderers, float from, float to,
        float duration, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        SetFade(renderers, from);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, token);
            elapsed += Time.deltaTime;
            SetFade(renderers, Mathf.Lerp(from, to, elapsed / duration));
        }
        token.ThrowIfCancellationRequested();
        SetFade(renderers, to);
    }

    internal static void SetFade(Renderer[] renderers, float fade)
    {
        if (renderers == null) return;
        properties ??= new MaterialPropertyBlock();
        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;
            properties.Clear();
            renderer.GetPropertyBlock(properties);
            properties.SetFloat(ModelFadeId, fade);
            renderer.SetPropertyBlock(properties);
        }
    }
}
