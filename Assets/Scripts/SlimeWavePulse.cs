using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class SlimeWavePulse : MonoBehaviour
{
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int UnlitColor = Shader.PropertyToID("_UnlitColor");
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissiveColor = Shader.PropertyToID("_EmissiveColor");

    private const float TwoPi = Mathf.PI * 2f;
    private readonly Collider[] hits = new Collider[96];
    private readonly HashSet<SlimeWaveDamageReceiver> damagedReceivers = new HashSet<SlimeWaveDamageReceiver>();
    private readonly List<Transform> blobTransforms = new List<Transform>();
    private readonly List<Vector3> blobSeeds = new List<Vector3>();

    private Transform ownerRoot;
    private SlimePlayerAbilities ownerAbilities;
    private LineRenderer outerCrestRenderer;
    private LineRenderer innerFoamRenderer;
    private Mesh blobMesh;
    private Material blobMaterial;
    private Material outerCrestMaterial;
    private Material innerFoamMaterial;
    private Color baseColor;
    private LayerMask hitMask;

    private float startRadius;
    private float maxRadius;
    private float travelSpeed;
    private float ringWidth;
    private float crestHeight;
    private float surfaceHeight;
    private float fallDuration;
    private float fallDistance;
    private float damage;
    private float travelDuration;
    private float elapsed;
    private int meshSegments;
    private bool initialized;

    public void Initialize(
        Transform owner,
        SlimePlayerAbilities abilities,
        Material sourceMaterial,
        Color color,
        float waveStartRadius,
        float waveMaxRadius,
        float waveTravelSpeed,
        float waveRingWidth,
        float waveCrestHeight,
        float waveSurfaceHeight,
        float waveFallDuration,
        float waveFallDistance,
        int waveMeshSegments,
        float waveDamage,
        LayerMask waveHitMask)
    {
        ownerRoot = owner;
        ownerAbilities = abilities;
        baseColor = color;
        startRadius = Mathf.Max(0.01f, waveStartRadius);
        maxRadius = Mathf.Max(startRadius + 0.01f, waveMaxRadius);
        travelSpeed = Mathf.Max(0.01f, waveTravelSpeed);
        ringWidth = Mathf.Max(0.01f, waveRingWidth);
        crestHeight = Mathf.Max(0f, waveCrestHeight);
        surfaceHeight = Mathf.Max(0f, waveSurfaceHeight);
        fallDuration = Mathf.Max(0.01f, waveFallDuration);
        fallDistance = Mathf.Max(0f, waveFallDistance);
        meshSegments = Mathf.Clamp(waveMeshSegments, 16, 160);
        damage = Mathf.Max(0f, waveDamage);
        hitMask = waveHitMask;
        travelDuration = Mathf.Max(0.01f, (maxRadius - startRadius) / travelSpeed);
        initialized = true;

        EnsureVisual(sourceMaterial);
        UpdateWave(0f, 0f);
    }

    private void Update()
    {
        if (!initialized)
            return;

        elapsed += Time.deltaTime;

        float travelT = Mathf.Clamp01(elapsed / travelDuration);
        float fadeElapsed = Mathf.Max(0f, elapsed - travelDuration);
        float fadeT = Mathf.Clamp01(fadeElapsed / fallDuration);

        if (fadeT >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        UpdateWave(travelT, fadeT);

        if (elapsed <= travelDuration)
            DamageTargetsInWave(travelT);
    }

    private void UpdateWave(float travelT, float fadeT)
    {
        float radius = Mathf.Lerp(startRadius, maxRadius, EaseOutCubic(travelT));
        float alpha = baseColor.a * Mathf.Clamp01(1f - fadeT);
        float height = surfaceHeight - fallDistance * fadeT;
        float crest = crestHeight * Mathf.Clamp01(1f - fadeT);

        UpdateBlobWave(radius, height, crest, alpha);
        UpdateCrestLines(radius, height, crest, alpha);
    }

    private void DamageTargetsInWave(float travelT)
    {
        float radius = Mathf.Lerp(startRadius, maxRadius, EaseOutCubic(travelT));
        float outerRadius = radius + ringWidth * 0.55f;
        float innerRadius = Mathf.Max(0f, radius - ringWidth * 0.65f);
        Vector3 center = transform.position;

        int hitCount = Physics.OverlapSphereNonAlloc(
            center,
            outerRadius,
            hits,
            hitMask,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hits[i];
            if (ShouldIgnore(hit))
                continue;

            Vector3 closest = ResolveClosestPoint(hit, center);
            if (closest == center)
                closest = hit.transform.position;

            Vector3 planarOffset = closest - center;
            planarOffset.y = 0f;
            float planarDistance = planarOffset.magnitude;
            if (planarDistance < innerRadius || planarDistance > outerRadius)
                continue;

            SlimeWaveDamageReceiver receiver = hit.GetComponentInParent<SlimeWaveDamageReceiver>();
            if (receiver == null || damagedReceivers.Contains(receiver))
                continue;

            damagedReceivers.Add(receiver);
            receiver.ApplyWaveHit(damage, center);
        }
    }

    private bool ShouldIgnore(Collider hit)
    {
        if (hit == null)
            return true;

        if (ownerRoot != null && (hit.transform == ownerRoot || hit.transform.IsChildOf(ownerRoot)))
            return true;

        SlimePlayerAbilities slime = hit.GetComponentInParent<SlimePlayerAbilities>();
        return slime != null && (ownerAbilities == null || slime == ownerAbilities);
    }

    private static Vector3 ResolveClosestPoint(Collider hit, Vector3 point)
    {
        if (hit == null)
            return point;

        MeshCollider meshCollider = hit as MeshCollider;
        if (meshCollider != null && !meshCollider.convex)
            return hit.bounds.ClosestPoint(point);

        TerrainCollider terrainCollider = hit as TerrainCollider;
        if (terrainCollider != null)
            return hit.bounds.ClosestPoint(point);

        return hit.ClosestPoint(point);
    }

    private void EnsureVisual(Material sourceMaterial)
    {
        blobMesh = CreateBlobMesh();
        blobMaterial = CreateWaveMaterial(sourceMaterial);

        int blobCount = Mathf.Clamp(Mathf.RoundToInt(meshSegments * 0.7f), 42, 86);
        for (int i = 0; i < blobCount; i++)
        {
            GameObject blobObject = new GameObject($"Slime Wave Blob {i:00}");
            blobObject.transform.SetParent(transform, false);

            MeshFilter blobFilter = blobObject.AddComponent<MeshFilter>();
            blobFilter.sharedMesh = blobMesh;

            MeshRenderer blobRenderer = blobObject.AddComponent<MeshRenderer>();
            blobRenderer.sharedMaterial = blobMaterial;
            blobRenderer.shadowCastingMode = ShadowCastingMode.Off;
            blobRenderer.receiveShadows = false;

            float seed = Mathf.Sin((i + 1) * 12.9898f) * 43758.5453f;
            seed -= Mathf.Floor(seed);

            blobTransforms.Add(blobObject.transform);
            blobSeeds.Add(new Vector3(
                seed,
                Mathf.Repeat(seed * 3.17f, 1f),
                Mathf.Repeat(seed * 7.91f, 1f)));
        }

        outerCrestMaterial = CreateWaveMaterial(null);
        innerFoamMaterial = CreateWaveMaterial(null);
        outerCrestRenderer = CreateLineRenderer("Slime Wave Outer Crest", outerCrestMaterial);
        innerFoamRenderer = CreateLineRenderer("Slime Wave Inner Foam", innerFoamMaterial);
    }

    private static Mesh CreateBlobMesh()
    {
        const int latitudeCount = 6;
        const int longitudeCount = 10;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        vertices.Add(new Vector3(0f, 0.52f, 0f));

        for (int latitude = 1; latitude < latitudeCount; latitude++)
        {
            float verticalT = latitude / (float)latitudeCount;
            float phi = verticalT * Mathf.PI;
            float ringRadius = Mathf.Sin(phi) * 0.5f;
            float y = Mathf.Cos(phi) * 0.42f;

            for (int longitude = 0; longitude < longitudeCount; longitude++)
            {
                float angle = (longitude / (float)longitudeCount) * TwoPi;
                vertices.Add(new Vector3(
                    Mathf.Cos(angle) * ringRadius,
                    y,
                    Mathf.Sin(angle) * ringRadius));
            }
        }

        int bottomIndex = vertices.Count;
        vertices.Add(new Vector3(0f, -0.18f, 0f));

        for (int longitude = 0; longitude < longitudeCount; longitude++)
        {
            int nextLongitude = (longitude + 1) % longitudeCount;
            triangles.Add(0);
            triangles.Add(1 + longitude);
            triangles.Add(1 + nextLongitude);
        }

        for (int latitude = 0; latitude < latitudeCount - 2; latitude++)
        {
            int rowStart = 1 + latitude * longitudeCount;
            int nextRowStart = rowStart + longitudeCount;

            for (int longitude = 0; longitude < longitudeCount; longitude++)
            {
                int nextLongitude = (longitude + 1) % longitudeCount;
                int i0 = rowStart + longitude;
                int i1 = rowStart + nextLongitude;
                int i2 = nextRowStart + longitude;
                int i3 = nextRowStart + nextLongitude;

                triangles.Add(i0);
                triangles.Add(i2);
                triangles.Add(i1);
                triangles.Add(i1);
                triangles.Add(i2);
                triangles.Add(i3);
            }
        }

        int lastRowStart = 1 + (latitudeCount - 2) * longitudeCount;
        for (int longitude = 0; longitude < longitudeCount; longitude++)
        {
            int nextLongitude = (longitude + 1) % longitudeCount;
            triangles.Add(lastRowStart + longitude);
            triangles.Add(bottomIndex);
            triangles.Add(lastRowStart + nextLongitude);
        }

        Mesh result = new Mesh
        {
            name = "Slime Wave Blob Mesh",
            hideFlags = HideFlags.HideAndDontSave
        };
        result.SetVertices(vertices);
        result.SetTriangles(triangles, 0);
        result.RecalculateNormals();
        result.RecalculateBounds();
        return result;
    }

    private void UpdateBlobWave(float radius, float height, float crest, float alpha)
    {
        Color color = baseColor;
        color.a = alpha;
        Color emission = new Color(color.r, color.g, color.b, 1f) * Mathf.Lerp(0.45f, 1.35f, Mathf.Clamp01(alpha));
        ApplyColorToMaterial(blobMaterial, color, emission);

        float alphaRatio = baseColor.a > 0.001f ? Mathf.Clamp01(alpha / baseColor.a) : Mathf.Clamp01(alpha);
        float fadeScale = Mathf.Lerp(0.35f, 1f, alphaRatio);
        float baseBlobRadius = Mathf.Max(0.18f, ringWidth * 0.48f);

        for (int i = 0; i < blobTransforms.Count; i++)
        {
            Transform blobTransform = blobTransforms[i];
            Vector3 seed = blobSeeds[i];
            float waveMotion = elapsed * 6.5f + seed.x * 11f;
            float angle = (i / (float)blobTransforms.Count) * TwoPi
                + (seed.x - 0.5f) * 0.08f
                + Mathf.Sin(waveMotion * 0.47f) * 0.035f;
            float radialJitter = Mathf.Sin(waveMotion) * ringWidth * 0.21f;
            float blobRadius = radius + radialJitter;
            float crestLift = crest * (0.55f + seed.y * 0.58f) + Mathf.Sin(waveMotion * 1.31f) * baseBlobRadius * 0.12f;

            blobTransform.gameObject.SetActive(alpha > 0.015f);
            blobTransform.localPosition = new Vector3(
                Mathf.Cos(angle) * blobRadius,
                height + crestLift,
                Mathf.Sin(angle) * blobRadius);
            blobTransform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg + 90f, 0f);

            float wobbleScale = 1f + Mathf.Sin(waveMotion * 1.73f) * 0.12f;
            float width = baseBlobRadius * Mathf.Lerp(0.88f, 1.46f, seed.y) * fadeScale * wobbleScale;
            float length = baseBlobRadius * Mathf.Lerp(1.28f, 2.05f, seed.z) * fadeScale * (2f - wobbleScale);
            float vertical = Mathf.Max(0.08f, baseBlobRadius * Mathf.Lerp(0.52f, 0.94f, seed.x) * fadeScale + crest * 0.72f);
            blobTransform.localScale = new Vector3(width, vertical, length);
        }
    }

    private LineRenderer CreateLineRenderer(string lineName, Material lineMaterial)
    {
        GameObject lineObject = new GameObject(lineName);
        lineObject.transform.SetParent(transform, false);

        LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = false;
        lineRenderer.loop = true;
        lineRenderer.positionCount = meshSegments;
        lineRenderer.sharedMaterial = lineMaterial;
        lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.textureMode = LineTextureMode.Tile;
        lineRenderer.alignment = LineAlignment.View;
        return lineRenderer;
    }

    private void UpdateCrestLines(float radius, float height, float crest, float alpha)
    {
        UpdateLine(
            outerCrestRenderer,
            radius + ringWidth * 0.5f,
            height + crest * 0.82f,
            Mathf.Max(0.025f, ringWidth * 0.11f),
            new Color(baseColor.r, baseColor.g, baseColor.b, alpha * 0.55f));

        Color foamColor = Color.Lerp(baseColor, Color.white, 0.18f);
        foamColor.a = alpha * 0.35f;
        UpdateLine(
            innerFoamRenderer,
            Mathf.Max(0.01f, radius - ringWidth * 0.36f),
            height + crest * 0.42f,
            Mathf.Max(0.014f, ringWidth * 0.05f),
            foamColor);
    }

    private void UpdateLine(LineRenderer lineRenderer, float radius, float height, float width, Color color)
    {
        if (lineRenderer == null)
            return;

        lineRenderer.enabled = color.a > 0.01f;
        lineRenderer.widthMultiplier = width;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;

        ApplyColorToMaterial(lineRenderer.sharedMaterial, color, new Color(color.r, color.g, color.b, 1f) * 1.2f);

        if (lineRenderer.positionCount != meshSegments)
            lineRenderer.positionCount = meshSegments;

        for (int i = 0; i < meshSegments; i++)
        {
            float angle = (i / (float)meshSegments) * TwoPi;
            lineRenderer.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius));
        }
    }

    private Material CreateWaveMaterial(Material sourceMaterial)
    {
        Material result;
        if (sourceMaterial != null)
        {
            result = new Material(sourceMaterial);
        }
        else
        {
            Shader shader = Shader.Find("HDRP/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Transparent");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Hidden/InternalErrorShader");

            result = new Material(shader);
        }

        result.name = "Runtime Slime Wave Material";
        result.hideFlags = HideFlags.HideAndDontSave;
        result.renderQueue = (int)RenderQueue.Transparent;
        result.SetOverrideTag("RenderType", "Transparent");

        if (result.HasProperty("_SurfaceType"))
            result.SetFloat("_SurfaceType", 1f);
        if (result.HasProperty("_BlendMode"))
            result.SetFloat("_BlendMode", 0f);
        if (result.HasProperty("_SrcBlend"))
            result.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (result.HasProperty("_DstBlend"))
            result.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (result.HasProperty("_ZWrite"))
            result.SetFloat("_ZWrite", 0f);
        if (result.HasProperty("_CullMode"))
            result.SetFloat("_CullMode", 0f);
        if (result.HasProperty("_Cull"))
            result.SetFloat("_Cull", 0f);

        result.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        result.EnableKeyword("_ALPHABLEND_ON");
        result.EnableKeyword("_EMISSION");
        ApplyColorToMaterial(result, baseColor, new Color(baseColor.r, baseColor.g, baseColor.b, 1f) * 1.2f);

        return result;
    }

    private static void ApplyColorToMaterial(Material targetMaterial, Color color, Color emission)
    {
        if (targetMaterial == null)
            return;

        if (targetMaterial.HasProperty(BaseColor))
            targetMaterial.SetColor(BaseColor, color);
        if (targetMaterial.HasProperty(ColorId))
            targetMaterial.SetColor(ColorId, color);
        if (targetMaterial.HasProperty(UnlitColor))
            targetMaterial.SetColor(UnlitColor, color);
        if (targetMaterial.HasProperty(EmissionColor))
            targetMaterial.SetColor(EmissionColor, emission);
        if (targetMaterial.HasProperty(EmissiveColor))
            targetMaterial.SetColor(EmissiveColor, emission);
    }

    private static float EaseOutCubic(float value)
    {
        value = Mathf.Clamp01(value);
        float inverse = 1f - value;
        return 1f - inverse * inverse * inverse;
    }

    private void OnDestroy()
    {
        if (blobMesh != null)
        {
            Destroy(blobMesh);
            blobMesh = null;
        }

        if (blobMaterial != null)
        {
            Destroy(blobMaterial);
            blobMaterial = null;
        }

        if (outerCrestMaterial != null)
        {
            Destroy(outerCrestMaterial);
            outerCrestMaterial = null;
        }

        if (innerFoamMaterial != null)
        {
            Destroy(innerFoamMaterial);
            innerFoamMaterial = null;
        }
    }
}
