using UnityEngine;

[DisallowMultipleComponent]
public class EnemyBarrierController : MonoBehaviour
{
    private readonly RaycastHit[] groundHits = new RaycastHit[12];

    [Header("References")]
    [SerializeField] private EnemyFireballAttacker attacker = null;
    [SerializeField] private SlimeWaveDamageReceiver defeatReceiver = null;
    [SerializeField] private SlimeWaveDamageReceiver[] additionalDefeatReceivers = null;
    [SerializeField] private bool requireAllDefeatReceiversForBarrier = false;
    [SerializeField] private GameObject[] barrierVisualPrefabs = null;

    [Header("Guide")]
    [SerializeField] private Transform guideTransform = null;
    [SerializeField] private string guideObjectName = "barrier";
    [SerializeField] private bool useGuideBounds = true;
    [SerializeField] private bool useLongestHorizontalGuideAxis = true;
    [SerializeField] private bool hideGuideRendererAtRuntime = true;
    [SerializeField] private bool disableGuideColliderAtRuntime = true;

    [Header("Baked Barrier")]
    [SerializeField] private bool useBakedBarrierPose = true;
    [SerializeField] private Vector3 bakedBarrierBasePosition = Vector3.zero;
    [SerializeField] private Vector3 bakedBarrierEulerAngles = Vector3.zero;
    [SerializeField] private float bakedBarrierLength = 7f;
    [SerializeField] private float bakedBarrierHeight = 3.2f;
    [SerializeField] private float bakedBarrierThickness = 0.85f;

    [Header("Placement")]
    [SerializeField] private float distanceFromEnemy = 5f;
    [SerializeField] private float barrierLength = 7f;
    [SerializeField] private float barrierHeight = 3.2f;
    [SerializeField] private float barrierThickness = 0.85f;

    [Header("Visuals")]
    [SerializeField] private float visualSpacing = 0.55f;
    [SerializeField] private int visualRows = 2;
    [SerializeField] private float visualRowDepth = 1.25f;
    [SerializeField] private float visualScale = 1.55f;
    [SerializeField] private float visualSinkDepth = 2.4f;
    [SerializeField] private float riseDuration = 0.75f;

    [Header("Grounding")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundProbeStart = 7f;
    [SerializeField] private float groundProbeDistance = 18f;

    private GameObject barrierRoot;
    private Transform visualRoot;
    private BoxCollider barrierCollider;
    private float riseElapsed;
    private float runtimeBarrierLength;
    private float runtimeBarrierHeight;
    private float runtimeBarrierThickness;
    private bool barrierWasRaised;
    private bool barrierDestroyed;

    private void Awake()
    {
        ResolveReferences();
        ResolveGuide();
        ApplyGuideRuntimeVisibility();
    }

    private void Update()
    {
        ResolveReferences();

        if (ShouldDestroyBarrier())
        {
            DestroyBarrier();
            return;
        }

        if (barrierRoot != null)
            UpdateBarrierRise();

        if (barrierWasRaised || barrierDestroyed || attacker == null || !attacker.IsActive)
            return;

        RaiseBarrier();
    }

    private void ResolveReferences()
    {
        if (attacker == null)
            attacker = GetComponent<EnemyFireballAttacker>();

        if (defeatReceiver == null)
            defeatReceiver = GetComponent<SlimeWaveDamageReceiver>();
    }

    private void RaiseBarrier()
    {
        ResolveGuide();

        if (!TryGetBarrierPose(out Vector3 position, out Quaternion rotation, out float length, out float height, out float thickness))
            return;

        runtimeBarrierLength = length;
        runtimeBarrierHeight = height;
        runtimeBarrierThickness = thickness;

        barrierRoot = new GameObject("Enemy Thuja Barrier");
        barrierRoot.transform.SetPositionAndRotation(position, rotation);

        barrierCollider = barrierRoot.AddComponent<BoxCollider>();
        barrierCollider.isTrigger = false;
        barrierCollider.size = new Vector3(runtimeBarrierLength, runtimeBarrierHeight, runtimeBarrierThickness);
        barrierCollider.center = new Vector3(0f, runtimeBarrierHeight * 0.5f, 0f);

        GameObject visuals = new GameObject("Thuja Visuals");
        visualRoot = visuals.transform;
        visualRoot.SetParent(barrierRoot.transform, false);
        visualRoot.localPosition = Vector3.down * visualSinkDepth;

        ApplyGuideRuntimeVisibility();
        SpawnBarrierVisuals();

        riseElapsed = 0f;
        barrierWasRaised = true;
        Heartwell.UI.InGameOverlayUI.ShowBarrierHint();
        UpdateBarrierRise();
    }

    public void ForceRaiseBarrier()
    {
        ResolveReferences();

        if (barrierDestroyed || barrierWasRaised || barrierRoot != null)
            return;

        if (ShouldDestroyBarrier())
        {
            DestroyBarrier();
            return;
        }

        RaiseBarrier();
    }

    private bool TryGetBarrierPose(out Vector3 position, out Quaternion rotation, out float length, out float height, out float thickness)
    {
        if (useBakedBarrierPose)
        {
            position = bakedBarrierBasePosition;
            rotation = Quaternion.Euler(bakedBarrierEulerAngles);
            length = Mathf.Max(0.5f, bakedBarrierLength);
            height = Mathf.Max(0.5f, bakedBarrierHeight);
            thickness = Mathf.Max(0.1f, bakedBarrierThickness);
            return true;
        }

        if (TryGetGuidePose(out position, out rotation, out length, out height, out thickness))
            return true;

        Vector3 enemyPosition = transform.position;
        Vector3 direction = transform.forward;
        Transform targetToIgnore = null;

        if (attacker != null && attacker.TryGetTarget(out Transform target) && target != null)
        {
            direction = target.position - enemyPosition;
            targetToIgnore = target;
        }

        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = transform.forward;

        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;

        position = enemyPosition + direction * distanceFromEnemy;
        position = SnapToGround(position, targetToIgnore);
        rotation = Quaternion.LookRotation(direction, Vector3.up);
        length = barrierLength;
        height = barrierHeight;
        thickness = barrierThickness;
        return true;
    }

    private bool TryGetGuidePose(out Vector3 position, out Quaternion rotation, out float length, out float height, out float thickness)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        length = barrierLength;
        height = barrierHeight;
        thickness = barrierThickness;

        if (guideTransform == null)
            return false;

        position = guideTransform.position;
        rotation = guideTransform.rotation;

        if (useGuideBounds)
        {
            Vector3 guideSize = ResolveGuideSize();
            position = ResolveGuideBasePosition(position);
            bool lengthUsesZ = useLongestHorizontalGuideAxis && guideSize.z > guideSize.x;
            Vector3 lengthDirection = ResolveGuideLengthDirection(lengthUsesZ);
            Vector3 barrierForward = Vector3.Cross(lengthDirection, Vector3.up);
            if (barrierForward.sqrMagnitude > 0.0001f)
                rotation = Quaternion.LookRotation(barrierForward.normalized, Vector3.up);

            length = Mathf.Max(0.5f, lengthUsesZ ? guideSize.z : guideSize.x);
            height = Mathf.Max(barrierHeight, guideSize.y);
            thickness = Mathf.Max(0.1f, lengthUsesZ ? guideSize.x : guideSize.z);
        }

        return true;
    }

    private Vector3 ResolveGuideLengthDirection(bool lengthUsesZ)
    {
        if (guideTransform == null)
            return Vector3.right;

        Vector3 direction = lengthUsesZ ? guideTransform.forward : guideTransform.right;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = lengthUsesZ ? guideTransform.right : guideTransform.forward;
            direction.y = 0f;
        }

        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right;
    }

    private Vector3 ResolveGuideSize()
    {
        Vector3 lossyScale = guideTransform != null ? guideTransform.lossyScale : Vector3.one;
        Vector3 guideSize = new Vector3(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z));

        BoxCollider box = guideTransform != null ? guideTransform.GetComponent<BoxCollider>() : null;
        if (box != null)
            guideSize = Vector3.Scale(box.size, guideSize);

        return new Vector3(
            Mathf.Max(0.5f, Mathf.Abs(guideSize.x)),
            Mathf.Max(0.5f, Mathf.Abs(guideSize.y)),
            Mathf.Max(0.1f, Mathf.Abs(guideSize.z)));
    }

    private Vector3 ResolveGuideBasePosition(Vector3 position)
    {
        if (guideTransform == null)
            return position;

        BoxCollider box = guideTransform.GetComponent<BoxCollider>();
        if (box != null)
        {
            Vector3 localBottom = box.center + Vector3.down * box.size.y * 0.5f;
            position.y = guideTransform.TransformPoint(localBottom).y;
            return position;
        }

        Renderer guideRenderer = guideTransform.GetComponentInChildren<Renderer>();
        if (guideRenderer != null)
            position.y = guideRenderer.bounds.min.y;

        return position;
    }

    private Vector3 SnapToGround(Vector3 position, Transform targetToIgnore)
    {
        Vector3 rayStart = position + Vector3.up * Mathf.Max(0f, groundProbeStart);
        float rayDistance = Mathf.Max(0.01f, groundProbeStart + groundProbeDistance);
        int hitCount = Physics.RaycastNonAlloc(rayStart, Vector3.down, groundHits, rayDistance, groundMask, QueryTriggerInteraction.Ignore);
        float bestDistance = float.PositiveInfinity;
        RaycastHit bestHit = default;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = groundHits[i].collider;
            if (ShouldIgnoreGroundHit(hitCollider, targetToIgnore))
                continue;

            if (groundHits[i].distance < bestDistance)
            {
                bestHit = groundHits[i];
                bestDistance = groundHits[i].distance;
            }
        }

        if (bestHit.collider != null)
            position.y = bestHit.point.y;

        return position;
    }

    private bool ShouldIgnoreGroundHit(Collider hitCollider, Transform targetToIgnore)
    {
        if (hitCollider == null)
            return true;

        Transform hitTransform = hitCollider.transform;
        if (hitTransform == transform || hitTransform.IsChildOf(transform))
            return true;

        return targetToIgnore != null && (hitTransform == targetToIgnore || hitTransform.IsChildOf(targetToIgnore));
    }

    private void SpawnBarrierVisuals()
    {
        if (visualRoot == null || barrierVisualPrefabs == null || barrierVisualPrefabs.Length == 0)
            return;

        int count = Mathf.Max(2, Mathf.RoundToInt(runtimeBarrierLength / Mathf.Max(0.1f, visualSpacing)) + 1);
        int rows = Mathf.Max(1, visualRows);
        float usableLength = Mathf.Max(0.01f, runtimeBarrierLength);
        float rowDepth = Mathf.Max(runtimeBarrierThickness, visualRowDepth);

        for (int row = 0; row < rows; row++)
        {
            float rowT = rows <= 1 ? 0.5f : row / (float)(rows - 1);
            float rowZ = Mathf.Lerp(-rowDepth * 0.5f, rowDepth * 0.5f, rowT);
            float rowOffset = row % 2 == 0 ? 0f : 0.5f;

            for (int i = 0; i < count; i++)
            {
                int visualIndex = row * count + i;
                GameObject prefab = barrierVisualPrefabs[visualIndex % barrierVisualPrefabs.Length];
                if (prefab == null)
                    continue;

                GameObject visual = Instantiate(prefab, visualRoot);
                visual.name = prefab.name;

                float t = count <= 1 ? 0.5f : Mathf.Clamp01((i + rowOffset) / (float)(count - 1));
                float x = Mathf.Lerp(-usableLength * 0.5f, usableLength * 0.5f, t);
                float z = rowZ + Mathf.Sin(visualIndex * 2.31f) * rowDepth * 0.09f;
                float yaw = Mathf.Sin(visualIndex * 1.71f) * 12f;
                float scale = visualScale * Mathf.Lerp(0.92f, 1.18f, Mathf.Repeat(visualIndex * 0.37f, 1f));

                visual.transform.localPosition = new Vector3(x, 0f, z);
                visual.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                visual.transform.localScale = Vector3.one * scale;
                DisableVisualColliders(visual);
            }
        }
    }

    private static void DisableVisualColliders(GameObject visual)
    {
        Collider[] colliders = visual.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;
    }

    private void ResolveGuide()
    {
        if (guideTransform != null || string.IsNullOrWhiteSpace(guideObjectName))
            return;

        GameObject guideObject = GameObject.Find(guideObjectName);
        if (guideObject == null)
            guideObject = FindObjectByNameIgnoreCase(guideObjectName);

        if (guideObject != null && guideObject.transform != transform)
            guideTransform = guideObject.transform;
    }

    private static GameObject FindObjectByNameIgnoreCase(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

#if UNITY_2023_1_OR_NEWER
        Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
#else
#pragma warning disable 0618
        Transform[] transforms = Object.FindObjectsOfType<Transform>();
#pragma warning restore 0618
#endif
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null && string.Equals(transforms[i].name, objectName, System.StringComparison.OrdinalIgnoreCase))
                return transforms[i].gameObject;
        }

        return null;
    }

    private void ApplyGuideRuntimeVisibility()
    {
        if (guideTransform == null)
            return;

        if (hideGuideRendererAtRuntime)
        {
            Renderer[] renderers = guideTransform.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].enabled = false;
        }

        if (disableGuideColliderAtRuntime)
        {
            Collider[] colliders = guideTransform.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;
        }
    }

    private void UpdateBarrierRise()
    {
        if (visualRoot == null)
            return;

        riseElapsed += Time.deltaTime;
        float duration = Mathf.Max(0.01f, riseDuration);
        float t = Mathf.Clamp01(riseElapsed / duration);
        float easedT = EaseOutCubic(t);
        visualRoot.localPosition = Vector3.Lerp(Vector3.down * visualSinkDepth, Vector3.zero, easedT);

        if (barrierCollider != null)
            barrierCollider.enabled = true;
    }

    private void DestroyBarrier()
    {
        barrierDestroyed = true;

        if (barrierRoot != null)
        {
            Destroy(barrierRoot);
            barrierRoot = null;
            visualRoot = null;
            barrierCollider = null;
        }
    }

    private bool ShouldDestroyBarrier()
    {
        if (!requireAllDefeatReceiversForBarrier)
            return defeatReceiver != null && defeatReceiver.IsDefeated;

        bool hasReceiver = false;

        if (defeatReceiver != null)
        {
            hasReceiver = true;
            if (!defeatReceiver.IsDefeated)
                return false;
        }

        if (additionalDefeatReceivers != null)
        {
            for (int i = 0; i < additionalDefeatReceivers.Length; i++)
            {
                SlimeWaveDamageReceiver receiver = additionalDefeatReceivers[i];
                if (receiver == null)
                    continue;

                hasReceiver = true;
                if (!receiver.IsDefeated)
                    return false;
            }
        }

        return hasReceiver;
    }

    private static float EaseOutCubic(float value)
    {
        value = Mathf.Clamp01(value);
        float inverse = 1f - value;
        return 1f - inverse * inverse * inverse;
    }

    private void OnDestroy()
    {
        DestroyBarrier();
    }

    private void OnValidate()
    {
        distanceFromEnemy = Mathf.Max(0.1f, distanceFromEnemy);
        bakedBarrierLength = Mathf.Max(0.5f, bakedBarrierLength);
        bakedBarrierHeight = Mathf.Max(0.5f, bakedBarrierHeight);
        bakedBarrierThickness = Mathf.Max(0.1f, bakedBarrierThickness);
        barrierLength = Mathf.Max(0.5f, barrierLength);
        barrierHeight = Mathf.Max(0.5f, barrierHeight);
        barrierThickness = Mathf.Max(0.1f, barrierThickness);
        visualSpacing = Mathf.Max(0.1f, visualSpacing);
        visualRows = Mathf.Max(1, visualRows);
        visualRowDepth = Mathf.Max(0.1f, visualRowDepth);
        visualScale = Mathf.Max(0.05f, visualScale);
        visualSinkDepth = Mathf.Max(0f, visualSinkDepth);
        riseDuration = Mathf.Max(0.01f, riseDuration);
        groundProbeStart = Mathf.Max(0f, groundProbeStart);
        groundProbeDistance = Mathf.Max(0.01f, groundProbeDistance);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.15f, 0.65f, 0.2f, 0.35f);
        Vector3 position = useBakedBarrierPose ? bakedBarrierBasePosition + Vector3.up * bakedBarrierHeight * 0.5f : transform.position + transform.forward * distanceFromEnemy + Vector3.up * barrierHeight * 0.5f;
        Quaternion rotation = useBakedBarrierPose ? Quaternion.Euler(bakedBarrierEulerAngles) : transform.rotation;
        Vector3 size = useBakedBarrierPose
            ? new Vector3(bakedBarrierLength, bakedBarrierHeight, bakedBarrierThickness)
            : new Vector3(barrierLength, barrierHeight, barrierThickness);
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(position, rotation, Vector3.one);
        Gizmos.DrawCube(Vector3.zero, size);
        Gizmos.matrix = oldMatrix;
    }
}
