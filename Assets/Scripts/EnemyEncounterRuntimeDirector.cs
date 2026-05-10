using Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class EnemyEncounterRuntimeDirector : MonoBehaviour
{
    [SerializeField] private EnemyBarrierController barrierController = null;
    [SerializeField] private SlimeWaveDamageReceiver[] requiredDefeats = null;

    [Header("Player")]
    [SerializeField] private LayerMask playerLayerMask = 1;
    [SerializeField] private string requiredTag = "";
    [SerializeField] private string playerObjectName = "Player";
    [SerializeField] private bool scanPlayerInsideTrigger = true;
    [SerializeField, Min(0f)] private float triggerScanInterval = 0.05f;
    [SerializeField, Min(0f)] private float triggerScanPadding = 0.75f;

    [Header("Clear View Camera")]
    [SerializeField] private CinemachineVirtualCamera clearViewCamera = null;
    [SerializeField] private string clearViewCameraName = "Enemy Arena Clear View Camera";
    [SerializeField] private Vector3 clearViewCameraPosition = new Vector3(15f, 22f, 23f);
    [SerializeField] private Vector3 clearViewCameraEulerAngles = new Vector3(62f, 0f, 0f);
    [SerializeField] private int clearViewCameraPriority = 18;
    [SerializeField] private float clearViewFieldOfView = 62f;
    [SerializeField] private int inactiveCameraPriority = 0;
    [SerializeField] private bool aimClearViewAtPlayer = true;
    [SerializeField] private bool useClearViewCameraBubble = true;
    [SerializeField] private LayerMask clearViewObstacleMask = ~0;
    [SerializeField, Min(0f)] private float clearViewCameraBubbleRadius = 0.8f;
    [SerializeField, Min(0.01f)] private float clearViewMinimumDistanceFromPlayer = 2.75f;

    [Header("Completion Recovery")]
    [SerializeField] private bool restorePlayerMassOnClear = true;
    [SerializeField, Min(0f)] private float clearedEncounterVolumeFloor = 0f;
    [SerializeField] private bool restorePlayerHealthOnClear = true;

    private int playerOverlapCount;
    private bool encounterStarted;
    private bool cameraActive;
    private bool clearRecoveryApplied;
    private SlimePlayerAbilities cachedPlayer;
    private readonly Collider[] scanHits = new Collider[32];
    private Collider triggerCollider;
    private Collider[] cachedPlayerColliders;
    private float nextTriggerScanTime;

    private void Awake()
    {
        ResolveReferences();
        EnsureTriggerCollider();
        EnsureClearViewCamera();
        DeactivateClearViewCamera();
    }

    private void Update()
    {
        ResolveReferences();

        if (EncounterCleared())
        {
            DeactivateClearViewCamera();
            ApplyCompletionRecovery();
            return;
        }

        UpdateScannedPlayerOverlap();

        if (encounterStarted)
            ForceBarrierUp();

        if (encounterStarted && playerOverlapCount > 0)
            ActivateClearViewCamera();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!MatchesPlayer(other))
            return;

        playerOverlapCount++;
        StartEncounter();
    }

    private void OnTriggerStay(Collider other)
    {
        if (!MatchesPlayer(other))
            return;

        if (playerOverlapCount <= 0)
            playerOverlapCount = 1;

        StartEncounter();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!MatchesPlayer(other))
            return;

        playerOverlapCount = Mathf.Max(0, playerOverlapCount - 1);
        if (playerOverlapCount == 0)
            DeactivateClearViewCamera();
    }

    private void OnDisable()
    {
        DeactivateClearViewCamera();
    }

    private void StartEncounter()
    {
        if (EncounterCleared())
        {
            DeactivateClearViewCamera();
            return;
        }

        bool wasAlreadyStarted = encounterStarted;
        encounterStarted = true;
        if (!wasAlreadyStarted)
            Heartwell.UI.InGameOverlayUI.ShowEnemyEncounter();

        ForceBarrierUp();
        ActivateClearViewCamera();
    }

    private void ForceBarrierUp()
    {
        if (barrierController != null)
            barrierController.ForceRaiseBarrier();
    }

    private void ActivateClearViewCamera()
    {
        EnsureClearViewCamera();
        if (clearViewCamera == null || EncounterCleared())
            return;

        ApplyClearViewPose();
        clearViewCamera.Priority = clearViewCameraPriority;
        cameraActive = true;
    }

    private void DeactivateClearViewCamera()
    {
        if (!cameraActive || clearViewCamera == null)
            return;

        clearViewCamera.Priority = inactiveCameraPriority;
        cameraActive = false;
    }

    private void EnsureClearViewCamera()
    {
        if (clearViewCamera != null)
        {
            ConfigureClearViewCamera();
            ApplyClearViewPose();
            return;
        }

        GameObject cameraObject = new GameObject(clearViewCameraName);
        clearViewCamera = cameraObject.AddComponent<CinemachineVirtualCamera>();
        clearViewCamera.Priority = inactiveCameraPriority;
        ConfigureClearViewCamera();
        ApplyClearViewPose();
    }

    private void ApplyClearViewPose()
    {
        if (clearViewCamera == null)
            return;

        clearViewCamera.transform.SetPositionAndRotation(
            clearViewCameraPosition,
            Quaternion.Euler(clearViewCameraEulerAngles));
        clearViewCamera.m_Lens.FieldOfView = Mathf.Max(1f, clearViewFieldOfView);
    }

    private void ConfigureClearViewCamera()
    {
        if (clearViewCamera == null)
            return;

        SlimePlayerAbilities player = ResolvePlayerAbilities();
        if (aimClearViewAtPlayer && player != null)
        {
            clearViewCamera.LookAt = player.transform;
            if (clearViewCamera.GetCinemachineComponent<CinemachineHardLookAt>() == null)
                clearViewCamera.AddCinemachineComponent<CinemachineHardLookAt>();
        }

        CinemachineCollider cameraCollider = clearViewCamera.GetComponent<CinemachineCollider>();
        if (!useClearViewCameraBubble)
        {
            if (cameraCollider != null)
                cameraCollider.enabled = false;

            return;
        }

        if (cameraCollider == null)
            cameraCollider = clearViewCamera.gameObject.AddComponent<CinemachineCollider>();

        cameraCollider.enabled = true;
        cameraCollider.m_AvoidObstacles = true;
        cameraCollider.m_CollideAgainst = clearViewObstacleMask;
        cameraCollider.m_TransparentLayers = 0;
        cameraCollider.m_MinimumDistanceFromTarget = Mathf.Max(0.01f, clearViewMinimumDistanceFromPlayer);
        cameraCollider.m_CameraRadius = Mathf.Max(0f, clearViewCameraBubbleRadius);
        cameraCollider.m_DistanceLimit = 0f;
        cameraCollider.m_MinimumOcclusionTime = 0f;
        cameraCollider.m_Strategy = CinemachineCollider.ResolutionStrategy.PreserveCameraHeight;
        cameraCollider.m_MaximumEffort = 8;
        cameraCollider.m_SmoothingTime = 0.05f;
        cameraCollider.m_Damping = 0.15f;
        cameraCollider.m_DampingWhenOccluded = 0.03f;
    }

    private bool EncounterCleared()
    {
        if (requiredDefeats == null || requiredDefeats.Length == 0)
            return false;

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

    private void ApplyCompletionRecovery()
    {
        if (clearRecoveryApplied || !restorePlayerMassOnClear)
            return;

        SlimePlayerAbilities player = ResolvePlayerAbilities();
        if (player == null)
            return;

        if (clearedEncounterVolumeFloor > 0f)
            player.RestoreVolumeToAtLeast(clearedEncounterVolumeFloor, restorePlayerHealthOnClear);
        else
            player.RestoreBaseVolume(restorePlayerHealthOnClear);

        clearRecoveryApplied = true;
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

    private void ResolveReferences()
    {
        if (barrierController == null)
        {
#if UNITY_2023_1_OR_NEWER
            barrierController = Object.FindFirstObjectByType<EnemyBarrierController>();
#else
#pragma warning disable 0618
            barrierController = Object.FindObjectOfType<EnemyBarrierController>();
#pragma warning restore 0618
#endif
        }
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

            StartEncounter();
            return;
        }

        if (playerOverlapCount > 0)
        {
            playerOverlapCount = 0;
            DeactivateClearViewCamera();
        }
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
        clearViewCameraPriority = Mathf.Max(inactiveCameraPriority + 1, clearViewCameraPriority);
        clearViewFieldOfView = Mathf.Max(1f, clearViewFieldOfView);
        triggerScanInterval = Mathf.Max(0f, triggerScanInterval);
        triggerScanPadding = Mathf.Max(0f, triggerScanPadding);
        clearViewCameraBubbleRadius = Mathf.Max(0f, clearViewCameraBubbleRadius);
        clearViewMinimumDistanceFromPlayer = Mathf.Max(0.01f, clearViewMinimumDistanceFromPlayer);
        clearedEncounterVolumeFloor = Mathf.Max(0f, clearedEncounterVolumeFloor);
        EnsureTriggerCollider();
    }
}
