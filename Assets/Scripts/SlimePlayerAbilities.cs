using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SlimePlayerAbilities : MonoBehaviour
{
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissiveColor = Shader.PropertyToID("_EmissiveColor");
    private const int AbsorbRingSegments = 56;

    [Header("References")]
    [SerializeField] private Transform bodyRoot = null;
    [SerializeField] private Renderer slimeRenderer = null;
    [SerializeField] private Material slimeMaterialOverride = null;
    [SerializeField] private Rigidbody bodyRigidbody = null;
    [SerializeField] private GameObject splitPiecePrefab = null;

    [Header("Input")]
    [SerializeField] private bool readKeyboardInput = true;
    [SerializeField] private Transform cameraTransform = null;
    [SerializeField] private bool cameraRelativeAim = true;
    [SerializeField] private KeyCode aimForwardKey = KeyCode.W;
    [SerializeField] private KeyCode aimBackKey = KeyCode.S;
    [SerializeField] private KeyCode aimLeftKey = KeyCode.A;
    [SerializeField] private KeyCode aimRightKey = KeyCode.D;
    [SerializeField] private KeyCode splitKey = KeyCode.Q;
    [SerializeField] private KeyCode mergeKey = KeyCode.E;
    [SerializeField] private KeyCode stretchKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode alternateStretchKey = KeyCode.RightShift;
    [SerializeField] private KeyCode carryKey = KeyCode.C;

    [Header("Last Split Input")]
    [SerializeField] private bool controlLastSplitPieceWithArrowKeys = true;
    [SerializeField] private bool splitPieceCameraRelativeMovement = true;
    [SerializeField] private KeyCode splitPieceForwardKey = KeyCode.UpArrow;
    [SerializeField] private KeyCode splitPieceBackKey = KeyCode.DownArrow;
    [SerializeField] private KeyCode splitPieceLeftKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode splitPieceRightKey = KeyCode.RightArrow;
    [SerializeField] private KeyCode splitPieceSplitKey = KeyCode.RightControl;
    [SerializeField] private KeyCode splitPieceMergeKey = KeyCode.RightAlt;
    [SerializeField] private KeyCode splitPieceStretchKey = KeyCode.RightShift;
    [SerializeField] private KeyCode splitPieceAlternateStretchKey = KeyCode.None;
    [SerializeField] private KeyCode splitPieceCarryKey = KeyCode.Keypad0;
    [SerializeField, Min(0)] private int splitPieceMaxSplitGeneration = 1;
    [SerializeField] private float splitPieceMoveSpeed = 3.8f;
    [SerializeField] private float splitPieceAcceleration = 18f;
    [SerializeField, Range(0f, 1f)] private float splitPieceOuterBodyDriveWeight = 0.75f;

    [Header("Body")]
    [SerializeField] private float baseVolume = 1f;
    [SerializeField] private float maxVolume = 0f;
    [SerializeField] private float minBodyVolume = 0.25f;
    [SerializeField] private float shapeLerpSpeed = 6f;
    [SerializeField] private bool moveBonesWithBodyScale = true;
    [SerializeField] private bool assistBodyShapeBones = false;
    [SerializeField] private float bodyShapeBoneAcceleration = 80f;
    [SerializeField] private float bodyShapeBoneDamping = 8f;
    [SerializeField] private float bodyShapeBoneMaxVelocityChange = 2f;
    [SerializeField] private float maxHealth = 1f;
    [SerializeField] private bool hitDamageReducesVolume = true;
    [SerializeField] private bool hitDamageScalesMass = false;

    [Header("Merge")]
    [SerializeField] private float mergeScale = 1.25f;
    [SerializeField] private float mergedPressureBonus = 0f;
    [SerializeField] private float pressureResistancePerVolume = 1f;
    [SerializeField] private bool pressureScalesWithVolume = false;
    [SerializeField] private bool mergeAddsPressureBonus = false;
    [SerializeField] private bool absorbAddsPressureBonus = false;
    [SerializeField] private bool mergeKeyAbsorbsNearbySlime = true;
    [SerializeField] private float manualAbsorbRadius = 1.4f;
    [SerializeField] private LayerMask manualAbsorbMask = ~0;

    [Header("Split")]
    [SerializeField] private int splitPieceCount = 3;
    [SerializeField] private int maxSplitPieces = 24;
    [SerializeField, Range(0.05f, 0.95f)] private float splitVolumeFraction = 0.5f;
    [SerializeField] private float minSplitPieceVolume = 0.05f;
    [SerializeField] private float splitScale = 1f;
    [SerializeField] private float splitPieceScale = 1f;
    [SerializeField] private float splitSpawnRadius = 0.35f;
    [SerializeField] private float splitLaunchSpeed = 1.5f;
    [SerializeField] private float splitLaunchUpward = 2.5f;
    [SerializeField] private float splitMergeDuration = 0.18f;
    [SerializeField] private float splitMergeTouchRadius = 1.4f;

    [Header("Split/Merge Momentum")]
    [SerializeField, Range(0f, 1f)] private float splitLaunchVelocityScale = 0.75f;
    [SerializeField, Min(0f)] private float maxSplitLaunchPlanarSpeed = 1.35f;
    [SerializeField, Min(0f)] private float maxSplitLaunchUpwardSpeed = 1.85f;
    [SerializeField, Range(0f, 1f)] private float splitTransitionVelocityRetain = 0.85f;
    [SerializeField, Range(0f, 1f)] private float mergeTransitionVelocityRetain = 0.9f;
    [SerializeField, Min(0f)] private float maxTransitionPlanarSpeed = 4.8f;
    [SerializeField, Min(0f)] private float maxTransitionUpwardSpeed = 2f;
    [SerializeField, Range(0f, 1f)] private float transitionAngularVelocityRetain = 0.35f;

    [Header("Stretch")]
    [SerializeField] private float stretchLength = 2.6f;
    [SerializeField] private float stretchThickness = 0.55f;
    [SerializeField] private float stretchTurnSpeed = 14f;
    [SerializeField] private bool stretchSoftbodyBones = true;
    [SerializeField] private float stretchBoneAcceleration = 160f;
    [SerializeField] private float stretchBoneDamping = 18f;
    [SerializeField] private float stretchBoneMaxVelocityChange = 3f;
    [SerializeField] private float stretchRelaxedSpring = 18f;
    [SerializeField] private float stretchRelaxedDamper = 12f;
    [SerializeField] private float stretchReturnDuration = 0.25f;

    [Header("Carry")]
    [SerializeField] private Transform carryContainer = null;
    [SerializeField] private bool keepCarryContainerAtBodyCenter = true;
    [SerializeField] private float carryPickupRadius = 1.1f;
    [SerializeField] private LayerMask carryMask = ~0;
    [SerializeField, Range(0.05f, 1f)] private float carryCapacityVolumeRatio = 0.65f;
    [SerializeField, Range(0.05f, 1f)] private float maxSingleCarryVolumeRatio = 0.55f;
    [SerializeField] private Vector3 carryLocalOffset = new Vector3(0f, 0.05f, 0f);
    [SerializeField] private float carriedItemSpacing = 0.18f;
    [SerializeField] private float releaseDistance = 1.15f;
    [SerializeField] private float releaseUpOffset = 0.25f;
    [SerializeField] private float releaseVelocity = 2.4f;

    [Header("Pickup Audio")]
    [SerializeField] private AudioClip pickClip = null;
    [SerializeField] private AudioSource pickAudioSource = null;
    [SerializeField, Range(0f, 1f)] private float pickVolume = 0.85f;
    [SerializeField] private Vector2 pickPitchRange = new Vector2(0.96f, 1.04f);
    [SerializeField] private float pickMinInterval = 0.04f;

    [Header("Timed Abilities")]
    [SerializeField] private float defaultAbsorbAbilityDuration = 15f;

    [Header("Absorb Feedback")]
    [SerializeField] private float absorbPulseScale = 1.32f;
    [SerializeField] private float absorbPulseDuration = 0.46f;
    [SerializeField, Range(0f, 1f)] private float absorbFlashStrength = 0.9f;
    [SerializeField] private float absorbSquashAmount = 0.14f;
    [SerializeField] private float absorbGlowIntensity = 1.75f;
    [SerializeField] private bool absorbRingEnabled = true;
    [SerializeField] private float absorbRingStartRadius = 0.35f;
    [SerializeField] private float absorbRingEndRadius = 1.55f;
    [SerializeField] private float absorbRingYOffset = 0.08f;
    [SerializeField] private float absorbRingWidth = 0.08f;

    private readonly List<SlimeSplitPiece> splitPieces = new List<SlimeSplitPiece>();
    private readonly List<CarriedItemState> carriedItems = new List<CarriedItemState>();
    private readonly Rigidbody[] stretchBoneBodies = new Rigidbody[6];
    private readonly SpringJoint[] stretchBoneJoints = new SpringJoint[6];
    private readonly Vector3[] stretchBoneBaseLocalOffsets = new Vector3[6];
    private readonly Vector3[] stretchBoneBaseConnectedAnchors = new Vector3[6];
    private readonly float[] stretchBoneBaseSprings = new float[6];
    private readonly float[] stretchBoneBaseDampers = new float[6];
    private readonly float[] stretchBoneBaseMasses = new float[6];
    private readonly bool[] stretchBoneBaseAutoConfigureAnchors = new bool[6];
    private readonly Collider[] absorbHits = new Collider[32];
    private readonly Collider[] carryHits = new Collider[32];
    private MaterialPropertyBlock propertyBlock;

    private sealed class CarriedItemState
    {
        public SlimeCarryable Item;
        public float Volume;
    }

    private Vector3 baseLocalScale;
    private Vector3 targetLocalScale;
    private Vector3 targetBodyShape = Vector3.one;
    private Vector3 lastAimDirection = Vector3.forward;
    private Color currentColor = new Color(0.25f, 0.95f, 0.55f, 1f);
    private Color neutralColor = new Color(0.25f, 0.95f, 0.55f, 1f);
    private float currentVolume;
    private float carriedVolume;
    private float absorbedPressureBonus;
    private float stretchMultiplierBonus;
    private float timedPressureResistanceBonus;
    private float timedStretchMultiplierBonus;
    private float materialAbilityExpireTime = -1f;
    private float materialAbilityDuration;
    private SlimeMaterialType timedMaterialAbility = SlimeMaterialType.Neutral;
    private SlimeAbsorbAbility activeAbsorbAbility = SlimeAbsorbAbility.None;
    private float activeAbsorbAbilityExpireTime = -1f;
    private float activeAbsorbAbilityDuration;
    private float currentHealth;
    private float baseBodyMass = -1f;
    private float stretchReleasedTime = -100f;
    private BoneSphere boneSphere;
    private Rigidbody stretchRootBody;
    private Transform runtimeCarryContainer;
    private SlimeSplitPiece controlledSplitPiece;
    private Coroutine absorbFeedbackCoroutine;
    private LineRenderer absorbRingRenderer;
    private Material absorbRingMaterial;
    private float lastPickAudioTime = -100f;
    private float absorbFeedbackScale = 1f;
    private Vector3 absorbFeedbackShape = Vector3.one;
    private bool isSplit;
    private bool isMerged;
    private bool isStretching;
    private bool isCrushed;
    private bool stretchBonesCached;
    private bool stretchJointsRelaxed;

    public SlimeMaterialType CurrentMaterial { get; private set; }
    public float CurrentVolume => currentVolume;
    public Color CurrentColor => currentColor;
    public Material SlimeSharedMaterial => slimeRenderer != null ? slimeRenderer.sharedMaterial : slimeMaterialOverride;
    public float BodyVolume => Mathf.Max(minBodyVolume, currentVolume);
    public float CarriedVolume => carriedVolume;
    public float CarryCapacity => GetCarryCapacity();
    public float FreeCarryVolume => Mathf.Max(0f, CarryCapacity - carriedVolume);
    public float CarryLoad01 => CarryCapacity <= 0f ? 0f : Mathf.Clamp01(carriedVolume / CarryCapacity);
    public int CarriedItemCount => carriedItems.Count;
    public float Health01 => maxHealth <= 0f ? 0f : currentHealth / maxHealth;
    public bool IsSplit => isSplit;
    public IReadOnlyList<SlimeSplitPiece> ActiveSplitPieces => splitPieces;
    public bool IsMerged => isMerged;
    public bool IsStretching => isStretching;
    public bool IsCrushed => isCrushed;
    public float PressureResistance
    {
        get
        {
            float pressureVolume = pressureScalesWithVolume ? BodyVolume : Mathf.Max(minBodyVolume, baseVolume);
            float absorbBonus = absorbAddsPressureBonus ? absorbedPressureBonus + timedPressureResistanceBonus : 0f;
            float mergeBonus = mergeAddsPressureBonus && isMerged ? mergedPressureBonus : 0f;
            return pressureVolume * pressureResistancePerVolume + absorbBonus + mergeBonus;
        }
    }
    private float TotalStretchMultiplierBonus => stretchMultiplierBonus + timedStretchMultiplierBonus;
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

        neutralColor = currentColor;
        ApplyColor(currentColor);
    }

    private void Start()
    {
        ResolveReferences();
        TryCacheStretchBones();
    }

    private void Update()
    {
        PruneCarriedItems();
        UpdateTimedAbsorbAbilities();

        if (isCrushed)
        {
            ApplyShapeScale();
            return;
        }

        if (readKeyboardInput)
            ReadKeyboardInput();

        UpdateShapeTarget();
        ApplyShapeScale();
    }

    private void LateUpdate()
    {
        if (carriedItems.Count > 0)
            SyncCarryContainerTransform();
    }

    private void FixedUpdate()
    {
        if (isCrushed)
        {
            SetStretchJointsRelaxed(false);
            return;
        }

        if ((stretchSoftbodyBones || moveBonesWithBodyScale) && !stretchBonesCached)
            TryCacheStretchBones();

        UpdateSoftbodyShape();
    }

    private void OnDisable()
    {
        ResetAbsorbFeedback();
        SetStretchJointsRelaxed(false);
        ReleaseAllCarriedItems();
    }

    private void OnDestroy()
    {
        ReleaseAllCarriedItems();
        DestroyAbsorbRing();

        if (runtimeCarryContainer != null)
            Destroy(runtimeCarryContainer.gameObject);
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

        if (!SpawnSplitPieces())
            return;

        isMerged = false;
        isStretching = false;
        isSplit = true;
        RefreshShapeTarget();
        DampenSplitTransitionMomentum();
    }

    public void Merge()
    {
        if (isCrushed)
            return;

        isSplit = false;
        isMerged = true;
        isStretching = false;

        MergeSplitPieces();
        RefreshShapeTarget();
        DampenMergeTransitionMomentum();
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
        {
            isSplit = false;
            SetControlledSplitPiece(null);
            RefreshShapeTarget();
            DampenMergeTransitionMomentum();
        }
        else
        {
            RefreshControlledSplitPiece();
        }

        return merged;
    }

    public bool MergeSplitPiece(SlimeSplitPiece piece)
    {
        if (isCrushed || piece == null || piece.IsMerging)
            return false;

        int pieceIndex = splitPieces.IndexOf(piece);
        if (pieceIndex < 0)
            return false;

        splitPieces.RemoveAt(pieceIndex);
        piece.BeginMerge(splitMergeDuration);

        if (splitPieces.Count == 0)
        {
            isSplit = false;
            SetControlledSplitPiece(null);
            RefreshShapeTarget();
            DampenMergeTransitionMomentum();
        }
        else
        {
            RefreshControlledSplitPiece();
        }

        return true;
    }

    public int MergeNearbySplitPiecesInto(SlimeSplitPiece targetPiece)
    {
        if (isCrushed || targetPiece == null || targetPiece.IsMerging)
            return 0;

        if (!splitPieces.Contains(targetPiece))
            return 0;

        Vector3 center = targetPiece.CenterPosition;
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

            if (piece == targetPiece || piece.IsMerging)
                continue;

            if ((piece.CenterPosition - center).sqrMagnitude <= radiusSqr)
            {
                piece.BeginMergeToSplitPiece(targetPiece, splitMergeDuration);
                splitPieces.RemoveAt(i);
                merged++;
            }
        }

        RefreshControlledSplitPiece();
        return merged;
    }

    public SlimeSplitPiece SplitFromSplitPiece(SlimeSplitPiece sourcePiece, Vector3 worldDirection)
    {
        if (isCrushed || sourcePiece == null || sourcePiece.IsMerging || !sourcePiece.CanSplitFurther || !splitPieces.Contains(sourcePiece))
            return null;

        for (int i = splitPieces.Count - 1; i >= 0; i--)
        {
            if (splitPieces[i] == null)
                splitPieces.RemoveAt(i);
        }

        if (splitPieces.Count >= maxSplitPieces)
            return null;

        Vector3 splitDirection = worldDirection;
        splitDirection.y = 0f;
        if (splitDirection.sqrMagnitude <= 0.0001f)
            splitDirection = transform.forward;
        splitDirection.y = 0f;
        if (splitDirection.sqrMagnitude <= 0.0001f)
            splitDirection = Vector3.forward;
        splitDirection.Normalize();

        float angle = Random.Range(-18f, 18f);
        splitDirection = Quaternion.Euler(0f, angle, 0f) * splitDirection;

        float childVolume = sourcePiece.TakeSplitVolume(splitVolumeFraction, minSplitPieceVolume);
        if (childVolume < minSplitPieceVolume)
            return null;

        Material sharedMaterial = slimeRenderer != null ? slimeRenderer.sharedMaterial : null;
        Vector3 spawnOffset = splitDirection * (splitSpawnRadius * Random.Range(0.75f, 1.05f));
        GameObject piece = splitPiecePrefab != null ? Instantiate(splitPiecePrefab) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
        piece.name = splitPiecePrefab != null ? $"{splitPiecePrefab.name} Piece" : "Slime Split Piece";
        piece.transform.position = sourcePiece.CenterPosition + spawnOffset;
        piece.transform.rotation = Quaternion.LookRotation(splitDirection, Vector3.up);
        piece.transform.localScale = Vector3.one * GetSplitPieceScale(childVolume);

        StripPieceControlScripts(piece);

        SlimeSplitPiece splitPiece = piece.GetComponent<SlimeSplitPiece>();
        if (splitPiece == null)
            splitPiece = piece.AddComponent<SlimeSplitPiece>();

        Vector3 launchVelocity = BuildSplitLaunchVelocity(splitDirection, 1f, 0.75f);
        splitPiece.Init(this, launchVelocity, sharedMaterial, currentColor, childVolume, splitMergeDuration);
        splitPiece.SetSplitGeneration(sourcePiece.SplitGeneration + 1, splitPieceMaxSplitGeneration);
        splitPieces.Add(splitPiece);
        isSplit = true;
        SetControlledSplitPiece(splitPiece);
        return splitPiece;
    }

    public void ReturnToNormalForm()
    {
        if (isCrushed)
            return;

        isSplit = false;
        isMerged = false;
        isStretching = false;

        MergeSplitPieces();
        RefreshShapeTarget();
        DampenMergeTransitionMomentum();
    }

    public void BeginStretch(Vector3 worldDirection)
    {
        if (isCrushed)
            return;

        SetAimDirection(worldDirection);

        isSplit = false;
        isMerged = false;
        isStretching = true;
        stretchReleasedTime = -100f;

        MergeSplitPieces();
    }

    public void EndStretch()
    {
        if (isCrushed)
            return;

        isStretching = false;
        stretchReleasedTime = Time.time;
    }

    public bool TryAbsorb(SlimeAbsorbable absorbable)
    {
        if (absorbable == null)
            return false;

        if (isCrushed)
            return false;

        AddVolume(absorbable.VolumeGain, hitDamageScalesMass);
        SlimeMaterialType absorbedMaterial = absorbable.MaterialType;
        float abilityDuration = ResolveAbsorbAbilityDuration(absorbable);
        if (absorbedMaterial != SlimeMaterialType.Neutral)
        {
            ActivateMaterialAbility(absorbable, absorbedMaterial, abilityDuration);
        }
        else
        {
            if (absorbAddsPressureBonus)
                absorbedPressureBonus += absorbable.PressureResistanceBonus;

            stretchMultiplierBonus += absorbable.StretchMultiplierBonus;
        }

        if (absorbable.GrantedAbility != SlimeAbsorbAbility.None)
            ActivateAbsorbAbility(absorbable.GrantedAbility, abilityDuration);

        currentHealth = Mathf.Min(maxHealth, currentHealth + absorbable.VolumeGain * 0.25f);

        ApplyColor(currentColor);
        RefreshSplitPieceColors();
        RefreshShapeTarget();
        PlayAbsorbFeedback(absorbable.Tint);
        PlayPickSound();
        Heartwell.UI.InGameOverlayUI.ShowAbsorbPopup(absorbable);
        return true;
    }

    public bool HasActiveAbsorbAbility(SlimeAbsorbAbility ability)
    {
        if (ability == SlimeAbsorbAbility.None)
            return false;

        return activeAbsorbAbility == ability
            && activeAbsorbAbilityExpireTime > Time.time;
    }

    public bool TryGetActiveTimedAbilityStatus(out string displayName, out float normalizedTime, out float remainingTime, out Color barColor)
    {
        displayName = string.Empty;
        normalizedTime = 0f;
        remainingTime = 0f;
        barColor = currentColor;

        bool hasMaterial = timedMaterialAbility != SlimeMaterialType.Neutral && materialAbilityExpireTime > Time.time;
        bool hasAbility = activeAbsorbAbility != SlimeAbsorbAbility.None && activeAbsorbAbilityExpireTime > Time.time;
        if (!hasMaterial && !hasAbility)
            return false;

        float materialRemaining = hasMaterial ? materialAbilityExpireTime - Time.time : float.PositiveInfinity;
        float abilityRemaining = hasAbility ? activeAbsorbAbilityExpireTime - Time.time : float.PositiveInfinity;
        if (abilityRemaining <= materialRemaining)
        {
            displayName = GetAbsorbAbilityDisplayName(activeAbsorbAbility);
            remainingTime = Mathf.Max(0f, abilityRemaining);
            normalizedTime = activeAbsorbAbilityDuration > 0.001f
                ? Mathf.Clamp01(remainingTime / activeAbsorbAbilityDuration)
                : 0f;
            barColor = new Color(0.38f, 1f, 0.55f, 1f);
            return true;
        }

        displayName = timedMaterialAbility.ToString();
        remainingTime = Mathf.Max(0f, materialRemaining);
        normalizedTime = materialAbilityDuration > 0.001f
            ? Mathf.Clamp01(remainingTime / materialAbilityDuration)
            : 0f;
        barColor = currentColor;
        return true;
    }

    private float ResolveAbsorbAbilityDuration(SlimeAbsorbable absorbable)
    {
        float duration = absorbable != null ? absorbable.AbilityDuration : 0f;
        return duration > 0.001f ? duration : Mathf.Max(0.001f, defaultAbsorbAbilityDuration);
    }

    private void ActivateMaterialAbility(SlimeAbsorbable absorbable, SlimeMaterialType material, float duration)
    {
        bool materialChanged = material != CurrentMaterial;
        timedMaterialAbility = material;
        materialAbilityDuration = Mathf.Max(0.001f, duration);
        materialAbilityExpireTime = Time.time + materialAbilityDuration;
        timedPressureResistanceBonus = absorbAddsPressureBonus && absorbable != null ? absorbable.PressureResistanceBonus : 0f;
        timedStretchMultiplierBonus = absorbable != null ? absorbable.StretchMultiplierBonus : 0f;
        CurrentMaterial = material;

        if (materialChanged && absorbable != null)
            currentColor = Color.Lerp(currentColor, absorbable.Tint, 0.55f);
    }

    private void ActivateAbsorbAbility(SlimeAbsorbAbility ability, float duration)
    {
        if (ability == SlimeAbsorbAbility.None)
            return;

        activeAbsorbAbility = ability;
        activeAbsorbAbilityDuration = Mathf.Max(0.001f, duration);
        activeAbsorbAbilityExpireTime = Time.time + activeAbsorbAbilityDuration;
    }

    private void UpdateTimedAbsorbAbilities()
    {
        bool shapeChanged = false;

        if (timedMaterialAbility != SlimeMaterialType.Neutral && materialAbilityExpireTime <= Time.time)
        {
            ExpireMaterialAbility();
            shapeChanged = true;
        }

        if (activeAbsorbAbility != SlimeAbsorbAbility.None && activeAbsorbAbilityExpireTime <= Time.time)
            ExpireAbsorbAbility();

        if (shapeChanged)
            RefreshShapeTarget();
    }

    private void ExpireMaterialAbility()
    {
        if (CurrentMaterial == timedMaterialAbility)
        {
            CurrentMaterial = SlimeMaterialType.Neutral;
            currentColor = neutralColor;
            ApplyColor(currentColor);
            RefreshSplitPieceColors();
        }

        timedMaterialAbility = SlimeMaterialType.Neutral;
        materialAbilityExpireTime = -1f;
        materialAbilityDuration = 0f;
        timedPressureResistanceBonus = 0f;
        timedStretchMultiplierBonus = 0f;
    }

    private void ExpireAbsorbAbility()
    {
        activeAbsorbAbility = SlimeAbsorbAbility.None;
        activeAbsorbAbilityExpireTime = -1f;
        activeAbsorbAbilityDuration = 0f;
    }

    private static string GetAbsorbAbilityDisplayName(SlimeAbsorbAbility ability)
    {
        return ability == SlimeAbsorbAbility.WaveAttack ? "Slime Wave" : ability.ToString();
    }

    private void AddVolume(float volumeGain, bool syncMass = false)
    {
        float gainedVolume = currentVolume + Mathf.Max(0f, volumeGain);
        currentVolume = maxVolume > 0f
            ? Mathf.Clamp(gainedVolume, minBodyVolume, maxVolume)
            : Mathf.Max(minBodyVolume, gainedVolume);

        if (syncMass)
            SyncBodyMassWithVolume();
    }

    public void AbsorbMergedSplitVolume(float volumeGain)
    {
        AddVolume(volumeGain);
        RefreshShapeTarget();
        DampenMergeTransitionMomentum();
    }

    public void RestoreBaseVolume(bool restoreHealth)
    {
        RestoreVolumeToAtLeast(baseVolume, restoreHealth);
    }

    public void RestoreVolumeToAtLeast(float targetVolume, bool restoreHealth)
    {
        if (isCrushed)
            return;

        float volumeFloor = Mathf.Max(minBodyVolume, targetVolume);
        if (maxVolume > 0f)
            volumeFloor = Mathf.Clamp(volumeFloor, minBodyVolume, maxVolume);

        bool volumeChanged = currentVolume < volumeFloor;
        if (volumeChanged)
        {
            currentVolume = volumeFloor;
            SyncBodyMassWithVolume();
            RefreshShapeTarget();
        }

        if (restoreHealth)
            currentHealth = Mathf.Max(0f, maxHealth);
    }

    private float GetSplitPieceScale(float pieceVolume)
    {
        return Mathf.Max(0.05f, GetSplitViewScale(pieceVolume) * Mathf.Max(0.01f, splitPieceScale));
    }

    public bool TryAbsorbNearbySlime(Vector3 center, float radius)
    {
        if (!mergeKeyAbsorbsNearbySlime || isCrushed)
            return false;

        int hitCount = Physics.OverlapSphereNonAlloc(center, Mathf.Max(0.01f, radius), absorbHits, manualAbsorbMask, QueryTriggerInteraction.Collide);
        SlimeAbsorbable bestAbsorbable = null;
        float bestDistanceSqr = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = absorbHits[i];
            if (hit == null || IsOwnManualAbsorbCollider(hit))
                continue;

            SlimeAbsorbable absorbable = hit.GetComponentInParent<SlimeAbsorbable>();
            if (absorbable == null || absorbable == bestAbsorbable)
                continue;

            float distanceSqr = (absorbable.transform.position - center).sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr)
                continue;

            bestAbsorbable = absorbable;
            bestDistanceSqr = distanceSqr;
        }

        return bestAbsorbable != null && bestAbsorbable.TryAbsorb(this);
    }

    public bool TryAbsorbNearbySlime(Vector3 center)
    {
        return TryAbsorbNearbySlime(center, manualAbsorbRadius);
    }

    public bool CanCarry(SlimeCarryable carryable)
    {
        if (carryable == null || isCrushed || carryable.IsCarried)
            return false;

        PruneCarriedItems();
        return CanCarryVolume(carryable.Volume);
    }

    public bool TryCarry(SlimeCarryable carryable)
    {
        if (!CanCarry(carryable))
            return false;

        Transform container = GetOrCreateCarryContainer();
        if (container == null)
            return false;

        SyncCarryContainerTransform();

        float itemVolume = Mathf.Max(0f, carryable.Volume);
        Vector3 localPosition = GetCarrySlotLocalPosition(carriedItems.Count);
        Quaternion localRotation = GetCarrySlotLocalRotation(carriedItems.Count);

        if (!carryable.BeginCarry(this, container, localPosition, localRotation))
            return false;

        carriedItems.Add(new CarriedItemState
        {
            Item = carryable,
            Volume = itemVolume
        });

        carriedVolume += itemVolume;
        UpdateCarriedItemLayout();
        PlayPickSound();
        return true;
    }

    public bool TryCarryNearestItem()
    {
        if (isCrushed)
            return false;

        PruneCarriedItems();

        Vector3 center = GetBodyCenterPosition();
        int hitCount = Physics.OverlapSphereNonAlloc(center, Mathf.Max(0.01f, carryPickupRadius), carryHits, carryMask, QueryTriggerInteraction.Collide);
        SlimeCarryable bestCarryable = null;
        float bestDistanceSqr = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = carryHits[i];
            if (hit == null || IsOwnCarryCollider(hit))
                continue;

            SlimeCarryable carryable = hit.GetComponentInParent<SlimeCarryable>();
            if (carryable == null || carryable == bestCarryable || !CanCarry(carryable))
                continue;

            float distanceSqr = (carryable.transform.position - center).sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr)
                continue;

            bestCarryable = carryable;
            bestDistanceSqr = distanceSqr;
        }

        return bestCarryable != null && TryCarry(bestCarryable);
    }

    public bool ReleaseLastCarriedItem()
    {
        PruneCarriedItems();

        for (int i = carriedItems.Count - 1; i >= 0; i--)
        {
            if (ReleaseCarriedItemAt(i))
                return true;
        }

        return false;
    }

    public int ReleaseAllCarriedItems()
    {
        int released = 0;

        for (int i = carriedItems.Count - 1; i >= 0; i--)
        {
            if (ReleaseCarriedItemAt(i))
                released++;
        }

        carriedItems.Clear();
        carriedVolume = 0f;
        return released;
    }

    public void PlayPickSound()
    {
        if (pickClip == null || pickVolume <= 0f)
            return;

        if (Time.time - lastPickAudioTime < Mathf.Max(0f, pickMinInterval))
            return;

        if (pickAudioSource == null)
            pickAudioSource = gameObject.AddComponent<AudioSource>();

        if (pickClip.loadState == AudioDataLoadState.Unloaded)
            pickClip.LoadAudioData();

        pickAudioSource.playOnAwake = false;
        pickAudioSource.loop = false;
        pickAudioSource.spatialBlend = 1f;
        pickAudioSource.dopplerLevel = 0f;
        pickAudioSource.rolloffMode = AudioRolloffMode.Linear;
        pickAudioSource.minDistance = 2f;
        pickAudioSource.maxDistance = 22f;
        pickAudioSource.volume = 1f;
        pickAudioSource.pitch = Random.Range(GetPickPitchMin(), GetPickPitchMax());
        pickAudioSource.PlayOneShot(pickClip, pickVolume);
        lastPickAudioTime = Time.time;
    }

    private float GetPickPitchMin()
    {
        return Mathf.Max(0.01f, Mathf.Min(pickPitchRange.x, pickPitchRange.y));
    }

    private float GetPickPitchMax()
    {
        return Mathf.Max(GetPickPitchMin(), Mathf.Max(pickPitchRange.x, pickPitchRange.y));
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
        float health01 = maxHealth > 0.001f ? currentHealth / maxHealth : 0f;
        Heartwell.UI.InGameOverlayUI.ShowHazard(health01);

        if (currentHealth <= 0f)
            OnCrushed();
    }

    public void ApplyHitDamage(float damageAmount, float volumeLossAmount)
    {
        if (currentHealth <= 0f)
            return;

        ApplyHazardDamage(damageAmount);

        if (isCrushed)
            return;

        if (hitDamageReducesVolume)
            ReduceVolumeFromHit(volumeLossAmount);
    }

    private void ReduceVolumeFromHit(float volumeLossAmount)
    {
        float loss = Mathf.Max(0f, volumeLossAmount);
        if (loss <= 0f || currentVolume <= minBodyVolume)
            return;

        currentVolume = Mathf.Max(minBodyVolume, currentVolume - loss);
        RefreshShapeTarget();
        SyncBodyMassWithVolume();
    }

    private float GetCarryCapacity()
    {
        return Mathf.Max(0f, BodyVolume * carryCapacityVolumeRatio);
    }

    private bool CanCarryVolume(float itemVolume)
    {
        itemVolume = Mathf.Max(0f, itemVolume);
        float bodyVolume = Mathf.Max(0f, BodyVolume);
        float singleItemLimit = bodyVolume * maxSingleCarryVolumeRatio;

        if (itemVolume > singleItemLimit + 0.0001f)
            return false;

        return carriedVolume + itemVolume <= GetCarryCapacity() + 0.0001f;
    }

    private Transform GetOrCreateCarryContainer()
    {
        if (carryContainer != null)
            return carryContainer;

        if (runtimeCarryContainer != null)
            return runtimeCarryContainer;

        GameObject containerObject = new GameObject("Slime Carry Container");
        runtimeCarryContainer = containerObject.transform;
        runtimeCarryContainer.position = GetBodyCenterPosition();
        runtimeCarryContainer.rotation = GetCarryContainerRotation();
        runtimeCarryContainer.localScale = Vector3.one;
        return runtimeCarryContainer;
    }

    private void SyncCarryContainerTransform()
    {
        if (!keepCarryContainerAtBodyCenter)
            return;

        Transform container = GetActiveCarryContainer();
        if (container == null)
            return;

        container.position = GetBodyCenterPosition();
        container.rotation = GetCarryContainerRotation();

        if (container == runtimeCarryContainer)
            container.localScale = Vector3.one;
    }

    private Transform GetActiveCarryContainer()
    {
        return carryContainer != null ? carryContainer : runtimeCarryContainer;
    }

    private Quaternion GetCarryContainerRotation()
    {
        Vector3 direction = lastAimDirection;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = transform.forward;

        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.forward;

        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private bool ReleaseCarriedItemAt(int index)
    {
        if (index < 0 || index >= carriedItems.Count)
            return false;

        CarriedItemState state = carriedItems[index];
        carriedItems.RemoveAt(index);

        if (state != null)
            carriedVolume = Mathf.Max(0f, carriedVolume - Mathf.Max(0f, state.Volume));

        SlimeCarryable item = state != null ? state.Item : null;
        if (item == null)
        {
            UpdateCarriedItemLayout();
            return false;
        }

        Vector3 direction = GetReleaseDirection();
        Vector3 releasePosition = GetBodyCenterPosition() + direction * Mathf.Max(0.05f, releaseDistance) + Vector3.up * releaseUpOffset;
        Quaternion releaseRotation = Quaternion.LookRotation(direction, Vector3.up);
        Vector3 releaseVelocityVector = direction * releaseVelocity + Vector3.up * (releaseVelocity * 0.25f);
        bool released = item.EndCarry(this, releasePosition, releaseRotation, releaseVelocityVector);

        UpdateCarriedItemLayout();
        return released;
    }

    private Vector3 GetReleaseDirection()
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

    private void UpdateCarriedItemLayout()
    {
        for (int i = 0; i < carriedItems.Count; i++)
        {
            CarriedItemState state = carriedItems[i];
            if (state == null || state.Item == null || !state.Item.IsCarriedBy(this))
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
            if (state != null && state.Item != null && state.Item.IsCarriedBy(this))
                continue;

            if (state != null)
                carriedVolume = Mathf.Max(0f, carriedVolume - Mathf.Max(0f, state.Volume));

            carriedItems.RemoveAt(i);
            changed = true;
        }

        if (changed)
            UpdateCarriedItemLayout();
    }

    private bool IsOwnCarryCollider(Collider hit)
    {
        if (hit == null)
            return true;

        if (hit.transform == transform || hit.transform.IsChildOf(transform))
            return true;

        SlimePlayerAbilities hitSlime = hit.GetComponentInParent<SlimePlayerAbilities>();
        if (hitSlime == this)
            return true;

        return ResolveBodyRigidbody() && hit.attachedRigidbody == bodyRigidbody;
    }

    private bool IsOwnManualAbsorbCollider(Collider hit)
    {
        if (hit == null)
            return true;

        if (hit.transform == transform || hit.transform.IsChildOf(transform))
            return true;

        SlimePlayerAbilities hitSlime = hit.GetComponentInParent<SlimePlayerAbilities>();
        return hitSlime == this;
    }

    private void ReadKeyboardInput()
    {
        Vector2 aimInput = ReadKeyVector(aimLeftKey, aimRightKey, aimBackKey, aimForwardKey);
        SetAimDirection(ResolveAimDirection(aimInput));

        if (IsKeyDown(splitKey))
            Split();

        if (IsKeyDown(mergeKey))
        {
            if (splitPieces.Count > 0)
            {
                if (MergeNearbySplitPieces() == 0)
                {
                    TryAbsorbNearbySlime(GetBodyCenterPosition());
                }
            }
            else if (TryAbsorbNearbySlime(GetBodyCenterPosition()))
            {
                return;
            }
            else if (isMerged)
            {
                ReturnToNormalForm();
            }
        }

        bool stretchPressed = IsStretchPressed();
        if (stretchPressed && !isStretching)
            BeginStretch(lastAimDirection);

        if (!stretchPressed && isStretching)
            EndStretch();

        if (IsKeyDown(carryKey) && !TryCarryNearestItem())
            ReleaseLastCarriedItem();
    }

    private bool IsStretchPressed()
    {
        return IsKeyPressed(stretchKey) || IsKeyPressed(alternateStretchKey);
    }

    private static Vector2 ReadKeyVector(KeyCode leftKey, KeyCode rightKey, KeyCode backKey, KeyCode forwardKey)
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

        return Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f);
    }

    private static bool IsKeyPressed(KeyCode key)
    {
        return key != KeyCode.None && Input.GetKey(key);
    }

    private static bool IsKeyDown(KeyCode key)
    {
        return key != KeyCode.None && Input.GetKeyDown(key);
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

    private void UpdateSoftbodyShape()
    {
        bool stretchShapeActive = stretchSoftbodyBones && (isStretching || Time.time - stretchReleasedTime <= stretchReturnDuration);
        bool bodyShapeActive = moveBonesWithBodyScale;

        if (!stretchShapeActive && !bodyShapeActive)
        {
            SetStretchJointsRelaxed(false);
            RestoreSpringTargetPositions();
            return;
        }

        if (!TryCacheStretchBones())
            return;

        SetStretchJointsRelaxed(stretchShapeActive);

        Vector3 targetShape = targetBodyShape;
        Quaternion targetRotation = transform.rotation;
        float acceleration = bodyShapeBoneAcceleration;
        float damping = bodyShapeBoneDamping;
        float maxVelocityChange = bodyShapeBoneMaxVelocityChange;

        if (stretchShapeActive)
        {
            targetShape = isStretching ? GetStretchShape() : GetNormalShape();
            targetRotation = GetStretchTargetRotation();
            acceleration = stretchBoneAcceleration;
            damping = stretchBoneDamping;
            maxVelocityChange = stretchBoneMaxVelocityChange;
        }

        bool useAssistForce = stretchShapeActive || assistBodyShapeBones;
        MoveSoftbodyBonesTowardShape(targetShape, targetRotation, acceleration, damping, maxVelocityChange, useAssistForce);
    }

    private void MoveSoftbodyBonesTowardShape(
        Vector3 targetShape,
        Quaternion targetRotation,
        float acceleration,
        float damping,
        float maxVelocityChange,
        bool useAssistForce)
    {
        for (int i = 0; i < stretchBoneBodies.Length; i++)
        {
            Rigidbody boneBody = stretchBoneBodies[i];
            if (boneBody == null)
                continue;

            Vector3 targetOffset = Vector3.Scale(stretchBoneBaseLocalOffsets[i], targetShape);
            Vector3 targetPosition = stretchRootBody.worldCenterOfMass + targetRotation * targetOffset;
            SetSpringTargetPosition(i, targetPosition);

            if (!useAssistForce)
                continue;

            Vector3 positionError = targetPosition - boneBody.worldCenterOfMass;
            Vector3 relativeVelocity = boneBody.velocity - stretchRootBody.velocity;
            Vector3 velocityChange = (positionError * acceleration - relativeVelocity * damping) * Time.fixedDeltaTime;
            velocityChange = Vector3.ClampMagnitude(velocityChange, maxVelocityChange);

            boneBody.AddForce(velocityChange, ForceMode.VelocityChange);
        }
    }

    private void SetSpringTargetPosition(int index, Vector3 targetPosition)
    {
        SpringJoint springJoint = stretchBoneJoints[index];
        if (springJoint == null || springJoint.connectedBody == null)
            return;

        springJoint.autoConfigureConnectedAnchor = false;
        springJoint.connectedAnchor = springJoint.connectedBody.transform.InverseTransformPoint(targetPosition);
    }

    private void RestoreSpringTargetPositions()
    {
        if (!stretchBonesCached)
            return;

        for (int i = 0; i < stretchBoneJoints.Length; i++)
        {
            SpringJoint springJoint = stretchBoneJoints[i];
            if (springJoint == null)
                continue;

            springJoint.connectedAnchor = stretchBoneBaseConnectedAnchors[i];
            springJoint.autoConfigureConnectedAnchor = stretchBoneBaseAutoConfigureAnchors[i];
        }
    }

    private bool TryCacheStretchBones()
    {
        if (stretchBonesCached && stretchRootBody != null)
            return true;

        if (boneSphere == null)
            boneSphere = GetComponent<BoneSphere>();

        if (boneSphere == null)
            boneSphere = GetComponentInParent<BoneSphere>();

        if (boneSphere == null)
            boneSphere = GetComponentInChildren<BoneSphere>();

        if (boneSphere == null || boneSphere.root == null)
            return false;

        stretchRootBody = boneSphere.root.GetComponent<Rigidbody>();
        if (stretchRootBody == null)
            return false;

        stretchBonesCached = true;
        CacheStretchBone(0, boneSphere.x);
        CacheStretchBone(1, boneSphere.x2);
        CacheStretchBone(2, boneSphere.y);
        CacheStretchBone(3, boneSphere.y2);
        CacheStretchBone(4, boneSphere.z);
        CacheStretchBone(5, boneSphere.z2);
        CacheBodyMass();
        return true;
    }

    private void CacheBodyMass()
    {
        if (stretchRootBody != null && baseBodyMass <= 0f)
            baseBodyMass = Mathf.Max(0.0001f, stretchRootBody.mass);
        else if (bodyRigidbody != null && baseBodyMass <= 0f)
            baseBodyMass = Mathf.Max(0.0001f, bodyRigidbody.mass);
    }

    private void SyncBodyMassWithVolume()
    {
        if (!hitDamageScalesMass)
            return;

        float massScale = Mathf.Max(0.05f, currentVolume / Mathf.Max(baseVolume, 0.001f));

        if (ResolveBodyRigidbody())
        {
            if (baseBodyMass <= 0f)
                baseBodyMass = Mathf.Max(0.0001f, bodyRigidbody.mass);

            bodyRigidbody.mass = Mathf.Max(0.0001f, baseBodyMass * massScale);
        }

        if (!TryCacheStretchBones())
            return;

        for (int i = 0; i < stretchBoneBodies.Length; i++)
        {
            Rigidbody boneBody = stretchBoneBodies[i];
            if (boneBody == null || stretchBoneBaseMasses[i] <= 0f)
                continue;

            boneBody.mass = Mathf.Max(0.0001f, stretchBoneBaseMasses[i] * massScale);
        }
    }

    private void CacheStretchBone(int index, GameObject boneObject)
    {
        stretchBoneBodies[index] = null;
        stretchBoneJoints[index] = null;
        stretchBoneBaseLocalOffsets[index] = Vector3.zero;
        stretchBoneBaseConnectedAnchors[index] = Vector3.zero;
        stretchBoneBaseSprings[index] = 0f;
        stretchBoneBaseDampers[index] = 0f;
        stretchBoneBaseMasses[index] = 0f;
        stretchBoneBaseAutoConfigureAnchors[index] = false;

        if (boneObject == null)
            return;

        Rigidbody boneBody = boneObject.GetComponent<Rigidbody>();
        if (boneBody == null)
            return;

        stretchBoneBodies[index] = boneBody;
        stretchBoneBaseMasses[index] = Mathf.Max(0.0001f, boneBody.mass);
        stretchBoneBaseLocalOffsets[index] = transform.InverseTransformDirection(boneBody.worldCenterOfMass - stretchRootBody.worldCenterOfMass);

        SpringJoint springJoint = boneObject.GetComponent<SpringJoint>();
        if (springJoint == null)
            return;

        stretchBoneJoints[index] = springJoint;
        stretchBoneBaseConnectedAnchors[index] = springJoint.connectedAnchor;
        stretchBoneBaseSprings[index] = springJoint.spring;
        stretchBoneBaseDampers[index] = springJoint.damper;
        stretchBoneBaseAutoConfigureAnchors[index] = springJoint.autoConfigureConnectedAnchor;
    }

    private void SetStretchJointsRelaxed(bool relaxed)
    {
        if (!stretchBonesCached || stretchJointsRelaxed == relaxed)
            return;

        for (int i = 0; i < stretchBoneJoints.Length; i++)
        {
            SpringJoint springJoint = stretchBoneJoints[i];
            if (springJoint == null)
                continue;

            springJoint.spring = relaxed ? stretchRelaxedSpring : stretchBoneBaseSprings[i];
            springJoint.damper = relaxed ? stretchRelaxedDamper : stretchBoneBaseDampers[i];
        }

        stretchJointsRelaxed = relaxed;
    }

    private Vector3 GetStretchShape()
    {
        float volumeScale = GetVolumeScale();
        float stretch = stretchLength + TotalStretchMultiplierBonus;
        return new Vector3(stretchThickness, stretchThickness, stretch) * volumeScale;
    }

    private Vector3 GetNormalShape()
    {
        return Vector3.one * GetVolumeScale();
    }

    private float GetVolumeScale()
    {
        return GetVolumeRadiusScale(BodyVolume);
    }

    private float GetVolumeRadiusScale(float volume)
    {
        return Mathf.Pow(Mathf.Max(volume, minBodyVolume) / Mathf.Max(baseVolume, 0.001f), 1f / 3f);
    }

    private float GetSplitViewScale(float volume)
    {
        float normalizedVolume = Mathf.Max(0.0001f, volume) / Mathf.Max(baseVolume, 0.001f);
        return Mathf.Sqrt(normalizedVolume);
    }

    private Quaternion GetStretchTargetRotation()
    {
        Vector3 direction = lastAimDirection;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = transform.forward;

        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.forward;

        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private void UpdateShapeTarget()
    {
        float volumeScale = GetVolumeScale();
        Vector3 shape = Vector3.one * volumeScale;

        if (isStretching)
        {
            if (!stretchSoftbodyBones || !TryCacheStretchBones())
            {
                float stretch = stretchLength + TotalStretchMultiplierBonus;
                shape = new Vector3(stretchThickness, stretchThickness, stretch) * volumeScale;
                RotateTowardAim();
            }
        }
        else if (isSplit)
        {
            shape = Vector3.one * GetSplitViewScale(BodyVolume) * splitScale;
        }
        else if (isMerged)
        {
            shape *= mergeScale;
        }

        SetTargetShape(shape);
    }

    private void SetTargetShape(Vector3 shape)
    {
        targetBodyShape = SanitizeShape(shape);
        targetLocalScale = ScaleVector(baseLocalScale, targetBodyShape);
    }

    private void ApplyShapeScale()
    {
        float lerp = Mathf.Clamp01(Time.deltaTime * shapeLerpSpeed);

        if (bodyRoot != null)
        {
            Vector3 feedbackScale = Vector3.Scale(targetLocalScale, absorbFeedbackShape) * absorbFeedbackScale;
            bodyRoot.localScale = Vector3.Lerp(bodyRoot.localScale, feedbackScale, lerp);
        }
    }

    private void RefreshShapeImmediate()
    {
        UpdateShapeTarget();

        if (bodyRoot != null)
            bodyRoot.localScale = targetLocalScale;

        ApplySpringTargetsToCurrentShape();
    }

    private void RefreshShapeTarget()
    {
        UpdateShapeTarget();
        ApplySpringTargetsToCurrentShape();
    }

    private void ApplySpringTargetsToCurrentShape()
    {
        if (!moveBonesWithBodyScale || !TryCacheStretchBones())
            return;

        Quaternion targetRotation = transform.rotation;
        for (int i = 0; i < stretchBoneJoints.Length; i++)
        {
            if (stretchBoneBodies[i] == null)
                continue;

            Vector3 targetOffset = Vector3.Scale(stretchBoneBaseLocalOffsets[i], targetBodyShape);
            Vector3 targetPosition = stretchRootBody.worldCenterOfMass + targetRotation * targetOffset;
            SetSpringTargetPosition(i, targetPosition);
        }
    }

    private static Vector3 ScaleVector(Vector3 baseScale, Vector3 shape)
    {
        return new Vector3(
            baseScale.x * shape.x,
            baseScale.y * shape.y,
            baseScale.z * shape.z);
    }

    private static Vector3 SanitizeShape(Vector3 shape)
    {
        return new Vector3(
            Mathf.Max(0.01f, shape.x),
            Mathf.Max(0.01f, shape.y),
            Mathf.Max(0.01f, shape.z));
    }

    private void RotateTowardAim()
    {
        if (lastAimDirection.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(lastAimDirection, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * stretchTurnSpeed);
    }

    private Vector3 BuildSplitLaunchVelocity(Vector3 splitDirection, float speedMultiplier, float upwardMultiplier)
    {
        Vector3 planarDirection = splitDirection;
        planarDirection.y = 0f;

        if (planarDirection.sqrMagnitude <= 0.0001f)
            planarDirection = lastAimDirection.sqrMagnitude > 0.0001f ? lastAimDirection : transform.forward;

        planarDirection.y = 0f;
        if (planarDirection.sqrMagnitude <= 0.0001f)
            planarDirection = Vector3.forward;

        planarDirection.Normalize();

        float launchScale = Mathf.Clamp01(splitLaunchVelocityScale);
        float planarSpeed = splitLaunchSpeed * Mathf.Max(0f, speedMultiplier) * launchScale;
        if (maxSplitLaunchPlanarSpeed > 0f)
            planarSpeed = Mathf.Min(planarSpeed, maxSplitLaunchPlanarSpeed);

        float upwardSpeed = splitLaunchUpward * Mathf.Max(0f, upwardMultiplier) * launchScale;
        if (maxSplitLaunchUpwardSpeed > 0f)
            upwardSpeed = Mathf.Min(upwardSpeed, maxSplitLaunchUpwardSpeed);

        return planarDirection * planarSpeed + Vector3.up * upwardSpeed;
    }

    private void DampenSplitTransitionMomentum()
    {
        DampenTransitionMomentum(splitTransitionVelocityRetain);
    }

    private void DampenMergeTransitionMomentum()
    {
        DampenTransitionMomentum(mergeTransitionVelocityRetain);
    }

    private void DampenTransitionMomentum(float velocityRetain)
    {
        float retain = Mathf.Clamp01(velocityRetain);

        if (ResolveBodyRigidbody())
            DampenRigidbodyMomentum(bodyRigidbody, retain);

        if (!TryCacheStretchBones())
            return;

        for (int i = 0; i < stretchBoneBodies.Length; i++)
        {
            Rigidbody boneBody = stretchBoneBodies[i];
            if (boneBody == null || boneBody == bodyRigidbody)
                continue;

            DampenRigidbodyMomentum(boneBody, retain);
        }
    }

    private void DampenRigidbodyMomentum(Rigidbody targetBody, float velocityRetain)
    {
        if (targetBody == null || targetBody.isKinematic)
            return;

        Vector3 velocity = targetBody.velocity * velocityRetain;
        Vector3 planarVelocity = new Vector3(velocity.x, 0f, velocity.z);
        if (maxTransitionPlanarSpeed > 0f)
            planarVelocity = Vector3.ClampMagnitude(planarVelocity, maxTransitionPlanarSpeed);

        float verticalVelocity = velocity.y;
        if (maxTransitionUpwardSpeed > 0f && verticalVelocity > maxTransitionUpwardSpeed)
            verticalVelocity = maxTransitionUpwardSpeed;

        targetBody.velocity = planarVelocity + Vector3.up * verticalVelocity;
        targetBody.angularVelocity *= Mathf.Clamp01(transitionAngularVelocityRetain);
    }

    private bool SpawnSplitPieces()
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
            return false;

        float totalSplitVolume = Mathf.Min(currentVolume * splitVolumeFraction, Mathf.Max(0f, currentVolume - minBodyVolume));
        if (totalSplitVolume <= 0f)
            return false;

        float pieceVolume = totalSplitVolume / count;
        if (pieceVolume < minSplitPieceVolume)
            return false;

        currentVolume = Mathf.Max(minBodyVolume, currentVolume - totalSplitVolume);

        Material sharedMaterial = slimeRenderer != null ? slimeRenderer.sharedMaterial : null;
        Vector3 splitOrigin = GetBodyCenterPosition();
        Vector3 splitForward = lastAimDirection.sqrMagnitude > 0.0001f ? lastAimDirection : transform.forward;
        splitForward.y = 0f;
        if (splitForward.sqrMagnitude < 0.0001f)
            splitForward = Vector3.forward;

        Quaternion splitRotation = Quaternion.LookRotation(splitForward.normalized, Vector3.up);
        float angleJitter = Random.Range(0f, 360f);
        float pieceScale = GetSplitPieceScale(pieceVolume);
        SlimeSplitPiece newestPiece = null;

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

            Vector3 launchVelocity = BuildSplitLaunchVelocity(splitDirection, speedJitter, 1f);

            splitPiece.Init(
                this,
                launchVelocity,
                sharedMaterial,
                currentColor,
                pieceVolume,
                splitMergeDuration);
            splitPiece.SetSplitGeneration(0, splitPieceMaxSplitGeneration);
            splitPieces.Add(splitPiece);
            newestPiece = splitPiece;
        }

        SetControlledSplitPiece(newestPiece);
        return true;
    }

    private void MergeSplitPieces()
    {
        for (int i = splitPieces.Count - 1; i >= 0; i--)
        {
            if (splitPieces[i] != null)
            {
                splitPieces[i].BeginMerge(splitMergeDuration);
            }
        }

        splitPieces.Clear();
        SetControlledSplitPiece(null);
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
                {
                    splitPieces[i].BeginMerge(splitMergeDuration);
                }
            }
        }

        splitPieces.Clear();
        SetControlledSplitPiece(null);
    }

    private void RefreshControlledSplitPiece()
    {
        if (!controlLastSplitPieceWithArrowKeys)
        {
            SetControlledSplitPiece(null);
            return;
        }

        if (controlledSplitPiece != null && !controlledSplitPiece.IsMerging && splitPieces.Contains(controlledSplitPiece))
            return;

        for (int i = splitPieces.Count - 1; i >= 0; i--)
        {
            SlimeSplitPiece piece = splitPieces[i];
            if (piece == null || piece.IsMerging)
                continue;

            SetControlledSplitPiece(piece);
            return;
        }

        SetControlledSplitPiece(null);
    }

    private void SetControlledSplitPiece(SlimeSplitPiece splitPiece)
    {
        if (controlledSplitPiece != null && (controlledSplitPiece != splitPiece || !controlLastSplitPieceWithArrowKeys))
            controlledSplitPiece.SetKeyboardControl(false, null);

        controlledSplitPiece = controlLastSplitPieceWithArrowKeys ? splitPiece : null;

        if (controlledSplitPiece != null)
        {
            controlledSplitPiece.SetSplitGeneration(controlledSplitPiece.SplitGeneration, splitPieceMaxSplitGeneration);
            controlledSplitPiece.SetKeyboardControlSettings(
                splitPieceCameraRelativeMovement,
                splitPieceForwardKey,
                splitPieceBackKey,
                splitPieceLeftKey,
                splitPieceRightKey,
                splitPieceMoveSpeed,
                splitPieceAcceleration,
                splitPieceOuterBodyDriveWeight);
            controlledSplitPiece.SetAbilityControlSettings(
                splitPieceSplitKey,
                splitPieceMergeKey,
                splitPieceStretchKey,
                splitPieceAlternateStretchKey,
                splitPieceCarryKey);
            controlledSplitPiece.SetAbilityTuning(
                stretchLength,
                stretchThickness,
                shapeLerpSpeed,
                stretchTurnSpeed,
                carryPickupRadius,
                carryMask,
                carryLocalOffset,
                carriedItemSpacing,
                releaseDistance,
                releaseUpOffset,
                releaseVelocity);
            controlledSplitPiece.SetKeyboardControl(true, cameraTransform);
        }
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

    private void PlayAbsorbFeedback(Color absorbedTint)
    {
        if (!isActiveAndEnabled || absorbPulseDuration <= 0f)
            return;

        if (absorbFeedbackCoroutine != null)
            StopCoroutine(absorbFeedbackCoroutine);

        absorbFeedbackCoroutine = StartCoroutine(PlayAbsorbFeedbackRoutine(absorbedTint));
    }

    private IEnumerator PlayAbsorbFeedbackRoutine(Color absorbedTint)
    {
        float duration = Mathf.Max(0.01f, absorbPulseDuration);
        float pulseAmount = Mathf.Max(0f, absorbPulseScale - 1f);
        float squashAmount = Mathf.Max(0f, absorbSquashAmount);
        Color flashColor = Color.Lerp(absorbedTint, Color.white, 0.55f);
        float elapsed = 0f;
        ShowAbsorbRing(false);

        while (elapsed < duration)
        {
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float pulse = Mathf.Sin(normalizedTime * Mathf.PI);
            float flashPulse = Mathf.Sin(normalizedTime * Mathf.PI * 2f);
            float flashAmount = Mathf.Clamp01(absorbFlashStrength * Mathf.Max(pulse, 0.45f + 0.55f * Mathf.Abs(flashPulse)));
            Color feedbackColor = Color.Lerp(currentColor, flashColor, flashAmount);
            Color glowColor = flashColor * Mathf.Max(0f, absorbGlowIntensity) * pulse;

            absorbFeedbackScale = 1f + pulseAmount * pulse;
            absorbFeedbackShape = new Vector3(
                1f + squashAmount * pulse,
                Mathf.Max(0.1f, 1f - squashAmount * 0.55f * pulse),
                1f + squashAmount * pulse);
            ApplyFeedbackColor(feedbackColor, glowColor);
            UpdateAbsorbRing(normalizedTime, pulse, flashColor);

            elapsed += Time.deltaTime;
            yield return null;
        }

        absorbFeedbackScale = 1f;
        absorbFeedbackShape = Vector3.one;
        ApplyFeedbackColor(currentColor, Color.black);
        ShowAbsorbRing(false);
        absorbFeedbackCoroutine = null;
    }

    private void ResetAbsorbFeedback()
    {
        if (absorbFeedbackCoroutine != null)
        {
            StopCoroutine(absorbFeedbackCoroutine);
            absorbFeedbackCoroutine = null;
        }

        absorbFeedbackScale = 1f;
        absorbFeedbackShape = Vector3.one;
        ApplyFeedbackColor(currentColor, Color.black);
        ShowAbsorbRing(false);
    }

    private void UpdateAbsorbRing(float normalizedTime, float pulse, Color color)
    {
        if (!absorbRingEnabled)
            return;

        EnsureAbsorbRing();
        if (absorbRingRenderer == null)
            return;

        float easedTime = 1f - Mathf.Pow(1f - Mathf.Clamp01(normalizedTime), 3f);
        float radius = Mathf.Lerp(Mathf.Max(0.01f, absorbRingStartRadius), Mathf.Max(absorbRingStartRadius, absorbRingEndRadius), easedTime);
        float alpha = Mathf.Clamp01((1f - normalizedTime) * 0.95f + pulse * 0.25f);
        Color ringColor = color;
        ringColor.a = alpha;

        absorbRingRenderer.enabled = true;
        absorbRingRenderer.startColor = ringColor;
        absorbRingRenderer.endColor = new Color(ringColor.r, ringColor.g, ringColor.b, alpha * 0.35f);
        absorbRingRenderer.widthMultiplier = Mathf.Max(0.01f, absorbRingWidth) * (1f + pulse * 0.85f);
        ApplyAbsorbRingMaterialColor(ringColor, color * Mathf.Max(0f, absorbGlowIntensity));

        Vector3 center = GetBodyCenterPosition() + Vector3.up * absorbRingYOffset;
        for (int i = 0; i < AbsorbRingSegments; i++)
        {
            float angle = (Mathf.PI * 2f * i) / AbsorbRingSegments;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            absorbRingRenderer.SetPosition(i, center + offset);
        }
    }

    private void ShowAbsorbRing(bool visible)
    {
        if (absorbRingRenderer != null)
            absorbRingRenderer.enabled = visible;
    }

    private void EnsureAbsorbRing()
    {
        if (absorbRingRenderer != null)
            return;

        GameObject ringObject = new GameObject("Absorb Feedback Ring");
        ringObject.transform.SetParent(transform, false);

        absorbRingRenderer = ringObject.AddComponent<LineRenderer>();
        absorbRingRenderer.useWorldSpace = true;
        absorbRingRenderer.loop = true;
        absorbRingRenderer.positionCount = AbsorbRingSegments;
        absorbRingRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        absorbRingRenderer.receiveShadows = false;
        absorbRingRenderer.enabled = false;

        Shader ringShader = Shader.Find("HDRP/Unlit");
        if (ringShader == null)
            ringShader = Shader.Find("Sprites/Default");
        if (ringShader == null)
            ringShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (ringShader == null)
            ringShader = Shader.Find("Unlit/Color");

        if (ringShader != null)
        {
            absorbRingMaterial = new Material(ringShader)
            {
                name = "Absorb Feedback Ring",
                hideFlags = HideFlags.HideAndDontSave
            };
            absorbRingRenderer.sharedMaterial = absorbRingMaterial;
        }
    }

    private void ApplyAbsorbRingMaterialColor(Color color, Color emission)
    {
        if (absorbRingMaterial == null)
            return;

        absorbRingMaterial.SetColor(BaseColor, color);
        absorbRingMaterial.SetColor(ColorId, color);
        absorbRingMaterial.SetColor(EmissionColor, emission);
        absorbRingMaterial.SetColor(EmissiveColor, emission);
    }

    private void DestroyAbsorbRing()
    {
        if (absorbRingRenderer != null)
        {
            Destroy(absorbRingRenderer.gameObject);
            absorbRingRenderer = null;
        }

        if (absorbRingMaterial != null)
        {
            Destroy(absorbRingMaterial);
            absorbRingMaterial = null;
        }
    }

    private void ApplyFeedbackColor(Color color, Color emission)
    {
        if (slimeRenderer != null)
            ApplyColor(slimeRenderer, color, true, emission);
    }

    private void ApplyColor(Renderer targetRenderer, Color color)
    {
        ApplyColor(targetRenderer, color, false, Color.black);
    }

    private void ApplyColor(Renderer targetRenderer, Color color, bool includeEmission, Color emission)
    {
        if (targetRenderer == null)
            return;

        EnsurePropertyBlock();
        propertyBlock.Clear();
        propertyBlock.SetColor(BaseColor, color);
        propertyBlock.SetColor(ColorId, color);

        if (includeEmission)
        {
            propertyBlock.SetColor(EmissionColor, emission);
            propertyBlock.SetColor(EmissiveColor, emission);
        }

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
        ReleaseAllCarriedItems();

        SetTargetShape(new Vector3(1.15f, 0.2f, 1.15f));

        if (ResolveBodyRigidbody())
            bodyRigidbody.velocity *= 0.25f;

        Heartwell.UI.InGameOverlayUI.ShowRespawn();
        SceneRespawnManager.RespawnCurrentScene();
    }

    private void ResolveReferences()
    {
        if (slimeRenderer == null)
            slimeRenderer = GetComponentInChildren<Renderer>();

        if (slimeRenderer != null && slimeMaterialOverride != null && slimeRenderer.sharedMaterial != slimeMaterialOverride)
            slimeRenderer.sharedMaterial = slimeMaterialOverride;

        if (bodyRoot == null)
            bodyRoot = transform;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        ResolveBodyRigidbody();
    }

    private bool ResolveBodyRigidbody()
    {
        if (bodyRigidbody != null)
        {
            CacheBodyMass();
            return true;
        }

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

        CacheBodyMass();
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
