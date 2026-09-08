using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CharacterPresentationController : MonoBehaviour
{
    [SerializeField]
    CharacterPreviewAnimator animationController;

    [SerializeField]
    FaceController faceController;

    CharacterPreviewActor actor;
    CharacterPresentationProfile profile;
    CharacterPresentationData currentPresentation;
    CharacterPresentationState currentState;
    PresentationPhase currentPhase;
    CancellationTokenSource playbackCancellation;

    public CharacterPreviewActor Actor => actor;
    public CharacterPresentationProfile Profile => profile;
    public CharacterPresentationState CurrentState => currentState;
    public PresentationPhase CurrentPhase => currentPhase;
    public bool HasCurrentPresentation => currentPresentation != null;

    public void Bind(
        CharacterPreviewActor targetActor,
        CharacterPresentationProfile targetProfile)
    {
        SetProfile(targetProfile);
        Bind(targetActor);
    }

    public void Bind(CharacterPreviewActor targetActor)
    {
        if (targetActor == null)
        {
            Unbind();
            return;
        }

        actor = targetActor;
        BindComponents(targetActor.Animator, targetActor.FaceRenderers);
    }

    public void Bind(Animator animator, SkinnedMeshRenderer[] faceRenderers)
    {
        actor = null;
        BindComponents(animator, faceRenderers);
    }

    void BindComponents(Animator animator, SkinnedMeshRenderer[] faceRenderers)
    {
        Stop();
        currentPresentation = null;
        currentPhase = PresentationPhase.None;
        animationController.Bind(animator);
        faceController.Bind(faceRenderers);
    }

    public void SetProfile(CharacterPresentationProfile targetProfile)
    {
        if (profile == targetProfile)
        {
            return;
        }

        Stop();
        profile = targetProfile;
        currentPresentation = null;
        currentPhase = PresentationPhase.None;
    }

    public void Unbind()
    {
        Stop();
        animationController.Unbind();
        faceController.Unbind();
        actor = null;
        profile = null;
        currentPresentation = null;
        currentPhase = PresentationPhase.None;
    }

    public async UniTask SwitchAsync(
        CharacterPresentationState targetState,
        PresentationTransitionMode transitionMode,
        float? crossFadeDurationOverride = null,
        CancellationToken cancellationToken = default)
    {
        if (profile == null)
        {
            Debug.LogWarning("Character presentation profile is not bound.");
            return;
        }

        CharacterPresentationData previousPresentation = currentPresentation;
        CharacterPresentationData targetPresentation = profile.Get(targetState);
        if (currentPresentation != null && currentState == targetState)
        {
            return;
        }

        Stop();

        bool useAuthoredTransition =
            transitionMode == PresentationTransitionMode.Authored;
        bool crossFade = transitionMode == PresentationTransitionMode.CrossFade;

        var currentCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            this.GetCancellationTokenOnDestroy());
        playbackCancellation = currentCancellation;

        try
        {
            currentCancellation.Token.ThrowIfCancellationRequested();

            if (useAuthoredTransition && previousPresentation != null)
            {
                currentPhase = PresentationPhase.Exit;
                await PlayTimedPhaseAsync(
                    previousPresentation.ExitBodyClip,
                    previousPresentation.ExitFaceA,
                    previousPresentation.ExitFaceB,
                    currentCancellation.Token);
            }

            currentCancellation.Token.ThrowIfCancellationRequested();
            currentPresentation = targetPresentation;
            currentState = targetState;

            if (useAuthoredTransition)
            {
                currentPhase = PresentationPhase.Enter;
                await PlayTimedPhaseAsync(
                    targetPresentation.EnterBodyClip,
                    targetPresentation.EnterFaceA,
                    targetPresentation.EnterFaceB,
                    currentCancellation.Token);
            }

            currentCancellation.Token.ThrowIfCancellationRequested();
            currentPhase = PresentationPhase.Loop;
            PlayPhase(
                targetPresentation.LoopBodyClip,
                targetPresentation.LoopFaceA,
                targetPresentation.LoopFaceB,
                crossFade,
                ResolveCrossFadeDuration(
                    targetPresentation,
                    crossFadeDurationOverride));
        }
        catch (OperationCanceledException)
        {
            if (ReferenceEquals(playbackCancellation, currentCancellation))
            {
                currentPhase = PresentationPhase.None;
            }
        }
        finally
        {
            if (ReferenceEquals(playbackCancellation, currentCancellation))
            {
                playbackCancellation = null;
                currentCancellation.Dispose();
            }
        }
    }

    async UniTask PlayTimedPhaseAsync(
        AnimationClip bodyClip,
        FaceExpressionPreset faceA,
        FaceExpressionPreset faceB,
        CancellationToken cancellationToken)
    {
        PlayPhase(bodyClip, faceA, faceB, false, 0f);

        if (bodyClip != null)
        {
            await animationController.WaitForClipEndAsync(
                bodyClip,
                cancellationToken);
            return;
        }

        float duration = GetFacePhaseDuration(faceA, faceB);
        if (duration > 0f)
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(duration),
                cancellationToken: cancellationToken);
        }
    }

    void PlayPhase(
        AnimationClip bodyClip,
        FaceExpressionPreset faceA,
        FaceExpressionPreset faceB,
        bool crossFade,
        float crossFadeDuration)
    {
        if (bodyClip != null)
        {
            animationController.Play(bodyClip, crossFade, crossFadeDuration);
        }

        if (faceA != null && faceB != null)
        {
            faceController.ApplyFacePresets(faceA, faceB);
        }
        else if (faceA != null || faceB != null)
        {
            faceController.ApplyFacePreset(faceA != null ? faceA : faceB);
        }
    }

    static float GetFacePhaseDuration(
        FaceExpressionPreset faceA,
        FaceExpressionPreset faceB)
    {
        return Mathf.Max(
            faceA == null ? 0f : faceA.duration,
            faceB == null ? 0f : faceB.duration);
    }

    static float ResolveCrossFadeDuration(
        CharacterPresentationData presentation,
        float? crossFadeDurationOverride)
    {
        return Mathf.Max(
            0f,
            crossFadeDurationOverride ?? presentation.CrossFadeDuration);
    }

    public void Stop()
    {
        currentPhase = PresentationPhase.None;
        CancellationTokenSource cancellation = playbackCancellation;
        playbackCancellation = null;
        if (cancellation == null)
        {
            return;
        }

        cancellation.Cancel();
        cancellation.Dispose();
    }

    public void PlayImmediate(AnimationClip clip)
    {
        animationController.PlayImmediate(clip);
    }

    public void CrossFadeTo(
        AnimationClip clip,
        float blendDuration,
        Action onBlendCompleted = null)
    {
        animationController.CrossFadeTo(clip, blendDuration, onBlendCompleted);
    }

    public void ApplyFacePreset(FaceExpressionPreset preset)
    {
        faceController.ApplyFacePreset(preset);
    }

    public void ApplyFacePresets(
        FaceExpressionPreset firstPreset,
        FaceExpressionPreset secondPreset)
    {
        faceController.ApplyFacePresets(firstPreset, secondPreset);
    }

    public void ResetFace()
    {
        faceController.ResetAll();
    }

    void OnDestroy()
    {
        Stop();
    }
}
