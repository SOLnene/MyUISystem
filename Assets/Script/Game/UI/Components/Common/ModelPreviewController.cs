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
    ParticleSystem characterSwitchParticles;

    [SerializeField]
    CharacterSwitchElementPalette characterSwitchElementPalette;

    [SerializeField]
    ModelPreviewDatabase modelPreviewDatabase;

    [SerializeField]
    CharacterPresentationController characterPresentation;

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
    GameObject suspendedCharacterPreviewObject;
    ModelPreviewDefinition activePreviewDefinition;
    ModelPreviewDefinition suspendedCharacterPreviewDefinition;
    ActivePreviewState activePreviewState = ActivePreviewState.DefaultCharacter;
    bool preserveCharacterOnNextEquipActivation;
    bool hasLoadFailed;
    string weaponCharacterKey;
    string characterWeaponKey;

    [SerializeField, Min(0f)]
    float characterSwitchFadeDuration = 0.15f;
    [SerializeField, Min(0f)]
    float characterSwitchFadeOutDuration = 0.06f;
    [SerializeField, Min(0f)]
    float characterSwitchEffectLeadTime = 0.2f;

    static readonly int ModelFadeId = Shader.PropertyToID("_ModelFade");
    CancellationTokenSource characterSwitchCancellation;
    Renderer[] fadingCharacterRenderers;
    MaterialPropertyBlock characterFadePropertyBlock;

    public bool IsCharacterPreviewActive =>
        activePreviewState != ActivePreviewState.Equip
        && characterRoot.gameObject.activeInHierarchy;
    public ModelPreviewDefinition ActivePreviewDefinition => activePreviewDefinition;
    public bool IsLoading =>
        pendingPreview != null
        && pendingPreview.State == PreviewPreparationState.Loading;
    public bool HasLoadFailed => hasLoadFailed;
    internal bool HasSuspendedCharacter => suspendedCharacterPreviewObject != null;

    internal void SetCharacterWeapon(string characterKey, string weaponKey)
    {
        weaponCharacterKey = characterKey;
        characterWeaponKey = weaponKey;
        RefreshCharacterWeapon();
    }

    void RefreshCharacterWeapon()
    {
        CharacterPreviewActor actor = characterPresentation.Actor;
        if (actor == null || !actor.TryGetComponent(out CharacterWeaponAttachment attachment))
        {
            return;
        }

        ModelPreviewDefinition weaponDefinition = null;
        if (activePreviewDefinition != null
            && activePreviewDefinition.TargetKey == weaponCharacterKey
            && !string.IsNullOrEmpty(characterWeaponKey))
        {
            weaponDefinition = modelPreviewDatabase.Get(ModelPreviewType.Equip, characterWeaponKey);
            // Standalone preview fallback must not put a different weapon into the character's hand.
            if (weaponDefinition != null && weaponDefinition.TargetKey != characterWeaponKey)
            {
                Debug.LogWarning($"Weapon model not configured: {characterWeaponKey}", actor);
                weaponDefinition = null;
            }
        }

        attachment.ShowWeapon(weaponDefinition, characterPresentation);
    }

    internal bool CanShowCharacter(string targetKey)
    {
        if (string.IsNullOrEmpty(targetKey))
            return false;

        ModelPreviewDefinition definition = modelPreviewDatabase.Get(ModelPreviewType.Character, targetKey);
        return definition != null && !string.IsNullOrEmpty(definition.ModelAddress);
    }

    public async UniTask<ModelPreviewDefinition> ShowAsync(
        ModelPreviewType previewType,
        string targetKey)
    {
        CancelCharacterSwitch();
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

    public async UniTask<ModelPreviewDefinition> ShowAsync(
        ModelPreviewDefinition definition)
    {
        if (definition == null)
        {
            return null;
        }

        CancelCharacterSwitch();
        Preload(definition);
        ModelPreviewDefinition preparedDefinition =
            await WaitForPreloadedAsync(
                definition.PreviewType,
                definition.TargetKey,
                CancellationToken.None);
        return preparedDefinition != null
               && TryActivatePreloaded(
                   definition.PreviewType,
                   definition.TargetKey,
                   out _)
            ? preparedDefinition
            : null;
    }

    internal async UniTask<ModelPreviewDefinition> ShowCharacterWithDitherAsync(
        string targetKey,
        Action<ModelPreviewDefinition> onActivated)
    {
        CancelCharacterSwitch();
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());
        characterSwitchCancellation = cancellation;
        try
        {
            ModelPreviewDefinition definition = await EnsurePreloadedAsync(
                ModelPreviewType.Character, targetKey, cancellation.Token);
            if (definition == null)
            {
                return null;
            }

            PreviewPreparation preparation = pendingPreview;
            string element = GameDatabase.CharacterDatabase?.Get(targetKey)?.element;
            CharacterSwitchParticlePlayback.Play(
                characterSwitchParticles, characterSwitchElementPalette.Resolve(element));
            bool replacingCharacter = activePreviewState == ActivePreviewState.Character
                                      && activePreviewObject != null;
            if (replacingCharacter)
            {
                fadingCharacterRenderers = activePreviewObject.GetComponentsInChildren<Renderer>(true);
                await FadeCharacterModelAsync(
                    fadingCharacterRenderers, 1f, 0f, characterSwitchFadeOutDuration, cancellation.Token);
            }

            // 粒子领先时间包含旧模型退场，避免把两个等待段重复累加。
            float leadTime = Mathf.Max(0f, characterSwitchEffectLeadTime
                - (replacingCharacter ? characterSwitchFadeOutDuration : 0f));
            await UniTask.Delay(TimeSpan.FromSeconds(leadTime), cancellationToken: cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (pendingPreview != preparation || preparation.State != PreviewPreparationState.Ready)
            {
                return null;
            }

            Renderer[] preparedRenderers = preparation.PreviewObject.GetComponentsInChildren<Renderer>(true);
            SetCharacterModelFade(preparedRenderers, 0f);
            // 旧模型在同一帧内隐藏并回收，先恢复池对象的材质状态。
            SetCharacterModelFade(fadingCharacterRenderers, 1f);
            fadingCharacterRenderers = preparedRenderers;
            if (!TryActivatePreloaded(ModelPreviewType.Character, targetKey, out _))
            {
                return null;
            }

            // 相机与 Default 动作在激活帧提交，模型显隐不延迟动作复位。
            cancellation.Token.ThrowIfCancellationRequested();
            onActivated(definition);
            cancellation.Token.ThrowIfCancellationRequested();
            // 粒子属于展示容器，旧模型回收后仍可播完尾段。
            await FadeCharacterModelAsync(
                preparedRenderers, 0f, 1f, characterSwitchFadeDuration, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            return definition;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return null;
        }
        finally
        {
            // 过期任务不得恢复或清理后续切换正在使用的模型。
            if (ReferenceEquals(characterSwitchCancellation, cancellation))
            {
                SetCharacterModelFade(fadingCharacterRenderers, 1f);
                fadingCharacterRenderers = null;
                characterSwitchCancellation = null;
            }
            cancellation.Dispose();
        }
    }

    internal void CancelCharacterSwitch()
    {
        characterSwitchParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        CancellationTokenSource cancellation = characterSwitchCancellation;
        characterSwitchCancellation = null;
        cancellation?.Cancel();
        SetCharacterModelFade(fadingCharacterRenderers, 1f);
        fadingCharacterRenderers = null;
    }

    async UniTask FadeCharacterModelAsync(
        Renderer[] renderers,
        float from,
        float to,
        float duration,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SetCharacterModelFade(renderers, from);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            elapsed += Time.deltaTime;
            SetCharacterModelFade(renderers, Mathf.Lerp(from, to, elapsed / duration));
        }
        cancellationToken.ThrowIfCancellationRequested();
        SetCharacterModelFade(renderers, to);
    }

    void SetCharacterModelFade(Renderer[] renderers, float fade)
    {
        if (renderers == null)
        {
            return;
        }

        characterFadePropertyBlock ??= new MaterialPropertyBlock();
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }
            characterFadePropertyBlock.Clear();
            renderer.GetPropertyBlock(characterFadePropertyBlock);
            characterFadePropertyBlock.SetFloat(ModelFadeId, fade);
            renderer.SetPropertyBlock(characterFadePropertyBlock);
        }
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
            CancelPendingLoad();
            hasLoadFailed = true;
            Debug.LogWarning(
                $"Model preview definition not found: {previewType}/{targetKey}",
                this);
            return;
        }

        // A fallback selects the resource; activation still belongs to the requested weapon.
        Preload(definition, targetKey);
    }

    void Preload(ModelPreviewDefinition definition, string targetKey = null)
    {
        if (definition == null)
        {
            return;
        }

        CancelPendingLoad();
        hasLoadFailed = false;

        pendingPreview = new PreviewPreparation(
            definition.PreviewType,
            targetKey ?? definition.TargetKey,
            definition);
        LoadPreviewAsync(pendingPreview).Forget(Debug.LogException);
    }

    internal async UniTask<ModelPreviewDefinition> EnsurePreloadedAsync(
        ModelPreviewType previewType,
        string targetKey,
        CancellationToken cancellationToken)
    {
        // 调用方可以直接等待；目标尚未开始准备时会在这里自动启动。
        Preload(previewType, targetKey);
        return await WaitForPreloadedAsync(
            previewType,
            targetKey,
            cancellationToken);
    }

    async UniTask<ModelPreviewDefinition> WaitForPreloadedAsync(
        ModelPreviewType previewType,
        string targetKey,
        CancellationToken cancellationToken)
    {
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
        ActivatePreview(previewType, previewObject, definition);
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
            hasLoadFailed = false;
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
                if (preparation.State == PreviewPreparationState.Failed
                    && pendingPreview == preparation)
                {
                    hasLoadFailed = true;
                }

                if (pendingPreview == preparation)
                {
                    pendingPreview = null;
                }

                preparation.Completion.TrySetResult(null);
                preparation.Cancellation.Dispose();
            }
        }
    }

    internal void ClearPreview()
    {
        CancelCharacterSwitch();
        CancelPendingLoad();
        preserveCharacterOnNextEquipActivation = false;
        ReleaseActivePreview();
        ReleaseSuspendedCharacter();
        characterPresentation.Unbind();
        characterRoot.gameObject.SetActive(false);
        equipRoot.gameObject.SetActive(false);
        hasLoadFailed = false;
    }

    public void ShowDefaultCharacter()
    {
        CancelCharacterSwitch();
        CancelPendingLoad();
        ReleaseActivePreview();
        ReleaseSuspendedCharacter();

        equipRoot.gameObject.SetActive(false);
        characterRoot.gameObject.SetActive(true);
        SetDirectChildrenActive(characterRoot, true);
        activePreviewState = ActivePreviewState.DefaultCharacter;
        hasLoadFailed = false;
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

        if (characterPresentation.Profile != null
            && TryGetPresentationState(preset, out CharacterPresentationState state))
        {
            ApplyPresentationStateAsync(state, immediate, onCompleted)
                .Forget(Debug.LogException);
            return;
        }

        if (immediate)
        {
            characterPresentation.PlayImmediate(preset.animationClip);
            onCompleted?.Invoke();
        }
        else
        {
            characterPresentation.CrossFadeTo(
                preset.animationClip,
                preset.crossFadeDuration,
                onCompleted);
        }
    }

    async UniTask ApplyPresentationStateAsync(
        CharacterPresentationState state,
        bool immediate,
        Action onCompleted)
    {
        try
        {
            await characterPresentation.SwitchAsync(
                state,
                immediate
                    ? PresentationTransitionMode.Immediate
                    : PresentationTransitionMode.AuthoredTimedBlend);
        }
        finally
        {
            onCompleted?.Invoke();
        }
    }

    static bool TryGetPresentationState(
        CameraPreset preset,
        out CharacterPresentationState state)
    {
        switch (preset.key)
        {
            case "Default":
            case "Detail":
                state = CharacterPresentationState.Default;
                return true;
            case "Equip":
                state = CharacterPresentationState.WeaponShow;
                return true;
            case "Relic":
                state = CharacterPresentationState.RelicShow;
                return true;
            default:
                state = CharacterPresentationState.Default;
                return false;
        }
    }

    public void ApplyFacePreset(FaceExpressionPreset preset)
    {
        if (IsCharacterPreviewActive && characterPresentation.Profile == null)
        {
            characterPresentation.ApplyFacePreset(preset);
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

    internal void PreserveActiveCharacterForEquipReturn()
    {
        preserveCharacterOnNextEquipActivation =
            activePreviewState == ActivePreviewState.Character
            && activePreviewObject != null;
    }

    internal void CancelCharacterPreservation()
    {
        preserveCharacterOnNextEquipActivation = false;
    }

    internal bool TryRestoreSuspendedCharacter()
    {
        if (suspendedCharacterPreviewObject == null)
        {
            return false;
        }

        CancelCharacterSwitch();
        GameObject equipPreviewObject = activePreviewObject;
        GameObject characterPreviewObject = suspendedCharacterPreviewObject;
        ModelPreviewDefinition definition = suspendedCharacterPreviewDefinition;
        suspendedCharacterPreviewObject = null;
        suspendedCharacterPreviewDefinition = null;

        equipRoot.gameObject.SetActive(false);
        characterRoot.gameObject.SetActive(true);
        SetDirectChildrenActive(characterRoot, false);

        activePreviewObject = characterPreviewObject;
        activePreviewDefinition = definition;
        activePreviewState = ActivePreviewState.Character;
        characterPreviewObject.SetActive(true);
        BindCharacter(characterPreviewObject, definition?.PresentationProfile);

        if (equipPreviewObject != null)
        {
            ResourceManager.Instance.Recycle(equipPreviewObject);
        }

        return true;
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

    void ActivatePreview(
        ModelPreviewType previewType,
        GameObject previewObject,
        ModelPreviewDefinition definition)
    {
        // 所有 Root 切换、人物绑定和旧对象回收都集中在这一处完成。
        GameObject previousPreviewObject = activePreviewObject;
        bool suspendCharacter =
            previewType == ModelPreviewType.Equip
            && preserveCharacterOnNextEquipActivation
            && activePreviewState == ActivePreviewState.Character
            && previousPreviewObject != null;
        preserveCharacterOnNextEquipActivation = false;
        if (suspendCharacter)
        {
            ReleaseSuspendedCharacter();
            suspendedCharacterPreviewObject = previousPreviewObject;
            suspendedCharacterPreviewDefinition = activePreviewDefinition;
            previousPreviewObject = null;
        }
        else if (previewType == ModelPreviewType.Character)
        {
            ReleaseSuspendedCharacter();
        }

        Transform previewRoot = previewType == ModelPreviewType.Character
            ? characterRoot
            : equipRoot;

        characterRoot.gameObject.SetActive(previewType == ModelPreviewType.Character);
        equipRoot.gameObject.SetActive(previewType == ModelPreviewType.Equip);
        SetDirectChildrenActive(previewRoot, false);

        activePreviewObject = previewObject;
        activePreviewDefinition = definition;
        activePreviewState = previewType == ModelPreviewType.Character
            ? ActivePreviewState.Character
            : ActivePreviewState.Equip;
        previewObject.SetActive(true);

        if (previewType == ModelPreviewType.Character)
        {
            BindCharacter(previewObject, definition.PresentationProfile);
        }
        else
        {
            characterPresentation.Unbind();
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

        characterPresentation.Unbind();
    }

    void BindCharacter(
        GameObject previewObject,
        CharacterPresentationProfile presentationProfile = null)
    {
        CharacterPreviewActor actor =
            previewObject.GetComponentInChildren<CharacterPreviewActor>(true);
        if (actor != null)
        {
            if (presentationProfile != null)
            {
                characterPresentation.Bind(actor, presentationProfile);
            }
            else
            {
                characterPresentation.SetProfile(null);
                characterPresentation.Bind(actor);
            }
            RefreshCharacterWeapon();
            return;
        }

        Debug.LogWarning(
            $"CharacterPreviewActor is missing on preview prefab: {previewObject.name}",
            previewObject);
        characterPresentation.SetProfile(presentationProfile);
        characterPresentation.Bind(
            previewObject.GetComponentInChildren<Animator>(true),
            previewObject.GetComponentsInChildren<SkinnedMeshRenderer>(true));
        if (presentationProfile != null)
        {
            characterPresentation.SwitchAsync(
                CharacterPresentationState.Default,
                PresentationTransitionMode.Immediate).Forget(Debug.LogException);
        }
    }

    void ReleaseActivePreview()
    {
        if (activePreviewObject == null)
        {
            activePreviewDefinition = null;
            return;
        }

        if (activePreviewState == ActivePreviewState.Character)
        {
            characterPresentation.Unbind();
        }

        ResourceManager.Instance.Recycle(activePreviewObject);
        activePreviewObject = null;
        activePreviewDefinition = null;
    }

    void ReleaseSuspendedCharacter()
    {
        if (suspendedCharacterPreviewObject == null)
        {
            suspendedCharacterPreviewDefinition = null;
            return;
        }

        ResourceManager.Instance.Recycle(suspendedCharacterPreviewObject);
        suspendedCharacterPreviewObject = null;
        suspendedCharacterPreviewDefinition = null;
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

    void OnDisable()
    {
        CancelCharacterSwitch();
    }

    void OnDestroy()
    {
        CancelCharacterSwitch();
        CancelPendingLoad();
        ReleaseSuspendedCharacter();
    }
}
