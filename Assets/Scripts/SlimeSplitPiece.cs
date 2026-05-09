using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SlimeSplitPiece : MonoBehaviour
{
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [Header("Arrow Key Control")]
    [SerializeField] private bool keyboardControlEnabled = false;
    [SerializeField] private bool cameraRelativeMovement = true;
    [SerializeField] private KeyCode forwardKey = KeyCode.UpArrow;
    [SerializeField] private KeyCode backKey = KeyCode.DownArrow;
    [SerializeField] private KeyCode leftKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode rightKey = KeyCode.RightArrow;
    [SerializeField] private float moveSpeed = 3.8f;
    [SerializeField] private float acceleration = 18f;
    [SerializeField, Range(0f, 1f)] public float outerBodyDriveWeight = 0.25f;
    [SerializeField] private float inputAxisThreshold = 0.01f;

    [Header("Ability Control")]
    [SerializeField] private KeyCode splitKey = KeyCode.RightControl;
    [SerializeField] private KeyCode mergeKey = KeyCode.RightAlt;
    [SerializeField] private KeyCode stretchKey = KeyCode.RightShift;
    [SerializeField] private KeyCode alternateStretchKey = KeyCode.None;
    [SerializeField] private KeyCode bridgeKey = KeyCode.Return;
    [SerializeField] private KeyCode carryKey = KeyCode.Keypad0;

    [Header("Split Abilities")]
    [SerializeField] private float stretchLength = 2.2f;
    [SerializeField] private float stretchThickness = 0.58f;
    [SerializeField] private float stretchLerpSpeed = 10f;
    [SerializeField] private float stretchTurnSpeed = 14f;
    [SerializeField] private float bridgeLength = 2.6f;
    [SerializeField] private float bridgeWidth = 0.55f;
    [SerializeField] private float bridgeThickness = 0.16f;
    [SerializeField] private float bridgeDuration = 2f;
    [SerializeField] private float bridgeYOffset = 0.05f;
    [SerializeField] private float carryPickupRadius = 0.85f;
    [SerializeField] private LayerMask carryMask = ~0;
    [SerializeField] private Vector3 carryLocalOffset = new Vector3(0f, 0.05f, 0f);
    [SerializeField] private float carriedItemSpacing = 0.16f;
    [SerializeField] private float releaseDistance = 0.9f;
    [SerializeField] private float releaseUpOffset = 0.2f;
    [SerializeField] private float releaseVelocity = 2f;
    [SerializeField, Min(0)] private int maxSplitGeneration = 1;

    private MaterialPropertyBlock propertyBlock;
    private readonly List<CarriedItemState> carriedItems = new List<CarriedItemState>();
    private readonly Collider[] carryHits = new Collider[24];

    private sealed class CarriedItemState
    {
        public SlimeCarryable Item;
    }

    private SlimePlayerAbilities owner;
    private Vector3 launchVelocity;
    private Vector3 mergeStartCenter;
    private Vector3 mergeStartScale;
    private Vector3 baseLocalScale;
    private Vector3 targetLocalScale;
    private Vector3 lastAimDirection = Vector3.forward;
    private Vector2 moveInput;
    private Transform cameraTransform;
    private Transform runtimeCarryContainer;
    private Rigidbody[] bodies;
    private float mergeStartTime;
    private float mergeDuration;
    private float volume = 1f;
    private int splitGeneration;
    private bool launchApplied;
    private bool merging;
    private bool stretching;
    private SlimeSplitPiece mergeTargetPiece;
    private Renderer[] pieceRenderers;

    public bool IsMerging => merging;
    public bool IsKeyboardControlled => keyboardControlEnabled;
    public int SplitGeneration => splitGeneration;
    public bool CanSplitFurther => splitGeneration < maxSplitGeneration;
    public Vector3 CenterPosition => GetCenterPosition();
    public float Volume => Mathf.Max(0f, volume);

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
    }

    public void Init(
        SlimePlayerAbilities newOwner,
        Vector3 newLaunchVelocity,
        Material sharedMaterial,
        Color color,
        float newVolume,
        float newMergeDuration)
    {
        owner = newOwner;
        launchVelocity = newLaunchVelocity;
        mergeDuration = Mathf.Max(0f, newMergeDuration);
        volume = Mathf.Max(0f, newVolume);
        launchApplied = false;
        merging = false;
        stretching = false;
        keyboardControlEnabled = false;
        moveInput = Vector2.zero;
        cameraTransform = null;
        bodies = null;
        splitGeneration = 0;
        baseLocalScale = transform.localScale;
        targetLocalScale = baseLocalScale;

        pieceRenderers = GetComponentsInChildren<Renderer>(true);
        if (sharedMaterial != null)
        {
            for (int i = 0; i < pieceRenderers.Length; i++)
            {
                if (pieceRenderers[i] != null)
                    pieceRenderers[i].sharedMaterial = sharedMaterial;
            }
        }

        SetColor(color);
    }

    public void AddVolume(float volumeGain)
    {
        SetVolume(volume + Mathf.Max(0f, volumeGain));
    }

    public float TakeSplitVolume(float fraction, float minRemainingVolume)
    {
        float current = Volume;
        float available = Mathf.Max(0f, current - Mathf.Max(0f, minRemainingVolume));
        float taken = Mathf.Min(current * Mathf.Clamp01(fraction), available);
        if (taken <= 0f)
            return 0f;

        SetVolume(current - taken);
        return taken;
    }

    private void SetVolume(float newVolume)
    {
        float oldVolume = Mathf.Max(0.0001f, volume);
        volume = Mathf.Max(0f, newVolume);
        float scaleRatio = Mathf.Pow(Mathf.Max(0.0001f, volume) / oldVolume, 1f / 3f);

        baseLocalScale *= scaleRatio;
        targetLocalScale *= scaleRatio;

        if (!stretching && !merging)
            transform.localScale = baseLocalScale;
    }

    public void SetKeyboardControl(bool enabled, Transform movementCamera)
    {
        keyboardControlEnabled = enabled;
        cameraTransform = movementCamera;

        if (!enabled)
            moveInput = Vector2.zero;
    }

    public void SetKeyboardControlSettings(
        bool useCameraRelativeMovement,
        KeyCode newForwardKey,
        KeyCode newBackKey,
        KeyCode newLeftKey,
        KeyCode newRightKey,
        float newMoveSpeed,
        float newAcceleration,
        float newOuterBodyDriveWeight)
    {
        cameraRelativeMovement = useCameraRelativeMovement;
        forwardKey = newForwardKey;
        backKey = newBackKey;
        leftKey = newLeftKey;
        rightKey = newRightKey;
        moveSpeed = Mathf.Max(0f, newMoveSpeed);
        acceleration = Mathf.Max(0f, newAcceleration);
        outerBodyDriveWeight = Mathf.Clamp01(newOuterBodyDriveWeight);
    }

    public void SetAbilityControlSettings(
        KeyCode newSplitKey,
        KeyCode newMergeKey,
        KeyCode newStretchKey,
        KeyCode newAlternateStretchKey,
        KeyCode newBridgeKey,
        KeyCode newCarryKey)
    {
        splitKey = newSplitKey;
        mergeKey = newMergeKey;
        stretchKey = newStretchKey;
        alternateStretchKey = newAlternateStretchKey;
        bridgeKey = newBridgeKey;
        carryKey = newCarryKey;
    }

    public void SetAbilityTuning(
        float newStretchLength,
        float newStretchThickness,
        float newStretchLerpSpeed,
        float newStretchTurnSpeed,
        float newBridgeLength,
        float newBridgeWidth,
        float newBridgeThickness,
        float newBridgeDuration,
        float newBridgeYOffset,
        float newCarryPickupRadius,
        LayerMask newCarryMask,
        Vector3 newCarryLocalOffset,
        float newCarriedItemSpacing,
        float newReleaseDistance,
        float newReleaseUpOffset,
        float newReleaseVelocity)
    {
        stretchLength = Mathf.Max(0.1f, newStretchLength);
        stretchThickness = Mathf.Max(0.05f, newStretchThickness);
        stretchLerpSpeed = Mathf.Max(0f, newStretchLerpSpeed);
        stretchTurnSpeed = Mathf.Max(0f, newStretchTurnSpeed);
        bridgeLength = Mathf.Max(0.05f, newBridgeLength);
        bridgeWidth = Mathf.Max(0.05f, newBridgeWidth);
        bridgeThickness = Mathf.Max(0.02f, newBridgeThickness);
        bridgeDuration = Mathf.Max(0.05f, newBridgeDuration);
        bridgeYOffset = newBridgeYOffset;
        carryPickupRadius = Mathf.Max(0.01f, newCarryPickupRadius);
        carryMask = newCarryMask;
        carryLocalOffset = newCarryLocalOffset;
        carriedItemSpacing = Mathf.Max(0f, newCarriedItemSpacing);
        releaseDistance = Mathf.Max(0f, newReleaseDistance);
        releaseUpOffset = newReleaseUpOffset;
        releaseVelocity = Mathf.Max(0f, newReleaseVelocity);
    }

    public void SetSplitGeneration(int generation, int maximumGeneration)
    {
        splitGeneration = Mathf.Max(0, generation);
        maxSplitGeneration = Mathf.Max(0, maximumGeneration);
    }

    public void BeginMerge(float duration)
    {
        BeginMerge(duration, null);
    }

    public void BeginMergeToSplitPiece(SlimeSplitPiece targetPiece, float duration)
    {
        if (targetPiece == null || targetPiece == this)
        {
            BeginMerge(duration);
            return;
        }

        BeginMerge(duration, targetPiece);
    }

    private void BeginMerge(float duration, SlimeSplitPiece targetPiece)
    {
        if (merging)
            return;

        stretching = false;
        ReleaseAllCarriedItems();
        merging = true;
        mergeTargetPiece = targetPiece;
        mergeDuration = Mathf.Max(0f, duration);
        mergeStartTime = Time.time;
        mergeStartScale = transform.localScale;
        mergeStartCenter = GetCenterPosition();

        if (mergeDuration <= 0.0001f)
        {
            CompleteMergeInstantly();
            return;
        }

        SpringJoint[] springs = GetComponentsInChildren<SpringJoint>(true);
        for (int i = 0; i < springs.Length; i++)
            Destroy(springs[i]);

        Joint[] joints = GetComponentsInChildren<Joint>(true);
        for (int i = 0; i < joints.Length; i++)
            Destroy(joints[i]);

        Rigidbody[] rigidbodies = GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            if (rigidbodies[i] == null)
                continue;
            rigidbodies[i].velocity = Vector3.zero;
            rigidbodies[i].angularVelocity = Vector3.zero;
            rigidbodies[i].isKinematic = true;
            rigidbodies[i].useGravity = false;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        transform.position = mergeStartCenter;
    }

    private void CompleteMergeInstantly()
    {
        transform.position = GetMergeTargetPosition();
        transform.localScale = Vector3.zero;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    public void SetColor(Color color)
    {
        if (pieceRenderers == null || pieceRenderers.Length == 0)
            pieceRenderers = GetComponentsInChildren<Renderer>(true);

        if (pieceRenderers == null || pieceRenderers.Length == 0)
            return;

        EnsurePropertyBlock();
        propertyBlock.SetColor(BaseColor, color);
        propertyBlock.SetColor(ColorId, color);

        for (int i = 0; i < pieceRenderers.Length; i++)
        {
            if (pieceRenderers[i] != null)
                pieceRenderers[i].SetPropertyBlock(propertyBlock);
        }
    }

    private void EnsurePropertyBlock()
    {
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();
    }

    private void Update()
    {
        if (owner == null)
        {
            Destroy(gameObject);
            return;
        }

        if (!launchApplied)
            TryApplyLaunch();

        if (keyboardControlEnabled && !merging)
            SetMoveInput(ReadArrowInput());
        else
            moveInput = Vector2.zero;

        if (keyboardControlEnabled && !merging)
            ReadAbilityInput();

        if (!merging)
            UpdateStretchVisual();

        if (merging)
            UpdateMerge();
    }

    private void LateUpdate()
    {
        if (carriedItems.Count > 0)
            SyncCarryContainerTransform();
    }

    private void OnDisable()
    {
        ReleaseAllCarriedItems();
    }

    private void OnDestroy()
    {
        ReleaseAllCarriedItems();

        if (runtimeCarryContainer != null)
            Destroy(runtimeCarryContainer.gameObject);
    }

    private void FixedUpdate()
    {
        if (!keyboardControlEnabled || merging)
            return;

        if (!EnsureBodies())
            return;

        Vector3 moveDirection = ResolveMoveDirection(moveInput);
        if (moveDirection.sqrMagnitude > 0.0001f)
            lastAimDirection = moveDirection.normalized;

        ApplyMovement(moveDirection);
    }

    private void TryApplyLaunch()
    {
        if (!EnsureBodies())
            return;

        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] == null || bodies[i].isKinematic)
                continue;
            bodies[i].velocity = launchVelocity;
        }

        launchApplied = true;
    }

    private bool EnsureBodies()
    {
        if (bodies == null || bodies.Length == 0)
            bodies = GetComponentsInChildren<Rigidbody>(true);

        return bodies != null && bodies.Length > 0;
    }

    private Vector2 ReadArrowInput()
    {
        float horizontal = 0f;
        float vertical = 0f;

        if (IsKeyPressed(leftKey))
            horizontal -= 1f;

        if (IsKeyPressed(rightKey))
            horizontal += 1f;

        if (IsKeyPressed(backKey))
            vertical -= 1f;

        if (IsKeyPressed(forwardKey))
            vertical += 1f;

        return new Vector2(horizontal, vertical);
    }

    private static bool IsKeyPressed(KeyCode key)
    {
        return key != KeyCode.None && Input.GetKey(key);
    }

    private static bool IsKeyDown(KeyCode key)
    {
        return key != KeyCode.None && Input.GetKeyDown(key);
    }

    private void ReadAbilityInput()
    {
        if (IsKeyDown(splitKey))
        {
            if (CanSplitFurther)
                owner?.SplitFromSplitPiece(this, lastAimDirection);
        }

        if (IsKeyDown(mergeKey))
        {
            if (owner == null || owner.MergeNearbySplitPiecesInto(this) == 0)
                owner?.TryAbsorbNearbySlime(CenterPosition);
        }

        stretching = IsKeyPressed(stretchKey) || IsKeyPressed(alternateStretchKey);

        if (IsKeyDown(bridgeKey))
            BuildBridge();

        if (IsKeyDown(carryKey) && !TryCarryNearestItem())
            ReleaseLastCarriedItem();
    }

    private void UpdateStretchVisual()
    {
        Vector3 stretchShape = stretching
            ? new Vector3(stretchThickness, stretchThickness, stretchLength)
            : Vector3.one;
        targetLocalScale = Vector3.Scale(baseLocalScale, stretchShape);

        float lerpAmount = stretchLerpSpeed <= 0f ? 1f : Mathf.Clamp01(Time.deltaTime * stretchLerpSpeed);
        transform.localScale = Vector3.Lerp(transform.localScale, targetLocalScale, lerpAmount);

        if (!stretching || lastAimDirection.sqrMagnitude <= 0.0001f || stretchTurnSpeed <= 0f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(lastAimDirection, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * stretchTurnSpeed);
    }

    private GameObject BuildBridge()
    {
        Vector3 direction = GetAimDirection();
        Vector3 start = CenterPosition + Vector3.up * bridgeYOffset;
        Vector3 center = start + direction * (bridgeLength * 0.5f);

        GameObject bridge = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bridge.name = "Split Slime Bridge";
        bridge.transform.position = center;
        bridge.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        bridge.transform.localScale = new Vector3(bridgeWidth, bridgeThickness, bridgeLength);

        Renderer bridgeRenderer = bridge.GetComponentInChildren<Renderer>();
        if (bridgeRenderer != null)
        {
            Material material = ResolveSharedMaterial();
            if (material != null)
                bridgeRenderer.sharedMaterial = material;

            ApplyColor(bridgeRenderer);
        }

        Destroy(bridge, bridgeDuration);
        return bridge;
    }

    private bool TryCarryNearestItem()
    {
        if (owner == null)
            return false;

        Vector3 center = CenterPosition;
        int hitCount = Physics.OverlapSphereNonAlloc(center, carryPickupRadius, carryHits, carryMask, QueryTriggerInteraction.Collide);
        SlimeCarryable bestCarryable = null;
        float bestDistanceSqr = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = carryHits[i];
            if (hit == null || IsOwnCollider(hit))
                continue;

            SlimeCarryable carryable = hit.GetComponentInParent<SlimeCarryable>();
            if (carryable == null || carryable.IsCarried)
                continue;

            float distanceSqr = (carryable.transform.position - center).sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr)
                continue;

            bestCarryable = carryable;
            bestDistanceSqr = distanceSqr;
        }

        return bestCarryable != null && TryCarry(bestCarryable);
    }

    private bool TryCarry(SlimeCarryable carryable)
    {
        Transform container = GetOrCreateCarryContainer();
        if (owner == null || carryable == null || container == null || carryable.IsCarried)
            return false;

        SyncCarryContainerTransform();

        if (!carryable.BeginCarry(owner, container, GetCarrySlotLocalPosition(carriedItems.Count), GetCarrySlotLocalRotation(carriedItems.Count)))
            return false;

        carriedItems.Add(new CarriedItemState { Item = carryable });
        UpdateCarriedItemLayout();
        owner.PlayPickSound();
        return true;
    }

    private bool ReleaseLastCarriedItem()
    {
        PruneCarriedItems();

        for (int i = carriedItems.Count - 1; i >= 0; i--)
        {
            if (ReleaseCarriedItemAt(i))
                return true;
        }

        return false;
    }

    private int ReleaseAllCarriedItems()
    {
        int released = 0;

        for (int i = carriedItems.Count - 1; i >= 0; i--)
        {
            if (ReleaseCarriedItemAt(i))
                released++;
        }

        carriedItems.Clear();
        return released;
    }

    private bool ReleaseCarriedItemAt(int index)
    {
        if (index < 0 || index >= carriedItems.Count)
            return false;

        CarriedItemState state = carriedItems[index];
        carriedItems.RemoveAt(index);

        SlimeCarryable item = state != null ? state.Item : null;
        if (item == null || owner == null)
        {
            UpdateCarriedItemLayout();
            return false;
        }

        Vector3 direction = GetAimDirection();
        Vector3 releasePosition = CenterPosition + direction * Mathf.Max(0.05f, releaseDistance) + Vector3.up * releaseUpOffset;
        Quaternion releaseRotation = Quaternion.LookRotation(direction, Vector3.up);
        Vector3 releaseVelocityVector = direction * releaseVelocity + Vector3.up * (releaseVelocity * 0.25f);
        bool released = item.EndCarry(owner, releasePosition, releaseRotation, releaseVelocityVector);

        UpdateCarriedItemLayout();
        return released;
    }

    private Transform GetOrCreateCarryContainer()
    {
        if (runtimeCarryContainer != null)
            return runtimeCarryContainer;

        GameObject containerObject = new GameObject("Split Slime Carry Container");
        runtimeCarryContainer = containerObject.transform;
        runtimeCarryContainer.position = CenterPosition;
        runtimeCarryContainer.rotation = Quaternion.LookRotation(GetAimDirection(), Vector3.up);
        runtimeCarryContainer.localScale = Vector3.one;
        return runtimeCarryContainer;
    }

    private void SyncCarryContainerTransform()
    {
        if (runtimeCarryContainer == null)
            return;

        runtimeCarryContainer.position = CenterPosition;
        runtimeCarryContainer.rotation = Quaternion.LookRotation(GetAimDirection(), Vector3.up);
        runtimeCarryContainer.localScale = Vector3.one;
    }

    private void UpdateCarriedItemLayout()
    {
        for (int i = 0; i < carriedItems.Count; i++)
        {
            CarriedItemState state = carriedItems[i];
            if (state == null || state.Item == null || owner == null || !state.Item.IsCarriedBy(owner))
                continue;

            state.Item.SetCarriedPose(GetCarrySlotLocalPosition(i), GetCarrySlotLocalRotation(i));
        }
    }

    private Vector3 GetCarrySlotLocalPosition(int index)
    {
        if (index <= 0 || carriedItemSpacing <= 0f)
            return carryLocalOffset;

        float angle = index * 137.508f * Mathf.Deg2Rad;
        float radius = carriedItemSpacing * Mathf.Sqrt(index);
        return carryLocalOffset + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
    }

    private Quaternion GetCarrySlotLocalRotation(int index)
    {
        return Quaternion.Euler(0f, index * 47f, 0f);
    }

    private void PruneCarriedItems()
    {
        bool changed = false;

        for (int i = carriedItems.Count - 1; i >= 0; i--)
        {
            CarriedItemState state = carriedItems[i];
            if (state != null && state.Item != null && owner != null && state.Item.IsCarriedBy(owner))
                continue;

            carriedItems.RemoveAt(i);
            changed = true;
        }

        if (changed)
            UpdateCarriedItemLayout();
    }

    private bool IsOwnCollider(Collider hit)
    {
        if (hit == null)
            return true;

        if (hit.transform == transform || hit.transform.IsChildOf(transform))
            return true;

        if (bodies == null || bodies.Length == 0)
            bodies = GetComponentsInChildren<Rigidbody>(true);

        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody body = bodies[i];
            if (body != null && hit.attachedRigidbody == body)
                return true;
        }

        return false;
    }

    private Vector3 GetAimDirection()
    {
        Vector3 direction = lastAimDirection;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = transform.forward;

        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.forward;

        return direction.normalized;
    }

    private Material ResolveSharedMaterial()
    {
        if (pieceRenderers == null || pieceRenderers.Length == 0)
            pieceRenderers = GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < pieceRenderers.Length; i++)
        {
            if (pieceRenderers[i] != null && pieceRenderers[i].sharedMaterial != null)
                return pieceRenderers[i].sharedMaterial;
        }

        return null;
    }

    private void ApplyColor(Renderer targetRenderer)
    {
        if (targetRenderer == null || propertyBlock == null)
            return;

        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private void SetMoveInput(Vector2 input)
    {
        input = Vector2.ClampMagnitude(input, 1f);

        if (Mathf.Abs(input.x) < inputAxisThreshold)
            input.x = 0f;

        if (Mathf.Abs(input.y) < inputAxisThreshold)
            input.y = 0f;

        moveInput = input;
    }

    private Vector3 ResolveMoveDirection(Vector2 input)
    {
        if (input.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;
        Transform movementCamera = cameraTransform;

        if (movementCamera == null && Camera.main != null)
        {
            movementCamera = Camera.main.transform;
            cameraTransform = movementCamera;
        }

        if (cameraRelativeMovement && movementCamera != null)
        {
            forward = movementCamera.forward;
            right = movementCamera.right;
            forward.y = 0f;
            right.y = 0f;

            if (forward.sqrMagnitude > 0.0001f)
                forward.Normalize();

            if (right.sqrMagnitude > 0.0001f)
                right.Normalize();
        }

        return Vector3.ClampMagnitude(right * input.x + forward * input.y, 1f);
    }

    private void ApplyMovement(Vector3 moveDirection)
    {
        Vector3 velocity = GetDrivenVelocity();
        Vector3 currentPlanarVelocity = new Vector3(velocity.x, 0f, velocity.z);
        Vector3 targetPlanarVelocity = moveDirection * moveSpeed;
        Vector3 velocityDelta = targetPlanarVelocity - currentPlanarVelocity;
        Vector3 pushAcceleration = Vector3.ClampMagnitude(velocityDelta / Time.fixedDeltaTime, acceleration);

        AddAccelerationToBodies(pushAcceleration);
    }

    private Vector3 GetDrivenVelocity()
    {
        Vector3 velocity = Vector3.zero;
        float totalWeight = 0f;

        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody body = bodies[i];
            if (body == null)
                continue;

            float weight = i == 0 ? 1f : outerBodyDriveWeight;
            if (weight <= 0f)
                continue;

            velocity += body.velocity * weight;
            totalWeight += weight;
        }

        return totalWeight > 0f ? velocity / totalWeight : Vector3.zero;
    }

    private void AddAccelerationToBodies(Vector3 accelerationToApply)
    {
        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody body = bodies[i];
            if (body == null || body.isKinematic)
                continue;

            float weight = i == 0 ? 1f : outerBodyDriveWeight;
            if (weight <= 0f)
                continue;

            body.AddForce(accelerationToApply * weight, ForceMode.Acceleration);
        }
    }

    private void UpdateMerge()
    {
        if (mergeDuration <= 0.0001f)
        {
            CompleteMergeInstantly();
            return;
        }

        float t = Mathf.Clamp01((Time.time - mergeStartTime) / mergeDuration);
        float eased = Mathf.SmoothStep(0f, 1f, t);
        Vector3 targetCenter = GetMergeTargetPosition();
        transform.position = Vector3.Lerp(mergeStartCenter, targetCenter, eased);
        transform.localScale = Vector3.Lerp(mergeStartScale, Vector3.zero, eased);

        if (t >= 1f)
            Destroy(gameObject);
    }

    private Vector3 GetMergeTargetPosition()
    {
        if (mergeTargetPiece != null && !mergeTargetPiece.IsMerging)
            return mergeTargetPiece.CenterPosition;

        return owner != null ? owner.BodyCenterPosition : mergeStartCenter;
    }

    private Vector3 GetCenterPosition()
    {
        if (bodies == null || bodies.Length == 0)
            bodies = GetComponentsInChildren<Rigidbody>(true);

        if (bodies != null && bodies.Length > 0)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] != null)
                {
                    sum += bodies[i].worldCenterOfMass;
                    count++;
                }
            }
            if (count > 0)
                return sum / count;
        }

        return transform.position;
    }
}
