using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class DungeonFireflySystem : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
    private static readonly int UnlitColorId = Shader.PropertyToID("_UnlitColor");

    [Header("Spawn Bounds")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField, Min(0)] private int fireflyCount = 45;
    [FormerlySerializedAs("maxFireflyCount")]
    [SerializeField, Min(1)] private int maxSpawnNumber = 80;
    [SerializeField] private BoxCollider boundingBox = null;
    [SerializeField] private LayerMask floorMask = ~0;
    [SerializeField] private float floorRaycastHeight = 8f;
    [SerializeField] private float floorRaycastDistance = 18f;
    [SerializeField] private float minHeightAboveFloor = 0.12f;
    [SerializeField] private float maxHeightAboveFloor = 1.25f;
    [SerializeField] private bool snapToFloorHeight = true;

    [Header("Visuals")]
    [SerializeField] private GameObject fireflyPrefab = null;
    [SerializeField] private Color fireflyColor = new Color(1f, 0.82f, 0.28f, 1f);
    [SerializeField] private float visualSize = 0.075f;
    [SerializeField] private float glowBrightness = 4f;
    [SerializeField] private int maxLightedFireflies = 18;
    [SerializeField] private float lightRange = 1.6f;
    [SerializeField] private float lightIntensity = 0.9f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 0.55f;
    [SerializeField] private float turnSpeed = 3.5f;
    [SerializeField] private float wanderNoiseSpeed = 0.7f;
    [SerializeField] private float boundaryPull = 1.35f;
    [SerializeField] private float heightFollowSpeed = 6f;
    [SerializeField] private float bobAmount = 0.18f;
    [SerializeField] private float bobSpeed = 2.1f;
    [SerializeField] private float flickerSpeed = 3.2f;
    [SerializeField, Range(0f, 1f)] private float flickerAmount = 0.45f;

    [Header("Dungeon Darkness")]
    [SerializeField] private bool darkenAmbientOnStart = false;
    [SerializeField] private Color darkAmbientColor = new Color(0.015f, 0.02f, 0.035f, 1f);
    [SerializeField, Range(0f, 1f)] private float darkAmbientIntensity = 0.18f;

    private readonly List<Firefly> fireflies = new List<Firefly>();
    private Material runtimeMaterial;
    private Transform runtimeRoot;
    private bool missingBoundingBoxWarningShown;

    private sealed class Firefly
    {
        public Transform Transform;
        public Renderer Renderer;
        public Light Light;
        public MaterialPropertyBlock PropertyBlock;
        public Vector3 Velocity;
        public float HeightOffset;
        public float SpeedMultiplier;
        public float Seed;
    }

    private void Reset()
    {
        boundingBox = GetComponent<BoxCollider>();
    }

    private void Start()
    {
        TryResolveBoundingBox(true);

        if (darkenAmbientOnStart)
            ApplyDungeonDarkness();

        if (playOnStart)
            Respawn();
    }

    private void OnDisable()
    {
        for (int i = 0; i < fireflies.Count; i++)
        {
            if (fireflies[i].Light != null)
                fireflies[i].Light.enabled = false;
        }
    }

    private void OnEnable()
    {
        for (int i = 0; i < fireflies.Count; i++)
        {
            if (fireflies[i].Light != null)
                fireflies[i].Light.enabled = true;
        }
    }

    private void OnDestroy()
    {
        ClearFireflies();

        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        float time = Time.time;

        for (int i = 0; i < fireflies.Count; i++)
            UpdateFirefly(fireflies[i], deltaTime, time);
    }

    public void Respawn()
    {
        ClearFireflies();

        if (!TryResolveBoundingBox(true))
            return;

        EnsureRuntimeRoot();
        EnsureMaterial();

        int spawnCount = GetSpawnCount();
        int lightCount = Mathf.Min(maxLightedFireflies, spawnCount);
        for (int i = 0; i < spawnCount; i++)
        {
            Firefly firefly = CreateFirefly(i < lightCount);
            firefly.Transform.position = GetRandomSpawnPosition();
            firefly.Velocity = Random.insideUnitSphere;
            firefly.Velocity.y = 0f;
            firefly.HeightOffset = Random.Range(minHeightAboveFloor, maxHeightAboveFloor);
            firefly.SpeedMultiplier = Random.Range(0.65f, 1.35f);
            firefly.Seed = Random.Range(0f, 1000f);
            fireflies.Add(firefly);
        }
    }

    public void ClearFireflies()
    {
        for (int i = fireflies.Count - 1; i >= 0; i--)
        {
            if (fireflies[i].Transform != null)
                Destroy(fireflies[i].Transform.gameObject);
        }

        fireflies.Clear();
    }

    public void ApplyDungeonDarkness()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = darkAmbientColor;
        RenderSettings.ambientIntensity = darkAmbientIntensity;
    }

    private void UpdateFirefly(Firefly firefly, float deltaTime, float time)
    {
        if (firefly.Transform == null)
            return;
        if (!TryResolveBoundingBox(false))
            return;

        Transform boundsTransform = GetBoundsTransform();
        Vector3 boundsCenter = GetBoundsCenter();
        Vector3 position = firefly.Transform.position;
        Vector3 localPosition = boundsTransform.InverseTransformPoint(position) - boundsCenter;
        Vector3 halfSize = GetHalfBoundsSize();

        float noiseX = Mathf.PerlinNoise(firefly.Seed, time * wanderNoiseSpeed) * 2f - 1f;
        float noiseZ = Mathf.PerlinNoise(firefly.Seed + 37.5f, time * wanderNoiseSpeed) * 2f - 1f;
        Vector3 desiredDirection = boundsTransform.TransformDirection(new Vector3(noiseX, 0f, noiseZ));

        Vector3 localPull = Vector3.zero;
        localPull.x = GetBoundaryPull(localPosition.x, halfSize.x);
        localPull.z = GetBoundaryPull(localPosition.z, halfSize.z);

        if (localPull.sqrMagnitude > 0.001f)
            desiredDirection += boundsTransform.TransformDirection(localPull) * boundaryPull;

        if (desiredDirection.sqrMagnitude < 0.001f)
            desiredDirection = firefly.Velocity.sqrMagnitude > 0.001f ? firefly.Velocity.normalized : transform.forward;

        Vector3 targetVelocity = desiredDirection.normalized * moveSpeed * firefly.SpeedMultiplier;
        firefly.Velocity = Vector3.Lerp(firefly.Velocity, targetVelocity, 1f - Mathf.Exp(-turnSpeed * deltaTime));
        position += firefly.Velocity * deltaTime;

        position = ClampToBounds(position, boundsTransform, boundsCenter, halfSize);

        float floorY = snapToFloorHeight ? FindFloorY(position) : GetBoundsWorldCenter().y;
        float bob = Mathf.Sin((time + firefly.Seed) * bobSpeed * firefly.SpeedMultiplier) * bobAmount;
        float verticalOffset = Mathf.Clamp(firefly.HeightOffset + bob, minHeightAboveFloor, maxHeightAboveFloor);
        float targetY = floorY + verticalOffset;
        position.y = Mathf.Lerp(position.y, targetY, 1f - Mathf.Exp(-heightFollowSpeed * deltaTime));
        position = ClampToBounds(position, boundsTransform, boundsCenter, halfSize);

        firefly.Transform.position = position;

        float flicker = 1f + (Mathf.PerlinNoise(firefly.Seed + 91.2f, time * flickerSpeed) * 2f - 1f) * flickerAmount;
        ApplyFireflyGlow(firefly, Mathf.Max(0.05f, flicker));
    }

    private Firefly CreateFirefly(bool createLight)
    {
        GameObject instance;
        if (fireflyPrefab != null)
        {
            instance = Instantiate(fireflyPrefab, runtimeRoot);
        }
        else
        {
            instance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            instance.name = "Firefly";
            instance.transform.SetParent(runtimeRoot, false);
            instance.transform.localScale = Vector3.one * visualSize;

            Collider fireflyCollider = instance.GetComponent<Collider>();
            if (fireflyCollider != null)
                fireflyCollider.isTrigger = true;

            Renderer generatedRenderer = instance.GetComponent<Renderer>();
            if (generatedRenderer != null && runtimeMaterial != null)
                generatedRenderer.sharedMaterial = runtimeMaterial;
        }

        if (fireflyPrefab != null)
            instance.transform.localScale *= visualSize;

        Firefly firefly = new Firefly
        {
            Transform = instance.transform,
            Renderer = instance.GetComponentInChildren<Renderer>(),
            PropertyBlock = new MaterialPropertyBlock()
        };

        if (createLight)
        {
            GameObject lightObject = new GameObject("Glow");
            lightObject.transform.SetParent(instance.transform, false);

            Light pointLight = lightObject.AddComponent<Light>();
            pointLight.type = LightType.Point;
            pointLight.color = fireflyColor;
            pointLight.range = lightRange;
            pointLight.intensity = lightIntensity;
            firefly.Light = pointLight;
        }

        ApplyFireflyGlow(firefly, 1f);
        return firefly;
    }

    private void ApplyFireflyGlow(Firefly firefly, float multiplier)
    {
        Color glowColor = fireflyColor * glowBrightness * multiplier;
        glowColor.a = fireflyColor.a;

        if (firefly.Renderer != null)
        {
            firefly.Renderer.GetPropertyBlock(firefly.PropertyBlock);
            firefly.PropertyBlock.SetColor(BaseColorId, fireflyColor);
            firefly.PropertyBlock.SetColor(ColorId, fireflyColor);
            firefly.PropertyBlock.SetColor(UnlitColorId, glowColor);
            firefly.PropertyBlock.SetColor(EmissionColorId, glowColor);
            firefly.PropertyBlock.SetColor(EmissiveColorId, glowColor);
            firefly.Renderer.SetPropertyBlock(firefly.PropertyBlock);
        }

        if (firefly.Light != null)
            firefly.Light.intensity = lightIntensity * multiplier;
    }

    private Vector3 GetRandomSpawnPosition()
    {
        Transform boundsTransform = GetBoundsTransform();
        Vector3 boundsCenter = GetBoundsCenter();
        Vector3 halfSize = GetHalfBoundsSize();
        Vector3 localPosition = new Vector3(
            Random.Range(-halfSize.x, halfSize.x),
            0f,
            Random.Range(-halfSize.z, halfSize.z));

        Vector3 worldPosition = boundsTransform.TransformPoint(boundsCenter + localPosition);
        float floorY = snapToFloorHeight ? FindFloorY(worldPosition) : GetBoundsWorldCenter().y;
        worldPosition.y = floorY + Random.Range(minHeightAboveFloor, maxHeightAboveFloor);
        return ClampToBounds(worldPosition, boundsTransform, boundsCenter, halfSize);
    }

    private float FindFloorY(Vector3 position)
    {
        Vector3 origin = new Vector3(position.x, GetBoundsWorldCenter().y + floorRaycastHeight, position.z);
        float distance = Mathf.Max(0.1f, floorRaycastHeight + floorRaycastDistance);

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, distance, floorMask, QueryTriggerInteraction.Ignore))
            return hit.point.y;

        return GetBoundsWorldCenter().y;
    }

    private Vector3 ClampToBounds(Vector3 worldPosition, Transform boundsTransform, Vector3 boundsCenter, Vector3 halfSize)
    {
        Vector3 localPosition = boundsTransform.InverseTransformPoint(worldPosition) - boundsCenter;
        localPosition.x = Mathf.Clamp(localPosition.x, -halfSize.x, halfSize.x);
        localPosition.y = Mathf.Clamp(localPosition.y, -halfSize.y, halfSize.y);
        localPosition.z = Mathf.Clamp(localPosition.z, -halfSize.z, halfSize.z);
        return boundsTransform.TransformPoint(boundsCenter + localPosition);
    }

    private float GetBoundaryPull(float localValue, float halfExtent)
    {
        if (halfExtent <= 0.001f)
            return 0f;

        float edgeStart = halfExtent * 0.72f;
        float distanceFromCenter = Mathf.Abs(localValue);
        if (distanceFromCenter <= edgeStart)
            return 0f;

        float edge01 = Mathf.InverseLerp(edgeStart, halfExtent, distanceFromCenter);
        return -Mathf.Sign(localValue) * edge01;
    }

    private bool TryResolveBoundingBox(bool warnIfMissing)
    {
        if (boundingBox == null)
            boundingBox = GetComponent<BoxCollider>();

        if (boundingBox != null)
        {
            missingBoundingBoxWarningShown = false;
            return true;
        }

        if (warnIfMissing && !missingBoundingBoxWarningShown)
        {
            Debug.LogWarning("DungeonFireflySystem needs a BoxCollider assigned to Bounding Box, or a BoxCollider on the same GameObject.", this);
            missingBoundingBoxWarningShown = true;
        }

        return false;
    }

    private Transform GetBoundsTransform()
    {
        return boundingBox.transform;
    }

    private Vector3 GetBoundsCenter()
    {
        return boundingBox.center;
    }

    private Vector3 GetBoundsWorldCenter()
    {
        return GetBoundsTransform().TransformPoint(GetBoundsCenter());
    }

    private Vector3 GetBoundsSize()
    {
        return boundingBox.size;
    }

    private Vector3 GetHalfBoundsSize()
    {
        Vector3 size = GetBoundsSize();
        return new Vector3(
            Mathf.Max(0.05f, Mathf.Abs(size.x)) * 0.5f,
            Mathf.Max(0.05f, Mathf.Abs(size.y)) * 0.5f,
            Mathf.Max(0.05f, Mathf.Abs(size.z)) * 0.5f);
    }

    private int GetSpawnCount()
    {
        return Mathf.Min(fireflyCount, maxSpawnNumber);
    }

    private void EnsureRuntimeRoot()
    {
        if (runtimeRoot != null)
            return;

        GameObject root = new GameObject("Runtime Fireflies");
        root.transform.SetParent(transform, false);
        runtimeRoot = root.transform;
    }

    private void EnsureMaterial()
    {
        if (runtimeMaterial != null)
            return;

        Shader shader = Shader.Find("HDRP/Unlit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
        {
            Debug.LogWarning("DungeonFireflySystem could not find a usable shader for generated fireflies.", this);
            return;
        }

        runtimeMaterial = new Material(shader);
        runtimeMaterial.name = "Runtime Firefly Glow";
        runtimeMaterial.EnableKeyword("_EMISSION");
        SetMaterialColor(BaseColorId, fireflyColor);
        SetMaterialColor(ColorId, fireflyColor);
        SetMaterialColor(UnlitColorId, fireflyColor * glowBrightness);
        SetMaterialColor(EmissionColorId, fireflyColor * glowBrightness);
        SetMaterialColor(EmissiveColorId, fireflyColor * glowBrightness);
    }

    private void SetMaterialColor(int propertyId, Color color)
    {
        if (runtimeMaterial != null && runtimeMaterial.HasProperty(propertyId))
            runtimeMaterial.SetColor(propertyId, color);
    }

    private void OnValidate()
    {
        TryResolveBoundingBox(false);
        maxSpawnNumber = Mathf.Max(1, maxSpawnNumber);
        fireflyCount = Mathf.Clamp(fireflyCount, 0, maxSpawnNumber);
        floorRaycastHeight = Mathf.Max(0.1f, floorRaycastHeight);
        floorRaycastDistance = Mathf.Max(0.1f, floorRaycastDistance);
        maxHeightAboveFloor = Mathf.Max(0.01f, maxHeightAboveFloor);
        minHeightAboveFloor = Mathf.Clamp(minHeightAboveFloor, 0.01f, maxHeightAboveFloor);
        visualSize = Mathf.Max(0.005f, visualSize);
        glowBrightness = Mathf.Max(0f, glowBrightness);
        maxLightedFireflies = Mathf.Max(0, maxLightedFireflies);
        lightRange = Mathf.Max(0f, lightRange);
        lightIntensity = Mathf.Max(0f, lightIntensity);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        turnSpeed = Mathf.Max(0f, turnSpeed);
        wanderNoiseSpeed = Mathf.Max(0f, wanderNoiseSpeed);
        boundaryPull = Mathf.Max(0f, boundaryPull);
        heightFollowSpeed = Mathf.Max(0f, heightFollowSpeed);
        bobAmount = Mathf.Max(0f, bobAmount);
        bobSpeed = Mathf.Max(0f, bobSpeed);
        flickerSpeed = Mathf.Max(0f, flickerSpeed);
    }

    private void OnDrawGizmosSelected()
    {
        if (!TryResolveBoundingBox(false))
            return;

        Transform boundsTransform = GetBoundsTransform();
        Gizmos.color = new Color(1f, 0.82f, 0.25f, 0.25f);
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = boundsTransform.localToWorldMatrix;
        Gizmos.DrawCube(GetBoundsCenter(), GetBoundsSize());
        Gizmos.color = new Color(1f, 0.82f, 0.25f, 0.85f);
        Gizmos.DrawWireCube(GetBoundsCenter(), GetBoundsSize());
        Gizmos.matrix = previousMatrix;
    }
}
