using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WeaponPreviewPose : MonoBehaviour
{
    [Serializable]
    struct HeldBonePose
    {
        public Transform bone;
        public Vector3 localEulerAngles;
    }

    [SerializeField] HeldBonePose[] heldPose;
    Quaternion[] standaloneRotations;

    void Awake()
    {
        standaloneRotations = new Quaternion[heldPose.Length];
        for (int index = 0; index < heldPose.Length; index++)
        {
            standaloneRotations[index] = heldPose[index].bone.localRotation;
        }
    }

    void OnEnable()
    {
        // A pooled book can resume under either a character socket or the standalone preview root.
        if (GetComponentInParent<CharacterWeaponAttachment>() == null)
        {
            ApplyStandalonePose();
            return;
        }

        foreach (HeldBonePose pose in heldPose)
        {
            pose.bone.localRotation = Quaternion.Euler(pose.localEulerAngles);
        }
    }

    internal void ApplyStandalonePose()
    {
        for (int index = 0; index < heldPose.Length; index++)
        {
            heldPose[index].bone.localRotation = standaloneRotations[index];
        }
    }

    void OnDisable()
    {
        // The same pooled model can next be used by a different preview context.
        ApplyStandalonePose();
    }
}
