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

    [Header("Thresholds")]
    [SerializeField] private float inputAxisThreshold = 0.01f;

    [Header("Jump")]
    [SerializeField] private float jumpSpeed = 6.5f;
    [SerializeField] private float groundedStickVelocity = -2f;
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float jumpBufferTime = 0.12f;

    [Header("Ground Check")]
    [SerializeField] private Vector3 groundCheckOffset = new Vector3(0f, -0.55f, 0f);
    [SerializeField] private float groundCheckRadius = 0.25f;
    [SerializeField] private float groundProbeDistance = 0.08f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Form Speed")]
    [SerializeField] private float splitSpeedMultiplier = 1.15f;
    [SerializeField] private float mergedSpeedMultiplier = 0.82f;
    [SerializeField] private float stretchSpeedMultiplier = 0.65f;
    [SerializeField] private float bridgeSpeedMultiplier = 0.85f;

    private readonly Collider[] groundHits = new Collider[16];
    private readonly RaycastHit[] groundRayHits = new RaycastHit[8];
    private readonly Rigidbody[] softbodyBodies = new Rigidbody[7];

    private Vector2 moveInput;
    private float lastGroundedTime = -100f;
    private float jumpQueuedTime = -100f;
    private bool jumpQueued;
    private bool grounded;
    private bool warnedMissingRigidbody;
    private int softbodyBodyCount;

    public Vector2 MoveInput => moveInput;
    public Vector3 Velocity => driveRigidbody != null ? driveRigidbody.velocity : Vector3.zero;
    public bool IsGrounded => grounded;

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

        if (abilities != null && abilities.IsCrushed)
        {
            DecelerateWhileDisabled();
            return;
        }

        Vector3 moveDirection = ResolveMoveDirection(moveInput);
        if (moveDirection.sqrMagnitude > 0.0001f)
        {
            abilities?.SetAimDirection(moveDirection);

            if (rotateDrivenBody)
                RotateToward(moveDirection);
        }

        MoveRigidbody(moveDirection);
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

    private void MoveRigidbody(Vector3 moveDirection)
    {
        Vector3 velocity = driveRigidbody.velocity;
        Vector3 currentPlanarVelocity = new Vector3(velocity.x, 0f, velocity.z);
        float speed = moveSpeed * GetFormSpeedMultiplier();
        float control = grounded ? acceleration : acceleration * airControl;
        Vector3 targetPlanarVelocity = moveDirection * speed;
        Vector3 velocityDelta = targetPlanarVelocity - currentPlanarVelocity;
        Vector3 pushAcceleration = Vector3.ClampMagnitude(velocityDelta / Time.fixedDeltaTime, control);
        bool jumped = ConsumeJumpIfReady();

        driveRigidbody.AddForce(pushAcceleration, ForceMode.Acceleration);

        if (jumped)
            driveRigidbody.AddForce(Vector3.up * (jumpSpeed - velocity.y), ForceMode.VelocityChange);
        else if (grounded && velocity.y < groundedStickVelocity)
            driveRigidbody.AddForce(Vector3.up * (groundedStickVelocity - velocity.y), ForceMode.VelocityChange);
    }

    private bool ConsumeJumpIfReady()
    {
        if (jumpQueued && Time.time - jumpQueuedTime > jumpBufferTime)
            jumpQueued = false;

        bool canUseBufferedJump = jumpQueued && Time.time - lastGroundedTime <= coyoteTime;
        if (!canUseBufferedJump)
            return false;

        jumpQueued = false;
        grounded = false;
        return true;
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
        }

        return false;
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
        softbodyBodyCount++;
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

        return multiplier;
    }

    private void DecelerateWhileDisabled()
    {
        Vector3 velocity = driveRigidbody.velocity;
        Vector3 planarVelocity = new Vector3(velocity.x, 0f, velocity.z);
        Vector3 pushAcceleration = Vector3.ClampMagnitude(-planarVelocity / Time.fixedDeltaTime, acceleration);
        driveRigidbody.AddForce(pushAcceleration, ForceMode.Acceleration);
    }

    private void OnDrawGizmosSelected()
    {
        Transform root = actorRoot != null ? actorRoot : (driveRigidbody != null ? driveRigidbody.transform : transform);
        Gizmos.color = grounded ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(root.TransformPoint(groundCheckOffset), groundCheckRadius);
    }
}
