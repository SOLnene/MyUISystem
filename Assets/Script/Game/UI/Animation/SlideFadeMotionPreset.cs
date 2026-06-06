using DG.Tweening;
using UnityEngine;

[CreateAssetMenu(fileName = "SlideFadeMotionPreset", menuName = "Game/UI Animation/Slide Fade Motion Preset")]
public class SlideFadeMotionPreset : ScriptableObject
{
    [SerializeField] Vector2 targetMove;
    [SerializeField] Vector2 originMove;
    [SerializeField] float moveDuration = 0.18f;
    [SerializeField] float fadeDuration = 0.12f;
    [SerializeField] Ease moveEase = Ease.OutCubic;
    [SerializeField] Ease fadeEase = Ease.Linear;

    public Vector2 TargetMove => targetMove;
    public Vector2 OriginMove => originMove;
    public float MoveDuration => moveDuration;
    public float FadeDuration => fadeDuration;
    public Ease MoveEase => moveEase;
    public Ease FadeEase => fadeEase;
}
