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
    [SerializeField] private string[] memberCharacterKeys;
    [SerializeField] private ModelPreviewDatabase modelPreviewDatabase;
    [SerializeField] private TeamStageCameraController cameraController;
    [SerializeField] private float memberFadeDuration = 0.15f;

    private GameObject[] activeMemberModels;
    private string[] activeMemberCharacterKeys;
    private Renderer[][] memberRenderers;
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
        memberFadeTween?.Kill();
        memberFadeTween = DOVirtual.Float(1f, 0f, memberFadeDuration, fade =>
        {
            for (int memberIndex = 0; memberIndex < MemberCount; memberIndex++)
            {
                SetMemberFade(memberIndex, memberIndex == index ? 1f : fade);
            }
        }).SetEase(Ease.OutCubic);
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

        memberCharacterKeys[index] = characterKey;
        if (string.IsNullOrEmpty(characterKey))
        {
            ClearMemberModel(index);
            return;
        }

        if (activeMemberModels[index] != null
            && activeMemberCharacterKeys[index] == characterKey)
        {
            CancelMemberLoad(index);
            return;
        }

        ModelPreviewDefinition definition = modelPreviewDatabase.Get(
            ModelPreviewType.Character,
            characterKey);
        if (definition == null)
        {
            Debug.LogWarning($"Team stage model definition not found: {characterKey}", this);
            ClearMemberModel(index);
            return;
        }

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
            if (previousModel != null)
            {
                SetMemberFade(index, 1f);
            }

            activeMemberModels[index] = loadedModel;
            activeMemberCharacterKeys[index] = characterKey;
            memberRenderers[index] = loadedModel.GetComponentsInChildren<Renderer>(true);
            SetMemberFade(index, focusedMemberIndex < 0 || focusedMemberIndex == index ? 1f : 0f);
            loadedModel.SetActive(true);
            loadedModel = null;

            if (previousModel != null)
            {
                ResourceManager.Instance.Recycle(previousModel);
            }
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
                memberLoadCancellations[index] = null;
            }

            cancellation.Dispose();
        }
    }

    private void ClearMemberModel(int index)
    {
        CancelMemberLoad(index);
        RecycleMemberModel(index);
    }

    private void CancelMemberLoad(int index)
    {
        memberLoadVersions[index]++;
        CancellationTokenSource cancellation = memberLoadCancellations[index];
        memberLoadCancellations[index] = null;
        cancellation?.Cancel();
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
            memberFadePropertyBlock.SetFloat(ModelFadeId, fade);
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

    private void OnDestroy()
    {
        ReleaseOwnedModels(true);
    }
}
