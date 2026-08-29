using System;
using UnityEngine;

[Serializable]
public sealed class CharacterPresentationData
{
    [Header("Enter")]
    [SerializeField]
    AnimationClip enterBodyClip;

    [SerializeField]
    FaceExpressionPreset enterFaceA;

    [SerializeField]
    FaceExpressionPreset enterFaceB;

    [Header("Loop")]
    [SerializeField]
    AnimationClip loopBodyClip;

    [SerializeField]
    FaceExpressionPreset loopFaceA;

    [SerializeField]
    FaceExpressionPreset loopFaceB;

    [Header("Exit")]
    [SerializeField]
    AnimationClip exitBodyClip;

    [SerializeField]
    FaceExpressionPreset exitFaceA;

    [SerializeField]
    FaceExpressionPreset exitFaceB;

    // Reserved for blending between presentation states, not AS/Loop/BS phase timing.
    [Min(0f)]
    [SerializeField]
    float crossFadeDuration = 0.15f;

    public AnimationClip EnterBodyClip => enterBodyClip;
    public FaceExpressionPreset EnterFaceA => enterFaceA;
    public FaceExpressionPreset EnterFaceB => enterFaceB;
    public AnimationClip LoopBodyClip => loopBodyClip;
    public FaceExpressionPreset LoopFaceA => loopFaceA;
    public FaceExpressionPreset LoopFaceB => loopFaceB;
    public AnimationClip ExitBodyClip => exitBodyClip;
    public FaceExpressionPreset ExitFaceA => exitFaceA;
    public FaceExpressionPreset ExitFaceB => exitFaceB;
    public float CrossFadeDuration => crossFadeDuration;
}

[CreateAssetMenu(
    fileName = "CharacterPresentationProfile",
    menuName = "Game/UI/Character Presentation Profile")]
public sealed class CharacterPresentationProfile : ScriptableObject
{
    [SerializeField]
    CharacterPresentationData defaultPresentation = new();

    [SerializeField]
    CharacterPresentationData teamSwitch = new();

    [SerializeField]
    CharacterPresentationData weaponShow = new();

    [SerializeField]
    CharacterPresentationData relicShow = new();

    public CharacterPresentationData DefaultPresentation => defaultPresentation;
    public CharacterPresentationData TeamSwitch => teamSwitch;
    public CharacterPresentationData WeaponShow => weaponShow;
    public CharacterPresentationData RelicShow => relicShow;
}
