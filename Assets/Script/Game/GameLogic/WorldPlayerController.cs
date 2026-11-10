using UnityEngine;
using Game.Domain.Character;

[DisallowMultipleComponent]
public sealed class WorldPlayerController : MonoBehaviour
{
    [SerializeField] CharacterController body;
    [SerializeField] WorldPlayerCamera followCamera;
    [SerializeField] WorldPlayerAnimation locomotion;
    [SerializeField] WorldPlayerInput inputReader;
    [SerializeField] MeshCollider waterSurface;
    [SerializeField, Min(.1f)] float walkSpeed = 1.3f;
    [SerializeField, Min(.1f)] float runSpeed = 5.5f;
    [SerializeField, Min(.1f)] float acceleration = 20f;
    [SerializeField, Min(.1f)] float braking = 28f;
    [SerializeField, Min(0f)] float airAcceleration = 6f;
    [SerializeField, Min(0f)] float turnSpeed = 720f;
    [SerializeField, Min(0f)] float jumpHeight = 1.2f;
    [SerializeField, Min(.1f)] float gravity = 25f;
    [SerializeField, Min(0f)] float groundSnapDistance = .25f;
    [SerializeField, Min(0f)] float coyoteTime = .1f;
    [SerializeField, Min(0f)] float jumpBuffer = .12f;
    [SerializeField] float rescueHeight = -100f;

    readonly RaycastHit[] groundProbeHits = new RaycastHit[8];
    Vector3 horizontalVelocity, spawnPosition;
    Quaternion spawnRotation;
    float verticalSpeed, stepOffset;
    float lastGroundedAt = float.NegativeInfinity;
    float jumpPressedAt = float.NegativeInfinity;
    enum MotionState { Locomotion, Climbing, Mantling, Swimming, Plunging, GroundAction }
    MotionState motionState;
    Vector3 climbNormal, mantleStart, mantleTarget;
    float climbAttachAfter;

    const float SwimDepth = .75f;
    const float SwimSpeed = 2.2f;
    const float ClimbSpeed = 1.25f;

    internal bool CanAcceptInput { get; private set; }
    internal CharacterModel Character { get; private set; }

    internal void BindCharacter(CharacterModel character)
    {
        if (Character != character && motionState != MotionState.Locomotion)
            EndSpecialMotion();
        // Re-entering a scene binds the session model without resetting its health.
        Character = character;
        locomotion.BindCharacter(character);
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
        Vector3 forward = followCamera.PlanarForward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        bool grounded = body.isGrounded && verticalSpeed <= 0f;
        Vector3 direction = forward * input.y + right * input.x;
        Vector3 attackDisplacement = locomotion.ConsumeAttackDisplacement();
        if (locomotion.SupportsSpecialMotion && Character != null &&
            MoveSpecial(input, direction, grounded, jump, deltaTime))
        {
            jumpPressedAt = lastGroundedAt = float.NegativeInfinity;
            UpdateMovementAnimation();
            return;
        }
        bool attackRequested = inputReader.NormalAttackPressed && Character != null;
        if (locomotion.UpdateNormalAttack(attackRequested, input.sqrMagnitude > .001f || jump, grounded, CanAcceptInput))
        {
            input = Vector2.zero;
            direction = Vector3.zero;
            horizontalVelocity = Vector3.zero;
            jumpPressedAt = float.NegativeInfinity;
        }
        Vector3 desiredVelocity = direction * (running ? runSpeed : walkSpeed);
        if (direction.sqrMagnitude > .001f)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * deltaTime);
            if (grounded)
            {
                // Slow down during a sharp turn so the feet do not run sideways into the new direction.
                float forwardSpeed = Mathf.Max(0f, Vector3.Dot(desiredVelocity, transform.forward));
                desiredVelocity = transform.forward * forwardSpeed;
            }
        }
        body.stepOffset = grounded ? stepOffset : 0f;
        if (grounded)
        {
            lastGroundedAt = Time.time;
            verticalSpeed = -2f;
        }
        bool gainingSpeed = desiredVelocity.sqrMagnitude > horizontalVelocity.sqrMagnitude;
        float rate = grounded ? (gainingSpeed ? acceleration : braking) : airAcceleration;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, desiredVelocity, rate * deltaTime);
        bool jumped = Time.time - jumpPressedAt <= jumpBuffer && Time.time - lastGroundedAt <= coyoteTime;
        if (jumped)
        {
            verticalSpeed = Mathf.Sqrt(2f * gravity * jumpHeight);
            lastGroundedAt = jumpPressedAt = float.NegativeInfinity;
            body.stepOffset = 0f;
        }
        verticalSpeed = Mathf.Max(verticalSpeed - gravity * deltaTime, -45f);

        Vector3 planarDisplacement = grounded && CanAcceptInput && !jumped && attackDisplacement.sqrMagnitude > 0f
            ? attackDisplacement : horizontalVelocity * deltaTime;
        float verticalDisplacement = verticalSpeed * deltaTime;
        if (grounded && !jumped && groundSnapDistance > 0f && planarDisplacement.sqrMagnitude > 0f)
        {
            verticalDisplacement = Mathf.Min(verticalDisplacement, -GetGroundSnapDrop(planarDisplacement));
        }

        // Keep the correction in the same sweep so collision flags describe the final move.
        CollisionFlags collision = body.Move(planarDisplacement + Vector3.up * verticalDisplacement);
        if ((collision & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;
        if ((collision & CollisionFlags.Below) != 0 && verticalSpeed < 0f) verticalSpeed = -2f;
        UpdateMovementAnimation();
    }

    void UpdateMovementAnimation()
    {
        locomotion.UpdateLocomotion(body.isGrounded && verticalSpeed <= 0f, HorizontalSpeed, verticalSpeed, walkSpeed, runSpeed);
        if (transform.position.y < rescueHeight) Respawn();
    }

    bool MoveSpecial(Vector2 input, Vector3 direction, bool grounded, bool jump, float deltaTime)
    {
        if (locomotion.HasSpecialMotion(WorldMotion.SwimIdle) && TryGetSwimHeight(out float swimHeight))
        {
            motionState = MotionState.Swimming;
            MoveSwimming(direction, swimHeight, deltaTime);
            return true;
        }
        if (motionState == MotionState.Swimming) EndSpecialMotion();

        switch (motionState)
        {
            case MotionState.Mantling:
                MoveMantle();
                return true;
            case MotionState.Climbing:
                return MoveClimbing(input, jump, deltaTime);
            case MotionState.Plunging:
                MovePlunge(deltaTime);
                return true;
            case MotionState.GroundAction:
                return MoveGroundAction(grounded, input.sqrMagnitude > .001f || jump, deltaTime);
        }

        if (!CanAcceptInput) return false;
        if (!grounded && inputReader.NormalAttackPressed && locomotion.HasSpecialMotion(WorldMotion.PlungeStart))
        {
            motionState = MotionState.Plunging;
            locomotion.PlaySpecialMotion(WorldMotion.PlungeStart);
            MovePlunge(deltaTime);
            return true;
        }
        WorldMotion requestedAction = inputReader.BurstPressed ? WorldMotion.Burst : WorldMotion.None;
        if (inputReader.SkillPressed && requestedAction == WorldMotion.None)
            requestedAction = locomotion.HasSpecialMotion(WorldMotion.SkillStart) ? WorldMotion.SkillStart : WorldMotion.SkillStrike;
        if (grounded && locomotion.HasSpecialMotion(requestedAction))
        {
            motionState = MotionState.GroundAction;
            locomotion.PlaySpecialMotion(requestedAction);
            return MoveGroundAction(true, false, deltaTime);
        }
        if (!locomotion.HasSpecialMotion(WorldMotion.ClimbIdle) || jump || Time.time < climbAttachAfter || direction.sqrMagnitude < .01f ||
            !TryGetClimbWall(direction.normalized, out climbNormal)) return false;

        motionState = MotionState.Climbing;
        return MoveClimbing(input, false, deltaTime);
    }

    bool TryGetClimbWall(Vector3 direction, out Vector3 normal)
    {
        Vector3 chest = transform.position + Vector3.up * (body.height * .62f);
        if (Physics.SphereCast(chest, .08f, direction, out RaycastHit hit, body.radius + .24f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            && !hit.collider.transform.IsChildOf(transform) && Mathf.Abs(hit.normal.y) < .35f)
        {
            normal = Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
            return Vector3.Dot(direction, -normal) > .6f;
        }
        normal = Vector3.zero;
        return false;
    }

    bool MoveClimbing(Vector2 input, bool jump, float deltaTime)
    {
        if (jump)
        {
            EndSpecialMotion();
            climbAttachAfter = Time.time + .25f;
            jumpPressedAt = lastGroundedAt = float.NegativeInfinity;
            horizontalVelocity = climbNormal * 2f;
            verticalSpeed = 3f;
            return false;
        }
        if (!TryGetClimbWall(-climbNormal, out Vector3 normal))
        {
            if (input.y > .1f && TryStartMantle())
            {
                MoveMantle();
                return true;
            }
            EndSpecialMotion();
            climbAttachAfter = Time.time + .2f;
            return false;
        }
        climbNormal = normal;
        transform.rotation = Quaternion.LookRotation(-climbNormal);
        horizontalVelocity = Vector3.zero;
        verticalSpeed = 0f;
        body.stepOffset = 0f;
        Vector3 motion = (transform.right * input.x + Vector3.up * input.y) * ClimbSpeed - climbNormal * .5f;
        CollisionFlags collision = body.Move(motion * deltaTime);
        if (input.y < -.1f && (collision & CollisionFlags.Below) != 0)
        {
            EndSpecialMotion();
            climbAttachAfter = Time.time + .2f;
        }
        else
        {
            WorldMotion clip = input.sqrMagnitude < .01f ? WorldMotion.ClimbIdle
                : Mathf.Abs(input.x) > Mathf.Abs(input.y)
                    ? (input.x > 0f ? WorldMotion.ClimbRight : WorldMotion.ClimbLeft)
                    : (input.y > 0f ? WorldMotion.ClimbUp : WorldMotion.ClimbDown);
            locomotion.PlaySpecialMotion(clip);
        }
        return true;
    }

    bool TryStartMantle()
    {
        Vector3 beyond = transform.position - climbNormal * (body.radius * 2f + .12f);
        Vector3 origin = beyond + Vector3.up * (body.height + .1f);
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit top, body.height,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            || top.normal.y < Mathf.Cos(body.slopeLimit * Mathf.Deg2Rad)) return false;

        Vector3 landing = top.point + Vector3.up * (body.skinWidth + .025f);
        Vector3 lower = landing + Vector3.up * body.radius;
        Vector3 upper = landing + Vector3.up * (body.height - body.radius);
        if (Physics.CheckCapsule(lower, upper, body.radius * .9f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;

        motionState = MotionState.Mantling;
        mantleStart = transform.position;
        mantleTarget = landing;
        locomotion.PlaySpecialMotion(WorldMotion.Mantle);
        return true;
    }

    void MoveMantle()
    {
        float progress = locomotion.SpecialMotionProgress;
        // Raise the capsule clear of the lip before moving inward over the ledge.
        Vector3 raised = new Vector3(mantleStart.x, mantleTarget.y, mantleStart.z);
        Vector3 target = progress < .55f
            ? Vector3.Lerp(mantleStart, raised, Mathf.SmoothStep(0f, 1f, progress / .55f))
            : Vector3.Lerp(raised, mantleTarget, Mathf.SmoothStep(0f, 1f, (progress - .55f) / .45f));
        body.Move(target - transform.position);
        if (progress >= 1f) EndSpecialMotion();
    }

    bool TryGetSwimHeight(out float surfaceHeight)
    {
        surfaceHeight = 0f;
        if (waterSurface == null) return false;
        Bounds bounds = waterSurface.bounds;
        Vector3 position = transform.position;
        float minDepth = motionState == MotionState.Swimming ? .4f : .65f;
        if (position.y > bounds.max.y - minDepth || position.x < bounds.min.x || position.x > bounds.max.x
            || position.z < bounds.min.z || position.z > bounds.max.z) return false;
        Vector3 origin = new Vector3(position.x, bounds.max.y + 1f, position.z);
        // Query the actual mesh because the map contains water patches at different elevations.
        if (!waterSurface.Raycast(new Ray(origin, Vector3.down), out RaycastHit water, bounds.size.y + 2f)
            || water.point.y - position.y < minDepth) return false;
        surfaceHeight = water.point.y;
        // A fast plunge can put the whole capsule below the water query origin.
        int hits = Physics.RaycastNonAlloc(water.point + Vector3.up * .05f, Vector3.down, groundProbeHits,
            .68f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits; i++)
            if (!groundProbeHits[i].collider.transform.IsChildOf(transform)) return false;
        return true;
    }

    void MoveSwimming(Vector3 direction, float surfaceHeight, float deltaTime)
    {
        bool moving = direction.sqrMagnitude > .01f;
        locomotion.PlaySpecialMotion(moving ? WorldMotion.SwimForward : WorldMotion.SwimIdle);
        if (moving)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * deltaTime);
        body.stepOffset = 0f;
        horizontalVelocity = Vector3.zero;
        verticalSpeed = 0f;
        float y = Mathf.MoveTowards(transform.position.y, surfaceHeight - SwimDepth, 5f * deltaTime);
        body.Move(direction * (SwimSpeed * deltaTime) + Vector3.up * (y - transform.position.y));
    }

    void MovePlunge(float deltaTime)
    {
        if (locomotion.SpecialMotion == WorldMotion.PlungeStart && locomotion.SpecialMotionProgress >= 1f)
            locomotion.PlaySpecialMotion(WorldMotion.PlungeLoop);
        horizontalVelocity = Vector3.zero;
        verticalSpeed = -18f;
        body.stepOffset = 0f;
        if ((body.Move(Vector3.up * (verticalSpeed * deltaTime)) & CollisionFlags.Below) == 0) return;
        verticalSpeed = -2f;
        motionState = MotionState.GroundAction;
        locomotion.PlaySpecialMotion(WorldMotion.PlungeImpact);
    }

    bool MoveGroundAction(bool grounded, bool wantsMovement, float deltaTime)
    {
        if (!grounded || !CanAcceptInput)
        {
            EndSpecialMotion();
            return false;
        }
        WorldMotion current = locomotion.SpecialMotion;
        float progress = locomotion.SpecialMotionProgress;
        bool recovery = current == WorldMotion.SkillRecovery || current == WorldMotion.BurstRecovery
            || current == WorldMotion.PlungeRecovery;
        if (progress >= 1f || (recovery && wantsMovement && progress >= .25f))
        {
            WorldMotion next = current switch
            {
                WorldMotion.SkillStart => WorldMotion.SkillStrike,
                WorldMotion.SkillStrike => WorldMotion.SkillRecovery,
                WorldMotion.Burst => WorldMotion.BurstRecovery,
                WorldMotion.PlungeImpact => WorldMotion.PlungeRecovery,
                _ => WorldMotion.None
            };
            if (!locomotion.HasSpecialMotion(next))
            {
                EndSpecialMotion();
                return false;
            }
            locomotion.PlaySpecialMotion(next);
        }
        horizontalVelocity = Vector3.zero;
        verticalSpeed = -2f;
        body.Move(Vector3.up * (verticalSpeed * deltaTime));
        return true;
    }

    void EndSpecialMotion()
    {
        motionState = MotionState.Locomotion;
        locomotion.PlaySpecialMotion(WorldMotion.None);
        horizontalVelocity = Vector3.zero;
        verticalSpeed = 0f;
    }

    float GetGroundSnapDrop(Vector3 planarDisplacement)
    {
        Vector3 scale = transform.lossyScale;
        float radius = body.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float halfHeight = Mathf.Max(body.height * Mathf.Abs(scale.y) * .5f, radius);
        Vector3 lowerCenter = transform.TransformPoint(body.center) - Vector3.up * (halfHeight - radius);
        float minUp = Mathf.Max(.01f, Mathf.Cos(body.slopeLimit * Mathf.Deg2Rad));
        int hitCount = Physics.RaycastNonAlloc(
            lowerCenter + planarDisplacement, Vector3.down, groundProbeHits,
            radius / minUp + groundSnapDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        RaycastHit support = default;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = groundProbeHits[i];
            Collider collider = hit.collider;
            if (hit.distance >= nearestDistance || collider.transform.IsChildOf(transform)
                || Physics.GetIgnoreLayerCollision(gameObject.layer, collider.gameObject.layer)
                || Physics.GetIgnoreCollision(body, collider)) continue;
            nearestDistance = hit.distance;
            support = hit;
        }
        if (support.normal.y < minUp) return 0f;

        // The capsule touches a slope before its lowest point reaches the surface under its center.
        float drop = support.distance - radius / support.normal.y;
        return drop > 0f && drop <= groundSnapDistance ? drop + body.skinWidth : 0f;
    }

    void Respawn()
    {
        EndSpecialMotion();
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
        EndSpecialMotion();
        CanAcceptInput = false;
        horizontalVelocity = Vector3.zero;
        verticalSpeed = 0f;
        lastGroundedAt = jumpPressedAt = float.NegativeInfinity;
    }
}
