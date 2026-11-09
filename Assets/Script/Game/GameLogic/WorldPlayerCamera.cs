using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorldPlayerCamera : MonoBehaviour
{
    [SerializeField] WorldPlayerController player;
    [SerializeField] Transform followTarget;
    [SerializeField] Camera viewCamera;
    [SerializeField] WorldPlayerInput inputReader;
    [SerializeField, Min(.1f)] float distance = 4.5f;
    [SerializeField, Min(.1f)] float minimumDistance = 1.2f;
    [SerializeField, Min(.1f)] float maximumDistance = 7f;
    [SerializeField] float pitch = 15f;
    [SerializeField] float minimumPitch = -25f;
    [SerializeField] float maximumPitch = 65f;
    [SerializeField, Min(0f)] float sensitivity = 3f;
    [SerializeField, Min(0f)] float zoomSpeed = 3f;
    [SerializeField, Min(.01f)] float followSmoothTime = .07f;
    [SerializeField, Min(.01f)] float collisionRadius = .18f;
    [SerializeField] LayerMask obstructionMask = ~0;

    readonly RaycastHit[] hits = new RaycastHit[32];
    Vector3 focus, focusVelocity;
    float yaw, currentDistance;

    void OnEnable()
    {
        yaw = player.transform.eulerAngles.y;
        currentDistance = distance;
        SnapToTarget();
    }

    internal void SnapToTarget()
    {
        focus = followTarget.position;
        focusVelocity = Vector3.zero;
        UpdateCameraPosition(0f);
    }

    void LateUpdate()
    {
        AdvanceView(inputReader.Look.x, inputReader.Look.y, inputReader.Zoom, inputReader.OrbitHeld);
    }

    internal void AdvanceView(float horizontalLook, float verticalLook, float scroll, bool orbit)
    {
        if (inputReader.CanControlCamera)
        {
            if (orbit)
            {
                yaw += horizontalLook * sensitivity;
                pitch = Mathf.Clamp(pitch - verticalLook * sensitivity, minimumPitch, maximumPitch);
            }
            distance = Mathf.Clamp(distance - scroll * zoomSpeed * .1f, minimumDistance, maximumDistance);
        }
        focus = Vector3.SmoothDamp(focus, followTarget.position, ref focusVelocity, followSmoothTime);
        UpdateCameraPosition(Time.deltaTime);
    }

    void UpdateCameraPosition(float deltaTime)
    {
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 backward = rotation * Vector3.back;
        float allowedDistance = distance;
        int count = Physics.SphereCastNonAlloc(focus, collisionRadius, backward, hits, distance,
            obstructionMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (hits[i].collider.transform.IsChildOf(player.transform)) continue;
            allowedDistance = Mathf.Min(allowedDistance, Mathf.Max(.05f, hits[i].distance - .05f));
        }
        // Pull in immediately at obstructions; only the return to the requested distance is smoothed.
        currentDistance = allowedDistance < currentDistance || deltaTime == 0f
            ? allowedDistance : Mathf.MoveTowards(currentDistance, allowedDistance, 8f * deltaTime);
        transform.SetPositionAndRotation(focus + backward * currentDistance, rotation);
        viewCamera.nearClipPlane = Mathf.Min(.1f, Mathf.Max(.01f, currentDistance * .25f));
    }
}
