using UnityEngine;

[DisallowMultipleComponent]
public class SlimeAbsorbable : MonoBehaviour
{
    [Header("Material")]
    [SerializeField] private SlimeMaterialType materialType = SlimeMaterialType.Neutral;
    [SerializeField] private Color tint = Color.white;

    [Header("Gains")]
    [SerializeField] private float volumeGain = 0.2f;
    [SerializeField] private float pressureResistanceBonus = 0f;
    [SerializeField] private float stretchMultiplierBonus = 0f;
    [SerializeField] private float bridgeDurationBonus = 0f;

    [Header("Pickup")]
    [SerializeField] private bool autoMergeOnContact = true;
    [SerializeField] private bool destroyOnAbsorb = true;
    [SerializeField] private bool createPickupTriggerWhenMissing = true;
    [SerializeField] private float fallbackPickupTriggerRadius = 0.5f;
    [SerializeField] private bool listenOnChildColliders = true;
    [SerializeField] private int childColliderRefreshFrames = 12;

    private bool absorbed;
    private int remainingChildColliderRefreshes;

    public SlimeMaterialType MaterialType => materialType;
    public Color Tint => tint;
    public float VolumeGain => volumeGain;
    public float PressureResistanceBonus => pressureResistanceBonus;
    public float StretchMultiplierBonus => stretchMultiplierBonus;
    public float BridgeDurationBonus => bridgeDurationBonus;
    public bool AutoMergeOnContact => autoMergeOnContact;

    private void Awake()
    {
        remainingChildColliderRefreshes = Mathf.Max(1, childColliderRefreshFrames);
        EnsurePickupTrigger();
        ConfigureChildColliderRelays();
    }

    private void Start()
    {
        EnsurePickupTrigger();
        ConfigureChildColliderRelays();
    }

    private void Update()
    {
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
}
