using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public struct TerrainPaintRockColliderBakeResult
{
    public int TerrainCount;
    public int PaintedInstanceCount;
    public int RockInstanceCount;
    public int MeshColliderCount;
    public int SkippedInstanceCount;
    public int FilteredInstanceCount;

    public void Add(TerrainPaintRockColliderBakeResult other)
    {
        TerrainCount += other.TerrainCount;
        PaintedInstanceCount += other.PaintedInstanceCount;
        RockInstanceCount += other.RockInstanceCount;
        MeshColliderCount += other.MeshColliderCount;
        SkippedInstanceCount += other.SkippedInstanceCount;
        FilteredInstanceCount += other.FilteredInstanceCount;
    }
}

public static class TerrainPaintRockColliderPopulator
{
    public const string ColliderRootName = "Terrain Paint Mesh Colliders";

    private static readonly string[] RockPrototypeKeywords =
    {
        "rock",
        "stone",
        "boulder",
        "cliff",
        "mountain"
    };

    private static readonly List<Terrain> TerrainBuffer = new List<Terrain>();
    private static readonly List<MeshSource> MeshSourceBuffer = new List<MeshSource>();
    private static readonly HashSet<Transform> MeshSourceTransforms = new HashSet<Transform>();

    private static bool sceneLoadedHooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapRuntimeColliders()
    {
        BakeLoadedScenes(false);

        if (sceneLoadedHooked)
        {
            return;
        }

        SceneManager.sceneLoaded += HandleSceneLoaded;
        sceneLoadedHooked = true;
    }

    public static TerrainPaintRockColliderBakeResult BakeLoadedScenes(bool rebuildExisting)
    {
        TerrainPaintRockColliderBakeResult total = new TerrainPaintRockColliderBakeResult();

        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            total.Add(BakeScene(SceneManager.GetSceneAt(sceneIndex), rebuildExisting));
        }

        return total;
    }

    public static TerrainPaintRockColliderBakeResult BakeScene(Scene scene, bool rebuildExisting)
    {
        TerrainPaintRockColliderBakeResult result = new TerrainPaintRockColliderBakeResult();

        if (!scene.IsValid() || !scene.isLoaded)
        {
            return result;
        }

        if (rebuildExisting)
        {
            ClearScene(scene);
        }
        else if (FindSceneColliderRoot(scene) != null)
        {
            return result;
        }

        CollectSceneTerrainsWithPaintedInstances(scene, TerrainBuffer);

        if (TerrainBuffer.Count == 0)
        {
            return result;
        }

        GameObject root = new GameObject(ColliderRootName);
        root.isStatic = true;
        SceneManager.MoveGameObjectToScene(root, scene);

        for (int terrainIndex = 0; terrainIndex < TerrainBuffer.Count; terrainIndex++)
        {
            BakeTerrain(TerrainBuffer[terrainIndex], root.transform, ref result);
        }

        TerrainBuffer.Clear();

        if (result.MeshColliderCount == 0)
        {
            DestroyObject(root);
        }

        return result;
    }

    public static void ClearScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = roots.Length - 1; i >= 0; i--)
        {
            if (roots[i].name == ColliderRootName)
            {
                DestroyObject(roots[i]);
            }
        }
    }

    public static GameObject FindSceneColliderRoot(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == ColliderRootName)
            {
                return roots[i];
            }
        }

        return null;
    }

    public static string FormatBakeResult(string action, TerrainPaintRockColliderBakeResult result)
    {
        return string.Format(
            "{0} terrain paint rock mesh colliders: {1} MeshCollider(s) for {2} rock instance(s) across {3} terrain(s). Ignored {4} non-rock instance(s), skipped {5} invalid instance(s).",
            action,
            result.MeshColliderCount,
            result.RockInstanceCount,
            result.TerrainCount,
            result.FilteredInstanceCount,
            result.SkippedInstanceCount);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BakeScene(scene, false);
    }

    private static void CollectSceneTerrainsWithPaintedInstances(Scene scene, List<Terrain> terrains)
    {
        terrains.Clear();

        Terrain[] allTerrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < allTerrains.Length; i++)
        {
            Terrain terrain = allTerrains[i];

            if (terrain == null || terrain.gameObject.scene != scene || terrain.terrainData == null)
            {
                continue;
            }

            TreeInstance[] treeInstances = terrain.terrainData.treeInstances;

            if (treeInstances != null && treeInstances.Length > 0)
            {
                terrains.Add(terrain);
            }
        }
    }

    private static void BakeTerrain(Terrain terrain, Transform root, ref TerrainPaintRockColliderBakeResult result)
    {
        TerrainData terrainData = terrain.terrainData;
        TreeInstance[] treeInstances = terrainData.treeInstances;
        TreePrototype[] prototypes = terrainData.treePrototypes;

        if (treeInstances == null || treeInstances.Length == 0)
        {
            return;
        }

        result.TerrainCount++;

        GameObject terrainRoot = new GameObject(terrain.name + " Rock Mesh Colliders");
        terrainRoot.layer = terrain.gameObject.layer;
        terrainRoot.isStatic = true;
        terrainRoot.transform.SetParent(root, false);

        for (int i = 0; i < treeInstances.Length; i++)
        {
            result.PaintedInstanceCount++;

            TreeInstance tree = treeInstances[i];

            if (tree.prototypeIndex < 0 || tree.prototypeIndex >= prototypes.Length)
            {
                result.SkippedInstanceCount++;
                continue;
            }

            GameObject prototype = prototypes[tree.prototypeIndex].prefab;

            if (prototype == null)
            {
                result.SkippedInstanceCount++;
                continue;
            }

            if (!ShouldBakePrototype(prototype))
            {
                result.FilteredInstanceCount++;
                continue;
            }

            result.RockInstanceCount++;
            CollectMeshSources(prototype, MeshSourceBuffer);

            if (MeshSourceBuffer.Count == 0)
            {
                result.SkippedInstanceCount++;
                continue;
            }

            GameObject instanceRoot = new GameObject(GetInstanceName(prototype, i));
            instanceRoot.layer = terrain.gameObject.layer;
            instanceRoot.isStatic = true;
            instanceRoot.transform.SetParent(terrainRoot.transform, false);
            ApplyPaintedInstanceTransform(instanceRoot.transform, terrain, terrainData, tree);

            Matrix4x4 prototypeWorldToLocal = prototype.transform.worldToLocalMatrix;

            for (int sourceIndex = 0; sourceIndex < MeshSourceBuffer.Count; sourceIndex++)
            {
                MeshSource source = MeshSourceBuffer[sourceIndex];

                GameObject colliderObject = new GameObject(source.Name + " MeshCollider");
                colliderObject.layer = terrain.gameObject.layer;
                colliderObject.isStatic = true;
                colliderObject.transform.SetParent(instanceRoot.transform, false);
                ApplyLocalMatrix(colliderObject.transform, prototypeWorldToLocal * source.Transform.localToWorldMatrix);

                MeshCollider meshCollider = colliderObject.AddComponent<MeshCollider>();
                meshCollider.convex = false;
                meshCollider.sharedMesh = source.Mesh;

                result.MeshColliderCount++;
            }

            MeshSourceBuffer.Clear();
        }

        if (terrainRoot.transform.childCount == 0)
        {
            DestroyObject(terrainRoot);
        }
    }

    private static bool ShouldBakePrototype(GameObject prototype)
    {
        string prototypeName = prototype.name.ToLowerInvariant();

        for (int i = 0; i < RockPrototypeKeywords.Length; i++)
        {
            if (prototypeName.Contains(RockPrototypeKeywords[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static void ApplyPaintedInstanceTransform(Transform target, Terrain terrain, TerrainData terrainData, TreeInstance tree)
    {
        Vector3 terrainLocalPosition = Vector3.Scale(tree.position, terrainData.size);
        Vector3 worldPosition = terrain.transform.position + terrainLocalPosition;
        Quaternion worldRotation = Quaternion.Euler(0f, tree.rotation * Mathf.Rad2Deg, 0f);
        Vector3 worldScale = new Vector3(tree.widthScale, tree.heightScale, tree.widthScale);

        target.SetPositionAndRotation(worldPosition, worldRotation);
        target.localScale = worldScale;
    }

    private static void ApplyLocalMatrix(Transform target, Matrix4x4 matrix)
    {
        Vector3 localPosition = GetColumn(matrix, 3);
        Vector3 right = GetColumn(matrix, 0);
        Vector3 up = GetColumn(matrix, 1);
        Vector3 forward = GetColumn(matrix, 2);
        Vector3 localScale = new Vector3(right.magnitude, up.magnitude, forward.magnitude);

        target.localPosition = localPosition;
        target.localScale = localScale;

        if (localScale.x <= Mathf.Epsilon || localScale.y <= Mathf.Epsilon || localScale.z <= Mathf.Epsilon)
        {
            target.localRotation = Quaternion.identity;
            return;
        }

        target.localRotation = Quaternion.LookRotation(forward / localScale.z, up / localScale.y);
    }

    private static Vector3 GetColumn(Matrix4x4 matrix, int column)
    {
        Vector4 value = matrix.GetColumn(column);
        return new Vector3(value.x, value.y, value.z);
    }

    private static void CollectMeshSources(GameObject prototype, List<MeshSource> meshSources)
    {
        meshSources.Clear();
        MeshSourceTransforms.Clear();

        LODGroup[] lodGroups = prototype.GetComponentsInChildren<LODGroup>(true);

        for (int i = 0; i < lodGroups.Length; i++)
        {
            LOD[] lods = lodGroups[i].GetLODs();

            if (lods.Length == 0)
            {
                continue;
            }

            Renderer[] renderers = lods[0].renderers;

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                AddRendererMeshSource(renderers[rendererIndex], meshSources);
            }
        }

        if (meshSources.Count > 0)
        {
            return;
        }

        MeshFilter[] meshFilters = prototype.GetComponentsInChildren<MeshFilter>(true);

        for (int i = 0; i < meshFilters.Length; i++)
        {
            AddMeshFilterSource(meshFilters[i], meshSources);
        }

        SkinnedMeshRenderer[] skinnedRenderers = prototype.GetComponentsInChildren<SkinnedMeshRenderer>(true);

        for (int i = 0; i < skinnedRenderers.Length; i++)
        {
            AddSkinnedMeshSource(skinnedRenderers[i], meshSources);
        }
    }

    private static void AddRendererMeshSource(Renderer renderer, List<MeshSource> meshSources)
    {
        if (renderer == null)
        {
            return;
        }

        MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();

        if (meshFilter != null)
        {
            AddMeshFilterSource(meshFilter, meshSources);
            return;
        }

        AddSkinnedMeshSource(renderer as SkinnedMeshRenderer, meshSources);
    }

    private static void AddMeshFilterSource(MeshFilter meshFilter, List<MeshSource> meshSources)
    {
        if (meshFilter == null || meshFilter.sharedMesh == null || !MeshSourceTransforms.Add(meshFilter.transform))
        {
            return;
        }

        meshSources.Add(new MeshSource(meshFilter.transform, meshFilter.sharedMesh, meshFilter.name));
    }

    private static void AddSkinnedMeshSource(SkinnedMeshRenderer skinnedRenderer, List<MeshSource> meshSources)
    {
        if (skinnedRenderer == null || skinnedRenderer.sharedMesh == null || !MeshSourceTransforms.Add(skinnedRenderer.transform))
        {
            return;
        }

        meshSources.Add(new MeshSource(skinnedRenderer.transform, skinnedRenderer.sharedMesh, skinnedRenderer.name));
    }

    private static string GetInstanceName(GameObject prototype, int index)
    {
        return string.Format("{0} Tree {1:0000}", prototype.name, index);
    }

    private static void DestroyObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(target);
        }
        else
        {
            Object.DestroyImmediate(target);
        }
    }

    private readonly struct MeshSource
    {
        public readonly Transform Transform;
        public readonly Mesh Mesh;
        public readonly string Name;

        public MeshSource(Transform transform, Mesh mesh, string name)
        {
            Transform = transform;
            Mesh = mesh;
            Name = name;
        }
    }
}
