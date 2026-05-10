using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class EnemyFireballAttacker : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform playerTarget = null;
    [SerializeField] private string fallbackPlayerName = "Player";
    [SerializeField] private Vector3 targetAimOffset = new Vector3(0f, 0.6f, 0f);
    [SerializeField, FormerlySerializedAs("trackPlayerDuringFlight")] private bool lockPlayerPositionOnFire = true;
    [SerializeField, FormerlySerializedAs("trackedHitRadius")] private float lockedHitRadius = 0.55f;

    [Header("Activation")]
    [SerializeField] private float activationRadius = 12f;
    [SerializeField] private float attackRadius = 14f;
    [SerializeField] private float firstShotDelay = 0.35f;
    [SerializeField] private float attackInterval = 1.75f;

    [Header("Fireball")]
    [SerializeField] private EnemyFireballProjectile fireballProjectilePrefab = null;
    [SerializeField] private GameObject fireballVisualPrefab = null;
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 1.1f, 0f);
    [SerializeField] private float projectileSpeed = 8f;
    [SerializeField] private float minFlightTime = 0.65f;
    [SerializeField] private float maxFlightTime = 2.25f;
    [SerializeField] private float projectileGravityMultiplier = 1f;
    [SerializeField] private float projectileLifetime = 5f;
    [SerializeField] private float fireballDamage = 0.25f;
    [SerializeField] private float fireballVolumeLossMultiplier = 1f;
    [SerializeField] private float fireballVisualScale = 1f;
    [SerializeField] private LayerMask impactMask = ~0;

    private SlimePlayerAbilities trackedPlayer;
    private float nextAttackTime;
    private bool isActive;

    public bool IsActive => isActive;

    public bool TryGetTarget(out Transform target)
    {
        ResolvePlayer();
        target = playerTarget != null ? playerTarget : (trackedPlayer != null ? trackedPlayer.transform : null);
        return target != null;
    }

    private void Awake()
    {
        ResolvePlayer();
        nextAttackTime = Time.time + firstShotDelay;
    }

    private void Update()
    {
        if (!ResolvePlayer())
            return;

        Vector3 trackedPosition = GetTrackedPlayerPosition();
        float activationRadiusSqr = activationRadius * activationRadius;
        float attackRadiusSqr = attackRadius * attackRadius;
        float playerDistanceSqr = (trackedPosition - transform.position).sqrMagnitude;
        bool playerInActivationRange = playerDistanceSqr <= activationRadiusSqr;
        bool playerInAttackRange = playerDistanceSqr <= attackRadiusSqr;

        if (!isActive && !playerInActivationRange)
            return;

        if (isActive && !playerInAttackRange)
        {
            isActive = false;
            return;
        }

        if (!isActive)
        {
            isActive = true;
            nextAttackTime = Time.time + firstShotDelay;
        }

        FaceTrackedPosition(trackedPosition);

        if (Time.time < nextAttackTime)
            return;

        ThrowFireball(trackedPosition);
        nextAttackTime = Time.time + attackInterval;
    }

    private bool ResolvePlayer()
    {
        if (playerTarget != null)
        {
            if (trackedPlayer == null)
            {
                trackedPlayer = playerTarget.GetComponentInParent<SlimePlayerAbilities>();
                if (trackedPlayer == null)
                    trackedPlayer = playerTarget.GetComponentInChildren<SlimePlayerAbilities>();
            }

            return true;
        }

        if (trackedPlayer == null)
            trackedPlayer = FindPlayerAbilities();

        if (trackedPlayer != null)
        {
            playerTarget = trackedPlayer.transform;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(fallbackPlayerName))
        {
            GameObject namedPlayer = GameObject.Find(fallbackPlayerName);
            if (namedPlayer != null)
            {
                playerTarget = namedPlayer.transform;
                trackedPlayer = namedPlayer.GetComponentInParent<SlimePlayerAbilities>();
                if (trackedPlayer == null)
                    trackedPlayer = namedPlayer.GetComponentInChildren<SlimePlayerAbilities>();

                return true;
            }
        }

        return false;
    }

    private static SlimePlayerAbilities FindPlayerAbilities()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<SlimePlayerAbilities>();
#else
#pragma warning disable 0618
        return Object.FindObjectOfType<SlimePlayerAbilities>();
#pragma warning restore 0618
#endif
    }

    private Vector3 GetTrackedPlayerPosition()
    {
        Transform target = playerTarget != null ? playerTarget : (trackedPlayer != null ? trackedPlayer.transform : null);
        return target != null ? target.position + targetAimOffset : transform.position + transform.forward;
    }

    private void FaceTrackedPosition(Vector3 trackedPosition)
    {
        Vector3 flatDirection = trackedPosition - transform.position;
        flatDirection.y = 0f;

        if (flatDirection.sqrMagnitude <= 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
    }

    private void ThrowFireball(Vector3 trackedPosition)
    {
        Vector3 spawnPosition = transform.TransformPoint(spawnOffset);
        float flightTime = CalculateFlightTime(spawnPosition, trackedPosition);
        Vector3 launchVelocity = CalculateBallisticVelocity(spawnPosition, trackedPosition, flightTime);

        Quaternion spawnRotation = Quaternion.LookRotation(launchVelocity.normalized, Vector3.up);
        EnemyFireballProjectile projectile = fireballProjectilePrefab != null
            ? Instantiate(fireballProjectilePrefab, spawnPosition, spawnRotation)
            : CreateRuntimeProjectile(spawnPosition, spawnRotation);

        Transform targetTransform = playerTarget != null ? playerTarget : (trackedPlayer != null ? trackedPlayer.transform : null);
        if (lockPlayerPositionOnFire && targetTransform != null)
        {
            projectile.InitializeTargetLockedBallistic(
                launchVelocity,
                projectileGravityMultiplier,
                projectileLifetime,
                fireballDamage,
                transform,
                impactMask,
                fireballVisualPrefab,
                fireballVisualScale,
                fireballVolumeLossMultiplier,
                targetTransform,
                targetAimOffset,
                flightTime,
                lockedHitRadius);
            return;
        }

        projectile.InitializeBallistic(
            launchVelocity,
            projectileGravityMultiplier,
            projectileLifetime,
            fireballDamage,
            transform,
            impactMask,
            fireballVisualPrefab,
            fireballVisualScale,
            fireballVolumeLossMultiplier);
    }

    private float CalculateFlightTime(Vector3 spawnPosition, Vector3 targetPosition)
    {
        Vector3 toTarget = targetPosition - spawnPosition;
        Vector3 horizontalOffset = new Vector3(toTarget.x, 0f, toTarget.z);
        float horizontalDistance = horizontalOffset.magnitude;
        return Mathf.Clamp(horizontalDistance / projectileSpeed, minFlightTime, maxFlightTime);
    }

    private Vector3 CalculateBallisticVelocity(Vector3 spawnPosition, Vector3 targetPosition, float flightTime)
    {
        Vector3 toTarget = targetPosition - spawnPosition;
        Vector3 gravity = Physics.gravity * projectileGravityMultiplier;

        if (gravity.sqrMagnitude <= 0.0001f)
            return toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized * projectileSpeed : transform.forward * projectileSpeed;

        return (toTarget - 0.5f * gravity * flightTime * flightTime) / flightTime;
    }

    private static EnemyFireballProjectile CreateRuntimeProjectile(Vector3 spawnPosition, Quaternion spawnRotation)
    {
        GameObject fireball = new GameObject("Enemy Fireball");
        fireball.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        return fireball.AddComponent<EnemyFireballProjectile>();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.35f, 0.05f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, activationRadius);

        Gizmos.color = new Color(1f, 0.1f, 0.02f, 0.22f);
        Gizmos.DrawWireSphere(transform.position, attackRadius);

        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
        Gizmos.DrawSphere(transform.TransformPoint(spawnOffset), 0.12f);
    }

    private void OnValidate()
    {
        activationRadius = Mathf.Max(0.1f, activationRadius);
        attackRadius = Mathf.Max(activationRadius, attackRadius);
        lockedHitRadius = Mathf.Max(0.05f, lockedHitRadius);
        firstShotDelay = Mathf.Max(0f, firstShotDelay);
        attackInterval = Mathf.Max(0.05f, attackInterval);
        projectileSpeed = Mathf.Max(0.1f, projectileSpeed);
        minFlightTime = Mathf.Max(0.1f, minFlightTime);
        maxFlightTime = Mathf.Max(minFlightTime, maxFlightTime);
        projectileGravityMultiplier = Mathf.Max(0f, projectileGravityMultiplier);
        projectileLifetime = Mathf.Max(0.1f, projectileLifetime);
        fireballDamage = Mathf.Max(0f, fireballDamage);
        fireballVolumeLossMultiplier = Mathf.Max(0f, fireballVolumeLossMultiplier);
        fireballVisualScale = Mathf.Max(0.05f, fireballVisualScale);
    }
}
