using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Heartwell.UI;
using UnityEngine.EventSystems;

namespace Heartwell.Editor
{
    public class HeartwellAutomation : EditorWindow
    {
        [MenuItem("Heartwell/Atmospheric Overhaul (Fix Layout & Background)")]
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
            rootRect.anchorMin = new Vector2(0.82f, 0.38f); 
            rootRect.anchorMax = new Vector2(0.82f, 0.38f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero; 
            rootRect.sizeDelta = new Vector2(600, 800);
            
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 20; 
            buttonRoot.AddComponent<UIMouseParallax>().amount = 15f;

            var ctrl = manager.GetComponent<MainMenuController>();
            CreatePolishedButton("Btn_Embark", buttonRoot.transform, ctrl.Embark, playBtn);
            CreatePolishedButton("Btn_Options", buttonRoot.transform, ctrl.OpenOptions, optBtn);
            CreatePolishedButton("Btn_Exit", buttonRoot.transform, ctrl.QuitGame, exitBtn);

            // 8. SLIME CHARACTER SETUP
            GameObject slimeContainer = new GameObject("Slime_Diorama");
            slimeContainer.transform.SetParent(uiRoot.transform, false);
            var slimeContRect = slimeContainer.AddComponent<RectTransform>();
            slimeContRect.anchorMin = new Vector2(0.2f, 0.2f); // Bottom Left
            slimeContRect.anchorMax = new Vector2(0.2f, 0.2f);
            slimeContRect.pivot = new Vector2(0.5f, 0.5f);
            slimeContRect.anchoredPosition = Vector2.zero;
            slimeContRect.sizeDelta = new Vector2(600, 600);
            
            GameObject slimeObj = new GameObject("Slime_Sprite");
            slimeObj.transform.SetParent(slimeContainer.transform, false);
            var slimeImg = slimeObj.AddComponent<Image>();
            Sprite slimeSprite = FindSpriteByName("slime");
            if (slimeSprite != null) 
            {
                slimeImg.sprite = slimeSprite;
                slimeImg.SetNativeSize();
            }
            else
            {
                // Make it completely invisible if missing, rather than a white square
                slimeImg.color = Color.clear;
            }
            slimeImg.raycastTarget = false;
            
            var slimeRect = slimeObj.GetComponent<RectTransform>();
            slimeRect.localScale = new Vector3(0.8f, 0.8f, 1f);
            
            slimeObj.AddComponent<MenuSlimeIdle>(); 
            slimeContainer.AddComponent<UIMouseParallax>().amount = 30f;

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
            if (bg != null) bgImg.sprite = bg;
            bgImg.color = Color.white;
            bgImg.raycastTarget = false;
            
            var bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.anchoredPosition = Vector2.zero;
            bgRect.sizeDelta = new Vector2(100, 100); // Overhang by 50px on all sides to prevent parallax clipping
            
            bgObj.AddComponent<UIMouseParallax>().amount = 20f;
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
            string fileName = "";
            string lower = namePart.ToLower();
            if (lower.Contains("background")) fileName = "background.png";
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
                if (importer.alphaIsTransparency == false) { importer.alphaIsTransparency = true; needsReimport = true; }
                
                if (needsReimport)
                {
                    importer.SaveAndReimport();
                }
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
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
            btn.onClick.AddListener(action);
            btnObj.AddComponent<SquishButton>();
        }
    }
}
