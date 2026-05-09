using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SlimePlayerAbilities))]
public class SlimeMovementController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform actorRoot = null;
    [SerializeField] private Rigidbody driveRigidbody = null;
    [SerializeField] private BoneSphere boneSphere = null;
    [SerializeField] private SlimePlayerAbilities abilities = null;
    [SerializeField] private Transform cameraTransform = null;
    [SerializeField] private bool preferBoneSphereRoot = true;

    [Header("Input")]
    [SerializeField] private bool readKeyboardInput = true;
    [SerializeField] private KeyCode forwardKey = KeyCode.W;
    [SerializeField] private KeyCode backKey = KeyCode.S;
    [SerializeField] private KeyCode leftKey = KeyCode.A;
    [SerializeField] private KeyCode rightKey = KeyCode.D;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private bool cameraRelativeMovement = true;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4.5f;
    [SerializeField] private float acceleration = 22f;
    [SerializeField] private float airControl = 0.45f;
    [SerializeField] private bool rotateDrivenBody = false;
    [SerializeField] private float turnSpeed = 14f;

    [Header("Softbody Squeeze")]
    [SerializeField] private bool driveWholeSoftbody = true;
    [SerializeField, Range(0f, 1f)] private float outerBodyDriveWeight = 0.18f;
    [SerializeField] private bool pressureAwareSqueeze = true;
    [SerializeField] private float squeezeProbeRadius = 0.22f;
    [SerializeField] private float squeezeProbeDistance = 0.35f;
    [SerializeField] private float squeezeCompressionAcceleration = 7f;
    [SerializeField] private float squeezeDamping = 2f;
    [SerializeField] private LayerMask squeezeMask = ~0;

    [Header("Thresholds")]
    [SerializeField] private float inputAxisThreshold = 0.01f;

    [Header("Jump")]
    [SerializeField] private float jumpSpeed = 6.5f;
    [SerializeField] private float groundedStickVelocity = -2f;
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float jumpBufferTime = 0.12f;

    [Header("Sticky Wall")]
    [SerializeField] private bool stickyWallEnabled = true;
    [SerializeField] private SlimeMaterialType stickyMaterial = SlimeMaterialType.Sticky;
    [SerializeField] private LayerMask stickyWallMask = ~0;
    [SerializeField] private bool requireStickyWallMarker = true;
    [SerializeField, Range(0f, 1f)] private float stickyWallMaxNormalY = 0.35f;
    [SerializeField] private float stickyWallContactMemory = 0.18f;
    [SerializeField] private float stickyWallSlideSpeed = 0.15f;
    [SerializeField] private float stickyWallGripAcceleration = 30f;
    [SerializeField] private float wallJumpUpSpeed = 6.5f;
    [SerializeField] private float wallJumpAwaySpeed = 4.8f;
    [SerializeField] private float wallJumpLockout = 0.12f;

    [Header("Impact Push")]
    [SerializeField] private bool impactPushEnabled = true;
    [SerializeField] private LayerMask impactPushMask = ~0;
    [SerializeField] private bool requireImpactObject = true;
    [SerializeField] private float minRunImpactSpeed = 2.4f;
    [SerializeField] private float minAirImpactSpeed = 1.4f;
    [SerializeField] private float baseImpactImpulse = 4f;
    [SerializeField] private float impulsePerSpeed = 3.5f;
    [SerializeField] private float maxImpactImpulse = 28f;
    [SerializeField] private float airborneImpactMultiplier = 1.35f;
    [SerializeField] private float massCompensation = 0.65f;
    [SerializeField] private float upwardImpulseFraction = 0.08f;
    [SerializeField] private float selfRecoilVelocity = 0.25f;
    [SerializeField] private float impactCooldown = 0.14f;

    [Header("Ground Check")]
    [SerializeField] private Vector3 groundCheckOffset = new Vector3(0f, -0.55f, 0f);
    [SerializeField] private float groundCheckRadius = 0.25f;
    [SerializeField] private float groundProbeDistance = 0.08f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Form Speed")]
    [SerializeField] private float splitSpeedMultiplier = 1.15f;
    [SerializeField] private float mergedSpeedMultiplier = 1f;
    [SerializeField] private float stretchSpeedMultiplier = 0.85f;
    [SerializeField] private float bridgeSpeedMultiplier = 0.85f;
    [SerializeField, Range(0.1f, 1f)] private float fullCarrySpeedMultiplier = 0.78f;

    private readonly Collider[] groundHits = new Collider[16];
    private readonly RaycastHit[] groundRayHits = new RaycastHit[8];
    private readonly Rigidbody[] softbodyBodies = new Rigidbody[7];
    private readonly Collider[] softbodyColliders = new Collider[7];
    private readonly Collider[] squeezeHits = new Collider[24];
    private readonly RaycastHit[] squeezeCastHits = new RaycastHit[12];
    private readonly Dictionary<Rigidbody, float> lastImpactPushTimes = new Dictionary<Rigidbody, float>();

    private Vector2 moveInput;
    private float lastGroundedTime = -100f;
    private float jumpQueuedTime = -100f;
    private float lastStickyWallContactTime = -100f;
    private float lastWallJumpTime = -100f;
    private Vector3 stickyWallNormal = Vector3.zero;
    private SlimeStickyWall activeStickyWall;
    private bool jumpQueued;
    private bool grounded;
    private bool stickyWallActive;
    private bool warnedMissingRigidbody;
    private int softbodyBodyCount;

    public Vector2 MoveInput => moveInput;
    public Vector3 Velocity => driveRigidbody != null ? driveRigidbody.velocity : Vector3.zero;
    public bool IsGrounded => grounded;
    public bool IsStickyWallActive => stickyWallActive;

    private void OnCollisionEnter(Collision collision)
    {
        HandleBodyPartImpact(collision, driveRigidbody);
    }

    private void OnCollisionStay(Collision collision)
    {
        HandleBodyPartImpact(collision, driveRigidbody);
    }

    private void Awake()
    {
        if (boneSphere == null)
            boneSphere = GetComponent<BoneSphere>();

        ResolveAbilities();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void Start()
    {
        TryResolveDriveRigidbody(false);
    }

    private void Update()
    {
        if (!readKeyboardInput)
            return;

        SetMoveInput(ReadWasdInput());

        if (Input.GetKeyDown(jumpKey))
            Jump();
    }

    private void FixedUpdate()
    {
        if (!TryResolveDriveRigidbody())
            return;

        UpdateGrounded();
        stickyWallActive = HasActiveStickyWallContact() && !grounded;

        if (abilities != null && abilities.IsCrushed)
        {
            DecelerateWhileDisabled();
            return;
        }

        Vector3 requestedMoveDirection = ResolveMoveDirection(moveInput);
        if (requestedMoveDirection.sqrMagnitude > 0.0001f)
        {
            abilities?.SetAimDirection(requestedMoveDirection);

            if (rotateDrivenBody)
                RotateToward(requestedMoveDirection);
        }

        bool hasMoveInput = requestedMoveDirection.sqrMagnitude > 0.0001f;
        Vector3 moveDirection = ResolvePressureAwareMoveDirection(requestedMoveDirection);
        MoveRigidbody(moveDirection, hasMoveInput, stickyWallActive);
        ApplyStickyWallGrip();

        if (pressureAwareSqueeze)
            ApplySqueezeForces(requestedMoveDirection);
    }

    public void SetMoveInput(Vector2 input)
    {
        input = Vector2.ClampMagnitude(input, 1f);

        if (Mathf.Abs(input.x) < inputAxisThreshold)
            input.x = 0f;

        if (Mathf.Abs(input.y) < inputAxisThreshold)
            input.y = 0f;

        moveInput = input;
    }

    public void SetMoveInput(float horizontal, float vertical)
    {
        SetMoveInput(new Vector2(horizontal, vertical));
    }

    public void StopMoveInput()
    {
        moveInput = Vector2.zero;
    }

    public void Jump()
    {
        jumpQueued = true;
        jumpQueuedTime = Time.time;
    }

    public void HandleBodyPartImpact(Collision collision, Rigidbody sourceBody)
    {
        RecordStickyWallContact(collision, sourceBody);

        if (!impactPushEnabled || collision == null || abilities == null || abilities.IsCrushed)
            return;

        Rigidbody targetBody = collision.rigidbody != null ? collision.rigidbody : (collision.collider != null ? collision.collider.attachedRigidbody : null);
        SlimeImpactObject impactObject = ResolveImpactObject(collision.collider, targetBody);
        if (!CanImpactPush(targetBody, collision.collider, impactObject))
            return;

        float lastPushTime;
        if (lastImpactPushTimes.TryGetValue(targetBody, out lastPushTime) && Time.time - lastPushTime < impactCooldown)
            return;

        Vector3 impactVelocity = ResolveImpactVelocity(sourceBody);
        Vector3 planarVelocity = Vector3.ProjectOnPlane(impactVelocity, Vector3.up);
        float impactSpeed = planarVelocity.magnitude;
        float requiredSpeed = impactObject != null ? impactObject.GetRequiredImpactSpeed(grounded) : (grounded ? minRunImpactSpeed : minAirImpactSpeed);
        if (impactSpeed < requiredSpeed)
            return;

        Vector3 sourcePosition = sourceBody != null
            ? sourceBody.worldCenterOfMass
            : (driveRigidbody != null ? driveRigidbody.worldCenterOfMass : transform.position);
        Vector3 pushDirection = ResolveImpactPushDirection(planarVelocity, sourcePosition, targetBody.worldCenterOfMass);
        if (pushDirection.sqrMagnitude <= 0.0001f)
            return;

        Vector3 contactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : targetBody.worldCenterOfMass;
        float impulse = CalculateImpactImpulse(impactSpeed, requiredSpeed, targetBody.mass, impactObject);
        float upwardLift = impactObject != null ? impactObject.UpwardLift : upwardImpulseFraction;
        Vector3 impulseVector = (pushDirection + Vector3.up * upwardLift).normalized * impulse;

        targetBody.AddForceAtPosition(impulseVector, contactPoint, ForceMode.Impulse);
        lastImpactPushTimes[targetBody] = Time.time;

        float recoilMultiplier = impactObject != null ? impactObject.SlimeRecoilMultiplier : 1f;
        if (selfRecoilVelocity > 0f && recoilMultiplier > 0f && driveRigidbody != null)
            AddVelocityChangeToDrivenBodies(-pushDirection * (selfRecoilVelocity * recoilMultiplier), true);
    }

    private bool CanImpactPush(Rigidbody targetBody, Collider targetCollider, SlimeImpactObject impactObject)
    {
        if (targetBody == null || targetBody.isKinematic || targetCollider == null)
            return false;

        if (requireImpactObject && impactObject == null)
            return false;

        if (impactObject != null && !impactObject.CanMoveFromSlimeImpact)
            return false;

        if (IsOwnCollider(targetCollider) || targetBody == driveRigidbody)
            return false;

        for (int i = 0; i < softbodyBodyCount; i++)
        {
            if (targetBody == softbodyBodies[i])
                return false;
        }

        int colliderLayerMask = 1 << targetCollider.gameObject.layer;
        int bodyLayerMask = 1 << targetBody.gameObject.layer;
        return (impactPushMask.value & (colliderLayerMask | bodyLayerMask)) != 0;
    }

    private SlimeImpactObject ResolveImpactObject(Collider targetCollider, Rigidbody targetBody)
    {
        if (targetCollider != null)
        {
            SlimeImpactObject impactObject = targetCollider.GetComponentInParent<SlimeImpactObject>();
            if (impactObject != null)
                return impactObject;
        }

        return targetBody != null ? targetBody.GetComponentInParent<SlimeImpactObject>() : null;
    }

    private Vector3 ResolveImpactVelocity(Rigidbody sourceBody)
    {
        Vector3 impactVelocity = sourceBody != null ? sourceBody.velocity : Vector3.zero;
        Vector3 drivenVelocity = driveRigidbody != null ? GetDrivenVelocity() : Vector3.zero;

        if (drivenVelocity.sqrMagnitude > impactVelocity.sqrMagnitude)
            impactVelocity = drivenVelocity;

        return impactVelocity;
    }

    private Vector3 ResolveImpactPushDirection(Vector3 planarVelocity, Vector3 sourcePosition, Vector3 targetPosition)
    {
        Vector3 direction = planarVelocity.sqrMagnitude > 0.0001f ? planarVelocity.normalized : Vector3.zero;
        Vector3 toTarget = Vector3.ProjectOnPlane(targetPosition - sourcePosition, Vector3.up);

        if (toTarget.sqrMagnitude > 0.0001f)
        {
            toTarget.Normalize();
            if (direction.sqrMagnitude <= 0.0001f || Vector3.Dot(direction, toTarget) < 0.2f)
                direction = toTarget;
        }

        return direction;
    }

    private float CalculateImpactImpulse(float impactSpeed, float requiredSpeed, float targetMass, SlimeImpactObject impactObject)
    {
        float speedBonus = Mathf.Max(0f, impactSpeed - requiredSpeed) * impulsePerSpeed;
        float impulse = Mathf.Max(0f, baseImpactImpulse + speedBonus);
        float volumeScale = impactObject == null || impactObject.ScaleWithSlimeVolume
            ? (abilities != null ? Mathf.Sqrt(Mathf.Max(0.25f, abilities.BodyVolume)) : 1f)
            : 1f;
        float massScale = impactObject == null || impactObject.ScaleWithOwnMass
            ? Mathf.Pow(Mathf.Max(1f, targetMass), Mathf.Clamp01(massCompensation))
            : 1f;

        if (!grounded)
            impulse *= airborneImpactMultiplier;

        impulse *= volumeScale * Mathf.Lerp(1f, 0.65f, abilities != null ? abilities.CarryLoad01 : 0f);
        impulse *= massScale;

        if (impactObject != null)
            return impactObject.ModifyImpactImpulse(impulse);

        return Mathf.Min(impulse, Mathf.Max(0f, maxImpactImpulse));
    }

    private Vector2 ReadWasdInput()
    {
        float horizontal = 0f;
        float vertical = 0f;

        if (Input.GetKey(leftKey))
            horizontal -= 1f;

        if (Input.GetKey(rightKey))
            horizontal += 1f;

        if (Input.GetKey(backKey))
            vertical -= 1f;

        if (Input.GetKey(forwardKey))
            vertical += 1f;

        return new Vector2(horizontal, vertical);
    }

    private void MoveRigidbody(Vector3 moveDirection, bool hasMoveInput, bool canWallJump)
    {
        Vector3 velocity = GetDrivenVelocity();
        Vector3 currentPlanarVelocity = new Vector3(velocity.x, 0f, velocity.z);
        float speed = moveSpeed * GetFormSpeedMultiplier();
        float control = grounded || canWallJump ? acceleration : acceleration * airControl;
        Vector3 targetPlanarVelocity = moveDirection * speed;
        Vector3 velocityDelta = targetPlanarVelocity - currentPlanarVelocity;
        Vector3 pushAcceleration = Vector3.ClampMagnitude(velocityDelta / Time.fixedDeltaTime, control);
        Vector3 jumpVelocityChange;
        bool jumped = ConsumeJumpIfReady(canWallJump, velocity, out jumpVelocityChange);
        bool includeWholeBody = hasMoveInput || driveWholeSoftbody;

        AddAccelerationToDrivenBodies(pushAcceleration, includeWholeBody);

        if (jumped)
            AddVelocityChangeToDrivenBodies(jumpVelocityChange, includeWholeBody);
        else if (grounded && velocity.y < groundedStickVelocity)
            AddVelocityChangeToDrivenBodies(Vector3.up * (groundedStickVelocity - velocity.y), false);
    }

    private bool ConsumeJumpIfReady(bool canWallJump, Vector3 velocity, out Vector3 velocityChange)
    {
        velocityChange = Vector3.zero;

        if (jumpQueued && Time.time - jumpQueuedTime > jumpBufferTime)
            jumpQueued = false;

        bool canUseGroundJump = jumpQueued && Time.time - lastGroundedTime <= coyoteTime;
        bool canUseWallJump = jumpQueued
            && canWallJump
            && CanUseCurrentStickyWallJump()
            && Time.time - lastStickyWallContactTime <= stickyWallContactMemory
            && Time.time - lastWallJumpTime >= wallJumpLockout;

        if (!canUseGroundJump && !canUseWallJump)
            return false;

        jumpQueued = false;
        grounded = false;

        if (canUseWallJump && !canUseGroundJump)
        {
            lastWallJumpTime = Time.time;
            stickyWallActive = false;
            velocityChange = CalculateWallJumpVelocityChange(velocity);
            return true;
        }

        velocityChange = Vector3.up * (jumpSpeed - velocity.y);
        return true;
    }

    private Vector3 CalculateWallJumpVelocityChange(Vector3 velocity)
    {
        Vector3 wallNormal = GetStickyWallHorizontalNormal();
        if (wallNormal.sqrMagnitude <= 0.0001f)
            wallNormal = Vector3.ProjectOnPlane(-ResolveMoveDirection(moveInput), Vector3.up).normalized;

        if (wallNormal.sqrMagnitude <= 0.0001f)
            wallNormal = Vector3.back;

        Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
        float currentAwaySpeed = Vector3.Dot(planarVelocity, wallNormal);
        float targetAwaySpeed = wallJumpAwaySpeed * GetActiveStickyWallJumpAwayMultiplier();
        float targetUpSpeed = wallJumpUpSpeed * GetActiveStickyWallJumpUpMultiplier();
        Vector3 awayChange = wallNormal * Mathf.Max(0f, targetAwaySpeed - currentAwaySpeed);
        Vector3 upChange = Vector3.up * (targetUpSpeed - velocity.y);
        return awayChange + upChange;
    }

    private void UpdateGrounded()
    {
        grounded = CheckGroundedFromDrivenBody() || CheckGroundedFromProbe();

        if (grounded)
            lastGroundedTime = Time.time;
    }

    private Vector3 ResolveMoveDirection(Vector2 input)
    {
        if (input.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;

        if (cameraRelativeMovement && cameraTransform != null)
        {
            forward = cameraTransform.forward;
            right = cameraTransform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
        }

        return Vector3.ClampMagnitude(right * input.x + forward * input.y, 1f);
    }

    private void RecordStickyWallContact(Collision collision, Rigidbody sourceBody)
    {
        if (!stickyWallEnabled || collision == null || !IsStickySlime())
            return;

        Collider otherCollider = collision.collider;
        if (otherCollider == null || IsOwnCollider(otherCollider) || !IsLayerInMask(otherCollider.gameObject.layer, stickyWallMask))
            return;

        SlimeStickyWall stickyWall = otherCollider.GetComponentInParent<SlimeStickyWall>();
        if (!CanStickToWall(stickyWall))
            return;

        Vector3 sourceCenter = sourceBody != null
            ? sourceBody.worldCenterOfMass
            : (driveRigidbody != null ? driveRigidbody.worldCenterOfMass : transform.position);
        Vector3 bestNormal = Vector3.zero;
        float bestScore = 0f;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            Vector3 normal = contact.normal;
            Vector3 toSlime = sourceCenter - contact.point;

            if (toSlime.sqrMagnitude > 0.0001f && Vector3.Dot(normal, toSlime) < 0f)
                normal = -normal;

            if (!IsStickyWallNormal(normal))
                continue;

            Vector3 horizontalNormal = Vector3.ProjectOnPlane(normal, Vector3.up);
            float score = horizontalNormal.sqrMagnitude * (1f - Mathf.Abs(normal.y));
            if (score <= bestScore)
                continue;

            bestScore = score;
            bestNormal = horizontalNormal.normalized;
        }

        if (bestNormal.sqrMagnitude <= 0.0001f)
            return;

        stickyWallNormal = bestNormal;
        activeStickyWall = stickyWall;
        lastStickyWallContactTime = Time.time;
    }

    private bool HasActiveStickyWallContact()
    {
        return stickyWallEnabled
            && IsStickySlime()
            && stickyWallNormal.sqrMagnitude > 0.0001f
            && CanStickToWall(activeStickyWall)
            && Time.time - lastStickyWallContactTime <= stickyWallContactMemory
            && Time.time - lastWallJumpTime >= wallJumpLockout;
    }

    private bool CanStickToWall(SlimeStickyWall stickyWall)
    {
        if (requireStickyWallMarker && stickyWall == null)
            return false;

        return stickyWall == null || stickyWall.CanStick;
    }

    private bool CanUseCurrentStickyWallJump()
    {
        if (requireStickyWallMarker && activeStickyWall == null)
            return false;

        return activeStickyWall == null || activeStickyWall.CanWallJump;
    }

    private float GetActiveStickyWallGripMultiplier()
    {
        return activeStickyWall != null ? activeStickyWall.GripMultiplier : 1f;
    }

    private float GetActiveStickyWallSlideSpeedMultiplier()
    {
        return activeStickyWall != null ? activeStickyWall.SlideSpeedMultiplier : 1f;
    }

    private float GetActiveStickyWallJumpUpMultiplier()
    {
        return activeStickyWall != null ? activeStickyWall.WallJumpUpMultiplier : 1f;
    }

    private float GetActiveStickyWallJumpAwayMultiplier()
    {
        return activeStickyWall != null ? activeStickyWall.WallJumpAwayMultiplier : 1f;
    }

    private bool IsStickySlime()
    {
        return abilities != null && abilities.CurrentMaterial == stickyMaterial;
    }

    private bool IsStickyWallNormal(Vector3 normal)
    {
        Vector3 horizontalNormal = Vector3.ProjectOnPlane(normal, Vector3.up);
        return horizontalNormal.sqrMagnitude > 0.25f && Mathf.Abs(normal.y) <= stickyWallMaxNormalY;
    }

    private Vector3 GetStickyWallHorizontalNormal()
    {
        Vector3 horizontalNormal = Vector3.ProjectOnPlane(stickyWallNormal, Vector3.up);
        return horizontalNormal.sqrMagnitude > 0.0001f ? horizontalNormal.normalized : Vector3.zero;
    }

    private void ApplyStickyWallGrip()
    {
        if (!stickyWallActive || driveRigidbody == null)
            return;

        Vector3 wallNormal = GetStickyWallHorizontalNormal();
        if (wallNormal.sqrMagnitude <= 0.0001f)
            return;

        AddAccelerationToDrivenBodies(-wallNormal * (stickyWallGripAcceleration * GetActiveStickyWallGripMultiplier()), true);

        Vector3 velocity = GetDrivenVelocity();
        float slideSpeed = stickyWallSlideSpeed * GetActiveStickyWallSlideSpeedMultiplier();
        if (velocity.y < -slideSpeed)
            AddVelocityChangeToDrivenBodies(Vector3.up * (-slideSpeed - velocity.y), true);
    }

    private Vector3 ResolvePressureAwareMoveDirection(Vector3 desiredDirection)
    {
        if (!pressureAwareSqueeze || driveRigidbody == null || desiredDirection.sqrMagnitude <= 0.0001f)
            return desiredDirection;

        float inputStrength = Mathf.Clamp01(desiredDirection.magnitude);
        Vector3 direction = desiredDirection.normalized;
        float radius = Mathf.Max(0.01f, squeezeProbeRadius);
        float distance = Mathf.Max(0f, squeezeProbeDistance);

        if (distance <= 0f)
            return desiredDirection;

        int hitCount = Physics.SphereCastNonAlloc(
            driveRigidbody.worldCenterOfMass,
            radius,
            direction,
            squeezeCastHits,
            distance,
            squeezeMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = squeezeCastHits[i];
            if (hit.collider == null || IsOwnCollider(hit.collider))
                continue;

            float intoSurface = Vector3.Dot(direction, -hit.normal);
            if (intoSurface <= 0.05f)
                continue;

            Vector3 slideDirection = Vector3.ProjectOnPlane(direction, hit.normal);
            if (slideDirection.sqrMagnitude <= 0.0001f)
                return Vector3.zero;

            direction = Vector3.Lerp(direction, slideDirection.normalized, Mathf.Clamp01(intoSurface)).normalized;
        }

        return direction * inputStrength;
    }

    private void ApplySqueezeForces(Vector3 desiredDirection)
    {
        if (driveRigidbody == null || softbodyBodyCount == 0 || squeezeProbeRadius <= 0f || desiredDirection.sqrMagnitude <= 0.0001f)
            return;

        Vector3 center = driveRigidbody.worldCenterOfMass;
        Vector3 desired = desiredDirection.sqrMagnitude > 0.0001f ? desiredDirection.normalized : Vector3.zero;

        for (int i = 0; i < softbodyBodyCount; i++)
        {
            Rigidbody body = softbodyBodies[i];
            Collider bodyCollider = GetSoftbodyCollider(i);
            if (body == null || bodyCollider == null || bodyCollider.isTrigger)
                continue;

            Vector3 pressureDirection;
            float pressure01;
            if (!TryGetSqueezePressure(body, bodyCollider, desired, out pressureDirection, out pressure01))
                continue;

            body.AddForce(pressureDirection * (squeezeCompressionAcceleration * pressure01), ForceMode.Acceleration);
            ApplyRadialSqueezeDamping(body, center);
        }
    }

    private bool TryGetSqueezePressure(Rigidbody body, Collider bodyCollider, Vector3 desiredDirection, out Vector3 pressureDirection, out float pressure01)
    {
        pressureDirection = Vector3.zero;
        pressure01 = 0f;

        Vector3 bodyCenter = body.worldCenterOfMass;
        float radius = Mathf.Max(0.001f, squeezeProbeRadius);
        int hitCount = Physics.OverlapSphereNonAlloc(bodyCenter, radius, squeezeHits, squeezeMask, QueryTriggerInteraction.Ignore);

        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            Collider hit = squeezeHits[hitIndex];
            if (hit == null || IsOwnCollider(hit))
                continue;

            Vector3 separationDirection;
            float separationDistance;
            if (TryGetSeparation(body, bodyCollider, hit, bodyCenter, desiredDirection, radius, out separationDirection, out separationDistance))
            {
                if (IsGroundSupportSeparation(separationDirection))
                    continue;

                pressureDirection += separationDirection * separationDistance;
                pressure01 += separationDistance / radius;
            }
        }

        if (pressureDirection.sqrMagnitude <= 0.0001f)
            return false;

        pressureDirection.Normalize();
        pressure01 = Mathf.Clamp01(pressure01);
        return pressure01 > 0f;
    }

    private bool TryGetSeparation(
        Rigidbody body,
        Collider bodyCollider,
        Collider hit,
        Vector3 bodyCenter,
        Vector3 desiredDirection,
        float radius,
        out Vector3 separationDirection,
        out float separationDistance)
    {
        if (Physics.ComputePenetration(
            bodyCollider,
            body.position,
            body.rotation,
            hit,
            hit.transform.position,
            hit.transform.rotation,
            out separationDirection,
            out separationDistance))
        {
            return separationDistance > 0f;
        }

        if (CanUseClosestPoint(hit))
        {
            Vector3 closestPoint = hit.ClosestPoint(bodyCenter);
            Vector3 fromObstacle = bodyCenter - closestPoint;
            float distanceSqr = fromObstacle.sqrMagnitude;
            if (distanceSqr > 0.000001f && distanceSqr < radius * radius)
            {
                float distance = Mathf.Sqrt(distanceSqr);
                separationDirection = fromObstacle / distance;
                separationDistance = radius - distance;
                return separationDistance > 0f;
            }

            if (distanceSqr <= 0.000001f && desiredDirection.sqrMagnitude > 0.0001f)
            {
                separationDirection = -desiredDirection;
                separationDistance = radius;
                return true;
            }
        }

        separationDirection = Vector3.zero;
        separationDistance = 0f;
        return false;
    }

    private bool CanUseClosestPoint(Collider targetCollider)
    {
        if (targetCollider == null)
            return false;

        if (targetCollider is BoxCollider || targetCollider is SphereCollider || targetCollider is CapsuleCollider)
            return true;

        MeshCollider meshCollider = targetCollider as MeshCollider;
        return meshCollider != null && meshCollider.convex;
    }

    private bool IsGroundSupportSeparation(Vector3 separationDirection)
    {
        if (!grounded || separationDirection.y <= 0.6f)
            return false;

        Vector3 horizontal = new Vector3(separationDirection.x, 0f, separationDirection.z);
        return horizontal.sqrMagnitude < 0.25f;
    }

    private void ApplyRadialSqueezeDamping(Rigidbody body, Vector3 center)
    {
        if (body == driveRigidbody || squeezeDamping <= 0f)
            return;

        Vector3 toCenter = center - body.worldCenterOfMass;
        if (toCenter.sqrMagnitude <= 0.0001f)
            return;

        toCenter.Normalize();
        Vector3 relativeVelocity = body.velocity - driveRigidbody.velocity;
        float outwardSpeed = -Vector3.Dot(relativeVelocity, toCenter);
        if (outwardSpeed <= 0f)
            return;

        body.AddForce(toCenter * (outwardSpeed * squeezeDamping), ForceMode.Acceleration);
    }

    private Vector3 GetDrivenVelocity()
    {
        if (!driveWholeSoftbody || softbodyBodyCount <= 1)
            return driveRigidbody.velocity;

        Vector3 velocity = Vector3.zero;
        float totalWeight = 0f;

        for (int i = 0; i < softbodyBodyCount; i++)
        {
            Rigidbody body = softbodyBodies[i];
            if (body == null)
                continue;

            float weight = body == driveRigidbody ? 1f : outerBodyDriveWeight;
            if (weight <= 0f)
                continue;

            velocity += body.velocity * weight;
            totalWeight += weight;
        }

        return totalWeight > 0f ? velocity / totalWeight : driveRigidbody.velocity;
    }

    private void AddAccelerationToDrivenBodies(Vector3 accelerationToApply, bool includeOuterBodies)
    {
        if (!includeOuterBodies || !driveWholeSoftbody || softbodyBodyCount <= 1)
        {
            driveRigidbody.AddForce(accelerationToApply, ForceMode.Acceleration);
            return;
        }

        for (int i = 0; i < softbodyBodyCount; i++)
        {
            Rigidbody body = softbodyBodies[i];
            if (body == null)
                continue;

            float weight = body == driveRigidbody ? 1f : outerBodyDriveWeight;
            if (weight <= 0f)
                continue;

            body.AddForce(accelerationToApply * weight, ForceMode.Acceleration);
        }
    }

    private void AddVelocityChangeToDrivenBodies(Vector3 velocityChange, bool includeOuterBodies)
    {
        if (!includeOuterBodies || !driveWholeSoftbody || softbodyBodyCount <= 1)
        {
            driveRigidbody.AddForce(velocityChange, ForceMode.VelocityChange);
            return;
        }

        for (int i = 0; i < softbodyBodyCount; i++)
        {
            Rigidbody body = softbodyBodies[i];
            if (body == null)
                continue;

            float weight = body == driveRigidbody ? 1f : outerBodyDriveWeight;
            if (weight <= 0f)
                continue;

            body.AddForce(velocityChange * weight, ForceMode.VelocityChange);
        }
    }

    private void RotateToward(Vector3 moveDirection)
    {
        if (turnSpeed <= 0f || moveDirection.sqrMagnitude <= 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
        Quaternion newRotation = Quaternion.Slerp(driveRigidbody.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime);
        driveRigidbody.MoveRotation(newRotation);
    }

    private bool CheckGroundedFromDrivenBody()
    {
        if (driveRigidbody == null)
            return false;

        Collider[] colliders = driveRigidbody.GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider bodyCollider = colliders[i];
            if (bodyCollider == null || !bodyCollider.enabled || bodyCollider.isTrigger)
                continue;

            Bounds bounds = bodyCollider.bounds;
            float radius = Mathf.Clamp(Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.85f, 0.02f, groundCheckRadius);
            Vector3 origin = new Vector3(bounds.center.x, bounds.min.y + radius + 0.02f, bounds.center.z);
            float distance = radius + Mathf.Max(0.02f, groundProbeDistance);
            int hitCount = Physics.SphereCastNonAlloc(origin, radius, Vector3.down, groundRayHits, distance, groundMask, QueryTriggerInteraction.Ignore);

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                Collider hitCollider = groundRayHits[hitIndex].collider;
                if (hitCollider != null && !IsOwnCollider(hitCollider))
                    return true;
            }
        }

        return false;
    }

    private bool CheckGroundedFromProbe()
    {
        Transform probeRoot = actorRoot != null ? actorRoot : (driveRigidbody != null ? driveRigidbody.transform : transform);
        Vector3 center = probeRoot.TransformPoint(groundCheckOffset);
        int hitCount = Physics.OverlapSphereNonAlloc(center, groundCheckRadius, groundHits, groundMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = groundHits[i];
            if (hit != null && !IsOwnCollider(hit))
                return true;
        }

        return false;
    }

    private bool IsOwnCollider(Collider hit)
    {
        if (hit.transform == transform || hit.transform.IsChildOf(transform))
            return true;

        if (hit.attachedRigidbody == driveRigidbody)
            return true;

        for (int i = 0; i < softbodyBodyCount; i++)
        {
            if (hit.attachedRigidbody == softbodyBodies[i])
                return true;

            if (hit == softbodyColliders[i])
                return true;
        }

        return false;
    }

    private static bool IsLayerInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private bool TryResolveDriveRigidbody(bool logWarning = true)
    {
        Rigidbody softbodyRootRigidbody = GetSoftbodyRootRigidbody();

        if (preferBoneSphereRoot && softbodyRootRigidbody != null && driveRigidbody != softbodyRootRigidbody)
            driveRigidbody = softbodyRootRigidbody;

        if (driveRigidbody == null)
            driveRigidbody = softbodyRootRigidbody;

        if (driveRigidbody == null)
            driveRigidbody = GetComponent<Rigidbody>();

        if (driveRigidbody == null)
            driveRigidbody = GetComponentInChildren<Rigidbody>();

        if (driveRigidbody == null)
        {
            if (logWarning && !warnedMissingRigidbody)
            {
                warnedMissingRigidbody = true;
                Debug.LogWarning($"{nameof(SlimeMovementController)} needs a Rigidbody to drive. Assign one, or keep it on the same object as BoneSphere so it can use BoneSphere.root.", this);
            }

            return false;
        }

        ConfigureDriveRigidbody();
        CacheSoftbodyBodies();
        return true;
    }

    private void ResolveAbilities()
    {
        if (abilities != null)
            return;

        abilities = GetComponent<SlimePlayerAbilities>();

        if (abilities == null)
            abilities = GetComponentInParent<SlimePlayerAbilities>();

        if (abilities == null)
            abilities = GetComponentInChildren<SlimePlayerAbilities>();

        if (abilities == null)
            abilities = gameObject.AddComponent<SlimePlayerAbilities>();
    }

    private Rigidbody GetSoftbodyRootRigidbody()
    {
        if (boneSphere == null)
            boneSphere = GetComponent<BoneSphere>();

        if (boneSphere == null)
            boneSphere = GetComponentInParent<BoneSphere>();

        if (boneSphere == null)
            boneSphere = GetComponentInChildren<BoneSphere>();

        if (boneSphere == null || boneSphere.root == null)
            return null;

        return boneSphere.root.GetComponent<Rigidbody>();
    }

    private void ConfigureDriveRigidbody()
    {
        driveRigidbody.freezeRotation = true;

        if (driveRigidbody.interpolation == RigidbodyInterpolation.None)
            driveRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void CacheSoftbodyBodies()
    {
        softbodyBodyCount = 0;
        for (int i = 0; i < softbodyColliders.Length; i++)
            softbodyColliders[i] = null;

        if (boneSphere == null)
            return;

        AddSoftbodyBody(boneSphere.root);
        AddSoftbodyBody(boneSphere.x);
        AddSoftbodyBody(boneSphere.x2);
        AddSoftbodyBody(boneSphere.y);
        AddSoftbodyBody(boneSphere.y2);
        AddSoftbodyBody(boneSphere.z);
        AddSoftbodyBody(boneSphere.z2);
    }

    private void AddSoftbodyBody(GameObject boneObject)
    {
        if (boneObject == null || softbodyBodyCount >= softbodyBodies.Length)
            return;

        Rigidbody body = boneObject.GetComponent<Rigidbody>();
        if (body == null)
            return;

        for (int i = 0; i < softbodyBodyCount; i++)
        {
            if (softbodyBodies[i] == body)
                return;
        }

        body.freezeRotation = true;

        if (body.interpolation == RigidbodyInterpolation.None)
            body.interpolation = RigidbodyInterpolation.Interpolate;

        softbodyBodies[softbodyBodyCount] = body;
        softbodyColliders[softbodyBodyCount] = boneObject.GetComponent<Collider>();
        ConfigureImpactBodyPart(boneObject, body);
        softbodyBodyCount++;
    }

    private void ConfigureImpactBodyPart(GameObject boneObject, Rigidbody body)
    {
        if (boneObject == null || body == null)
            return;

        SlimeImpactBodyPart impactPart = boneObject.GetComponent<SlimeImpactBodyPart>();
        if (impactPart == null)
            impactPart = boneObject.AddComponent<SlimeImpactBodyPart>();

        impactPart.Init(this, body);
    }

    private Collider GetSoftbodyCollider(int bodyIndex)
    {
        if (bodyIndex < 0 || bodyIndex >= softbodyBodyCount)
            return null;

        Collider bodyCollider = softbodyColliders[bodyIndex];
        if (bodyCollider != null)
            return bodyCollider;

        Rigidbody body = softbodyBodies[bodyIndex];
        if (body == null)
            return null;

        bodyCollider = body.GetComponent<Collider>();
        softbodyColliders[bodyIndex] = bodyCollider;
        return bodyCollider;
    }

    private float GetFormSpeedMultiplier()
    {
        if (abilities == null)
            return 1f;

        float multiplier = 1f;

        if (abilities.IsSplit)
            multiplier *= splitSpeedMultiplier;

        if (abilities.IsMerged)
            multiplier *= mergedSpeedMultiplier;

        if (abilities.IsStretching)
            multiplier *= stretchSpeedMultiplier;

        if (abilities.IsBridging)
            multiplier *= bridgeSpeedMultiplier;

        if (abilities.CarryLoad01 > 0f)
            multiplier *= Mathf.Lerp(1f, fullCarrySpeedMultiplier, abilities.CarryLoad01);

        return multiplier;
    }

    private void DecelerateWhileDisabled()
    {
        Vector3 velocity = GetDrivenVelocity();
        Vector3 planarVelocity = new Vector3(velocity.x, 0f, velocity.z);
        Vector3 pushAcceleration = Vector3.ClampMagnitude(-planarVelocity / Time.fixedDeltaTime, acceleration);
        AddAccelerationToDrivenBodies(pushAcceleration, false);
    }

    private void OnDrawGizmosSelected()
    {
        Transform root = actorRoot != null ? actorRoot : (driveRigidbody != null ? driveRigidbody.transform : transform);
        Gizmos.color = grounded ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(root.TransformPoint(groundCheckOffset), groundCheckRadius);
    }
}
