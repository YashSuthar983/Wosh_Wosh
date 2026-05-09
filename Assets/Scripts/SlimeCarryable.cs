using UnityEngine;

[DisallowMultipleComponent]
public class SlimeCarryable : MonoBehaviour
{
    [Header("Volume")]
    [SerializeField] private float volume = 0.2f;
    [SerializeField] private bool estimateVolumeFromBounds = false;
    [SerializeField] private float boundsVolumeMultiplier = 1f;
    [SerializeField] private float minimumEstimatedVolume = 0.02f;

    [Header("Carry")]
    [SerializeField] private bool carryOnContact = false;
    [SerializeField] private bool keepWorldScaleWhenCarried = true;
    [SerializeField] private float carriedVisualScale = 1f;
    [SerializeField] private Vector3 carriedLocalEulerAngles = Vector3.zero;
    [SerializeField] private bool restoreVelocityOnRelease = false;
    [SerializeField] private float releaseImpulseMultiplier = 1f;

    [Header("Carried Float")]
    [SerializeField] private bool floatWhileCarried = true;
    [SerializeField] private Vector3 floatAmplitude = new Vector3(0.08f, 0.06f, 0.08f);
    [SerializeField] private float floatSpeed = 1.6f;
    [SerializeField] private float floatTiltAngle = 8f;
    [SerializeField] private float floatSpinSpeed = 18f;
    [SerializeField] private float floatPoseLerpSpeed = 8f;

    private RigidbodyState[] rigidbodyStates;
    private ColliderState[] colliderStates;
    private Transform previousParent;
    private Vector3 previousLocalScale;
    private Vector3 previousWorldScale;
    private Vector3 carriedBaseLocalPosition;
    private Quaternion carriedBaseLocalRotation = Quaternion.identity;
    private SlimePlayerAbilities carriedBy;
    private float floatSeed;
    private bool hasCarriedPose;

    private struct RigidbodyState
    {
        public Rigidbody Body;
        public bool IsKinematic;
        public bool UseGravity;
        public bool DetectCollisions;
        public RigidbodyConstraints Constraints;
        public CollisionDetectionMode CollisionDetectionMode;
        public RigidbodyInterpolation Interpolation;
        public Vector3 Velocity;
        public Vector3 AngularVelocity;
    }

    private struct ColliderState
    {
        public Collider Collider;
        public bool Enabled;
        public bool IsTrigger;
    }

    public float Volume => GetVolume();
    public bool CarryOnContact => carryOnContact;
    public bool IsCarried => carriedBy != null;
    public SlimePlayerAbilities CarriedBy => carriedBy;

    private void OnValidate()
    {
        volume = Mathf.Max(0f, volume);
        boundsVolumeMultiplier = Mathf.Max(0f, boundsVolumeMultiplier);
        minimumEstimatedVolume = Mathf.Max(0f, minimumEstimatedVolume);
        carriedVisualScale = Mathf.Max(0.05f, carriedVisualScale);
        releaseImpulseMultiplier = Mathf.Max(0f, releaseImpulseMultiplier);
        floatAmplitude = new Vector3(Mathf.Abs(floatAmplitude.x), Mathf.Abs(floatAmplitude.y), Mathf.Abs(floatAmplitude.z));
        floatSpeed = Mathf.Max(0f, floatSpeed);
        floatTiltAngle = Mathf.Max(0f, floatTiltAngle);
        floatPoseLerpSpeed = Mathf.Max(0f, floatPoseLerpSpeed);
    }

    private void Update()
    {
        if (IsCarried && hasCarriedPose)
            UpdateCarriedFloatPose();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryCarryOnContact(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryCarryOnContact(collision.collider);
    }

    public bool BeginCarry(SlimePlayerAbilities slime, Transform container, Vector3 localPosition, Quaternion localRotation)
    {
        if (slime == null || container == null || IsCarried)
            return false;

        previousParent = transform.parent;
        previousLocalScale = transform.localScale;
        previousWorldScale = transform.lossyScale;
        CapturePhysicsState();
        ApplyCarriedPhysicsState();

        carriedBy = slime;
        floatSeed = Random.Range(0f, 100f);
        transform.SetParent(container, false);
        SetCarriedPose(localPosition, localRotation);

        if (keepWorldScaleWhenCarried)
            transform.localScale = DivideScale(previousWorldScale * carriedVisualScale, container.lossyScale);
        else
            transform.localScale = previousLocalScale * carriedVisualScale;

        return true;
    }

    public bool EndCarry(SlimePlayerAbilities slime, Vector3 worldPosition, Quaternion worldRotation, Vector3 releaseVelocity)
    {
        if (carriedBy != slime)
            return false;

        carriedBy = null;
        transform.SetParent(previousParent, true);
        transform.position = worldPosition;
        transform.rotation = worldRotation;
        transform.localScale = previousLocalScale;

        RestorePhysicsState(releaseVelocity * releaseImpulseMultiplier);

        previousParent = null;
        previousLocalScale = Vector3.one;
        previousWorldScale = Vector3.one;
        hasCarriedPose = false;
        return true;
    }

    public bool IsCarriedBy(SlimePlayerAbilities slime)
    {
        return carriedBy == slime;
    }

    public void SetCarriedPose(Vector3 localPosition, Quaternion localRotation)
    {
        carriedBaseLocalPosition = localPosition;
        carriedBaseLocalRotation = localRotation * Quaternion.Euler(carriedLocalEulerAngles);
        hasCarriedPose = true;
        ApplyCarriedPose(1f);
    }

    private void UpdateCarriedFloatPose()
    {
        float lerpAmount = floatPoseLerpSpeed <= 0f ? 1f : 1f - Mathf.Exp(-floatPoseLerpSpeed * Time.deltaTime);
        ApplyCarriedPose(lerpAmount);
    }

    private void ApplyCarriedPose(float lerpAmount)
    {
        Vector3 targetPosition = carriedBaseLocalPosition;
        Quaternion targetRotation = carriedBaseLocalRotation;

        if (floatWhileCarried)
        {
            float time = (Time.time + floatSeed) * floatSpeed;
            Vector3 floatOffset = new Vector3(
                Mathf.Sin(time * 0.83f) * floatAmplitude.x,
                Mathf.Sin(time * 1.17f + 1.3f) * floatAmplitude.y,
                Mathf.Cos(time * 0.91f + 2.1f) * floatAmplitude.z);

            Quaternion floatRotation = Quaternion.Euler(
                Mathf.Sin(time * 0.71f + 0.4f) * floatTiltAngle,
                Time.time * floatSpinSpeed,
                Mathf.Cos(time * 0.67f + 1.1f) * floatTiltAngle);

            targetPosition += floatOffset;
            targetRotation *= floatRotation;
        }

        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, Mathf.Clamp01(lerpAmount));
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, Mathf.Clamp01(lerpAmount));
    }

    private void TryCarryOnContact(Collider other)
    {
        if (!carryOnContact || IsCarried || other == null)
            return;

        SlimePlayerAbilities slime = other.GetComponentInParent<SlimePlayerAbilities>();
        if (slime != null)
            slime.TryCarry(this);
    }

    private float GetVolume()
    {
        if (!estimateVolumeFromBounds)
            return Mathf.Max(0f, volume);

        float estimatedVolume = EstimateVolumeFromBounds();
        if (estimatedVolume <= 0f)
            return Mathf.Max(0f, volume);

        return Mathf.Max(minimumEstimatedVolume, estimatedVolume * boundsVolumeMultiplier);
    }

    private float EstimateVolumeFromBounds()
    {
        Bounds bounds;
        if (!TryGetWorldBounds(out bounds))
            return 0f;

        Vector3 size = bounds.size;
        return Mathf.Max(0f, size.x * size.y * size.z);
    }

    private bool TryGetWorldBounds(out Bounds bounds)
    {
        bounds = new Bounds(transform.position, Vector3.zero);
        bool hasBounds = false;

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null)
                continue;

            Bounds colliderBounds = colliders[i].bounds;
            if (colliderBounds.size.sqrMagnitude <= 0.000001f)
                continue;

            if (hasBounds)
                bounds.Encapsulate(colliderBounds);
            else
                bounds = colliderBounds;

            hasBounds = true;
        }

        if (hasBounds)
            return true;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            Bounds rendererBounds = renderers[i].bounds;
            if (rendererBounds.size.sqrMagnitude <= 0.000001f)
                continue;

            if (hasBounds)
                bounds.Encapsulate(rendererBounds);
            else
                bounds = rendererBounds;

            hasBounds = true;
        }

        return hasBounds;
    }

    private void CapturePhysicsState()
    {
        Rigidbody[] bodies = GetComponentsInChildren<Rigidbody>(true);
        rigidbodyStates = new RigidbodyState[bodies.Length];
        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody body = bodies[i];
            rigidbodyStates[i] = new RigidbodyState
            {
                Body = body,
                IsKinematic = body != null && body.isKinematic,
                UseGravity = body != null && body.useGravity,
                DetectCollisions = body != null && body.detectCollisions,
                Constraints = body != null ? body.constraints : RigidbodyConstraints.None,
                CollisionDetectionMode = body != null ? body.collisionDetectionMode : CollisionDetectionMode.Discrete,
                Interpolation = body != null ? body.interpolation : RigidbodyInterpolation.None,
                Velocity = body != null ? body.velocity : Vector3.zero,
                AngularVelocity = body != null ? body.angularVelocity : Vector3.zero
            };
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        colliderStates = new ColliderState[colliders.Length];
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider itemCollider = colliders[i];
            colliderStates[i] = new ColliderState
            {
                Collider = itemCollider,
                Enabled = itemCollider != null && itemCollider.enabled,
                IsTrigger = itemCollider != null && itemCollider.isTrigger
            };
        }
    }

    private void ApplyCarriedPhysicsState()
    {
        if (colliderStates != null)
        {
            for (int i = 0; i < colliderStates.Length; i++)
            {
                Collider itemCollider = colliderStates[i].Collider;
                if (itemCollider != null)
                    itemCollider.enabled = false;
            }
        }

        if (rigidbodyStates == null)
            return;

        for (int i = 0; i < rigidbodyStates.Length; i++)
        {
            Rigidbody body = rigidbodyStates[i].Body;
            if (body == null)
                continue;

            if (!body.isKinematic)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            body.useGravity = false;
            body.detectCollisions = false;
            body.isKinematic = true;
        }
    }

    private void RestorePhysicsState(Vector3 releaseVelocity)
    {
        if (rigidbodyStates != null)
        {
            for (int i = 0; i < rigidbodyStates.Length; i++)
            {
                RigidbodyState state = rigidbodyStates[i];
                Rigidbody body = state.Body;
                if (body == null)
                    continue;

                body.isKinematic = state.IsKinematic;
                body.useGravity = state.UseGravity;
                body.detectCollisions = state.DetectCollisions;
                body.constraints = state.Constraints;
                body.collisionDetectionMode = state.CollisionDetectionMode;
                body.interpolation = state.Interpolation;

                if (!body.isKinematic)
                {
                    body.velocity = restoreVelocityOnRelease ? state.Velocity + releaseVelocity : releaseVelocity;
                    body.angularVelocity = restoreVelocityOnRelease ? state.AngularVelocity : Vector3.zero;
                    body.WakeUp();
                }
            }
        }

        if (colliderStates != null)
        {
            for (int i = 0; i < colliderStates.Length; i++)
            {
                ColliderState state = colliderStates[i];
                Collider itemCollider = state.Collider;
                if (itemCollider == null)
                    continue;

                itemCollider.isTrigger = state.IsTrigger;
                itemCollider.enabled = state.Enabled;
            }
        }

        rigidbodyStates = null;
        colliderStates = null;
    }

    private static Vector3 DivideScale(Vector3 worldScale, Vector3 parentScale)
    {
        return new Vector3(
            DivideScaleAxis(worldScale.x, parentScale.x),
            DivideScaleAxis(worldScale.y, parentScale.y),
            DivideScaleAxis(worldScale.z, parentScale.z));
    }

    private static float DivideScaleAxis(float value, float divisor)
    {
        return Mathf.Abs(divisor) > 0.0001f ? value / divisor : value;
    }
}
