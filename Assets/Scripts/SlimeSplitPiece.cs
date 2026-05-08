using UnityEngine;

[DisallowMultipleComponent]
public class SlimeSplitPiece : MonoBehaviour
{
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private MaterialPropertyBlock propertyBlock;

    private SlimePlayerAbilities owner;
    private Vector3 launchVelocity;
    private Vector3 mergeStartCenter;
    private Vector3 mergeStartScale;
    private Rigidbody[] bodies;
    private float mergeStartTime;
    private float mergeDuration;
    private bool launchApplied;
    private bool merging;
    private Renderer[] pieceRenderers;

    public bool IsMerging => merging;
    public Vector3 CenterPosition => GetCenterPosition();

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
    }

    public void Init(
        SlimePlayerAbilities newOwner,
        Vector3 newLaunchVelocity,
        Material sharedMaterial,
        Color color,
        float newMergeDuration)
    {
        owner = newOwner;
        launchVelocity = newLaunchVelocity;
        mergeDuration = Mathf.Max(0.01f, newMergeDuration);
        launchApplied = false;
        merging = false;
        bodies = null;

        pieceRenderers = GetComponentsInChildren<Renderer>(true);
        if (sharedMaterial != null)
        {
            for (int i = 0; i < pieceRenderers.Length; i++)
            {
                if (pieceRenderers[i] != null)
                    pieceRenderers[i].sharedMaterial = sharedMaterial;
            }
        }

        SetColor(color);
    }

    public void BeginMerge(float duration)
    {
        if (merging)
            return;

        merging = true;
        mergeDuration = Mathf.Max(0.01f, duration);
        mergeStartTime = Time.time;
        mergeStartScale = transform.localScale;
        mergeStartCenter = GetCenterPosition();

        SpringJoint[] springs = GetComponentsInChildren<SpringJoint>(true);
        for (int i = 0; i < springs.Length; i++)
            Destroy(springs[i]);

        Joint[] joints = GetComponentsInChildren<Joint>(true);
        for (int i = 0; i < joints.Length; i++)
            Destroy(joints[i]);

        Rigidbody[] rigidbodies = GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            if (rigidbodies[i] == null)
                continue;
            rigidbodies[i].velocity = Vector3.zero;
            rigidbodies[i].angularVelocity = Vector3.zero;
            rigidbodies[i].isKinematic = true;
            rigidbodies[i].useGravity = false;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        transform.position = mergeStartCenter;
    }

    public void SetColor(Color color)
    {
        if (pieceRenderers == null || pieceRenderers.Length == 0)
            pieceRenderers = GetComponentsInChildren<Renderer>(true);

        if (pieceRenderers == null || pieceRenderers.Length == 0)
            return;

        EnsurePropertyBlock();
        propertyBlock.SetColor(BaseColor, color);
        propertyBlock.SetColor(ColorId, color);

        for (int i = 0; i < pieceRenderers.Length; i++)
        {
            if (pieceRenderers[i] != null)
                pieceRenderers[i].SetPropertyBlock(propertyBlock);
        }
    }

    private void EnsurePropertyBlock()
    {
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();
    }

    private void Update()
    {
        if (owner == null)
        {
            Destroy(gameObject);
            return;
        }

        if (!launchApplied)
            TryApplyLaunch();

        if (merging)
            UpdateMerge();
    }

    private void TryApplyLaunch()
    {
        bodies = GetComponentsInChildren<Rigidbody>(true);
        if (bodies == null || bodies.Length == 0)
            return;

        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] == null || bodies[i].isKinematic)
                continue;
            bodies[i].velocity = launchVelocity;
        }

        launchApplied = true;
    }

    private void UpdateMerge()
    {
        float t = Mathf.Clamp01((Time.time - mergeStartTime) / mergeDuration);
        float eased = Mathf.SmoothStep(0f, 1f, t);
        Vector3 ownerCenter = owner.BodyCenterPosition;
        transform.position = Vector3.Lerp(mergeStartCenter, ownerCenter, eased);
        transform.localScale = Vector3.Lerp(mergeStartScale, Vector3.zero, eased);

        if (t >= 1f)
            Destroy(gameObject);
    }

    private Vector3 GetCenterPosition()
    {
        if (bodies == null || bodies.Length == 0)
            bodies = GetComponentsInChildren<Rigidbody>(true);

        if (bodies != null && bodies.Length > 0)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] != null)
                {
                    sum += bodies[i].worldCenterOfMass;
                    count++;
                }
            }
            if (count > 0)
                return sum / count;
        }

        return transform.position;
    }
}
