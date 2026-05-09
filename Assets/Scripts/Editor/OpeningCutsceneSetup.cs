using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.IO;
using Heartwell.UI;

namespace Heartwell.Editor
{
    public class OpeningCutsceneSetup : EditorWindow
    {
        private const string ScenePath = "Assets/Scenes/OpeningCutscene.unity";
        private const string NextSceneDefault = "MainMenu";
        private const string ArtPath = "Assets/Art/Opening";

        [MenuItem("Heartwell/Setup Opening Cutscene")]
        public static void Setup()
        {
            // 1. Ensure textures are imported as Sprites
            FixSpriteImportSettings();
            AssetDatabase.Refresh();

            // 2. Create/Open the scene
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            
            // 3. Setup UI
            GameObject canvasObj = new GameObject("CutsceneCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.AddComponent<GraphicRaycaster>();

            GameObject bgObj = new GameObject("BlackBackground", typeof(RectTransform));
            bgObj.transform.SetParent(canvasObj.transform, false);
            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = Color.black;
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.anchoredPosition = Vector2.zero;
            bgRect.sizeDelta = Vector2.zero;

            GameObject displayObj = new GameObject("SlideDisplay", typeof(RectTransform));
            displayObj.transform.SetParent(canvasObj.transform, false);
            Image displayImage = displayObj.AddComponent<Image>();
            // We use AspectRatioFitter instead of preserveAspect to force "Fit to Width"
            var fitter = displayObj.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
            
            RectTransform displayRect = displayObj.GetComponent<RectTransform>();
            displayRect.anchorMin = new Vector2(0, 0.5f);
            displayRect.anchorMax = new Vector2(1, 0.5f);
            displayRect.anchoredPosition = Vector2.zero;
            displayRect.sizeDelta = Vector2.zero;

            CanvasGroup canvasGroup = displayObj.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f; // Set to 1 so it's visible in Editor. The Controller script fades from 0 on Play.

            // 4. Add Controller
            GameObject controllerObj = new GameObject("CutsceneController");
            OpeningCutsceneController controller = controllerObj.AddComponent<OpeningCutsceneController>();
            
            // Use Reflection to assign private fields if necessary, but here they are serialized
            // We can just assign them directly since they are public/serialized
            
            // Find slides manually to be more robust
            Debug.Log("Searching for slides in: " + ArtPath);
            List<Sprite> slides = new List<Sprite>();
            for (int i = 1; i <= 7; i++)
            {
                string path = $"{ArtPath}/slide_{i}.png";
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null)
                {
                    slides.Add(sprite);
                    Debug.Log($"Loaded slide {i}: {path}");
                }
                else
                {
                    Debug.LogWarning($"Could not load slide {i} at {path}. Make sure it is imported as a Sprite.");
                }
            }

            // Assign via SerializedObject to handle private serialized fields
            SerializedObject so = new SerializedObject(controller);
            SerializedProperty slidesProp = so.FindProperty("slides");
            SerializedProperty displayProp = so.FindProperty("displayImage");
            SerializedProperty cgProp = so.FindProperty("canvasGroup");
            SerializedProperty nextSceneProp = so.FindProperty("targetNextScene");

            if (slidesProp != null)
            {
                slidesProp.ClearArray();
                for (int i = 0; i < slides.Count; i++)
                {
                    slidesProp.InsertArrayElementAtIndex(i);
                    slidesProp.GetArrayElementAtIndex(i).objectReferenceValue = slides[i];
                }
            }
            else
            {
                Debug.LogError("Could not find 'slides' property on OpeningCutsceneController!");
            }

            if (displayProp != null) displayProp.objectReferenceValue = displayImage;
            if (cgProp != null) cgProp.objectReferenceValue = canvasGroup;
            if (nextSceneProp != null) nextSceneProp.stringValue = NextSceneDefault;
            
            SerializedProperty durationProp = so.FindProperty("displayDuration");
            if (durationProp != null) durationProp.floatValue = 2.0f;
            
            SerializedProperty fadeProp = so.FindProperty("fadeDuration");
            if (fadeProp != null) fadeProp.floatValue = 0.5f;
            
            // Assign first slide to Image so it's visible in Editor
            if (slides.Count > 0)
            {
                displayImage.sprite = slides[0];
                var aspectFitter = displayImage.GetComponent<AspectRatioFitter>();
                if (aspectFitter != null)
                {
                    aspectFitter.aspectRatio = slides[0].rect.width / slides[0].rect.height;
                }
            }

            so.ApplyModifiedProperties();

            // 5. Save Scene
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"Opening Cutscene setup complete! Scene saved to {ScenePath}");
            
            // Add to Build Settings if not already there
            AddSceneToBuildSettings(ScenePath);
        }

        private static void FixSpriteImportSettings()
        {
            if (!Directory.Exists(ArtPath)) return;

            string[] files = Directory.GetFiles(ArtPath, "*.png");
            foreach (string file in files)
            {
                TextureImporter importer = AssetImporter.GetAtPath(file) as TextureImporter;
                if (importer != null)
                {
                    bool needsReimport = false;
                    
                    if (importer.textureType != TextureImporterType.Sprite)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        needsReimport = true;
                    }
                    
                    if (importer.spriteImportMode != SpriteImportMode.Single)
                    {
                        importer.spriteImportMode = SpriteImportMode.Single;
                        needsReimport = true;
                    }

                    if (needsReimport)
                    {
                        importer.SaveAndReimport();
                    }
                }
            }
        }

        private static void AddSceneToBuildSettings(string path)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool alreadyExists = false;
            foreach (var s in scenes)
            {
                if (s.path == path)
                {
                    alreadyExists = true;
                    break;
                }
            }

            if (!alreadyExists)
            {
                scenes.Insert(0, new EditorBuildSettingsScene(path, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
