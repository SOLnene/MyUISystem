using Cinemachine;
using Cinemachine.Utility;
using UnityEngine;

[DefaultExecutionOrder(-40)]
[DisallowMultipleComponent]
public sealed class WorldPlayerCamera : MonoBehaviour
{
    [SerializeField] WorldPlayerController player;
    [SerializeField] Transform followTarget;
    [SerializeField] CinemachineBrain brain;
    [SerializeField] CinemachineVirtualCamera explorationCamera;
    [SerializeField] Cinemachine3rdPersonFollow cameraBody;
    [SerializeField] WorldPlayerInput inputReader;
    [SerializeField, Min(.1f)] float distance = 4.5f;
    [SerializeField, Min(.1f)] float minimumDistance = 1.2f;
    [SerializeField, Min(.1f)] float maximumDistance = 7f;
    [SerializeField] float pitch = 15f;
    [SerializeField] float minimumPitch = -25f;
    [SerializeField] float maximumPitch = 65f;
    [SerializeField, Min(0f)] float sensitivity = 3f;
    [SerializeField, Min(0f)] float zoomSpeed = 3f;
    [SerializeField, Min(0f)] float rotationDamping = .12f;
    [SerializeField, Min(0f)] float zoomDamping = .28f;

    float yaw, requestedYaw, requestedPitch, requestedDistance;

    internal Vector3 PlanarForward => Quaternion.AngleAxis(yaw, Vector3.up) * Vector3.forward;

    void OnEnable()
    {
        requestedYaw = yaw = player.transform.eulerAngles.y;
        requestedPitch = pitch;
        requestedDistance = distance;
        SnapToTarget();
    }

    internal void SnapToTarget()
    {
        // The next camera update starts at the teleported target with fresh damping state.
        explorationCamera.PreviousStateIsValid = false;
    }

    void Update()
    {
        if (!inputReader.GameplayEnabled)
        {
            // Discard the pending motion when a menu or loss of focus takes over.
            requestedYaw = yaw;
            requestedPitch = pitch;
            requestedDistance = distance;
            return;
        }

        AdvanceView(inputReader.Look.x, inputReader.Look.y, inputReader.Zoom, inputReader.OrbitHeld);
        float deltaTime = Time.deltaTime;
        yaw += Damper.Damp(requestedYaw - yaw, rotationDamping, deltaTime);
        pitch += Damper.Damp(requestedPitch - pitch, rotationDamping, deltaTime);
        distance += Damper.Damp(requestedDistance - distance, zoomDamping, deltaTime);
    }

    internal void AdvanceView(float horizontalLook, float verticalLook, float scroll, bool orbit)
    {
        if (!inputReader.CanControlCamera) return;

        if (orbit)
        {
            requestedYaw += horizontalLook * sensitivity;
            requestedPitch = Mathf.Clamp(requestedPitch - verticalLook * sensitivity, minimumPitch, maximumPitch);
        }
        // Limit each input update so native or accumulated wheel deltas cannot skip the zoom range.
        float scrollStep = Mathf.Clamp(scroll, -1f, 1f);
        requestedDistance = Mathf.Clamp(requestedDistance - scrollStep * zoomSpeed * .1f, minimumDistance, maximumDistance);
    }

    void LateUpdate()
    {
        // The target is parented to the player, so apply its world rotation after the player turns.
        followTarget.rotation = Quaternion.Euler(pitch, yaw, 0f);
        cameraBody.CameraDistance = distance;
        brain.ManualUpdate();
    }
}
