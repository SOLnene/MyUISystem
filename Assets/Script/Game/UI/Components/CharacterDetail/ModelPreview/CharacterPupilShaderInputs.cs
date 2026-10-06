using System;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class CharacterPupilShaderInputs : MonoBehaviour
{
    [SerializeField] Transform head;
    [SerializeField] Renderer pupilRenderer;
    [SerializeField] Vector3 headCenterLocal;
    // Odette's exported head has local X pointing down and local Y facing forward, not Unity's usual axes.
    [SerializeField] Vector3 headXDirectionLocal = Vector3.right;
    [SerializeField] Vector3 faceDirectionLocal = Vector3.up;

    static readonly int HeadCenter = Shader.PropertyToID("_CharacterHeadCenterWorldPosition");
    static readonly int HeadXDirection = Shader.PropertyToID("_CharacterHeadCenterXDirWS");
    static readonly int FaceDirection = Shader.PropertyToID("_CharacterFaceWorldDirection");
    MaterialPropertyBlock propertyBlock;
    SkinnedMeshRenderer[] faceRenderers;

    void OnEnable()
    {
        // Standalone pupil previews have no actor; presentation prefabs already bind their face renderers.
        var actor = GetComponentInParent<CharacterPreviewActor>();
        faceRenderers = actor != null ? actor.FaceRenderers : Array.Empty<SkinnedMeshRenderer>();
    }

    void LateUpdate()
    {
        // Upload after animation evaluation; skinned mesh object axes do not follow the head bone.
        propertyBlock ??= new MaterialPropertyBlock();
        pupilRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetVector(HeadCenter, head.TransformPoint(headCenterLocal));
        propertyBlock.SetVector(HeadXDirection, head.TransformDirection(headXDirectionLocal).normalized);
        propertyBlock.SetVector(FaceDirection, head.TransformDirection(faceDirectionLocal).normalized);
        pupilRenderer.SetPropertyBlock(propertyBlock);

        // Preserve each renderer's existing overrides; only the native Face branch consumes these axes.
        foreach (var faceRenderer in faceRenderers)
        {
            faceRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetVector(HeadXDirection, head.TransformDirection(headXDirectionLocal).normalized);
            propertyBlock.SetVector(FaceDirection, head.TransformDirection(faceDirectionLocal).normalized);
            faceRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
