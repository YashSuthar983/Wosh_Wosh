using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Heartwell.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace Heartwell.Editor
{
    public class HeartwellAutomation : EditorWindow
    {
        [MenuItem("Heartwell/1. Finalize Entire Project")]
        public static void FinalizeProject()
        {
            // 1. Setup Opening Cutscene
            // This creates OpeningCutscene.unity, sets it up, and saves it.
            OpeningCutsceneSetup.Setup();

            // 2. Setup Main Menu
            // We need to open or create MainMenu.unity before running AtmosphericOverhaul
            string mainMenuPath = "Assets/Scenes/MainMenu.unity";
            Scene mainMenuScene;
            if (System.IO.File.Exists(mainMenuPath))
            {
                mainMenuScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(mainMenuPath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            }
            else
            {
                mainMenuScene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects, UnityEditor.SceneManagement.NewSceneMode.Single);
            }

            AtmosphericOverhaul();
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(mainMenuScene, mainMenuPath);

            // 3. Fix Build Settings
            // 0: MainMenu, 1: OpeningCutscene, 2: OutdoorsScene
            string[] requiredScenes = { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/OpeningCutscene.unity", "Assets/OutdoorsScene.unity" };
            List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>();
            foreach (string scenePath in requiredScenes)
            {
                if (System.IO.File.Exists(scenePath))
                {
                    buildScenes.Add(new EditorBuildSettingsScene(scenePath, true));
                }
            }
            EditorBuildSettings.scenes = buildScenes.ToArray();

            Debug.Log("=========================================");
            Debug.Log("PROJECT FINALIZATION COMPLETE!");
            Debug.Log("1. Opening Cutscene Created");
            Debug.Log("2. Main Menu Overhauled");
            Debug.Log("3. Build Settings Ordered");
            Debug.Log("You are now in the Main Menu scene. Press Play to test!");
            Debug.Log("=========================================");
        }

        [MenuItem("Heartwell/2. Atmospheric Overhaul (Fix Layout & Background)")]
        public static void AtmosphericOverhaul()
        {
            // PRE-FLIGHT CHECK: Load assets
            Sprite bg = FindSpriteByName("background");
            Sprite logo = FindSpriteByName("logo");
            Sprite playBtn = FindSpriteByName("start");
            Sprite optBtn = FindSpriteByName("options");
            Sprite exitBtn = FindSpriteByName("exit");

            // 1. CLEAN SWEEP - Remove old clutter
            DestroyOldObjects();
            // 2. Setup Menu Manager
            GameObject manager = new GameObject("_MenuManager");
            manager.AddComponent<MainMenuController>();

            // 3. Setup UI Root
            GameObject uiRoot = new GameObject("HEARTWELL_UI_ROOT");
            Canvas canvas = uiRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            
            var scaler = uiRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            uiRoot.AddComponent<GraphicRaycaster>();

            // 4. Create Background (Clean Forest)
            SetupBackground(uiRoot.transform, bg);



            // 7. Setup Button Container
            GameObject buttonRoot = new GameObject("Button_Container");
            buttonRoot.transform.SetParent(uiRoot.transform, false);
            var vlg = buttonRoot.AddComponent<VerticalLayoutGroup>();
            vlg.childControlHeight = false;
            vlg.childControlWidth = false;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            
            var rootRect = buttonRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.2f); 
            rootRect.anchorMax = new Vector2(0.5f, 0.2f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero; 
            rootRect.sizeDelta = new Vector2(600, 800);
            
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 20; 

            var ctrl = manager.GetComponent<MainMenuController>();
            CreatePolishedButton("Btn_Embark", buttonRoot.transform, ctrl.Embark, playBtn);
            CreatePolishedButton("Btn_Options", buttonRoot.transform, ctrl.OpenOptions, optBtn);
            CreatePolishedButton("Btn_Exit", buttonRoot.transform, ctrl.QuitGame, exitBtn);

            // 9. Setup Cursor
            SlimeCursorSetup.SetupCursor();

            // 10. EventSystem
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                new GameObject("EventSystem").AddComponent<EventSystem>().gameObject.AddComponent<StandaloneInputModule>();
            }

            Debug.Log("HEARTWELL: Atmospheric Overhaul Complete!");
        }

        private static void SetupBackground(Transform parent, Sprite bg)
        {
            GameObject bgObj = new GameObject("Main_Background");
            bgObj.transform.SetParent(parent, false);
            bgObj.transform.SetAsFirstSibling();
            
            var bgImg = bgObj.AddComponent<Image>();
            if (bg != null) 
            {
                bgImg.sprite = bg;
                bgImg.color = Color.white;
            }
            else
            {
                bgImg.color = Color.magenta;
            }
            
            bgImg.raycastTarget = false;
            
            var bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.anchoredPosition = Vector2.zero;
            bgRect.sizeDelta = Vector2.zero;
            
            bgRect.localScale = Vector3.one; // No scaling!
        }

        private static void DestroyOldObjects()
        {
            // REMOVED 'Canvas' and 'EventSystem' so we don't accidentally delete the user's custom UI!
            string[] names = { "HEARTWELL_UI_ROOT", "_MenuManager", "Menu Slime", "SLIME_CURSOR_ROOT" };
            
            GameObject[] allObjects = Object.FindObjectsOfType<GameObject>(true);
            foreach (var obj in allObjects)
            {
                if (obj == null) continue;
                foreach (var n in names)
                {
                    if (obj.name == n)
                    {
                        DestroyImmediate(obj);
                        break;
                    }
                }
            }
        }

        private static Sprite FindSpriteByName(string namePart)
        {
            AssetDatabase.Refresh();

            string fileName = "";
            string lower = namePart.ToLower();
            if (lower.Contains("background")) fileName = "New_Background.png";
            else if (lower.Contains("logo")) fileName = "heartwell_logo.png";
            else if (lower.Contains("slime")) fileName = "heartwell_slime.png";
            else if (lower.Contains("start") || lower.Contains("play")) fileName = "start.png";
            else if (lower.Contains("options") || lower.Contains("settings")) fileName = "options.png";
            else if (lower.Contains("exit") || lower.Contains("quit")) fileName = "exit.png";
            else return null;

            string path = "Assets/Art/UI/" + fileName;
            
            // Force Import
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                bool needsReimport = false;
                if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; needsReimport = true; }
                if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; needsReimport = true; }
                if (importer.alphaIsTransparency == false) { importer.alphaIsTransparency = true; needsReimport = true; }
                
                if (needsReimport)
                {
                    importer.SaveAndReimport();
                }
            }

            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var asset in allAssets)
            {
                if (asset is Sprite s) return s;
            }

            // FALLBACK: If Unity's importer is completely broken, just read the raw texture and generate a Sprite manually!
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null)
            {
                Debug.Log($"HEARTWELL: Importer failed, but recovered texture {fileName}! Generating Sprite manually.");
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            
            Debug.LogError($"HEARTWELL: Failed to load Sprite at {path} entirely!");
            return null;
        }

        private static void CreatePolishedButton(string name, Transform parent, UnityEngine.Events.UnityAction action, Sprite sprite)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(450, 140); 

            Image img = btnObj.AddComponent<Image>();
            if (sprite != null) img.sprite = sprite;
            img.type = Image.Type.Simple; 
            img.preserveAspect = true;

            Button btn = btnObj.AddComponent<Button>();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, action);
            btnObj.AddComponent<SquishButton>();
        }
    }
}
