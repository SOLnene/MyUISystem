using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ModelPreviewController : MonoBehaviour
{
    [SerializeField]
    Transform characterRoot;

    [SerializeField]
    Transform equipRoot;

    [SerializeField]
    ModelPreviewDatabase modelPreviewDatabase;

    [SerializeField]
    CharacterPreviewAnimator characterPreviewAnimator;

    [SerializeField]
    FaceController faceController;

    // 描述一次预览请求从后台加载到接管画面的完整生命周期。
    enum PreviewPreparationState
    {
        Loading,
        Ready,
        Cancelled,
        Failed,
        Activated
    }

    // 当前实际显示的内容独立于后台准备状态，加载期间不会提前隐藏现有模型。
    enum ActivePreviewState
    {
        DefaultCharacter,
        Character,
        Equip
    }

    // 将一次加载请求的身份、资源、取消和完成信号收束在同一个状态对象中。
    sealed class PreviewPreparation
    {
        public readonly ModelPreviewType PreviewType;
        public readonly string TargetKey;
        public readonly ModelPreviewDefinition Definition;
        public readonly CancellationTokenSource Cancellation = new();
        public readonly UniTaskCompletionSource<ModelPreviewDefinition> Completion = new();

        public PreviewPreparationState State = PreviewPreparationState.Loading;
        public GameObject PreviewObject;

        public PreviewPreparation(
            ModelPreviewType previewType,
            string targetKey,
            ModelPreviewDefinition definition)
        {
            PreviewType = previewType;
            TargetKey = targetKey;
            Definition = definition;
        }

        public bool Matches(ModelPreviewType previewType, string targetKey)
        {
            return PreviewType == previewType
                   && string.Equals(TargetKey, targetKey, StringComparison.Ordinal);
        }
    }

    PreviewPreparation pendingPreview;
    GameObject activePreviewObject;
    ActivePreviewState activePreviewState = ActivePreviewState.DefaultCharacter;

    public bool IsCharacterPreviewActive =>
        activePreviewState != ActivePreviewState.Equip
        && characterRoot.gameObject.activeInHierarchy;

    public async UniTask<ModelPreviewDefinition> ShowAsync(
        ModelPreviewType previewType,
        string targetKey)
    {
        // 普通显示同样走“准备后激活”，避免维护另一套加载和回收流程。
        ModelPreviewDefinition definition = await EnsurePreloadedAsync(
            previewType,
            targetKey,
            CancellationToken.None);
        return definition != null
               && TryActivatePreloaded(previewType, targetKey, out _)
            ? definition
            : null;
    }

    internal void Preload(
        ModelPreviewType previewType,
        string targetKey)
    {
        // 同一目标只保留一个准备任务；重复请求复用正在加载或已经就绪的结果。
        if (pendingPreview != null
            && pendingPreview.Matches(previewType, targetKey))
        {
            return;
        }

        ModelPreviewDefinition definition = modelPreviewDatabase.Get(previewType, targetKey);
        if (definition == null)
        {
            Debug.LogWarning(
                $"Model preview definition not found: {previewType}/{targetKey}",
                this);
            return;
        }

        CancelPendingLoad();

        pendingPreview = new PreviewPreparation(previewType, targetKey, definition);
        LoadPreviewAsync(pendingPreview).Forget(Debug.LogException);
    }

    internal async UniTask<ModelPreviewDefinition> EnsurePreloadedAsync(
        ModelPreviewType previewType,
        string targetKey,
        CancellationToken cancellationToken)
    {
        // 调用方可以直接等待；目标尚未开始准备时会在这里自动启动。
        Preload(previewType, targetKey);
        PreviewPreparation preparation = pendingPreview;
        if (preparation == null
            || !preparation.Matches(previewType, targetKey))
        {
            return null;
        }

        await preparation.Completion.Task.AttachExternalCancellation(cancellationToken);
        return pendingPreview == preparation
               && preparation.State == PreviewPreparationState.Ready
            ? preparation.Definition
            : null;
    }

    internal bool TryActivatePreloaded(
        ModelPreviewType previewType,
        string targetKey,
        out ModelPreviewDefinition definition)
    {
        // 只有 Ready 状态允许切换 Root，保证旧模型一直显示到新模型可用。
        definition = null;
        PreviewPreparation preparation = pendingPreview;
        if (preparation == null
            || preparation.State != PreviewPreparationState.Ready
            || !preparation.Matches(previewType, targetKey))
        {
            return false;
        }

        pendingPreview = null;
        preparation.State = PreviewPreparationState.Activated;
        definition = preparation.Definition;
        GameObject previewObject = preparation.PreviewObject;
        preparation.PreviewObject = null;
        ActivatePreview(previewType, previewObject);
        return true;
    }

    async UniTask LoadPreviewAsync(PreviewPreparation preparation)
    {
        // 资源加载是唯一异步边界；配置完成前，实例始终保持隐藏。
        Transform previewRoot = preparation.PreviewType == ModelPreviewType.Character
            ? characterRoot
            : equipRoot;
        GameObject previewObject = null;

        try
        {
            previewObject = await ResourceManager.Instance.InstantiateItemAsync(
                preparation.Definition.ModelAddress,
                previewRoot,
                false,
                preparation.Cancellation.Token);
            if (previewObject == null
                || pendingPreview != preparation
                || preparation.Cancellation.IsCancellationRequested)
            {
                return;
            }

            ConfigurePreview(previewObject, preparation.Definition);
            preparation.PreviewObject = previewObject;
            preparation.State = PreviewPreparationState.Ready;
            previewObject = null;
            preparation.Cancellation.Dispose();
            preparation.Completion.TrySetResult(preparation.Definition);
        }
        catch (OperationCanceledException)
            when (preparation.Cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            if (previewObject != null)
            {
                ResourceManager.Instance.Recycle(previewObject);
            }

            if (preparation.State == PreviewPreparationState.Loading)
            {
                preparation.State = PreviewPreparationState.Failed;
            }

            if (preparation.State == PreviewPreparationState.Failed
                || preparation.State == PreviewPreparationState.Cancelled)
            {
                if (pendingPreview == preparation)
                {
                    pendingPreview = null;
                }

                preparation.Completion.TrySetResult(null);
                preparation.Cancellation.Dispose();
            }
        }
    }

    public void ShowDefaultCharacter()
    {
        CancelPendingLoad();
        ReleaseActivePreview();

        equipRoot.gameObject.SetActive(false);
        characterRoot.gameObject.SetActive(true);
        SetDirectChildrenActive(characterRoot, true);
        activePreviewState = ActivePreviewState.DefaultCharacter;
        BindFirstCharacter();
    }

    public void ApplyAnimationPreset(
        CameraPreset preset,
        bool immediate,
        Action onCompleted)
    {
        if (!IsCharacterPreviewActive)
        {
            onCompleted?.Invoke();
            return;
        }

        if (immediate)
        {
            characterPreviewAnimator.ApplyPresetImmediate(preset, onCompleted);
        }
        else
        {
            characterPreviewAnimator.ApplyPreset(preset, onCompleted);
        }
    }

    public void ApplyFacePreset(FaceExpressionPreset preset)
    {
        if (IsCharacterPreviewActive)
        {
            faceController.ApplyFacePreset(preset);
        }
    }

    public void CancelPendingLoad()
    {
        // Ready 对象直接回收；Loading 对象由加载任务在收到取消后统一回收。
        PreviewPreparation preparation = pendingPreview;
        pendingPreview = null;
        if (preparation == null)
        {
            return;
        }

        preparation.State = PreviewPreparationState.Cancelled;
        preparation.Completion.TrySetResult(null);
        if (preparation.PreviewObject != null)
        {
            ResourceManager.Instance.Recycle(preparation.PreviewObject);
            preparation.PreviewObject = null;
            return;
        }

        preparation.Cancellation.Cancel();
    }

    void ConfigurePreview(
        GameObject previewObject,
        ModelPreviewDefinition definition)
    {
        Transform previewTransform = previewObject.transform;
        previewTransform.localPosition = definition.LocalPosition;
        previewTransform.localRotation = Quaternion.Euler(definition.LocalEulerAngles);
        previewTransform.localScale = definition.LocalScale;
        SetLayerRecursively(previewObject, LayerMask.NameToLayer("ModelDisplay"));
    }

    void ActivatePreview(ModelPreviewType previewType, GameObject previewObject)
    {
        // 所有 Root 切换、人物绑定和旧对象回收都集中在这一处完成。
        GameObject previousPreviewObject = activePreviewObject;
        Transform previewRoot = previewType == ModelPreviewType.Character
            ? characterRoot
            : equipRoot;

        characterRoot.gameObject.SetActive(previewType == ModelPreviewType.Character);
        equipRoot.gameObject.SetActive(previewType == ModelPreviewType.Equip);
        SetDirectChildrenActive(previewRoot, false);

        activePreviewObject = previewObject;
        activePreviewState = previewType == ModelPreviewType.Character
            ? ActivePreviewState.Character
            : ActivePreviewState.Equip;
        previewObject.SetActive(true);

        if (previewType == ModelPreviewType.Character)
        {
            BindCharacter(previewObject);
        }
        else
        {
            characterPreviewAnimator.Unbind();
            faceController.Unbind();
        }

        if (previousPreviewObject != null)
        {
            ResourceManager.Instance.Recycle(previousPreviewObject);
        }
    }

    void BindFirstCharacter()
    {
        for (int i = 0; i < characterRoot.childCount; i++)
        {
            GameObject child = characterRoot.GetChild(i).gameObject;
            if (child.activeSelf)
            {
                BindCharacter(child);
                return;
            }
        }

        characterPreviewAnimator.Unbind();
        faceController.Unbind();
    }

    void BindCharacter(GameObject previewObject)
    {
        CharacterPreviewActor actor =
            previewObject.GetComponentInChildren<CharacterPreviewActor>(true);
        if (actor != null)
        {
            characterPreviewAnimator.Bind(actor.Animator);
            faceController.Bind(actor.FaceRenderers);
            return;
        }

        Debug.LogWarning(
            $"CharacterPreviewActor is missing on preview prefab: {previewObject.name}",
            previewObject);
        characterPreviewAnimator.Bind(
            previewObject.GetComponentInChildren<Animator>(true));
        faceController.Bind(
            previewObject.GetComponentsInChildren<SkinnedMeshRenderer>(true));
    }

    void ReleaseActivePreview()
    {
        if (activePreviewObject == null)
        {
            return;
        }

        ResourceManager.Instance.Recycle(activePreviewObject);
        activePreviewObject = null;
    }

    static void SetDirectChildrenActive(Transform root, bool active)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            root.GetChild(i).gameObject.SetActive(active);
        }
    }

    static void SetLayerRecursively(GameObject root, int layer)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.layer = layer;
        }
    }

    void OnDestroy()
    {
        CancelPendingLoad();
    }
}
