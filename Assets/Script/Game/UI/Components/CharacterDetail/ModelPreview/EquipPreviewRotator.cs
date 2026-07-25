using UnityEngine;

public sealed class EquipPreviewRotator : MonoBehaviour
{
    [SerializeField] Vector3 rotationAxis = Vector3.up;
    [SerializeField] float degreesPerSecond = 12f;

    Quaternion initialLocalRotation;
    float angle;

    void Awake()
    {
        initialLocalRotation = transform.localRotation;
    }

    void OnEnable()
    {
        angle = 0f;
        transform.localRotation = initialLocalRotation;
    }

    void Update()
    {
        if (rotationAxis.sqrMagnitude <= Mathf.Epsilon)
            return;

        angle = Mathf.Repeat(angle + degreesPerSecond * Time.unscaledDeltaTime, 360f);
        transform.localRotation =
            initialLocalRotation * Quaternion.AngleAxis(angle, rotationAxis.normalized);
    }
}
