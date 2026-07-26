using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CharacterPreviewActor : MonoBehaviour
{
    [SerializeField]
    Animator animator;

    [SerializeField]
    SkinnedMeshRenderer[] faceRenderers = Array.Empty<SkinnedMeshRenderer>();

    public Animator Animator => animator;
    public SkinnedMeshRenderer[] FaceRenderers => faceRenderers;

    void Reset()
    {
        RefreshBindings();
    }

    void OnValidate()
    {
        RefreshBindings();
    }

    [ContextMenu("Refresh Preview Bindings")]
    void RefreshBindings()
    {
        animator = GetComponent<Animator>();

        SkinnedMeshRenderer[] renderers =
            GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var results = new List<SkinnedMeshRenderer>(3);

        TryAddFaceRenderer(renderers, "Brow", "Brow_", results);
        TryAddFaceRenderer(renderers, "Face", "Mouth_", results);
        TryAddFaceRenderer(renderers, "Face_Eye", "Eye_", results);

        if (!HaveSameReferences(faceRenderers, results))
        {
            faceRenderers = results.ToArray();
        }
    }

    static void TryAddFaceRenderer(
        SkinnedMeshRenderer[] renderers,
        string objectName,
        string blendShapePrefix,
        List<SkinnedMeshRenderer> results)
    {
        foreach (SkinnedMeshRenderer renderer in renderers)
        {
            if (renderer.name == objectName &&
                ContainsBlendShapePrefix(renderer.sharedMesh, blendShapePrefix))
            {
                results.Add(renderer);
                return;
            }
        }
    }

    static bool ContainsBlendShapePrefix(Mesh mesh, string prefix)
    {
        if (mesh == null)
        {
            return false;
        }

        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            if (mesh.GetBlendShapeName(i).StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    static bool HaveSameReferences(
        SkinnedMeshRenderer[] current,
        List<SkinnedMeshRenderer> expected)
    {
        if (current == null || current.Length != expected.Count)
        {
            return false;
        }

        for (int i = 0; i < current.Length; i++)
        {
            if (current[i] != expected[i])
            {
                return false;
            }
        }

        return true;
    }
}
