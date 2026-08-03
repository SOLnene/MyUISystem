using UnityEngine;

[CreateAssetMenu(fileName = "AnimatedPanelGroupPreset", menuName = "Game/UI Animation/Animated Panel Group Preset")]
public class AnimatedPanelGroupPreset : ScriptableObject
{
    [SerializeField]
    [Min(0f)]
    float staggerInterval = 0.04f;

    public float StaggerInterval => staggerInterval;
}
