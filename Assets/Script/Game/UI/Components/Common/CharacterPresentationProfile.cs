using System;
using UnityEngine;

public enum CharacterPresentationState
{
    Default,
    TeamSwitch,
    WeaponShow,
    RelicShow
}

public enum PresentationTransitionMode
{
    Immediate,
    CrossFade,
    Authored,
    AuthoredTimedBlend
}

public enum PresentationPhase
{
    None,
    Exit,
    Enter,
    Loop
}

internal enum PresentationFaceSource
{
    Preset,
    Embedded,
    None
}

[Serializable]
public sealed class CharacterPresentationData
{
    [Header("Enter")]
    [SerializeField]
    AnimationClip enterBodyClip;

    [SerializeField]
    AnimationClip enterAuxiliaryClip;

    [SerializeField]
    FaceExpressionPreset enterFaceA;

    [SerializeField]
    FaceExpressionPreset enterFaceB;

    [SerializeField]
    PresentationFaceSource enterFaceSource;

    [Header("Loop")]
    [SerializeField]
    AnimationClip loopBodyClip;

    [SerializeField]
    AnimationClip loopAuxiliaryClip;

    [SerializeField]
    FaceExpressionPreset loopFaceA;

    [SerializeField]
    FaceExpressionPreset loopFaceB;

    [SerializeField]
    PresentationFaceSource loopFaceSource;

    [Header("Exit")]
    [SerializeField]
    AnimationClip exitBodyClip;

    [SerializeField]
    AnimationClip exitAuxiliaryClip;

    [SerializeField]
    FaceExpressionPreset exitFaceA;

    [SerializeField]
    FaceExpressionPreset exitFaceB;

    [SerializeField]
    PresentationFaceSource exitFaceSource;

    // Reserved for blending between presentation states, not AS/Loop/BS phase timing.
    [Min(0f)]
    [SerializeField]
    float crossFadeDuration = 0.15f;

    [SerializeField]
    bool useDirectSpecialSwitch;

    [Min(0f)]
    [SerializeField]
    float directSpecialEnterToLoopTime;

    [Min(0f)]
    [SerializeField]
    float directSpecialLoopBlendDuration = 0.12f;

    public AnimationClip EnterBodyClip => enterBodyClip;
    public AnimationClip EnterAuxiliaryClip => enterAuxiliaryClip;
    public FaceExpressionPreset EnterFaceA => enterFaceA;
    public FaceExpressionPreset EnterFaceB => enterFaceB;
    internal PresentationFaceSource EnterFaceSource => enterFaceSource;
    public AnimationClip LoopBodyClip => loopBodyClip;
    public AnimationClip LoopAuxiliaryClip => loopAuxiliaryClip;
    public FaceExpressionPreset LoopFaceA => loopFaceA;
    public FaceExpressionPreset LoopFaceB => loopFaceB;
    internal PresentationFaceSource LoopFaceSource => loopFaceSource;
    public AnimationClip ExitBodyClip => exitBodyClip;
    public AnimationClip ExitAuxiliaryClip => exitAuxiliaryClip;
    public FaceExpressionPreset ExitFaceA => exitFaceA;
    public FaceExpressionPreset ExitFaceB => exitFaceB;
    internal PresentationFaceSource ExitFaceSource => exitFaceSource;
    public float CrossFadeDuration => crossFadeDuration;
    internal bool UseDirectSpecialSwitch => useDirectSpecialSwitch;
    internal float DirectSpecialEnterToLoopTime => directSpecialEnterToLoopTime;
    internal float DirectSpecialLoopBlendDuration => directSpecialLoopBlendDuration;
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

    public CharacterPresentationData Get(CharacterPresentationState state)
    {
        return state switch
        {
            CharacterPresentationState.Default => defaultPresentation,
            CharacterPresentationState.TeamSwitch => teamSwitch,
            CharacterPresentationState.WeaponShow => weaponShow,
            CharacterPresentationState.RelicShow => relicShow,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };
    }
}
