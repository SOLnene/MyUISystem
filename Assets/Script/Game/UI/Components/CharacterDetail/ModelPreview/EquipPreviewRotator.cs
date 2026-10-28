using UnityEngine;

public sealed class EquipPreviewRotator : MonoBehaviour
{
    [SerializeField] Vector3 rotationAxis = Vector3.up;
    [SerializeField] float degreesPerSecond = 12f;

    Quaternion initialLocalRotation;
    Vector3 initialLocalPosition;
    Vector3 localCenter;
    Vector3 fixedCenterInParent;
    bool hasPreviewBounds;
    float angle;

    void Awake()
    {
        initialLocalRotation = transform.localRotation;
        initialLocalPosition = transform.localPosition;
    }

    void OnEnable()
    {
        angle = 0f;
        hasPreviewBounds = false;
        transform.localRotation = initialLocalRotation;
        transform.localPosition = initialLocalPosition;
    }

    internal void BindPreview(GameObject previewObject)
    {
        hasPreviewBounds = false;
        Bounds bounds = default;
        // Cache root-space bounds once; world-aligned bounds would change with the current rotation.
        foreach (Renderer renderer in previewObject.GetComponentsInChildren<Renderer>(true))
        {
            Bounds rendererBounds = renderer.localBounds;
            Matrix4x4 toRoot = transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 offset = Vector3.Scale(rendererBounds.extents, new Vector3(
                    (corner & 1) == 0 ? -1f : 1f,
                    (corner & 2) == 0 ? -1f : 1f,
                    (corner & 4) == 0 ? -1f : 1f));
                Vector3 point = toRoot.MultiplyPoint3x4(rendererBounds.center + offset);
                if (hasPreviewBounds)
                    bounds.Encapsulate(point);
                else
                {
                    bounds = new Bounds(point, Vector3.zero);
                    hasPreviewBounds = true;
                }
            }
        }

        localCenter = bounds.center;
        fixedCenterInParent = initialLocalPosition
            + initialLocalRotation * Vector3.Scale(transform.localScale, localCenter);
        angle = 0f;
        transform.localRotation = initialLocalRotation;
        transform.localPosition = initialLocalPosition;
    }

    void Update()
    {
        if (!hasPreviewBounds || rotationAxis.sqrMagnitude <= Mathf.Epsilon)
            return;

        angle = Mathf.Repeat(angle + degreesPerSecond * Time.unscaledDeltaTime, 360f);
        transform.localRotation =
            Quaternion.AngleAxis(angle, rotationAxis.normalized) * initialLocalRotation;
        // Keep the visible model center fixed even when its authored pivot is offset.
        transform.localPosition = fixedCenterInParent
            - transform.localRotation * Vector3.Scale(transform.localScale, localCenter);
    }
}
