using UnityEngine;

public sealed class StarDomeRotator : MonoBehaviour
{
    [SerializeField] Vector3 rotationAxis = new Vector3(0.15f, 1f, 0.05f);
    [SerializeField, Min(0f)] float degreesPerSecond = 0.08f;

    void Update()
    {
        if (rotationAxis.sqrMagnitude <= Mathf.Epsilon)
            return;

        transform.Rotate(
            rotationAxis.normalized,
            degreesPerSecond * Time.unscaledDeltaTime,
            Space.World);
    }
}
