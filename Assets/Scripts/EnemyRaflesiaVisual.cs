using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
public class EnemyRaflesiaVisual : MonoBehaviour
{
    private const string VisualRootName = "__RaflesiaVisual";

    [SerializeField] private Texture2D raflesiaTexture = null;
    [SerializeField, Min(0.5f)] private float visualWidth = 2.4f;
    [SerializeField, Min(0.5f)] private float visualDepth = 2.4f;
    [SerializeField] private float surfaceHeight = 0.05f;
    [SerializeField, Range(3, 8)] private int petalCount = 5;
    [SerializeField, Min(0.05f)] private float centerRadius = 0.38f;
    [SerializeField, Min(0f)] private float petalLift = 0.22f;
    [SerializeField, Min(0f)] private float petalDroop = 0.18f;
    [SerializeField] private Color tint = Color.white;
    [SerializeField] private bool hideSourceMesh = true;
    [SerializeField] private bool drawEditorGizmo = true;
    [SerializeField] private Color editorGizmoColor = new Color(1f, 0.05f, 0.02f, 0.65f);
    [SerializeField, Min(0.1f)] private float editorGizmoRadius = 1.15f;

    private void OnEnable()
    {
        BuildVisual();
    }

    private void OnDisable()
    {
        DestroyExistingVisual();
    }

    private void OnDestroy()
    {
        DestroyExistingVisual();
    }

    private void OnDrawGizmos()
    {
        if (!drawEditorGizmo)
            return;

        Gizmos.color = editorGizmoColor;
        Vector3 center = transform.position + Vector3.up * 0.35f;
        Gizmos.DrawSphere(center, editorGizmoRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, editorGizmoRadius * 1.05f);
    }

    private void BuildVisual()
    {
        if (hideSourceMesh)
        {
            MeshRenderer sourceRenderer = GetComponent<MeshRenderer>();
            if (sourceRenderer != null)
            {
                sourceRenderer.enabled = false;
            }
        }

        DestroyExistingVisual();

        GameObject visualRoot = new GameObject(VisualRootName);
        visualRoot.transform.SetParent(transform, false);
        visualRoot.transform.localPosition = Vector3.zero;
        visualRoot.transform.localRotation = Quaternion.identity;
        visualRoot.transform.localScale = Vector3.one;

        if (!Application.isPlaying)
        {
            visualRoot.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        }

        CreateMeshChild(
            visualRoot.transform,
            "Raflesia Petals",
            CreatePetalMesh(),
            CreateMaterial(raflesiaTexture, tint, true));

        CreateMeshChild(
            visualRoot.transform,
            "Raflesia Mouth",
            CreateMouthMesh(),
            CreateMaterial(null, new Color(0.045f, 0.005f, 0.005f, 1f), false));
    }

    private void CreateMeshChild(Transform parent, string childName, Mesh mesh, Material material)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;

        if (!Application.isPlaying)
        {
            child.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        }

        MeshFilter meshFilter = child.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        MeshRenderer meshRenderer = child.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = ShadowCastingMode.On;
        meshRenderer.receiveShadows = true;
    }

    private Mesh CreatePetalMesh()
    {
        const int radialSegments = 7;
        const int angularSegments = 8;

        int petals = Mathf.Max(3, petalCount);
        int columns = angularSegments + 1;
        float halfWidth = visualWidth * 0.5f;
        float halfDepth = visualDepth * 0.5f;
        float normalizedCenterRadius = Mathf.Clamp01(centerRadius / Mathf.Max(halfWidth, halfDepth));
        float angleStep = Mathf.PI * 2f / petals;
        float halfPetalAngle = angleStep * 0.48f;

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        for (int petal = 0; petal < petals; petal++)
        {
            float centerAngle = petal * angleStep + Mathf.PI * 0.5f;
            int startIndex = vertices.Count;

            for (int radial = 0; radial <= radialSegments; radial++)
            {
                float t = radial / (float)radialSegments;
                float radius = Mathf.Lerp(normalizedCenterRadius * 0.55f, 1f, t);
                float spread = halfPetalAngle * Mathf.Lerp(0.2f, 1f, Mathf.SmoothStep(0f, 1f, t));
                float curve = Mathf.Sin(t * Mathf.PI);

                for (int angleIndex = 0; angleIndex <= angularSegments; angleIndex++)
                {
                    float side = (angleIndex / (float)angularSegments) * 2f - 1f;
                    float sideFalloff = 1f - Mathf.Abs(side);
                    float angle = centerAngle + side * spread;
                    float edgeRadius = radius * Mathf.Lerp(0.92f, 1.04f, sideFalloff);

                    float x = Mathf.Cos(angle) * edgeRadius * halfWidth;
                    float z = Mathf.Sin(angle) * edgeRadius * halfDepth;
                    float y = surfaceHeight + curve * petalLift - t * t * petalDroop + sideFalloff * curve * 0.035f;

                    vertices.Add(new Vector3(x, y, z));
                    uvs.Add(new Vector2(
                        Mathf.InverseLerp(-halfWidth, halfWidth, x),
                        Mathf.InverseLerp(-halfDepth, halfDepth, z)));
                }
            }

            for (int radial = 0; radial < radialSegments; radial++)
            {
                for (int angleIndex = 0; angleIndex < angularSegments; angleIndex++)
                {
                    int i0 = startIndex + radial * columns + angleIndex;
                    int i1 = i0 + 1;
                    int i2 = i0 + columns;
                    int i3 = i2 + 1;

                    triangles.Add(i0);
                    triangles.Add(i1);
                    triangles.Add(i2);
                    triangles.Add(i1);
                    triangles.Add(i3);
                    triangles.Add(i2);
                }
            }
        }

        return CreateMesh("Raflesia Petal Mesh", vertices, uvs, triangles);
    }

    private Mesh CreateMouthMesh()
    {
        const int radialSegments = 6;
        const int angularSegments = 48;
        int columns = angularSegments + 1;
        float rimHeight = surfaceHeight + 0.2f;
        float centerDepth = surfaceHeight - 0.18f;

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        for (int radial = 0; radial <= radialSegments; radial++)
        {
            float t = radial / (float)radialSegments;
            float radius = centerRadius * Mathf.SmoothStep(0f, 1f, t);
            float y = Mathf.Lerp(centerDepth, rimHeight, t * t);

            for (int angleIndex = 0; angleIndex <= angularSegments; angleIndex++)
            {
                float angle = (angleIndex / (float)angularSegments) * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;

                vertices.Add(new Vector3(x, y, z));
                uvs.Add(new Vector2(t, angleIndex / (float)angularSegments));
            }
        }

        for (int radial = 0; radial < radialSegments; radial++)
        {
            for (int angleIndex = 0; angleIndex < angularSegments; angleIndex++)
            {
                int i0 = radial * columns + angleIndex;
                int i1 = i0 + 1;
                int i2 = i0 + columns;
                int i3 = i2 + 1;

                triangles.Add(i0);
                triangles.Add(i2);
                triangles.Add(i1);
                triangles.Add(i1);
                triangles.Add(i2);
                triangles.Add(i3);
            }
        }

        return CreateMesh("Raflesia Mouth Mesh", vertices, uvs, triangles);
    }

    private Mesh CreateMesh(string meshName, List<Vector3> vertices, List<Vector2> uvs, List<int> triangles)
    {
        Mesh mesh = new Mesh
        {
            name = meshName
        };

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        if (!Application.isPlaying)
        {
            mesh.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        }

        return mesh;
    }

    private Material CreateMaterial(Texture texture, Color color, bool transparent)
    {
        Shader shader = FindCompatibleShader();
        Material material = new Material(shader)
        {
            name = "Raflesia Enemy Visual",
            renderQueue = transparent ? (int)RenderQueue.Transparent : (int)RenderQueue.Geometry
        };

        if (!Application.isPlaying)
        {
            material.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        }

        AssignTexture(material, texture);
        AssignColor(material, color);

        if (transparent)
        {
            ConfigureTransparency(material);
        }
        else
        {
            ConfigureCullOff(material);
        }

        return material;
    }

    private void DestroyExistingVisual()
    {
        Transform existingVisual = transform.Find(VisualRootName);
        if (existingVisual == null)
        {
            return;
        }

        MeshFilter[] meshFilters = existingVisual.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < meshFilters.Length; i++)
        {
            if (meshFilters[i].sharedMesh != null)
            {
                DestroyUnityObject(meshFilters[i].sharedMesh);
            }
        }

        MeshRenderer[] meshRenderers = existingVisual.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < meshRenderers.Length; i++)
        {
            Material[] materials = meshRenderers[i].sharedMaterials;
            for (int j = 0; j < materials.Length; j++)
            {
                if (materials[j] != null)
                {
                    DestroyUnityObject(materials[j]);
                }
            }
        }

        DestroyUnityObject(existingVisual.gameObject);
    }

    private static void AssignTexture(Material material, Texture texture)
    {
        if (texture == null)
        {
            return;
        }

        if (material.HasProperty("_BaseColorMap"))
        {
            material.SetTexture("_BaseColorMap", texture);
        }
        if (material.HasProperty("_UnlitColorMap"))
        {
            material.SetTexture("_UnlitColorMap", texture);
        }
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }
        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
        }
    }

    private static void AssignColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        if (material.HasProperty("_UnlitColor"))
        {
            material.SetColor("_UnlitColor", color);
        }
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private static void ConfigureTransparency(Material material)
    {
        material.SetOverrideTag("RenderType", "Transparent");

        if (material.HasProperty("_SurfaceType"))
        {
            material.SetFloat("_SurfaceType", 1f);
        }
        if (material.HasProperty("_BlendMode"))
        {
            material.SetFloat("_BlendMode", 0f);
        }
        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        }
        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        }
        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }
        if (material.HasProperty("_AlphaCutoffEnable"))
        {
            material.SetFloat("_AlphaCutoffEnable", 0f);
        }

        ConfigureCullOff(material);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
    }

    private static void ConfigureCullOff(Material material)
    {
        if (material.HasProperty("_CullMode"))
        {
            material.SetFloat("_CullMode", 0f);
        }
        if (material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", 0f);
        }
    }

    private static Shader FindCompatibleShader()
    {
        Shader shader = Shader.Find("HDRP/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");
        if (shader == null)
            shader = Shader.Find("Unlit/Texture");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Hidden/InternalErrorShader");
        return shader;
    }

    private static void DestroyUnityObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
