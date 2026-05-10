using UnityEngine;

public enum SlimeAbsorbAbility
{
    None,
    WaveAttack
}

public enum SlimePickupVisualKind
{
    Auto,
    MagicRock,
    GlowingMushroom,
    None
}

[ExecuteAlways]
[DisallowMultipleComponent]
public class SlimeAbsorbable : MonoBehaviour
{
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorMap = Shader.PropertyToID("_BaseColorMap");
    private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");
    private static readonly int NormalMap = Shader.PropertyToID("_NormalMap");
    private static readonly int BumpMap = Shader.PropertyToID("_BumpMap");
    private static readonly int MaskMap = Shader.PropertyToID("_MaskMap");
    private static readonly int MetallicGlossMap = Shader.PropertyToID("_MetallicGlossMap");
    private static readonly int EmissiveColorMap = Shader.PropertyToID("_EmissiveColorMap");
    private static readonly int EmissionMap = Shader.PropertyToID("_EmissionMap");
    private static readonly int EmissiveColor = Shader.PropertyToID("_EmissiveColor");
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissiveIntensity = Shader.PropertyToID("_EmissiveIntensity");

    private const string MagicRockModelPath = "AbilityPickups/GlowingMagicRock/source/Rock Low Poly";
    private const string MagicRockBaseMapPath = "AbilityPickups/GlowingMagicRock/textures/DefaultMaterial_BaseColor";
    private const string MagicRockNormalMapPath = "AbilityPickups/GlowingMagicRock/textures/DefaultMaterial_Normal";
    private const string MagicRockMetallicMapPath = "AbilityPickups/GlowingMagicRock/textures/DefaultMaterial_Metallic";
    private const string MagicRockEmissiveMapPath = "AbilityPickups/GlowingMagicRock/textures/DefaultMaterial_Emissive_2";
    private const string GlowingMushroomModelPath = "AbilityPickups/GlowingMushroom/source/mushroom_Final";
    private const string GlowingMushroomBaseMapPath = "AbilityPickups/GlowingMushroom/textures/mushroom_diffuse";
    private const string GlowingMushroomEmissiveMapPath = "AbilityPickups/GlowingMushroom/textures/mushroom_emission";
    private const string GeneratedVisualSuffix = " Generated";

    [Header("Material")]
    [SerializeField] private SlimeMaterialType materialType = SlimeMaterialType.Neutral;
    [SerializeField] private Color tint = Color.white;

    [Header("Gains")]
    [SerializeField] private float volumeGain = 0.2f;
    [SerializeField] private float pressureResistanceBonus = 0f;
    [SerializeField] private float stretchMultiplierBonus = 0f;

    [Header("Timed Ability")]
    [SerializeField] private SlimeAbsorbAbility grantedAbility = SlimeAbsorbAbility.None;
    [SerializeField] private float abilityDuration = 15f;

    [Header("Pickup")]
    [SerializeField] private bool autoMergeOnContact = false;
    [SerializeField] private bool destroyOnAbsorb = true;
    [SerializeField] private bool createPickupTriggerWhenMissing = true;
    [SerializeField] private float fallbackPickupTriggerRadius = 0.5f;
    [SerializeField] private bool listenOnChildColliders = true;
    [SerializeField] private int childColliderRefreshFrames = 12;

    [Header("Visual Override")]
    [SerializeField] private SlimePickupVisualKind visualKind = SlimePickupVisualKind.Auto;
    [SerializeField] private bool replaceExistingVisuals = true;
    [SerializeField] private Vector3 visualLocalOffset = Vector3.zero;
    [SerializeField] private Vector3 visualLocalEulerAngles = Vector3.zero;
    [SerializeField] private Vector3 visualLocalScale = Vector3.one;
    [SerializeField] private bool normalizeVisualBounds = true;
    [SerializeField] private float visualTargetSize = 1.25f;
    [SerializeField] private bool animateVisual = true;
    [SerializeField] private float visualBobAmplitude = 0.08f;
    [SerializeField] private float visualBobSpeed = 1.8f;
    [SerializeField] private float visualSpinSpeed = 35f;

    private bool absorbed;
    private int remainingChildColliderRefreshes;
    private bool visualInitialized;
    private GameObject visualInstance;
    private Material runtimeVisualMaterial;
    private Vector3 visualBaseLocalPosition;
    private Vector3 visualBaseLocalEulerAngles;
    private float visualSeed;

    public SlimeMaterialType MaterialType => materialType;
    public Color Tint => tint;
    public float VolumeGain => volumeGain;
    public float PressureResistanceBonus => pressureResistanceBonus;
    public float StretchMultiplierBonus => stretchMultiplierBonus;
    public SlimeAbsorbAbility GrantedAbility => grantedAbility;
    public float AbilityDuration => Mathf.Max(0f, abilityDuration);
    public bool AutoMergeOnContact => autoMergeOnContact;

    private void Awake()
    {
        visualSeed = Random.value * 10f;
        remainingChildColliderRefreshes = Mathf.Max(1, childColliderRefreshFrames);
        EnsureVisualOverride();
        EnsurePickupTrigger();
        ConfigureChildColliderRelays();
    }

    private void OnEnable()
    {
        EnsureVisualOverride();
    }

    private void Start()
    {
        EnsureVisualOverride();
        EnsurePickupTrigger();
        ConfigureChildColliderRelays();
    }

    private void Update()
    {
        AnimatePickupVisual();

        if (!listenOnChildColliders || remainingChildColliderRefreshes <= 0)
            return;

        ConfigureChildColliderRelays();
        remainingChildColliderRefreshes--;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryAbsorbFromCollider(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryAbsorbFromCollider(collision.collider);
    }

    public void TryAbsorbFromCollider(Collider other)
    {
        if (!autoMergeOnContact || absorbed || other == null || other.transform.IsChildOf(transform))
            return;

        SlimePlayerAbilities slime = other.GetComponentInParent<SlimePlayerAbilities>();
        TryAbsorb(slime);
    }

    public bool TryAbsorb(SlimePlayerAbilities slime)
    {
        if (absorbed || slime == null || !slime.TryAbsorb(this))
            return false;

        CompleteAbsorb();
        return true;
    }

    private void CompleteAbsorb()
    {
        if (absorbed)
            return;

        absorbed = true;

        if (destroyOnAbsorb)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }

    private void EnsureVisualOverride()
    {
        if (visualInitialized)
            return;

        visualInitialized = true;
        SlimePickupVisualKind resolvedVisual = ResolveVisualKind();
        if (resolvedVisual == SlimePickupVisualKind.None)
            return;

        DestroyGeneratedVisualChildren();

        if (replaceExistingVisuals)
            HideExistingRenderers();

        GameObject sourceVisual = Resources.Load<GameObject>(GetModelPath(resolvedVisual));
        visualInstance = sourceVisual != null
            ? Instantiate(sourceVisual, transform)
            : GameObject.CreatePrimitive(GetFallbackPrimitive(resolvedVisual));

        visualInstance.name = GetVisualName(resolvedVisual) + GeneratedVisualSuffix;
        visualInstance.transform.SetParent(transform, false);
        ApplyGeneratedVisualFlags(visualInstance);
        visualBaseLocalPosition = ResolveVisualOffset(resolvedVisual);
        visualBaseLocalEulerAngles = ResolveVisualEulerAngles(resolvedVisual);
        visualInstance.transform.localPosition = visualBaseLocalPosition;
        visualInstance.transform.localRotation = Quaternion.Euler(visualBaseLocalEulerAngles);
        visualInstance.transform.localScale = ResolveVisualScale(resolvedVisual);

        DisableVisualColliders(visualInstance);
        ApplyRuntimeVisualMaterial(resolvedVisual);
        NormalizeVisualBounds(resolvedVisual);
    }

    private void DestroyGeneratedVisualChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.name.EndsWith(GeneratedVisualSuffix))
                SafeDestroy(child.gameObject);
        }
    }

    private static void ApplyGeneratedVisualFlags(GameObject visual)
    {
        if (visual == null || Application.isPlaying)
            return;

        Transform[] children = visual.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null)
                children[i].gameObject.hideFlags = HideFlags.DontSaveInEditor;
        }
    }

    private SlimePickupVisualKind ResolveVisualKind()
    {
        if (visualKind != SlimePickupVisualKind.Auto)
            return visualKind;

        if (grantedAbility == SlimeAbsorbAbility.WaveAttack)
            return SlimePickupVisualKind.GlowingMushroom;

        if (materialType == SlimeMaterialType.Sticky)
            return SlimePickupVisualKind.MagicRock;

        return SlimePickupVisualKind.None;
    }

    private static string GetModelPath(SlimePickupVisualKind resolvedVisual)
    {
        return resolvedVisual == SlimePickupVisualKind.GlowingMushroom
            ? GlowingMushroomModelPath
            : MagicRockModelPath;
    }

    private static PrimitiveType GetFallbackPrimitive(SlimePickupVisualKind resolvedVisual)
    {
        return resolvedVisual == SlimePickupVisualKind.GlowingMushroom
            ? PrimitiveType.Capsule
            : PrimitiveType.Sphere;
    }

    private static string GetVisualName(SlimePickupVisualKind resolvedVisual)
    {
        return resolvedVisual == SlimePickupVisualKind.GlowingMushroom
            ? "Glowing Mushroom Pickup Visual"
            : "Glowing Magic Rock Pickup Visual";
    }

    private Vector3 ResolveVisualOffset(SlimePickupVisualKind resolvedVisual)
    {
        if (visualLocalOffset != Vector3.zero)
            return visualLocalOffset;

        return resolvedVisual == SlimePickupVisualKind.GlowingMushroom
            ? new Vector3(0f, 0.1f, 0f)
            : new Vector3(0f, 0.35f, 0f);
    }

    private Vector3 ResolveVisualEulerAngles(SlimePickupVisualKind resolvedVisual)
    {
        if (visualLocalEulerAngles != Vector3.zero)
            return visualLocalEulerAngles;

        return resolvedVisual == SlimePickupVisualKind.GlowingMushroom
            ? Vector3.zero
            : new Vector3(0f, 25f, 0f);
    }

    private Vector3 ResolveVisualScale(SlimePickupVisualKind resolvedVisual)
    {
        if (visualLocalScale != Vector3.one)
            return visualLocalScale;

        return resolvedVisual == SlimePickupVisualKind.GlowingMushroom
            ? Vector3.one * 0.85f
            : Vector3.one * 0.58f;
    }

    private void HideExistingRenderers()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = false;
        }
    }

    private static void DisableVisualColliders(GameObject visual)
    {
        if (visual == null)
            return;

        Collider[] colliders = visual.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        Rigidbody[] rigidbodies = visual.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            if (rigidbodies[i] != null)
                rigidbodies[i].isKinematic = true;
        }
    }

    private void ApplyRuntimeVisualMaterial(SlimePickupVisualKind resolvedVisual)
    {
        if (visualInstance == null)
            return;

        runtimeVisualMaterial = CreateRuntimeVisualMaterial(resolvedVisual);
        Renderer[] renderers = visualInstance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer childRenderer = renderers[i];
            if (childRenderer == null)
                continue;

            Material[] materials = childRenderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                childRenderer.sharedMaterial = runtimeVisualMaterial;
                continue;
            }

            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                materials[materialIndex] = runtimeVisualMaterial;

            childRenderer.sharedMaterials = materials;
        }
    }

    private void NormalizeVisualBounds(SlimePickupVisualKind resolvedVisual)
    {
        if (!normalizeVisualBounds || visualInstance == null)
            return;

        if (!TryGetVisualBounds(out Bounds bounds))
        {
            CreateFallbackVisibleProxy(resolvedVisual);
            return;
        }

        float largestSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (largestSize <= 0.001f)
        {
            CreateFallbackVisibleProxy(resolvedVisual);
            return;
        }

        float targetSize = visualTargetSize > 0.001f
            ? visualTargetSize
            : (resolvedVisual == SlimePickupVisualKind.GlowingMushroom ? 1.15f : 1.35f);
        float scaleFactor = Mathf.Clamp(targetSize / largestSize, 0.02f, 250f);
        visualInstance.transform.localScale *= scaleFactor;
    }

    private bool TryGetVisualBounds(out Bounds bounds)
    {
        bounds = new Bounds(transform.position, Vector3.zero);
        if (visualInstance == null)
            return false;

        Renderer[] renderers = visualInstance.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer childRenderer = renderers[i];
            if (childRenderer == null)
                continue;

            if (hasBounds)
                bounds.Encapsulate(childRenderer.bounds);
            else
                bounds = childRenderer.bounds;

            hasBounds = true;
        }

        return hasBounds;
    }

    private void CreateFallbackVisibleProxy(SlimePickupVisualKind resolvedVisual)
    {
        GameObject fallback = GameObject.CreatePrimitive(GetFallbackPrimitive(resolvedVisual));
        fallback.name = $"{GetVisualName(resolvedVisual)} Fallback";
        fallback.transform.SetParent(visualInstance != null ? visualInstance.transform : transform, false);
        fallback.transform.localPosition = Vector3.zero;
        fallback.transform.localRotation = Quaternion.Euler(0f, 18f, 0f);
        fallback.transform.localScale = Vector3.one * Mathf.Max(0.25f, visualTargetSize);

        Collider fallbackCollider = fallback.GetComponent<Collider>();
        if (fallbackCollider != null)
            SafeDestroy(fallbackCollider);

        MeshRenderer fallbackRenderer = fallback.GetComponent<MeshRenderer>();
        if (fallbackRenderer != null)
            fallbackRenderer.sharedMaterial = runtimeVisualMaterial != null
                ? runtimeVisualMaterial
                : CreateRuntimeVisualMaterial(resolvedVisual);
    }

    private static void SafeDestroy(Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    private Material CreateRuntimeVisualMaterial(SlimePickupVisualKind resolvedVisual)
    {
        Shader shader = Shader.Find("HDRP/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        Material material = new Material(shader)
        {
            name = $"{GetVisualName(resolvedVisual)} Material",
            hideFlags = HideFlags.HideAndDontSave
        };

        Color baseColor = resolvedVisual == SlimePickupVisualKind.GlowingMushroom
            ? new Color(0.68f, 0.96f, 0.72f, 1f)
            : new Color(0.32f, 0.95f, 0.88f, 1f);
        Color emission = resolvedVisual == SlimePickupVisualKind.GlowingMushroom
            ? new Color(0.45f, 1.8f, 0.6f, 1f)
            : new Color(0.35f, 2.6f, 3.2f, 1f);

        SetColorIfPresent(material, BaseColor, baseColor);
        SetColorIfPresent(material, ColorId, baseColor);
        SetColorIfPresent(material, EmissiveColor, emission);
        SetColorIfPresent(material, EmissionColor, emission);
        SetFloatIfPresent(material, EmissiveIntensity, resolvedVisual == SlimePickupVisualKind.GlowingMushroom ? 2.2f : 3.2f);

        Texture2D baseTexture = LoadBaseTexture(resolvedVisual);
        Texture2D normalTexture = LoadNormalTexture(resolvedVisual);
        Texture2D metallicTexture = LoadMetallicTexture(resolvedVisual);
        Texture2D emissiveTexture = LoadEmissiveTexture(resolvedVisual);

        SetTextureIfPresent(material, BaseColorMap, baseTexture);
        SetTextureIfPresent(material, BaseMap, baseTexture);
        SetTextureIfPresent(material, MainTex, baseTexture);
        SetTextureIfPresent(material, NormalMap, normalTexture);
        SetTextureIfPresent(material, BumpMap, normalTexture);
        SetTextureIfPresent(material, MaskMap, metallicTexture);
        SetTextureIfPresent(material, MetallicGlossMap, metallicTexture);
        SetTextureIfPresent(material, EmissiveColorMap, emissiveTexture);
        SetTextureIfPresent(material, EmissionMap, emissiveTexture);

        if (normalTexture != null)
            material.EnableKeyword("_NORMALMAP");

        if (emissiveTexture != null)
        {
            material.EnableKeyword("_EMISSION");
            material.EnableKeyword("_EMISSIVE_COLOR_MAP");
        }

        return material;
    }

    private static Texture2D LoadBaseTexture(SlimePickupVisualKind resolvedVisual)
    {
        return Resources.Load<Texture2D>(resolvedVisual == SlimePickupVisualKind.GlowingMushroom
            ? GlowingMushroomBaseMapPath
            : MagicRockBaseMapPath);
    }

    private static Texture2D LoadNormalTexture(SlimePickupVisualKind resolvedVisual)
    {
        return resolvedVisual == SlimePickupVisualKind.GlowingMushroom
            ? null
            : Resources.Load<Texture2D>(MagicRockNormalMapPath);
    }

    private static Texture2D LoadMetallicTexture(SlimePickupVisualKind resolvedVisual)
    {
        return resolvedVisual == SlimePickupVisualKind.GlowingMushroom
            ? null
            : Resources.Load<Texture2D>(MagicRockMetallicMapPath);
    }

    private static Texture2D LoadEmissiveTexture(SlimePickupVisualKind resolvedVisual)
    {
        return Resources.Load<Texture2D>(resolvedVisual == SlimePickupVisualKind.GlowingMushroom
            ? GlowingMushroomEmissiveMapPath
            : MagicRockEmissiveMapPath);
    }

    private static void SetColorIfPresent(Material material, int propertyId, Color color)
    {
        if (material != null && material.HasProperty(propertyId))
            material.SetColor(propertyId, color);
    }

    private static void SetFloatIfPresent(Material material, int propertyId, float value)
    {
        if (material != null && material.HasProperty(propertyId))
            material.SetFloat(propertyId, value);
    }

    private static void SetTextureIfPresent(Material material, int propertyId, Texture texture)
    {
        if (material != null && texture != null && material.HasProperty(propertyId))
            material.SetTexture(propertyId, texture);
    }

    private void AnimatePickupVisual()
    {
        if (!animateVisual || visualInstance == null)
            return;

        float time = (Application.isPlaying ? Time.time : Time.realtimeSinceStartup) + visualSeed;
        float bob = Mathf.Sin(time * Mathf.Max(0f, visualBobSpeed)) * Mathf.Max(0f, visualBobAmplitude);
        float spin = time * visualSpinSpeed;
        visualInstance.transform.localPosition = visualBaseLocalPosition + Vector3.up * bob;
        visualInstance.transform.localRotation = Quaternion.Euler(visualBaseLocalEulerAngles + Vector3.up * spin);
    }

    private void ConfigureChildColliderRelays()
    {
        if (!listenOnChildColliders)
            return;

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider childCollider = colliders[i];
            if (childCollider == null || childCollider.transform == transform)
                continue;

            SlimeAbsorbableContactRelay relay = childCollider.GetComponent<SlimeAbsorbableContactRelay>();
            if (relay == null)
                relay = childCollider.gameObject.AddComponent<SlimeAbsorbableContactRelay>();

            relay.Init(this);
        }
    }

    private void EnsurePickupTrigger()
    {
        if (!createPickupTriggerWhenMissing || GetComponent<Collider>() != null)
            return;

        SphereCollider pickupTrigger = gameObject.AddComponent<SphereCollider>();
        pickupTrigger.isTrigger = true;

        Bounds bounds;
        if (TryGetRendererBounds(out bounds))
        {
            pickupTrigger.center = transform.InverseTransformPoint(bounds.center);
            float largestWorldExtent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
            float largestScale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y), Mathf.Abs(transform.lossyScale.z));
            pickupTrigger.radius = largestScale > 0.0001f ? largestWorldExtent / largestScale : largestWorldExtent;
        }
        else
        {
            pickupTrigger.radius = Mathf.Max(0.01f, fallbackPickupTriggerRadius);
        }
    }

    private bool TryGetRendererBounds(out Bounds bounds)
    {
        bounds = new Bounds(transform.position, Vector3.zero);
        bool hasBounds = false;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer childRenderer = renderers[i];
            if (childRenderer == null)
                continue;

            if (hasBounds)
                bounds.Encapsulate(childRenderer.bounds);
            else
                bounds = childRenderer.bounds;

            hasBounds = true;
        }

        return hasBounds;
    }

    private void OnValidate()
    {
        volumeGain = Mathf.Max(0f, volumeGain);
        abilityDuration = Mathf.Max(0f, abilityDuration);
        fallbackPickupTriggerRadius = Mathf.Max(0.01f, fallbackPickupTriggerRadius);
        childColliderRefreshFrames = Mathf.Max(1, childColliderRefreshFrames);
        visualTargetSize = Mathf.Max(0f, visualTargetSize);
        visualBobAmplitude = Mathf.Max(0f, visualBobAmplitude);
        visualBobSpeed = Mathf.Max(0f, visualBobSpeed);
    }

    private void OnDrawGizmos()
    {
        SlimePickupVisualKind resolvedVisual = ResolveVisualKind();
        if (resolvedVisual == SlimePickupVisualKind.None)
            return;

        Vector3 markerCenter = transform.position + transform.rotation * ResolveVisualOffset(resolvedVisual);
        float markerRadius = grantedAbility == SlimeAbsorbAbility.WaveAttack
            ? 0.75f
            : Mathf.Max(0.28f, ResolveVisualScale(resolvedVisual).magnitude * 0.18f);
        Color markerColor = resolvedVisual == SlimePickupVisualKind.GlowingMushroom
            ? new Color(0.35f, 1f, 0.75f, 0.75f)
            : new Color(0.1f, 0.95f, 0.9f, 0.42f);

        Gizmos.color = markerColor;
        Gizmos.DrawSphere(markerCenter, markerRadius);
        Gizmos.color = new Color(markerColor.r, markerColor.g, markerColor.b, 0.95f);
        Gizmos.DrawWireSphere(markerCenter, markerRadius * 2.4f);
    }

    private void OnDestroy()
    {
        if (runtimeVisualMaterial != null)
        {
            SafeDestroy(runtimeVisualMaterial);
            runtimeVisualMaterial = null;
        }
    }
}
