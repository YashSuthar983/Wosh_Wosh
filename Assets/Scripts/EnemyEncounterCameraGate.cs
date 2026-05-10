using Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class EnemyEncounterCameraGate : MonoBehaviour
{
    [SerializeField] private SlimeWaveDamageReceiver[] requiredDefeats = null;
    [SerializeField] private CinemachineVirtualCamera targetCamera = null;
    [SerializeField] private int priorityBoost = 20;
    [SerializeField] private LayerMask playerLayerMask = 1;
    [SerializeField] private string requiredTag = "";
    [SerializeField] private string playerObjectName = "Player";
    [SerializeField] private MonoBehaviour disabledTriggerAction = null;
    [SerializeField] private bool scanPlayerInsideTrigger = true;
    [SerializeField, Min(0f)] private float triggerScanInterval = 0.05f;
    [SerializeField, Min(0f)] private float triggerScanPadding = 0.75f;

    private int basePriority;
    private int playerOverlapCount;
    private bool basePriorityCaptured;
    private bool unlocked;
    private bool boosted;
    private readonly Collider[] scanHits = new Collider[32];
    private Collider triggerCollider;
    private SlimePlayerAbilities cachedPlayer;
    private Collider[] cachedPlayerColliders;
    private float nextTriggerScanTime;

    private void Awake()
    {
        CaptureBasePriority();
        EnsureTriggerCollider();

        if (disabledTriggerAction != null)
            disabledTriggerAction.enabled = false;
    }

    private void Update()
    {
        UpdateScannedPlayerOverlap();
        TryUnlock();

        if (unlocked && playerOverlapCount > 0)
            ApplyBoost();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!MatchesPlayer(other))
            return;

        playerOverlapCount++;
        TryUnlock();

        if (unlocked)
            ApplyBoost();
    }

    private void OnTriggerStay(Collider other)
    {
        if (!MatchesPlayer(other))
            return;

        if (playerOverlapCount <= 0)
            playerOverlapCount = 1;

        TryUnlock();

        if (unlocked)
            ApplyBoost();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!MatchesPlayer(other))
            return;

        playerOverlapCount = Mathf.Max(0, playerOverlapCount - 1);

        if (playerOverlapCount == 0)
            ClearBoost();
    }

    private void OnDisable()
    {
        ClearBoost();
    }

    private void TryUnlock()
    {
        if (unlocked || !AllRequiredDefeatsCleared())
            return;

        unlocked = true;

        if (disabledTriggerAction != null)
            disabledTriggerAction.enabled = false;
    }

    private bool AllRequiredDefeatsCleared()
    {
        if (requiredDefeats == null || requiredDefeats.Length == 0)
            return true;

        bool hasReceiver = false;
        for (int i = 0; i < requiredDefeats.Length; i++)
        {
            SlimeWaveDamageReceiver receiver = requiredDefeats[i];
            if (receiver == null)
                continue;

            hasReceiver = true;
            if (!receiver.IsDefeated)
                return false;
        }

        return hasReceiver;
    }

    private bool MatchesPlayer(Collider other)
    {
        if (other == null)
            return false;

        int otherLayer = other.gameObject.layer;
        if ((playerLayerMask.value & (1 << otherLayer)) == 0)
            return false;

        if (!string.IsNullOrWhiteSpace(requiredTag) && !other.CompareTag(requiredTag))
            return false;

        SlimePlayerAbilities player = other.GetComponentInParent<SlimePlayerAbilities>();
        if (player != null)
        {
            cachedPlayer = player;
            cachedPlayerColliders = null;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(playerObjectName))
        {
            Transform current = other.transform;
            while (current != null)
            {
                if (current.name == playerObjectName)
                {
                    CachePlayerFromTransform(current);
                    return true;
                }

                current = current.parent;
            }
        }

        return false;
    }

    private void ApplyBoost()
    {
        if (targetCamera == null)
            return;

        CaptureBasePriority();

        int boostedPriority = basePriority + priorityBoost;
        if (targetCamera.Priority != boostedPriority)
            targetCamera.Priority = boostedPriority;

        boosted = true;
    }

    private void ClearBoost()
    {
        if (!boosted || targetCamera == null)
            return;

        CaptureBasePriority();
        targetCamera.Priority = basePriority;
        boosted = false;
    }

    private void CaptureBasePriority()
    {
        if (basePriorityCaptured || targetCamera == null)
            return;

        basePriority = targetCamera.Priority;
        basePriorityCaptured = true;
    }

    private void UpdateScannedPlayerOverlap()
    {
        if (!scanPlayerInsideTrigger)
            return;

        if (triggerScanInterval > 0f && Time.time < nextTriggerScanTime)
            return;

        nextTriggerScanTime = Time.time + triggerScanInterval;

        bool playerInside = false;
        SlimePlayerAbilities player = ResolvePlayerAbilities();
        if (player != null)
            playerInside = IsPlayerInsideTrigger(player);

        if (!playerInside)
            playerInside = TryFindPlayerInsideTrigger();

        if (playerInside)
        {
            if (playerOverlapCount <= 0)
                playerOverlapCount = 1;

            TryUnlock();
            return;
        }

        if (playerOverlapCount > 0)
        {
            playerOverlapCount = 0;
            ClearBoost();
        }
    }

    private SlimePlayerAbilities ResolvePlayerAbilities()
    {
        if (cachedPlayer != null)
            return cachedPlayer;

        if (!string.IsNullOrWhiteSpace(playerObjectName))
        {
            GameObject playerObject = GameObject.Find(playerObjectName);
            if (playerObject != null)
                CachePlayerFromTransform(playerObject.transform);
        }

        if (cachedPlayer != null)
            return cachedPlayer;

#if UNITY_2023_1_OR_NEWER
        cachedPlayer = Object.FindFirstObjectByType<SlimePlayerAbilities>();
#else
#pragma warning disable 0618
        cachedPlayer = Object.FindObjectOfType<SlimePlayerAbilities>();
#pragma warning restore 0618
#endif
        return cachedPlayer;
    }

    private void CachePlayerFromTransform(Transform playerTransform)
    {
        if (playerTransform == null)
            return;

        cachedPlayer = playerTransform.GetComponentInParent<SlimePlayerAbilities>();
        if (cachedPlayer == null)
            cachedPlayer = playerTransform.GetComponentInChildren<SlimePlayerAbilities>();

        cachedPlayerColliders = null;
    }

    private bool IsPlayerInsideTrigger(SlimePlayerAbilities player)
    {
        Collider currentTrigger = ResolveTriggerCollider();
        if (player == null || currentTrigger == null)
            return false;

        Bounds triggerBounds = currentTrigger.bounds;
        triggerBounds.Expand(triggerScanPadding * 2f);
        if (triggerBounds.Contains(player.BodyCenterPosition) || triggerBounds.Contains(player.transform.position))
            return true;

        if (cachedPlayerColliders == null || cachedPlayerColliders.Length == 0)
            cachedPlayerColliders = player.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < cachedPlayerColliders.Length; i++)
        {
            Collider playerCollider = cachedPlayerColliders[i];
            if (playerCollider == null || !playerCollider.enabled || !playerCollider.gameObject.activeInHierarchy)
                continue;

            if ((playerLayerMask.value & (1 << playerCollider.gameObject.layer)) == 0)
                continue;

            Bounds playerBounds = playerCollider.bounds;
            playerBounds.Expand(triggerScanPadding * 2f);
            if (triggerBounds.Intersects(playerBounds))
                return true;
        }

        return false;
    }

    private bool TryFindPlayerInsideTrigger()
    {
        Collider currentTrigger = ResolveTriggerCollider();
        if (currentTrigger == null)
            return false;

        Bounds bounds = currentTrigger.bounds;
        bounds.Expand(triggerScanPadding * 2f);
        int hitCount = Physics.OverlapBoxNonAlloc(
            bounds.center,
            bounds.extents,
            scanHits,
            currentTrigger.transform.rotation,
            playerLayerMask,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = scanHits[i];
            scanHits[i] = null;
            if (hit != null && MatchesPlayer(hit))
                return true;
        }

        return false;
    }

    private Collider ResolveTriggerCollider()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;

        return triggerCollider;
    }

    private void EnsureTriggerCollider()
    {
        ResolveTriggerCollider();
    }

    private void OnValidate()
    {
        triggerScanInterval = Mathf.Max(0f, triggerScanInterval);
        triggerScanPadding = Mathf.Max(0f, triggerScanPadding);
        EnsureTriggerCollider();
    }
}
