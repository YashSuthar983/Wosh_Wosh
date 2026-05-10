using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class EnemyFireballProjectile : MonoBehaviour
{
    private const float DefaultRadius = 0.28f;

    [SerializeField] private float radius = DefaultRadius;
    [SerializeField] private float speed = 8f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private float damage = 0.25f;
    [SerializeField] private float gravityMultiplier = 1f;
    [SerializeField] private float visualScale = 1f;
    [SerializeField] private float volumeLossMultiplier = 1f;
    [SerializeField, FormerlySerializedAs("trackedTargetHitRadius")] private float lockedTargetHitRadius = 0.55f;
    [SerializeField] private LayerMask impactMask = ~0;

    private Vector3 direction = Vector3.forward;
    private Vector3 velocity = Vector3.forward * 8f;
    private Transform ownerRoot;
    private Transform trackedTarget;
    private Vector3 trackedTargetOffset;
    private Vector3 lockedTargetPosition;
    private SlimePlayerAbilities trackedSlime;
    private SphereCollider triggerCollider;
    private Rigidbody body;
    private float destroyTime;
    private float trackedFlightDuration = 1f;
    private float trackedFlightElapsed;
    private bool initialized;
    private bool hasVisual;
    private bool hasImpacted;
    private bool isTrackingTarget;

    public void Initialize(
        Vector3 launchDirection,
        float launchSpeed,
        float projectileLifetime,
        float projectileDamage,
        Transform owner,
        LayerMask collisionMask,
        GameObject visualPrefab)
    {
        Vector3 safeDirection = launchDirection.sqrMagnitude > 0.0001f ? launchDirection.normalized : transform.forward;
        InitializeBallistic(
            safeDirection * launchSpeed,
            0f,
            projectileLifetime,
            projectileDamage,
            owner,
            collisionMask,
            visualPrefab,
            1f,
            volumeLossMultiplier);
    }

    public void InitializeBallistic(
        Vector3 launchVelocity,
        float launchGravityMultiplier,
        float projectileLifetime,
        float projectileDamage,
        Transform owner,
        LayerMask collisionMask,
        GameObject visualPrefab,
        float spawnVisualScale)
    {
        InitializeBallistic(
            launchVelocity,
            launchGravityMultiplier,
            projectileLifetime,
            projectileDamage,
            owner,
            collisionMask,
            visualPrefab,
            spawnVisualScale,
            volumeLossMultiplier);
    }

    public void InitializeBallistic(
        Vector3 launchVelocity,
        float launchGravityMultiplier,
        float projectileLifetime,
        float projectileDamage,
        Transform owner,
        LayerMask collisionMask,
        GameObject visualPrefab,
        float spawnVisualScale,
        float hitVolumeLossMultiplier)
    {
        velocity = launchVelocity.sqrMagnitude > 0.0001f ? launchVelocity : transform.forward * speed;
        direction = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : transform.forward;
        speed = velocity.magnitude;
        gravityMultiplier = Mathf.Max(0f, launchGravityMultiplier);
        lifetime = Mathf.Max(0.1f, projectileLifetime);
        damage = Mathf.Max(0f, projectileDamage);
        visualScale = Mathf.Max(0.05f, spawnVisualScale);
        volumeLossMultiplier = Mathf.Max(0f, hitVolumeLossMultiplier);
        ownerRoot = owner;
        impactMask = collisionMask;
        destroyTime = Time.time + lifetime;
        initialized = true;
        trackedTarget = null;
        trackedSlime = null;
        trackedFlightElapsed = 0f;
        isTrackingTarget = false;

        EnsureTriggerCollider();
        EnsureKinematicBody();
        AttachVisualPrefab(visualPrefab);
    }

    public void InitializeTargetLockedBallistic(
        Vector3 launchVelocity,
        float launchGravityMultiplier,
        float projectileLifetime,
        float projectileDamage,
        Transform owner,
        LayerMask collisionMask,
        GameObject visualPrefab,
        float spawnVisualScale,
        float hitVolumeLossMultiplier,
        Transform target,
        Vector3 targetOffset,
        float flightDuration,
        float hitRadius)
    {
        InitializeBallistic(
            launchVelocity,
            launchGravityMultiplier,
            projectileLifetime,
            projectileDamage,
            owner,
            collisionMask,
            visualPrefab,
            spawnVisualScale,
            hitVolumeLossMultiplier);

        trackedTarget = target;
        trackedTargetOffset = targetOffset;
        lockedTargetPosition = target != null ? target.position + targetOffset : transform.position + transform.forward;
        trackedSlime = ResolveSlimeFromTarget(target);
        trackedFlightDuration = Mathf.Max(0.1f, flightDuration);
        trackedFlightElapsed = 0f;
        lockedTargetHitRadius = Mathf.Max(radius, hitRadius);
        isTrackingTarget = trackedTarget != null || trackedSlime != null;
    }

    public void InitializeTrackedBallistic(
        Vector3 launchVelocity,
        float launchGravityMultiplier,
        float projectileLifetime,
        float projectileDamage,
        Transform owner,
        LayerMask collisionMask,
        GameObject visualPrefab,
        float spawnVisualScale,
        float hitVolumeLossMultiplier,
        Transform target,
        Vector3 targetOffset,
        float flightDuration,
        float hitRadius)
    {
        InitializeTargetLockedBallistic(
            launchVelocity,
            launchGravityMultiplier,
            projectileLifetime,
            projectileDamage,
            owner,
            collisionMask,
            visualPrefab,
            spawnVisualScale,
            hitVolumeLossMultiplier,
            target,
            targetOffset,
            flightDuration,
            hitRadius);
    }

    private void Awake()
    {
        EnsureTriggerCollider();
        EnsureKinematicBody();
        destroyTime = Time.time + lifetime;
    }

    private void Start()
    {
        if (!hasVisual)
            CreateFallbackVisual();
    }

    private void Update()
    {
        if (!initialized)
        {
            initialized = true;
            direction = transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : Vector3.forward;
            velocity = direction * speed;
            destroyTime = Time.time + lifetime;
        }

        if (Time.time >= destroyTime)
        {
            Destroy(gameObject);
            return;
        }

        MoveProjectile();
    }

    private void MoveProjectile()
    {
        if (isTrackingTarget)
        {
            MoveTrackedProjectile();
            return;
        }

        MoveUnguidedProjectile();
    }

    private void MoveUnguidedProjectile()
    {
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
            return;

        Vector3 gravity = Physics.gravity * gravityMultiplier;
        Vector3 movement = velocity * deltaTime + 0.5f * gravity * deltaTime * deltaTime;
        float distance = movement.magnitude;

        if (distance <= 0.0001f)
        {
            velocity += gravity * deltaTime;
            return;
        }

        direction = movement / distance;

        if (TryGetImpact(direction, distance, out RaycastHit hit))
        {
            transform.position = hit.point;
            HandleImpact(hit.collider);
            return;
        }

        transform.position += movement;
        velocity += gravity * deltaTime;
        RotateAlongVelocity();
    }

    private void MoveTrackedProjectile()
    {
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
            return;

        if (trackedTarget == null && trackedSlime == null)
        {
            isTrackingTarget = false;
            MoveUnguidedProjectile();
            return;
        }

        if (TryDamageTrackedTargetIfReached(transform.position))
            return;

        Vector3 targetPosition = lockedTargetPosition;
        float remainingTime = Mathf.Max(0.03f, trackedFlightDuration - trackedFlightElapsed);
        velocity = CalculateVelocityToTarget(transform.position, targetPosition, remainingTime);

        Vector3 gravity = Physics.gravity * gravityMultiplier;
        Vector3 movement = velocity * deltaTime + 0.5f * gravity * deltaTime * deltaTime;
        float distance = movement.magnitude;

        if (distance > 0.0001f)
        {
            direction = movement / distance;
            if (TryGetSlimeImpact(direction, distance, out Collider hitCollider))
            {
                HandleImpact(hitCollider);
                return;
            }

            transform.position += movement;
            velocity += gravity * deltaTime;
            RotateAlongVelocity();
        }

        trackedFlightElapsed += deltaTime;

        if (TryDamageTrackedTargetIfReached(transform.position))
            return;

        if (trackedFlightElapsed >= trackedFlightDuration)
        {
            transform.position = targetPosition;
            if (!TryDamageLockedTarget())
            {
                Destroy(gameObject);
            }
        }
    }

    private bool TryGetImpact(Vector3 castDirection, float distance, out RaycastHit nearestHit)
    {
        RaycastHit[] hits = Physics.SphereCastAll(
            transform.position,
            radius,
            castDirection,
            distance,
            impactMask,
            QueryTriggerInteraction.Collide);

        nearestHit = default;
        float nearestDistance = float.PositiveInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (ShouldIgnoreCollider(hitCollider))
                continue;

            if (hits[i].distance < nearestDistance)
            {
                nearestHit = hits[i];
                nearestDistance = hits[i].distance;
            }
        }

        return nearestHit.collider != null;
    }

    private bool TryGetSlimeImpact(Vector3 castDirection, float distance, out Collider hitCollider)
    {
        RaycastHit[] hits = Physics.SphereCastAll(
            transform.position,
            radius,
            castDirection,
            distance,
            impactMask,
            QueryTriggerInteraction.Collide);

        hitCollider = null;
        float nearestDistance = float.PositiveInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider candidate = hits[i].collider;
            if (ShouldIgnoreCollider(candidate))
                continue;

            if (candidate.GetComponentInParent<SlimePlayerAbilities>() == null)
                continue;

            if (hits[i].distance < nearestDistance)
            {
                hitCollider = candidate;
                nearestDistance = hits[i].distance;
            }
        }

        return hitCollider != null;
    }

    private void RotateAlongVelocity()
    {
        if (velocity.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isTrackingTarget && other != null && other.GetComponentInParent<SlimePlayerAbilities>() == null)
            return;

        if (!ShouldIgnoreCollider(other))
            HandleImpact(other);
    }

    private void HandleImpact(Collider other)
    {
        if (hasImpacted || other == null)
            return;

        SlimePlayerAbilities slime = other.GetComponentInParent<SlimePlayerAbilities>();
        if (slime != null)
        {
            hasImpacted = true;

            DamageSlime(slime);

            Destroy(gameObject);
            return;
        }

        if (!other.isTrigger)
        {
            hasImpacted = true;
            Destroy(gameObject);
        }
    }

    private bool ShouldIgnoreCollider(Collider other)
    {
        if (other == null)
            return true;

        if (other.transform == transform || other.transform.IsChildOf(transform))
            return true;

        if (other.isTrigger && other.GetComponentInParent<SlimePlayerAbilities>() == null)
            return true;

        return ownerRoot != null && (other.transform == ownerRoot || other.transform.IsChildOf(ownerRoot));
    }

    private Vector3 GetCurrentTrackedTargetPosition()
    {
        if (trackedTarget != null)
            return trackedTarget.position + trackedTargetOffset;

        return trackedSlime != null ? trackedSlime.transform.position + trackedTargetOffset : transform.position;
    }

    private Vector3 CalculateVelocityToTarget(Vector3 currentPosition, Vector3 targetPosition, float timeToTarget)
    {
        Vector3 toTarget = targetPosition - currentPosition;
        Vector3 gravity = Physics.gravity * gravityMultiplier;

        if (gravity.sqrMagnitude <= 0.0001f)
            return toTarget.sqrMagnitude > 0.0001f ? toTarget / timeToTarget : direction * speed;

        return (toTarget - 0.5f * gravity * timeToTarget * timeToTarget) / timeToTarget;
    }

    private bool TryDamageTrackedTargetIfReached(Vector3 position)
    {
        if (trackedSlime == null)
            trackedSlime = ResolveSlimeFromTarget(trackedTarget);

        if (trackedSlime == null)
            return false;

        float impactRadius = Mathf.Max(radius, lockedTargetHitRadius);
        if ((position - lockedTargetPosition).sqrMagnitude > impactRadius * impactRadius)
            return false;

        return TryDamageLockedTarget();
    }

    private bool TryDamageLockedTarget()
    {
        if (hasImpacted)
            return true;

        if (trackedSlime == null)
            trackedSlime = ResolveSlimeFromTarget(trackedTarget);

        if (trackedSlime == null)
            return false;

        Vector3 currentTargetPosition = GetCurrentTrackedTargetPosition();
        float impactRadius = Mathf.Max(radius, lockedTargetHitRadius);
        if ((currentTargetPosition - lockedTargetPosition).sqrMagnitude > impactRadius * impactRadius)
            return false;

        hasImpacted = true;
        DamageSlime(trackedSlime);
        Destroy(gameObject);
        return true;
    }

    private void DamageSlime(SlimePlayerAbilities slime)
    {
        if (slime != null && slime.CurrentMaterial != SlimeMaterialType.Fireproof)
            slime.ApplyHitDamage(damage, damage * volumeLossMultiplier);
    }

    private static SlimePlayerAbilities ResolveSlimeFromTarget(Transform target)
    {
        if (target == null)
            return null;

        SlimePlayerAbilities slime = target.GetComponentInParent<SlimePlayerAbilities>();
        return slime != null ? slime : target.GetComponentInChildren<SlimePlayerAbilities>();
    }

    private void EnsureTriggerCollider()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<SphereCollider>();

        if (triggerCollider == null)
            triggerCollider = gameObject.AddComponent<SphereCollider>();

        triggerCollider.isTrigger = true;
        triggerCollider.radius = radius;
    }

    private void EnsureKinematicBody()
    {
        if (body == null)
            body = GetComponent<Rigidbody>();

        if (body == null)
            body = gameObject.AddComponent<Rigidbody>();

        body.useGravity = false;
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private void AttachVisualPrefab(GameObject visualPrefab)
    {
        if (visualPrefab == null)
            return;

        GameObject visual = Instantiate(visualPrefab, transform);
        visual.name = visualPrefab.name;
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one * visualScale;
        DisableVisualColliders(visual);
        hasVisual = true;
    }

    private static void DisableVisualColliders(GameObject visual)
    {
        Collider[] visualColliders = visual.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < visualColliders.Length; i++)
            visualColliders[i].enabled = false;
    }

    private void CreateFallbackVisual()
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "Fallback Fireball Core";
        sphere.transform.SetParent(transform, false);
        sphere.transform.localScale = Vector3.one * (radius * 1.6f);

        Collider visualCollider = sphere.GetComponent<Collider>();
        if (visualCollider != null)
            Destroy(visualCollider);

        MeshRenderer meshRenderer = sphere.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
            meshRenderer.material = CreateMaterial(new Color(1f, 0.32f, 0.03f), true);

        GameObject particlesObject = new GameObject("Fallback Fire Trail");
        particlesObject.transform.SetParent(transform, false);

        ParticleSystem particles = particlesObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.75f);
        main.startSize = new ParticleSystem.MinMaxCurve(radius * 0.8f, radius * 1.4f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.18f, 0.02f, 0.8f), new Color(1f, 0.82f, 0.2f, 0.55f));

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 45f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius * 0.45f;

        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer != null)
            particleRenderer.material = CreateMaterial(new Color(1f, 0.45f, 0.02f), true);

        Light fireLight = gameObject.AddComponent<Light>();
        fireLight.type = LightType.Point;
        fireLight.color = new Color(1f, 0.45f, 0.08f);
        fireLight.range = 3f;
        fireLight.intensity = 2.2f;

        hasVisual = true;
    }

    private static Material CreateMaterial(Color color, bool emissive)
    {
        Shader shader = Shader.Find("HDRP/Unlit");
        if (shader == null)
            shader = Shader.Find("HDRP/Lit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        Material material = new Material(shader);
        material.name = "Runtime Fireball Material";

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        if (emissive)
        {
            Color emissionColor = color * 2.5f;
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", emissionColor);
            if (material.HasProperty("_EmissiveColor"))
                material.SetColor("_EmissiveColor", emissionColor);

            material.EnableKeyword("_EMISSION");
        }

        return material;
    }

    private void OnValidate()
    {
        radius = Mathf.Max(0.05f, radius);
        speed = Mathf.Max(0.1f, speed);
        lifetime = Mathf.Max(0.1f, lifetime);
        damage = Mathf.Max(0f, damage);
        gravityMultiplier = Mathf.Max(0f, gravityMultiplier);
        visualScale = Mathf.Max(0.05f, visualScale);
        volumeLossMultiplier = Mathf.Max(0f, volumeLossMultiplier);
        lockedTargetHitRadius = Mathf.Max(0.05f, lockedTargetHitRadius);

        if (triggerCollider != null)
            triggerCollider.radius = radius;
    }
}
