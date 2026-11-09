using UnityEngine;
using Game.Domain.Character;

[DisallowMultipleComponent]
public sealed class WorldPlayerController : MonoBehaviour
{
    [SerializeField] CharacterController body;
    [SerializeField] WorldPlayerCamera followCamera;
    [SerializeField] WorldPlayerAnimation locomotion;
    [SerializeField] WorldPlayerInput inputReader;
    [SerializeField, Min(.1f)] float walkSpeed = 2.5f;
    [SerializeField, Min(.1f)] float runSpeed = 5.5f;
    [SerializeField, Min(.1f)] float acceleration = 20f;
    [SerializeField, Min(.1f)] float braking = 28f;
    [SerializeField, Min(0f)] float airAcceleration = 6f;
    [SerializeField, Min(0f)] float turnSpeed = 720f;
    [SerializeField, Min(0f)] float jumpHeight = 1.2f;
    [SerializeField, Min(.1f)] float gravity = 25f;
    [SerializeField, Min(0f)] float coyoteTime = .1f;
    [SerializeField, Min(0f)] float jumpBuffer = .12f;
    [SerializeField] float rescueHeight = -100f;

    Vector3 horizontalVelocity, spawnPosition;
    Quaternion spawnRotation;
    float verticalSpeed, stepOffset;
    float lastGroundedAt = float.NegativeInfinity;
    float jumpPressedAt = float.NegativeInfinity;

    internal bool CanAcceptInput { get; private set; }
    internal CharacterModel Character { get; private set; }

    internal void BindCharacter(CharacterModel character)
    {
        // Re-entering a scene binds the session model without resetting its health.
        Character = character;
    }
    internal bool IsGrounded => body.isGrounded;
    internal float VerticalSpeed => verticalSpeed;
    internal float HorizontalSpeed => Vector3.ProjectOnPlane(body.velocity, Vector3.up).magnitude;

    void Awake()
    {
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        stepOffset = body.stepOffset;
    }

    void Update()
    {
        AdvanceMovement(inputReader.Move, inputReader.SprintHeld, inputReader.JumpPressed);
    }

    // All input sources share one movement path; only the input reader controls gameplay access.
    internal void AdvanceMovement(Vector2 input, bool running, bool jump)
    {
        CanAcceptInput = inputReader.GameplayEnabled;
        if (CanAcceptInput)
        {
            input = Vector2.ClampMagnitude(input, 1f);
            if (jump) jumpPressedAt = Time.time;
        }
        else
        {
            // Menus must discard buffered actions, so a jump cannot fire when a page finishes closing.
            jumpPressedAt = float.NegativeInfinity;
            horizontalVelocity = Vector3.zero;
            input = Vector2.zero;
        }

        float deltaTime = Time.deltaTime;
        Vector3 forward = Vector3.ProjectOnPlane(followCamera.transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 direction = forward * input.y + right * input.x;
        Vector3 desiredVelocity = direction * (running ? runSpeed : walkSpeed);
        bool grounded = body.isGrounded && verticalSpeed <= 0f;
        body.stepOffset = grounded ? stepOffset : 0f;
        if (grounded)
        {
            lastGroundedAt = Time.time;
            verticalSpeed = -2f;
        }
        float rate = grounded ? (input.sqrMagnitude > .001f ? acceleration : braking) : airAcceleration;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, desiredVelocity, rate * deltaTime);
        if (Time.time - jumpPressedAt <= jumpBuffer && Time.time - lastGroundedAt <= coyoteTime)
        {
            verticalSpeed = Mathf.Sqrt(2f * gravity * jumpHeight);
            lastGroundedAt = jumpPressedAt = float.NegativeInfinity;
            body.stepOffset = 0f;
        }
        verticalSpeed = Mathf.Max(verticalSpeed - gravity * deltaTime, -45f);

        // Resolve gravity and planar movement together: CharacterController.Move is called once per frame.
        CollisionFlags collision = body.Move((horizontalVelocity + Vector3.up * verticalSpeed) * deltaTime);
        if ((collision & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;
        if ((collision & CollisionFlags.Below) != 0 && verticalSpeed < 0f) verticalSpeed = -2f;
        if (direction.sqrMagnitude > .001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * deltaTime);
        locomotion.UpdateLocomotion(body.isGrounded, HorizontalSpeed, verticalSpeed, walkSpeed, runSpeed);
        if (transform.position.y < rescueHeight) Respawn();
    }

    void Respawn()
    {
        // Teleport only with the capsule disabled; ordinary movement always goes through its collision sweep.
        body.enabled = false;
        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        body.enabled = true;
        horizontalVelocity = Vector3.zero;
        verticalSpeed = 0f;
        lastGroundedAt = jumpPressedAt = float.NegativeInfinity;
        followCamera.SnapToTarget();
    }

    void OnDisable()
    {
        CanAcceptInput = false;
        horizontalVelocity = Vector3.zero;
        verticalSpeed = 0f;
        lastGroundedAt = jumpPressedAt = float.NegativeInfinity;
    }
}
