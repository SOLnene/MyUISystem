using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CharacterWeaponAttachment : MonoBehaviour
{
    static readonly int WeaponDissolveId = Shader.PropertyToID("_WeaponDissolveValue");
    static readonly int WeaponPatternColorId = Shader.PropertyToID("_WeaponPatternColor");

    [SerializeField] Transform weaponSocket;
    [SerializeField, Min(0f)] float revealDelay = 0.2f;
    [SerializeField, Min(0f)] float revealDuration = 0.35f;
    [SerializeField, Min(0f)] float concealDuration = 0.18f;
    [SerializeField] Color dissolvePatternColor = new Color(0.4f, 0.28f, 0.04f, 0f);

    CharacterPresentationController presentation;
    ModelPreviewDefinition requestedDefinition;
    ModelPreviewDefinition displayedDefinition;
    CancellationTokenSource loadCancellation;
    GameObject weaponObject;
    Renderer[] weaponRenderers;
    bool[] rendererSupportsDissolve;
    bool[] rendererEnabledBeforeReveal;
    bool[] rendererSupportsPatternColor;
    Vector4[] rendererPatternColors;
    MaterialPropertyBlock dissolvePropertyBlock;
    float dissolveProgress;
    bool isQuitting;

    internal bool IsConcealed => weaponObject == null || dissolveProgress <= 0f;

    internal void ShowWeapon(
        ModelPreviewDefinition definition,
        CharacterPresentationController characterPresentation)
    {
        if (isQuitting)
        {
            return;
        }

        presentation = characterPresentation;
        if (requestedDefinition == definition
            && (loadCancellation != null
                || (weaponObject != null && displayedDefinition == definition)))
        {
            return;
        }

        requestedDefinition = definition;
        if (definition == null)
        {
            ReleaseWeapon();
            return;
        }

        CancelPendingLoad();
        if (isActiveAndEnabled)
        {
            LoadWeaponAsync(definition).Forget(Debug.LogException);
        }
    }

    async UniTask LoadWeaponAsync(ModelPreviewDefinition definition)
    {
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());
        loadCancellation = cancellation;
        GameObject loadedWeapon = null;
        try
        {
            loadedWeapon = await ResourceManager.Instance.InstantiateItemAsync(
                definition.ModelAddress, weaponSocket, false, cancellation.Token);

            // A superseded load must never replace the newer selection or a recycled actor.
            if (loadedWeapon == null
                || cancellation.IsCancellationRequested
                || loadCancellation != cancellation
                || requestedDefinition != definition
                || !isActiveAndEnabled)
            {
                return;
            }

            // The prefab owns grip calibration; the animated socket's transform stays untouched.
            loadedWeapon.transform.localPosition = Vector3.zero;
            loadedWeapon.transform.localRotation = Quaternion.identity;
            loadedWeapon.transform.localScale = Vector3.one;
            foreach (Transform child in loadedWeapon.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = weaponSocket.gameObject.layer;
            }

            RecycleDisplayedWeapon();
            weaponObject = loadedWeapon;
            displayedDefinition = definition;
            loadedWeapon = null;
            weaponRenderers = weaponObject.GetComponentsInChildren<Renderer>(true);
            rendererSupportsDissolve = new bool[weaponRenderers.Length];
            rendererEnabledBeforeReveal = new bool[weaponRenderers.Length];
            rendererSupportsPatternColor = new bool[weaponRenderers.Length];
            rendererPatternColors = new Vector4[weaponRenderers.Length];
            dissolvePropertyBlock ??= new MaterialPropertyBlock();
            for (int index = 0; index < weaponRenderers.Length; index++)
            {
                Renderer renderer = weaponRenderers[index];
                rendererEnabledBeforeReveal[index] = renderer.enabled;
                Material[] materials = renderer.sharedMaterials;
                rendererSupportsDissolve[index] = materials.Length > 0
                    && Array.TrueForAll(materials,
                        material => material != null && material.HasProperty(WeaponDissolveId));
                rendererSupportsPatternColor[index] = rendererSupportsDissolve[index]
                    && Array.TrueForAll(materials, material => material.HasProperty(WeaponPatternColorId));
                if (rendererSupportsPatternColor[index])
                {
                    renderer.GetPropertyBlock(dissolvePropertyBlock);
                    rendererPatternColors[index] = dissolvePropertyBlock.HasVector(WeaponPatternColorId)
                        ? dissolvePropertyBlock.GetVector(WeaponPatternColorId)
                        : renderer.sharedMaterial.GetVector(WeaponPatternColorId);
                }
            }
            ApplyDissolveProgress();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            if (loadedWeapon != null)
            {
                ResourceManager.Instance.Recycle(loadedWeapon, isQuitting);
            }
            if (loadCancellation == cancellation)
            {
                loadCancellation = null;
            }
            cancellation.Dispose();
        }
    }

    bool HasPresentationOwner => presentation != null
                                 && presentation.Actor != null
                                 && presentation.Actor.gameObject == gameObject;

    bool ShouldRevealWeapon()
    {
        return HasPresentationOwner
               && presentation.HasPlayingPresentation
               // A departure request fades the weapon while its current holding loop is still playing.
               && (!presentation.HasTargetPresentation
                   || presentation.TargetState == CharacterPresentationState.WeaponShow)
               && presentation.PlayingState == CharacterPresentationState.WeaponShow
               && (presentation.CurrentPhase == PresentationPhase.Loop
                   || (presentation.CurrentPhase == PresentationPhase.Enter
                       && presentation.PhaseTime >= revealDelay));
    }

    void Update()
    {
        if (weaponObject == null)
        {
            return;
        }

        UpdateWeaponVisibility(Time.deltaTime);
    }

    void UpdateWeaponVisibility(float deltaTime)
    {
        bool reveal = ShouldRevealWeapon();
        float duration = reveal ? revealDuration : concealDuration;
        float progress = HasPresentationOwner
            ? Mathf.MoveTowards(dissolveProgress, reveal ? 1f : 0f,
                duration > 0f ? deltaTime / duration : 1f)
            : 0f;
        if (progress == dissolveProgress)
        {
            return;
        }

        dissolveProgress = progress;
        ApplyDissolveProgress();
    }

    void ApplyDissolveProgress()
    {
        float shaderProgress = Mathf.SmoothStep(0f, 1f, dissolveProgress);
        for (int index = 0; index < weaponRenderers.Length; index++)
        {
            Renderer renderer = weaponRenderers[index];
            if (rendererSupportsDissolve[index])
            {
                renderer.GetPropertyBlock(dissolvePropertyBlock);
                dissolvePropertyBlock.SetFloat(WeaponDissolveId, shaderProgress);
                if (rendererSupportsPatternColor[index])
                {
                    // Keep the reveal gold within the LDR preview range; values are already shader-linear.
                    dissolvePropertyBlock.SetVector(WeaponPatternColorId,
                        dissolveProgress > 0f && dissolveProgress < 1f
                            ? (Vector4)dissolvePatternColor
                            : rendererPatternColors[index]);
                }
                renderer.SetPropertyBlock(dissolvePropertyBlock);
            }
            else
            {
                // Auxiliary effects with unrelated shaders must not appear before the weapon is formed.
                renderer.enabled = rendererEnabledBeforeReveal[index] && dissolveProgress >= 1f;
            }
        }

        bool visible = dissolveProgress > 0f;
        if (weaponObject.activeSelf != visible)
        {
            weaponObject.SetActive(visible);
        }
    }

    void CancelPendingLoad()
    {
        CancellationTokenSource cancellation = loadCancellation;
        loadCancellation = null;
        cancellation?.Cancel();
    }

    void ReleaseWeapon()
    {
        CancelPendingLoad();
        RecycleDisplayedWeapon();
    }

    void RecycleDisplayedWeapon()
    {
        if (weaponObject != null)
        {
            // A standalone preview can borrow the same pooled model after this attachment releases it.
            for (int index = 0; !isQuitting && index < weaponRenderers.Length; index++)
            {
                Renderer renderer = weaponRenderers[index];
                if (rendererSupportsDissolve[index])
                {
                    renderer.GetPropertyBlock(dissolvePropertyBlock);
                    dissolvePropertyBlock.SetFloat(WeaponDissolveId,
                        renderer.sharedMaterial.GetFloat(WeaponDissolveId));
                    if (rendererSupportsPatternColor[index])
                    {
                        dissolvePropertyBlock.SetVector(WeaponPatternColorId, rendererPatternColors[index]);
                    }
                    renderer.SetPropertyBlock(dissolvePropertyBlock);
                }
                renderer.enabled = rendererEnabledBeforeReveal[index];
            }
            ResourceManager.Instance.Recycle(weaponObject, isQuitting);
            weaponObject = null;
        }
        displayedDefinition = null;
        weaponRenderers = null;
        rendererSupportsDissolve = null;
        rendererEnabledBeforeReveal = null;
        rendererSupportsPatternColor = null;
        rendererPatternColors = null;
        dissolveProgress = 0f;
    }

    void OnEnable()
    {
        // Visibility suspension can resume its weapon; a pooled actor must wait for its new owner's request.
        if (!isQuitting && requestedDefinition != null && HasPresentationOwner)
        {
            LoadWeaponAsync(requestedDefinition).Forget(Debug.LogException);
        }
    }

    void OnDisable()
    {
        ReleaseWeapon();
    }

    void OnApplicationQuit()
    {
        // Reparenting into a pool during shutdown can rescue children from a scene being destroyed.
        isQuitting = true;
        ReleaseWeapon();
    }
}
