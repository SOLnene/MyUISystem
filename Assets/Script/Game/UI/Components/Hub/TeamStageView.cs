using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public sealed class TeamStageView : MonoBehaviour
{
    private static readonly int ModelFadeId = Shader.PropertyToID("_ModelFade");

    [SerializeField] private Camera displayCamera;
    [SerializeField] private Transform[] memberInfoAnchors;
    [SerializeField] private Transform[] memberModelRoots;
    [SerializeField] private ParticleSystem[] memberSwitchParticles;
    [SerializeField] private CharacterSwitchElementPalette characterSwitchElementPalette;
    [SerializeField] private string[] memberCharacterKeys;
    [SerializeField] private ModelPreviewDatabase modelPreviewDatabase;
    [SerializeField] private CharacterPresentationController[] memberPresentations;
    [SerializeField] private TeamStageCameraController cameraController;
    [SerializeField] private float memberFadeDuration = 0.15f;
    [SerializeField, Min(0f)] private float memberSwitchFadeOutDuration = 0.06f;
    [SerializeField, Min(0f)] private float memberSwitchFadeInDuration = 0.15f;
    [SerializeField, Min(0f)] private float memberSwitchEffectLeadTime = 0.2f;

    private GameObject[] activeMemberModels;
    private string[] activeMemberCharacterKeys;
    private Renderer[][] memberRenderers;
    private float[] memberVisibility;
    private float[] memberSwitchFades;
    private CancellationTokenSource[] memberLoadCancellations;
    private int[] memberLoadVersions;
    private MaterialPropertyBlock memberFadePropertyBlock;
    private Tween memberFadeTween;
    private int focusedMemberIndex = -1;
    private bool isReleased;

    public Camera DisplayCamera => displayCamera;
    public int MemberCount => Mathf.Min(
        memberInfoAnchors.Length,
        Mathf.Min(memberModelRoots.Length, memberCharacterKeys.Length));

    private void Awake()
    {
        activeMemberModels = new GameObject[MemberCount];
        activeMemberCharacterKeys = new string[MemberCount];
        memberRenderers = new Renderer[MemberCount][];
        memberVisibility = new float[MemberCount];
        memberSwitchFades = new float[MemberCount];
        for (int index = 0; index < MemberCount; index++)
        {
            memberVisibility[index] = 1f;
            memberSwitchFades[index] = 1f;
        }
        memberLoadCancellations = new CancellationTokenSource[MemberCount];
        memberLoadVersions = new int[MemberCount];
        memberFadePropertyBlock = new MaterialPropertyBlock();
    }

    public Vector3 GetMemberInfoPosition(int index)
    {
        return memberInfoAnchors[index].position;
    }

    public string GetMemberCharacterKey(int index)
    {
        return memberCharacterKeys[index];
    }

    public bool TryGetMemberCharacterKey(int index, out string characterKey)
    {
        characterKey = memberCharacterKeys[index];
        return !string.IsNullOrEmpty(characterKey);
    }

    public void FocusMember(int index)
    {
        cameraController.FocusMember(index);
        focusedMemberIndex = index;
        for (int memberIndex = 0; memberIndex < MemberCount; memberIndex++)
        {
            if (memberIndex != index)
            {
                memberSwitchParticles[memberIndex].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
        memberFadeTween?.Kill();
        memberFadeTween = DOVirtual.Float(1f, 0f, memberFadeDuration, fade =>
        {
            for (int memberIndex = 0; memberIndex < MemberCount; memberIndex++)
            {
                SetMemberFade(memberIndex, memberIndex == index ? 1f : fade);
            }
        }).SetEase(Ease.InQuad);
    }

    public void ShowOverview()
    {
        cameraController.ShowOverview();
        memberFadeTween?.Kill();
        if (focusedMemberIndex < 0)
        {
            for (int index = 0; index < MemberCount; index++)
            {
                SetMemberFade(index, 1f);
            }

            return;
        }

        int previousFocusedMemberIndex = focusedMemberIndex;
        focusedMemberIndex = -1;
        memberFadeTween = DOVirtual.Float(0f, 1f, memberFadeDuration, fade =>
        {
            for (int memberIndex = 0; memberIndex < MemberCount; memberIndex++)
            {
                SetMemberFade(
                    memberIndex,
                    memberIndex == previousFocusedMemberIndex ? 1f : fade);
            }
        }).SetEase(Ease.OutCubic);
    }

    public async UniTask SetMemberAsync(int index, string characterKey)
    {
        if (isReleased)
        {
            return;
        }

        if (string.IsNullOrEmpty(characterKey))
        {
            memberCharacterKeys[index] = characterKey;
            ClearMemberModel(index);
            return;
        }

        if (activeMemberModels[index] != null
            && activeMemberCharacterKeys[index] == characterKey)
        {
            memberCharacterKeys[index] = characterKey;
            CancelMemberLoad(index);
            return;
        }

        ModelPreviewDefinition definition = modelPreviewDatabase.Get(
            ModelPreviewType.Character,
            characterKey);
        if (definition == null)
        {
            Debug.LogWarning($"Team stage model definition not found: {characterKey}", this);
            return;
        }

        memberCharacterKeys[index] = characterKey;
        await LoadMemberModelAsync(index, characterKey, definition);
    }

    public void ClearMember(int index)
    {
        memberCharacterKeys[index] = null;
        ClearMemberModel(index);
    }

    public async UniTask LoadInitialMembersAsync()
    {
        for (int index = 0; index < MemberCount; index++)
        {
            string characterKey = memberCharacterKeys[index];
            if (string.IsNullOrEmpty(characterKey))
            {
                ClearMemberModel(index);
                continue;
            }

            await SetMemberAsync(index, characterKey);
        }
    }

    private async UniTask LoadMemberModelAsync(
        int index,
        string characterKey,
        ModelPreviewDefinition definition)
    {
        CancelMemberLoad(index);
        int requestVersion = memberLoadVersions[index];
        CancellationTokenSource cancellation = new CancellationTokenSource();
        memberLoadCancellations[index] = cancellation;
        GameObject loadedModel = null;

        try
        {
            loadedModel = await ResourceManager.Instance.InstantiateItemAsync(
                definition.ModelAddress,
                memberModelRoots[index],
                false,
                cancellation.Token);
            if (loadedModel == null)
            {
                return;
            }

            if (isReleased
                || requestVersion != memberLoadVersions[index]
                || cancellation.IsCancellationRequested)
            {
                return;
            }

            Transform loadedTransform = loadedModel.transform;
            loadedTransform.localPosition = definition.LocalPosition;
            loadedTransform.localRotation = Quaternion.Euler(definition.LocalEulerAngles);
            loadedTransform.localScale = definition.LocalScale;
            SetLayerRecursively(loadedModel, LayerMask.NameToLayer("ModelDisplay"));

            GameObject previousModel = activeMemberModels[index];
            // 独立粒子不会随角色 Dither 裁剪；聚焦时仅播放当前可见位置。
            if (focusedMemberIndex < 0 || focusedMemberIndex == index)
            {
                // 使用本次请求的目标元素，旧角色在预加载阶段仍可能可见。
                string element = GameDatabase.CharacterDatabase?.Get(characterKey)?.element;
                CharacterSwitchParticlePlayback.Play(
                    memberSwitchParticles[index], characterSwitchElementPalette.Resolve(element));
            }
            if (previousModel != null)
            {
                await FadeMemberReplacementAsync(
                    index, 0f, memberSwitchFadeOutDuration, cancellation.Token);
            }

            float leadTime = Mathf.Max(0f, memberSwitchEffectLeadTime
                - (previousModel != null ? memberSwitchFadeOutDuration : 0f));
            await UniTask.Delay(TimeSpan.FromSeconds(leadTime), cancellationToken: cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (previousModel != null)
            {
                // 同一帧内回收旧模型，先恢复池对象的可见度。
                memberSwitchFades[index] = 1f;
                SetMemberFade(index, 1f);
            }

            memberPresentations[index].Unbind();
            activeMemberModels[index] = loadedModel;
            activeMemberCharacterKeys[index] = characterKey;
            memberRenderers[index] = loadedModel.GetComponentsInChildren<Renderer>(true);
            memberSwitchFades[index] = 0f;
            SetMemberFade(index, focusedMemberIndex < 0 || focusedMemberIndex == index ? 1f : 0f);
            loadedModel.SetActive(true);
            BindMemberPresentation(index, loadedModel, definition);
            loadedModel = null;

            if (previousModel != null)
            {
                ResourceManager.Instance.Recycle(previousModel);
            }
            // 空位加入也从不可见开始，避免先出现实体后补播光束。
            await FadeMemberReplacementAsync(
                index, 1f, memberSwitchFadeInDuration, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            if (loadedModel != null)
            {
                ResourceManager.Instance.Recycle(loadedModel, isReleased);
            }

            if (memberLoadCancellations[index] == cancellation)
            {
                memberCharacterKeys[index] = activeMemberCharacterKeys[index];
                memberSwitchFades[index] = 1f;
                SetMemberFade(index, memberVisibility[index]);
                memberLoadCancellations[index] = null;
            }

            cancellation.Dispose();
        }
    }

    private void BindMemberPresentation(
        int index,
        GameObject model,
        ModelPreviewDefinition definition)
    {
        CharacterPreviewActor actor = model.GetComponentInChildren<CharacterPreviewActor>(true);
        if (actor == null)
        {
            Debug.LogWarning($"Team stage actor is missing: {definition.TargetKey}", model);
            return;
        }

        CharacterPresentationProfile profile = definition.PresentationProfile;
        memberPresentations[index].Bind(actor, profile);
        // Some actors have no team animation; their profile's default loop remains the fallback.
        if (profile != null && profile.TeamSwitch.LoopBodyClip != null)
        {
            memberPresentations[index].SwitchAsync(
                CharacterPresentationState.TeamSwitch,
                PresentationTransitionMode.Immediate).Forget(Debug.LogException);
        }
    }

    private async UniTask FadeMemberReplacementAsync(
        int index,
        float targetFade,
        float duration,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        float from = memberSwitchFades[index];
        float elapsed = 0f;
        while (elapsed < duration)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            elapsed += Time.deltaTime;
            memberSwitchFades[index] = Mathf.Lerp(from, targetFade, elapsed / duration);
            SetMemberFade(index, memberVisibility[index]);
        }
        cancellationToken.ThrowIfCancellationRequested();
        memberSwitchFades[index] = targetFade;
        SetMemberFade(index, memberVisibility[index]);
    }

    private void ClearMemberModel(int index)
    {
        CancelMemberLoad(index);
        RecycleMemberModel(index);
    }

    private void CancelMemberLoad(int index)
    {
        memberSwitchParticles[index].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        memberLoadVersions[index]++;
        CancellationTokenSource cancellation = memberLoadCancellations[index];
        memberLoadCancellations[index] = null;
        cancellation?.Cancel();
        memberSwitchFades[index] = 1f;
        SetMemberFade(index, memberVisibility[index]);
    }

    private void RecycleMemberModel(int index, bool forceDestroy = false)
    {
        GameObject memberModel = activeMemberModels[index];
        if (memberModel == null)
        {
            activeMemberCharacterKeys[index] = null;
            memberRenderers[index] = null;
            return;
        }

        // MaterialPropertyBlock is retained by pooled renderers, so restore the
        // shared model to its visible state before returning it to the pool.
        SetMemberFade(index, 1f);
        if (!forceDestroy)
        {
            memberPresentations[index].Unbind();
        }
        ResourceManager.Instance.Recycle(memberModel, forceDestroy);
        activeMemberModels[index] = null;
        activeMemberCharacterKeys[index] = null;
        memberRenderers[index] = null;
    }

    internal void Open()
    {
        if (!isReleased)
        {
            gameObject.SetActive(true);
            for (int index = 0; index < MemberCount; index++)
            {
                if (activeMemberModels[index] != null)
                {
                    BindMemberPresentation(index, activeMemberModels[index],
                        modelPreviewDatabase.Get(ModelPreviewType.Character, activeMemberCharacterKeys[index]));
                }
            }
        }
    }

    internal void Close()
    {
        if (isReleased)
        {
            return;
        }

        for (int index = 0; index < MemberCount; index++)
        {
            CancelMemberLoad(index);
            SetMemberFade(index, 1f);
            memberPresentations[index].Unbind();
        }

        memberFadeTween?.Kill();
        memberFadeTween = null;
        focusedMemberIndex = -1;
        cameraController.ShowOverview();
        gameObject.SetActive(false);
    }

    internal void Release()
    {
        ReleaseOwnedModels(false);
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void ReleaseOwnedModels(bool forceDestroy)
    {
        if (isReleased)
        {
            return;
        }

        // Pooling is valid during an explicit release; destruction fallback must not
        // move scene-owned models into DontDestroyOnLoad while Unity is unloading.
        isReleased = true;
        memberFadeTween?.Kill();
        memberFadeTween = null;
        for (int index = 0; index < MemberCount; index++)
        {
            CancelMemberLoad(index);
            RecycleMemberModel(index, forceDestroy);
        }
    }

    private void SetMemberFade(int index, float fade)
    {
        // 聚焦可见度与换人显隐相乘，两个流程不会覆盖彼此。
        memberVisibility[index] = fade;
        Renderer[] renderers = memberRenderers[index];
        if (renderers == null)
        {
            return;
        }

        foreach (Renderer memberRenderer in renderers)
        {
            if (memberRenderer == null)
            {
                continue;
            }

            memberFadePropertyBlock.Clear();
            memberRenderer.GetPropertyBlock(memberFadePropertyBlock);
            memberFadePropertyBlock.SetFloat(ModelFadeId, fade * memberSwitchFades[index]);
            memberRenderer.SetPropertyBlock(memberFadePropertyBlock);
        }
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.layer = layer;
        }
    }

    private void OnDisable()
    {
        for (int index = 0; index < MemberCount; index++)
        {
            CancelMemberLoad(index);
            memberPresentations[index].Unbind();
        }
    }

    private void OnDestroy()
    {
        ReleaseOwnedModels(true);
    }
}
