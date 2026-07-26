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

    CancellationTokenSource loadCancellation;
    GameObject loadedPreviewObject;
    ModelPreviewType? currentPreviewType;

    public bool IsCharacterPreviewActive =>
        (currentPreviewType == ModelPreviewType.Character
         || currentPreviewType == null)
        && characterRoot.gameObject.activeInHierarchy;

    public async UniTask<ModelPreviewDefinition> ShowAsync(
        ModelPreviewType previewType,
        string targetKey)
    {
        ModelPreviewDefinition definition = modelPreviewDatabase.Get(previewType, targetKey);
        if (definition == null)
        {
            Debug.LogWarning(
                $"Model preview definition not found: {previewType}/{targetKey}",
                this);
            return null;
        }

        CancelPendingLoad();

        Transform previewRoot = previewType == ModelPreviewType.Character
            ? characterRoot
            : equipRoot;
        var cancellation = new CancellationTokenSource();
        loadCancellation = cancellation;
        GameObject previewObject = null;

        try
        {
            previewObject = await ResourceManager.Instance.InstantiateItemAsync(
                definition.ModelAddress,
                previewRoot,
                false,
                cancellation.Token);

            if (previewObject == null)
            {
                return null;
            }

            if (loadCancellation != cancellation || cancellation.IsCancellationRequested)
            {
                return null;
            }

            ConfigurePreview(previewObject, definition);
            if (previewType == ModelPreviewType.Character)
            {
                BindCharacter(previewObject);
            }

            CommitPreview(previewType, previewRoot, previewObject);
            previewObject = null;
            return definition;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            if (previewObject != null)
            {
                ResourceManager.Instance.Recycle(previewObject);
            }

            if (loadCancellation == cancellation)
            {
                loadCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    public void ShowDefaultCharacter()
    {
        CancelPendingLoad();
        ReleaseLoadedPreview();

        equipRoot.gameObject.SetActive(false);
        characterRoot.gameObject.SetActive(true);
        SetDirectChildrenActive(characterRoot, true);
        currentPreviewType = ModelPreviewType.Character;
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
        CancellationTokenSource cancellation = loadCancellation;
        loadCancellation = null;
        cancellation?.Cancel();
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

    void CommitPreview(
        ModelPreviewType previewType,
        Transform previewRoot,
        GameObject previewObject)
    {
        GameObject previousPreviewObject = loadedPreviewObject;

        characterRoot.gameObject.SetActive(previewType == ModelPreviewType.Character);
        equipRoot.gameObject.SetActive(previewType == ModelPreviewType.Equip);
        SetDirectChildrenActive(previewRoot, false);

        loadedPreviewObject = previewObject;
        currentPreviewType = previewType;
        previewObject.SetActive(true);

        if (previewType == ModelPreviewType.Equip)
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

    void ReleaseLoadedPreview()
    {
        if (loadedPreviewObject == null)
        {
            return;
        }

        ResourceManager.Instance.Recycle(loadedPreviewObject);
        loadedPreviewObject = null;
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
