using System;
using UnityEngine;

internal sealed class WorldCharacterMotionSet : ScriptableObject
{
    [SerializeField] internal string characterKey;
    [SerializeField] internal AnimationClip[] locomotion;
    [SerializeField] internal AnimationClip[] locomotionAuxiliary = Array.Empty<AnimationClip>();
    [SerializeField] internal WorldPlayerAnimation.NormalAttackStage[] attacks;
    [SerializeField] internal AnimationClip[] special;
    [SerializeField] internal string weaponSocketPath;
    [SerializeField] internal bool attackRootMotion;
    [SerializeField] internal Material swordTrailMaterial;
}
