using Cysharp.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ModelPreviewTestBootstrap : MonoBehaviour
{
    public bool IsReady { get; private set; }

    void Awake()
    {
        ResourceManager.Instance.Init();
        InitializeAsync().Forget(Debug.LogException);
    }

    async UniTask InitializeAsync()
    {
        await ResourceManager.Instance.InitAsync();
        IsReady = true;
    }
}
