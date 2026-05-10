using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TerrainPaintRockColliderBakerEditor
{
    private const string BakeMenuPath = "Tools/Wosh Wosh/Terrain Paint Colliders/Bake Scene Rock Mesh Colliders";
    private const string ClearMenuPath = "Tools/Wosh Wosh/Terrain Paint Colliders/Clear Scene Rock Mesh Colliders";

    [MenuItem(BakeMenuPath)]
    private static void BakeSceneRockMeshColliders()
    {
        Scene scene = SceneManager.GetActiveScene();
        TerrainPaintRockColliderBakeResult result = TerrainPaintRockColliderPopulator.BakeScene(scene, true);
        GameObject root = TerrainPaintRockColliderPopulator.FindSceneColliderRoot(scene);

        if (root != null)
        {
            Undo.RegisterCreatedObjectUndo(root, "Bake Terrain Paint Rock Mesh Colliders");
            Selection.activeGameObject = root;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log(TerrainPaintRockColliderPopulator.FormatBakeResult("Baked", result));
    }

    [MenuItem(ClearMenuPath)]
    private static void ClearSceneRockMeshColliders()
    {
        Scene scene = SceneManager.GetActiveScene();
        TerrainPaintRockColliderPopulator.ClearScene(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Cleared terrain paint rock mesh colliders from the active scene.");
    }
}
