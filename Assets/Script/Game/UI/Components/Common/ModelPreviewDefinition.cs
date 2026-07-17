using UnityEngine;

public enum ModelPreviewType
{
    Character,
    Equip
}

[CreateAssetMenu(
    fileName = "ModelPreviewDefinition",
    menuName = "Game/UI/ModelViewer/Preview Definition")]
public class ModelPreviewDefinition : ScriptableObject
{
    [SerializeField] ModelPreviewType previewType;
    [SerializeField] string targetKey;
    [SerializeField] string modelAddress;
    [SerializeField] Vector3 localPosition;
    [SerializeField] Vector3 localEulerAngles;
    [SerializeField] Vector3 localScale = Vector3.one;
    [SerializeField] CameraPreset cameraPreset;

    public ModelPreviewType PreviewType => previewType;
    public string TargetKey => targetKey;
    public string ModelAddress => modelAddress;
    public Vector3 LocalPosition => localPosition;
    public Vector3 LocalEulerAngles => localEulerAngles;
    public Vector3 LocalScale => localScale;
    public CameraPreset CameraPreset => cameraPreset;
}
