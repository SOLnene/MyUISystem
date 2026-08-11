using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public sealed class TeamStageView : MonoBehaviour
{
    [SerializeField] private Camera displayCamera;
    [SerializeField] private Transform[] memberInfoAnchors;
    [SerializeField] private Transform[] memberModelRoots;
    [SerializeField] private string[] memberCharacterKeys;
    [SerializeField] private ModelPreviewDatabase modelPreviewDatabase;
    [SerializeField] private Transform overviewCameraPose;
    [SerializeField] private Transform[] memberFocusCameraPoses;
    [SerializeField] private float cameraTransitionDuration = 0.35f;

    private GameObject[] activeMemberModels;
    private CancellationTokenSource[] memberLoadCancellations;
    private int[] memberLoadVersions;
    private Tween cameraTransition;

    public Camera DisplayCamera => displayCamera;
    public int MemberCount => Mathf.Min(
        memberInfoAnchors.Length,
        Mathf.Min(memberModelRoots.Length, memberCharacterKeys.Length));

    private void Awake()
    {
        activeMemberModels = new GameObject[MemberCount];
        memberLoadCancellations = new CancellationTokenSource[MemberCount];
        memberLoadVersions = new int[MemberCount];
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
        MoveCameraTo(memberFocusCameraPoses[index]);
    }

    public void ShowOverview()
    {
        MoveCameraTo(overviewCameraPose);
    }

    public async UniTask SetMemberAsync(int index, string characterKey)
    {
        memberCharacterKeys[index] = characterKey;
        if (string.IsNullOrEmpty(characterKey))
        {
            ClearMemberModel(index);
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

        await LoadMemberModelAsync(index, definition);
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

    private async UniTask LoadMemberModelAsync(int index, ModelPreviewDefinition definition)
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

            if (requestVersion != memberLoadVersions[index]
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
            activeMemberModels[index] = loadedModel;
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
                ResourceManager.Instance.Recycle(loadedModel);
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

    private void RecycleMemberModel(int index)
    {
        GameObject memberModel = activeMemberModels[index];
        if (memberModel == null)
        {
            return;
        }

        ResourceManager.Instance.Recycle(memberModel);
        activeMemberModels[index] = null;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.layer = layer;
        }
    }

    private void MoveCameraTo(Transform targetPose)
    {
        cameraTransition?.Kill();
        Transform cameraTransform = displayCamera.transform;
        cameraTransition = DOTween.Sequence()
            .Join(cameraTransform.DOMove(targetPose.position, cameraTransitionDuration))
            .Join(cameraTransform.DORotateQuaternion(
                targetPose.rotation,
                cameraTransitionDuration))
            .SetEase(Ease.InOutCubic)
            .OnComplete(() => cameraTransition = null);
    }

    private void OnDestroy()
    {
        cameraTransition?.Kill();
        for (int index = 0; index < MemberCount; index++)
        {
            CancelMemberLoad(index);
            RecycleMemberModel(index);
        }
    }
}
