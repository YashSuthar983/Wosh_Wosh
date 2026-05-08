using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SlimePlayerAbilities : MonoBehaviour
{
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [Header("References")]
    [SerializeField] private Transform bodyRoot = null;
    [SerializeField] private Renderer slimeRenderer = null;
    [SerializeField] private Material slimeMaterialOverride = null;
    [SerializeField] private Rigidbody bodyRigidbody = null;
    [SerializeField] private GameObject splitPiecePrefab = null;
    [SerializeField] private GameObject bridgePrefab = null;

    [Header("Input")]
    [SerializeField] private bool readKeyboardInput = true;
    [SerializeField] private Transform cameraTransform = null;
    [SerializeField] private bool cameraRelativeAim = true;
    [SerializeField] private KeyCode splitKey = KeyCode.Q;
    [SerializeField] private KeyCode mergeKey = KeyCode.E;
    [SerializeField] private KeyCode stretchKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode bridgeKey = KeyCode.F;

    [Header("Body")]
    [SerializeField] private float baseVolume = 1f;
    [SerializeField] private float maxVolume = 3f;
    [SerializeField] private float minBodyVolume = 0.25f;
    [SerializeField] private float shapeLerpSpeed = 10f;
    [SerializeField] private float maxHealth = 1f;

    [Header("Merge")]
    [SerializeField] private float mergeScale = 1.25f;
    [SerializeField] private float mergedPressureBonus = 1.5f;
    [SerializeField] private float pressureResistancePerVolume = 1f;

    [Header("Split")]
    [SerializeField] private int splitPieceCount = 3;
    [SerializeField] private int maxSplitPieces = 24;
    [SerializeField] private float splitScale = 0.62f;
    [SerializeField] private float splitPieceScale = 0.5f;
    [SerializeField] private float splitSpawnRadius = 0.35f;
    [SerializeField] private float splitLaunchSpeed = 4.5f;
    [SerializeField] private float splitLaunchUpward = 2.5f;
    [SerializeField] private float splitMergeDuration = 0.28f;
    [SerializeField] private float splitMergeTouchRadius = 1.4f;

    [Header("Stretch")]
    [SerializeField] private float stretchLength = 2.6f;
    [SerializeField] private float stretchThickness = 0.55f;
    [SerializeField] private float stretchTurnSpeed = 14f;

    [Header("Bridge")]
    [SerializeField] private float bridgeLength = 3.4f;
    [SerializeField] private float bridgeWidth = 0.9f;
    [SerializeField] private float bridgeThickness = 0.22f;
    [SerializeField] private float bridgeDuration = 2.5f;
    [SerializeField] private float bridgeVolumeCost = 0.2f;
    [SerializeField] private float bridgeYOffset = 0.05f;

    private readonly List<SlimeSplitPiece> splitPieces = new List<SlimeSplitPiece>();
    private MaterialPropertyBlock propertyBlock;

    private Vector3 baseLocalScale;
    private Vector3 targetLocalScale;
    private Vector3 lastAimDirection = Vector3.forward;
    private Color currentColor = new Color(0.25f, 0.95f, 0.55f, 1f);
    private float currentVolume;
    private float reservedBridgeVolume;
    private float absorbedPressureBonus;
    private float stretchMultiplierBonus;
    private float bridgeDurationBonus;
    private float currentHealth;
    private int activeBridgeCount;
    private BoneSphere boneSphere;
    private bool isSplit;
    private bool isMerged;
    private bool isStretching;
    private bool isCrushed;

    public SlimeMaterialType CurrentMaterial { get; private set; }
    public float CurrentVolume => currentVolume;
    public float BodyVolume => Mathf.Max(minBodyVolume, currentVolume - reservedBridgeVolume);
    public float Health01 => maxHealth <= 0f ? 0f : currentHealth / maxHealth;
    public bool IsSplit => isSplit;
    public bool IsMerged => isMerged;
    public bool IsStretching => isStretching;
    public bool IsBridging => activeBridgeCount > 0;
    public bool IsCrushed => isCrushed;
    public float PressureResistance => BodyVolume * pressureResistancePerVolume + absorbedPressureBonus + (isMerged ? mergedPressureBonus : 0f);
    public Vector3 BodyCenterPosition => GetBodyCenterPosition();

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        ResolveReferences();

        baseLocalScale = bodyRoot.localScale;
        targetLocalScale = baseLocalScale;
        currentVolume = Mathf.Max(minBodyVolume, baseVolume);
        currentHealth = maxHealth;

        if (slimeRenderer != null && slimeRenderer.sharedMaterial != null)
            currentColor = ResolveRendererColor(slimeRenderer);

        ApplyColor(currentColor);
    }

    private void Start()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (isCrushed)
        {
            bodyRoot.localScale = Vector3.Lerp(bodyRoot.localScale, targetLocalScale, Mathf.Clamp01(Time.deltaTime * shapeLerpSpeed));
            return;
        }

        if (readKeyboardInput)
            ReadKeyboardInput();

        UpdateShapeTarget();
        bodyRoot.localScale = Vector3.Lerp(bodyRoot.localScale, targetLocalScale, Mathf.Clamp01(Time.deltaTime * shapeLerpSpeed));
    }

    public void SetAimDirection(Vector3 worldDirection)
    {
        worldDirection.y = 0f;

        if (worldDirection.sqrMagnitude < 0.0001f)
            return;

        lastAimDirection = worldDirection.normalized;
    }

    public void Split()
    {
        if (isCrushed)
            return;

        isSplit = true;
        isMerged = false;
        isStretching = false;

        SpawnSplitPieces();
    }

    public void Merge()
    {
        if (isCrushed)
            return;

        isSplit = false;
        isMerged = true;
        isStretching = false;

        MergeSplitPieces();
    }

    public int MergeNearbySplitPieces()
    {
        if (isCrushed)
            return 0;

        Vector3 center = GetBodyCenterPosition();
        float radiusSqr = splitMergeTouchRadius * splitMergeTouchRadius;
        int merged = 0;

        for (int i = splitPieces.Count - 1; i >= 0; i--)
        {
            SlimeSplitPiece piece = splitPieces[i];
            if (piece == null)
            {
                splitPieces.RemoveAt(i);
                continue;
            }

            if (piece.IsMerging)
                continue;

            if ((piece.CenterPosition - center).sqrMagnitude <= radiusSqr)
            {
                piece.BeginMerge(splitMergeDuration);
                splitPieces.RemoveAt(i);
                merged++;
            }
        }

        if (splitPieces.Count == 0)
            isSplit = false;

        return merged;
    }

    public void ReturnToNormalForm()
    {
        if (isCrushed)
            return;

        isSplit = false;
        isMerged = false;
        isStretching = false;

        MergeSplitPieces();
    }

    public void BeginStretch(Vector3 worldDirection)
    {
        if (isCrushed)
            return;

        SetAimDirection(worldDirection);

        isSplit = false;
        isMerged = false;
        isStretching = true;

        MergeSplitPieces();
    }

    public void EndStretch()
    {
        if (isCrushed)
            return;

        isStretching = false;
    }

    public GameObject BuildBridge(Vector3 worldDirection)
    {
        if (isCrushed)
            return null;

        SetAimDirection(worldDirection);

        float availableVolume = Mathf.Max(0f, currentVolume - reservedBridgeVolume - minBodyVolume);
        float reserve = Mathf.Min(bridgeVolumeCost, availableVolume);
        if (reserve <= 0f && bridgeVolumeCost > 0f)
            return null;

        reservedBridgeVolume += reserve;
        activeBridgeCount++;

        Vector3 direction = lastAimDirection.sqrMagnitude > 0.0001f ? lastAimDirection.normalized : transform.forward;
        float volumeScale = Mathf.Pow(Mathf.Max(BodyVolume, minBodyVolume) / Mathf.Max(baseVolume, 0.001f), 1f / 3f);
        float length = bridgeLength * Mathf.Max(0.5f, volumeScale);
        float width = bridgeWidth * Mathf.Max(0.55f, volumeScale);
        Vector3 start = GetBodyCenterPosition() + Vector3.up * bridgeYOffset;
        Vector3 center = start + direction * (length * 0.5f);

        GameObject bridge = bridgePrefab != null ? Instantiate(bridgePrefab) : GameObject.CreatePrimitive(PrimitiveType.Cube);
        bridge.name = "Slime Body Bridge";
        bridge.transform.position = center;
        bridge.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        bridge.transform.localScale = new Vector3(width, bridgeThickness, length);

        Collider bridgeCollider = bridge.GetComponent<Collider>();
        if (bridgeCollider == null)
            bridgeCollider = bridge.AddComponent<BoxCollider>();

        bridgeCollider.isTrigger = false;

        Rigidbody bridgeBody = bridge.GetComponent<Rigidbody>();
        if (bridgeBody != null)
        {
            bridgeBody.isKinematic = true;
            bridgeBody.useGravity = false;
        }

        Renderer bridgeRenderer = bridge.GetComponentInChildren<Renderer>();
        if (bridgeRenderer != null)
        {
            if (slimeRenderer != null)
                bridgeRenderer.sharedMaterial = slimeRenderer.sharedMaterial;

            ApplyColor(bridgeRenderer, currentColor);
        }

        SlimeBridgeSegment segment = bridge.GetComponent<SlimeBridgeSegment>();
        if (segment == null)
            segment = bridge.AddComponent<SlimeBridgeSegment>();

        segment.Init(this, bridgeDuration + bridgeDurationBonus, reserve);
        return bridge;
    }

    public bool TryAbsorb(SlimeAbsorbable absorbable)
    {
        if (absorbable == null)
            return false;

        if (isCrushed)
            return false;

        currentVolume = Mathf.Clamp(currentVolume + absorbable.VolumeGain, minBodyVolume, maxVolume);
        absorbedPressureBonus += absorbable.PressureResistanceBonus;
        stretchMultiplierBonus += absorbable.StretchMultiplierBonus;
        bridgeDurationBonus += absorbable.BridgeDurationBonus;
        CurrentMaterial = absorbable.MaterialType;
        currentColor = Color.Lerp(currentColor, absorbable.Tint, 0.55f);
        currentHealth = Mathf.Min(maxHealth, currentHealth + absorbable.VolumeGain * 0.25f);

        ApplyColor(currentColor);
        RefreshSplitPieceColors();
        return true;
    }

    public bool CanSurvivePressure(float requiredResistance)
    {
        return PressureResistance >= requiredResistance;
    }

    public bool HandlePressure(float requiredResistance, float damage, Vector3 pressureDirection, float pushForce)
    {
        bool survives = CanSurvivePressure(requiredResistance);
        if (survives)
            return true;

        ApplyHazardDamage(damage);

        if (ResolveBodyRigidbody() && pushForce > 0f)
        {
            Vector3 direction = pressureDirection.sqrMagnitude > 0.0001f ? pressureDirection.normalized : Vector3.down;
            bodyRigidbody.AddForce(direction * pushForce, ForceMode.Force);
        }

        return false;
    }

    public void ApplyHazardDamage(float amount)
    {
        if (amount <= 0f || currentHealth <= 0f)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);

        if (currentHealth <= 0f)
            OnCrushed();
    }

    public void ReleaseBridgeVolume(float volume)
    {
        reservedBridgeVolume = Mathf.Max(0f, reservedBridgeVolume - Mathf.Max(0f, volume));
        activeBridgeCount = Mathf.Max(0, activeBridgeCount - 1);
    }

    private void ReadKeyboardInput()
    {
        Vector2 aimInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        SetAimDirection(ResolveAimDirection(aimInput));

        if (Input.GetKeyDown(splitKey))
            Split();

        if (Input.GetKeyDown(mergeKey))
        {
            if (splitPieces.Count > 0)
                MergeNearbySplitPieces();
            else if (isMerged)
                ReturnToNormalForm();
            else
                Merge();
        }

        if (Input.GetKeyDown(stretchKey))
            BeginStretch(lastAimDirection);

        if (Input.GetKeyUp(stretchKey))
            EndStretch();

        if (Input.GetKeyDown(bridgeKey))
            BuildBridge(lastAimDirection);
    }

    private Vector3 ResolveAimDirection(Vector2 input)
    {
        if (input.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;

        if (cameraRelativeAim && cameraTransform != null)
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

    private void UpdateShapeTarget()
    {
        float volumeScale = Mathf.Pow(Mathf.Max(BodyVolume, minBodyVolume) / Mathf.Max(baseVolume, 0.001f), 1f / 3f);
        Vector3 shape = Vector3.one * volumeScale;

        if (isStretching)
        {
            float stretch = stretchLength + stretchMultiplierBonus;
            shape = new Vector3(stretchThickness, stretchThickness, stretch) * volumeScale;
            RotateTowardAim();
        }
        else if (isSplit)
        {
            shape *= splitScale;
        }
        else if (isMerged)
        {
            shape *= mergeScale;
        }

        targetLocalScale = new Vector3(
            baseLocalScale.x * shape.x,
            baseLocalScale.y * shape.y,
            baseLocalScale.z * shape.z);
    }

    private void RotateTowardAim()
    {
        if (lastAimDirection.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(lastAimDirection, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * stretchTurnSpeed);
    }

    private void SpawnSplitPieces()
    {
        for (int i = splitPieces.Count - 1; i >= 0; i--)
        {
            if (splitPieces[i] == null)
                splitPieces.RemoveAt(i);
        }

        int requested = Mathf.Max(0, splitPieceCount);
        int available = Mathf.Max(0, maxSplitPieces - splitPieces.Count);
        int count = Mathf.Min(requested, available);
        if (count == 0)
            return;

        Material sharedMaterial = slimeRenderer != null ? slimeRenderer.sharedMaterial : null;
        Vector3 splitOrigin = GetBodyCenterPosition();
        Vector3 splitForward = lastAimDirection.sqrMagnitude > 0.0001f ? lastAimDirection : transform.forward;
        splitForward.y = 0f;
        if (splitForward.sqrMagnitude < 0.0001f)
            splitForward = Vector3.forward;

        Quaternion splitRotation = Quaternion.LookRotation(splitForward.normalized, Vector3.up);
        float angleJitter = Random.Range(0f, 360f);
        float pieceScale = Mathf.Max(0.05f, splitPieceScale);

        for (int i = 0; i < count; i++)
        {
            float baseAngle = count == 1 ? 0f : (360f / count) * i;
            float angle = baseAngle + angleJitter + Random.Range(-12f, 12f);
            float radiusJitter = Random.Range(0.85f, 1.2f);
            float speedJitter = Random.Range(0.85f, 1.2f);
            Vector3 splitDirection = splitRotation * (Quaternion.Euler(0f, angle, 0f) * Vector3.forward);
            Vector3 spawnOffset = splitDirection * (splitSpawnRadius * radiusJitter);
            GameObject piece = splitPiecePrefab != null ? Instantiate(splitPiecePrefab) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            piece.name = splitPiecePrefab != null ? $"{splitPiecePrefab.name} Piece" : "Slime Split Piece";
            piece.transform.position = splitOrigin + spawnOffset;
            piece.transform.rotation = splitRotation;
            piece.transform.localScale = Vector3.one * pieceScale;

            StripPieceControlScripts(piece);

            SlimeSplitPiece splitPiece = piece.GetComponent<SlimeSplitPiece>();
            if (splitPiece == null)
                splitPiece = piece.AddComponent<SlimeSplitPiece>();

            Vector3 launchVelocity = splitDirection * (splitLaunchSpeed * speedJitter)
                                     + Vector3.up * splitLaunchUpward;

            splitPiece.Init(
                this,
                launchVelocity,
                sharedMaterial,
                currentColor,
                splitMergeDuration);
            splitPieces.Add(splitPiece);
        }
    }

    private void MergeSplitPieces()
    {
        for (int i = splitPieces.Count - 1; i >= 0; i--)
        {
            if (splitPieces[i] != null)
                splitPieces[i].BeginMerge(splitMergeDuration);
        }

        splitPieces.Clear();
    }

    private void ClearSplitPieces(bool immediate)
    {
        for (int i = splitPieces.Count - 1; i >= 0; i--)
        {
            if (splitPieces[i] != null)
            {
                if (immediate)
                    Destroy(splitPieces[i].gameObject);
                else
                    splitPieces[i].BeginMerge(splitMergeDuration);
            }
        }

        splitPieces.Clear();
    }

    private void StripPieceControlScripts(GameObject piece)
    {
        SlimeMovementController[] movementScripts = piece.GetComponentsInChildren<SlimeMovementController>(true);
        for (int i = 0; i < movementScripts.Length; i++)
        {
            movementScripts[i].enabled = false;
            DestroyImmediate(movementScripts[i]);
        }

        SlimePlayerAbilities[] playerScripts = piece.GetComponentsInChildren<SlimePlayerAbilities>(true);
        for (int i = 0; i < playerScripts.Length; i++)
        {
            if (playerScripts[i] == this)
                continue;

            playerScripts[i].enabled = false;
            DestroyImmediate(playerScripts[i]);
        }
    }

    private void RefreshSplitPieceColors()
    {
        for (int i = splitPieces.Count - 1; i >= 0; i--)
        {
            if (splitPieces[i] == null)
            {
                splitPieces.RemoveAt(i);
                continue;
            }

            splitPieces[i].SetColor(currentColor);
        }
    }

    private void ApplyColor(Color color)
    {
        if (slimeRenderer != null)
            ApplyColor(slimeRenderer, color);
    }

    private void ApplyColor(Renderer targetRenderer, Color color)
    {
        if (targetRenderer == null)
            return;

        EnsurePropertyBlock();
        propertyBlock.SetColor(BaseColor, color);
        propertyBlock.SetColor(ColorId, color);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private Color ResolveRendererColor(Renderer targetRenderer)
    {
        if (targetRenderer == null || targetRenderer.sharedMaterial == null)
            return currentColor;

        Material material = targetRenderer.sharedMaterial;
        if (material.HasProperty(BaseColor))
            return material.GetColor(BaseColor);

        if (material.HasProperty(ColorId))
            return material.GetColor(ColorId);

        return currentColor;
    }

    private void EnsurePropertyBlock()
    {
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();
    }

    private void OnCrushed()
    {
        isSplit = false;
        isMerged = false;
        isStretching = false;
        isCrushed = true;
        ClearSplitPieces(true);

        targetLocalScale = new Vector3(baseLocalScale.x * 1.15f, baseLocalScale.y * 0.2f, baseLocalScale.z * 1.15f);

        if (ResolveBodyRigidbody())
            bodyRigidbody.velocity *= 0.25f;
    }

    private void ResolveReferences()
    {
        if (bodyRoot == null)
            bodyRoot = transform;

        if (slimeRenderer == null)
            slimeRenderer = GetComponentInChildren<Renderer>();

        if (slimeRenderer != null && slimeMaterialOverride != null && slimeRenderer.sharedMaterial != slimeMaterialOverride)
            slimeRenderer.sharedMaterial = slimeMaterialOverride;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        ResolveBodyRigidbody();
    }

    private bool ResolveBodyRigidbody()
    {
        if (bodyRigidbody != null)
            return true;

        if (boneSphere == null)
            boneSphere = GetComponent<BoneSphere>();

        if (boneSphere == null)
            boneSphere = GetComponentInParent<BoneSphere>();

        if (boneSphere == null)
            boneSphere = GetComponentInChildren<BoneSphere>();

        if (boneSphere != null && boneSphere.root != null)
            bodyRigidbody = boneSphere.root.GetComponent<Rigidbody>();

        if (bodyRigidbody == null)
            bodyRigidbody = GetComponent<Rigidbody>();

        if (bodyRigidbody == null)
            bodyRigidbody = GetComponentInChildren<Rigidbody>();

        return bodyRigidbody != null;
    }

    private Vector3 GetBodyCenterPosition()
    {
        if (ResolveBodyRigidbody())
            return bodyRigidbody.worldCenterOfMass;

        if (bodyRoot != null)
            return bodyRoot.position;

        return transform.position;
    }
}
