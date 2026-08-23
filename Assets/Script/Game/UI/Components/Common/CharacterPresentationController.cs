using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CharacterPresentationController : MonoBehaviour
{
    [SerializeField]
    CharacterPreviewAnimator animationController;

    [SerializeField]
    FaceController faceController;

    CharacterPreviewActor actor;

    public CharacterPreviewActor Actor => actor;

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
        animationController.Bind(animator);
        faceController.Bind(faceRenderers);
    }

    public void Unbind()
    {
        animationController.Unbind();
        faceController.Unbind();
        actor = null;
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

    public void ResetFace()
    {
        faceController.ResetAll();
    }
}
