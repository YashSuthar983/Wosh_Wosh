using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SlimePlayerAbilities))]
public class SlimeWaveAttack : MonoBehaviour
{
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private readonly RaycastHit[] groundHits = new RaycastHit[16];

    [Header("References")]
    [SerializeField] private SlimePlayerAbilities abilities = null;
    [SerializeField] private Transform originOverride = null;

    [Header("Input")]
    [SerializeField] private bool readKeyboardInput = true;
    [SerializeField] private KeyCode waveKey = KeyCode.R;
    [SerializeField] private float cooldown = 1.2f;
    [SerializeField] private bool requireAbsorbedAbility = true;
    [SerializeField] private SlimeAbsorbAbility requiredAbility = SlimeAbsorbAbility.WaveAttack;

    [Header("Visual")]
    [SerializeField] private Material waveMaterial = null;
    [SerializeField] private Color waveColor = new Color(0.25f, 0.95f, 0.55f, 0.72f);

    [Header("Wave")]
    [SerializeField] private float startRadius = 0.45f;
    [SerializeField] private float maxRadius = 5.5f;
    [SerializeField] private float travelSpeed = 7f;
    [SerializeField] private float ringWidth = 0.6f;
    [SerializeField] private float crestHeight = 0.16f;
    [SerializeField] private float surfaceHeight = 0.08f;
    [SerializeField] private float fallDuration = 0.42f;
    [SerializeField] private float fallDistance = 0.16f;
    [SerializeField, Range(16, 160)] private int meshSegments = 80;

    [Header("Hit")]
    [SerializeField] private float damage = 1f;
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Grounding")]
    [SerializeField] private bool snapToGround = true;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundProbeStart = 1.6f;
    [SerializeField] private float groundProbeDistance = 5f;
    [SerializeField] private float fallbackGroundOffset = 0.65f;

    private float nextAttackTime;
    private int missingAbilityAttempts;
    private float firstMissingAbilityAttemptTime = -100f;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (!readKeyboardInput || waveKey == KeyCode.None)
            return;

        if (Input.GetKeyDown(waveKey))
            TryAttack();
    }

    public bool TryAttack()
    {
        ResolveReferences();

        if (abilities != null && abilities.IsCrushed)
            return false;

        if (requireAbsorbedAbility && (abilities == null || !abilities.HasActiveAbsorbAbility(requiredAbility)))
        {
            RegisterMissingAbilityAttempt();
            return false;
        }

        if (Time.time < nextAttackTime)
            return false;

        missingAbilityAttempts = 0;
        SpawnWave();
        nextAttackTime = Time.time + Mathf.Max(0f, cooldown);
        return true;
    }

    private void RegisterMissingAbilityAttempt()
    {
        if (Time.time - firstMissingAbilityAttemptTime > 5f)
        {
            missingAbilityAttempts = 0;
            firstMissingAbilityAttemptTime = Time.time;
        }

        missingAbilityAttempts++;
        if (missingAbilityAttempts < 2)
            return;

        missingAbilityAttempts = 0;
        firstMissingAbilityAttemptTime = Time.time;
        Heartwell.UI.InGameOverlayUI.ShowContextHint();
    }

    private void SpawnWave()
    {
        Vector3 origin = ResolveWaveOrigin();
        GameObject waveObject = new GameObject("Slime Wave Pulse");
        waveObject.transform.position = origin;

        SlimeWavePulse pulse = waveObject.AddComponent<SlimeWavePulse>();
        pulse.Initialize(
            transform,
            abilities,
            ResolveWaveMaterial(),
            ResolveWaveColor(),
            startRadius,
            maxRadius,
            travelSpeed,
            ringWidth,
            crestHeight,
            surfaceHeight,
            fallDuration,
            fallDistance,
            meshSegments,
            damage,
            hitMask);
    }

    private Vector3 ResolveWaveOrigin()
    {
        Vector3 origin = originOverride != null
            ? originOverride.position
            : (abilities != null ? abilities.BodyCenterPosition : transform.position);

        if (snapToGround)
        {
            Vector3 rayStart = origin + Vector3.up * Mathf.Max(0f, groundProbeStart);
            float rayDistance = Mathf.Max(0.01f, groundProbeStart + groundProbeDistance);
            int hitCount = Physics.RaycastNonAlloc(rayStart, Vector3.down, groundHits, rayDistance, groundMask, QueryTriggerInteraction.Ignore);
            float bestDistance = float.PositiveInfinity;
            RaycastHit bestHit = default;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = groundHits[i];
                if (ShouldIgnoreGroundHit(hit.collider))
                    continue;

                if (hit.distance < bestDistance)
                {
                    bestHit = hit;
                    bestDistance = hit.distance;
                }
            }

            if (bestHit.collider != null)
            {
                origin = bestHit.point;
                return origin;
            }
        }

        origin.y -= Mathf.Max(0f, fallbackGroundOffset);
        return origin;
    }

    private bool ShouldIgnoreGroundHit(Collider hit)
    {
        if (hit == null)
            return true;

        if (hit.transform == transform || hit.transform.IsChildOf(transform))
            return true;

        SlimePlayerAbilities slime = hit.GetComponentInParent<SlimePlayerAbilities>();
        return slime != null && (abilities == null || slime == abilities);
    }

    private Material ResolveWaveMaterial()
    {
        if (waveMaterial != null)
            return waveMaterial;

        return abilities != null ? abilities.SlimeSharedMaterial : null;
    }

    private Color ResolveWaveColor()
    {
        Color color;
        if (waveMaterial != null && TryResolveMaterialColor(waveMaterial, out color))
        {
            color.a = waveColor.a;
            return color;
        }

        color = abilities != null ? abilities.CurrentColor : waveColor;
        color.a = waveColor.a;
        return color;
    }

    private static bool TryResolveMaterialColor(Material material, out Color color)
    {
        color = Color.white;
        if (material == null)
            return false;

        if (material.HasProperty(BaseColor))
        {
            color = material.GetColor(BaseColor);
            return true;
        }

        if (material.HasProperty(ColorId))
        {
            color = material.GetColor(ColorId);
            return true;
        }

        return false;
    }

    private void ResolveReferences()
    {
        if (abilities == null)
            abilities = GetComponent<SlimePlayerAbilities>();
    }

    private void OnValidate()
    {
        cooldown = Mathf.Max(0f, cooldown);
        startRadius = Mathf.Max(0.01f, startRadius);
        maxRadius = Mathf.Max(startRadius + 0.01f, maxRadius);
        travelSpeed = Mathf.Max(0.01f, travelSpeed);
        ringWidth = Mathf.Max(0.01f, ringWidth);
        crestHeight = Mathf.Max(0f, crestHeight);
        surfaceHeight = Mathf.Max(0f, surfaceHeight);
        fallDuration = Mathf.Max(0.01f, fallDuration);
        fallDistance = Mathf.Max(0f, fallDistance);
        damage = Mathf.Max(0f, damage);
        groundProbeStart = Mathf.Max(0f, groundProbeStart);
        groundProbeDistance = Mathf.Max(0.01f, groundProbeDistance);
        fallbackGroundOffset = Mathf.Max(0f, fallbackGroundOffset);
    }
}
