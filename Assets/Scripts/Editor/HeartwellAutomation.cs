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
            // 1. CLEAN SWEEP - Remove old clutter
            DestroyOldObjects();

            // 2. Setup Menu Manager
            GameObject manager = new GameObject("_MenuManager");
            manager.AddComponent<MainMenuController>();

            // 3. Setup UI Root (Canvas)
            GameObject canvasObj = new GameObject("HEARTWELL_UI_ROOT");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();

            // 4. ADD BACKGROUND - The secret to making it look like a game
            GameObject bgObj = new GameObject("Background_Image");
            bgObj.transform.SetParent(canvas.transform, false);
            var bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.1f, 0.15f); // Deep dark blue fallback
            
            // Try to find the Master Background sprite
            Sprite bgSprite = FindSpriteByName("background") ?? FindSpriteByName("master");
            if (bgSprite != null) bgImg.sprite = bgSprite;
            
            var bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            bgRect.localScale = new Vector3(1.1f, 1.1f, 1f); // OVERSCAN: Makes BG slightly larger to prevent clipping
            
            // Add subtle parallax to background
            bgObj.AddComponent<UIMouseParallax>().amount = 8f; // Reduced for subtle feel

            // 4. VIGNETTE OVERLAY
            GameObject vignette = new GameObject("UI_Vignette");
            vignette.transform.SetParent(canvas.transform, false);
            var vigImg = vignette.AddComponent<Image>();
            vigImg.color = new Color(0, 0, 0, 0.4f);
            var vigRect = vignette.GetComponent<RectTransform>();
            vigRect.anchorMin = Vector2.zero;
            vigRect.anchorMax = Vector2.one;
            vigRect.sizeDelta = Vector2.zero;

            // 5. BUBBLY LOGO SETUP (IMAGE BASED)
            GameObject logoObj = new GameObject("HEARTWELL_LOGO");
            logoObj.transform.SetParent(canvas.transform, false);
            var logoImg = logoObj.AddComponent<Image>();
            Sprite logoSprite = FindSpriteByName("logo");
            if (logoSprite != null) 
            {
                logoImg.sprite = logoSprite;
                logoImg.SetNativeSize();
            }

            var logoRect = logoObj.GetComponent<RectTransform>();
            logoRect.anchorMin = new Vector2(0.5f, 0.85f);
            logoRect.anchorMax = new Vector2(0.5f, 0.85f);
            logoRect.pivot = new Vector2(0.5f, 0.5f);
            logoRect.anchoredPosition = Vector2.zero;
            logoRect.localScale = new Vector3(0.6f, 0.6f, 1f);
            logoObj.AddComponent<FloatingLogo>();
            logoObj.AddComponent<UIMouseParallax>().amount = 40f; 

            // 6. BUTTONS SETUP
            GameObject buttonRoot = new GameObject("Button_Container");
            buttonRoot.transform.SetParent(canvas.transform, false);
            var vlg = buttonRoot.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 20;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childForceExpandHeight = false;
            
            var rootRect = buttonRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.85f, 0.4f); // Right Center
            rootRect.anchorMax = new Vector2(0.85f, 0.4f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero; 
            rootRect.sizeDelta = new Vector2(500, 800);
            
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = -20; // Overlap slightly for slime-drip look
            buttonRoot.AddComponent<UIMouseParallax>().amount = 15f;

            var ctrl = manager.GetComponent<MainMenuController>();
            CreatePolishedButton("Btn_Embark", "EMBARK", buttonRoot.transform, ctrl.Embark, FindSpriteByName("btn_green"));
            CreatePolishedButton("Btn_Options", "ADAPTATIONS", buttonRoot.transform, ctrl.OpenOptions, FindSpriteByName("btn_blue"));
            CreatePolishedButton("Btn_Exit", "EXIT", buttonRoot.transform, ctrl.QuitGame, FindSpriteByName("btn_red"));

            // 7. SLIME CHARACTER SETUP (With Masking)
            GameObject slimeContainer = new GameObject("Slime_Diorama");
            slimeContainer.transform.SetParent(canvas.transform, false);
            var slimeContRect = slimeContainer.AddComponent<RectTransform>();
            slimeContRect.anchorMin = new Vector2(0.2f, 0.2f); // Bottom Left
            slimeContRect.anchorMax = new Vector2(0.2f, 0.2f);
            slimeContRect.pivot = new Vector2(0.5f, 0.5f);
            slimeContRect.anchoredPosition = Vector2.zero;
            slimeContRect.sizeDelta = new Vector2(600, 600);
            
            // Add a Mask to hide the square background
            var maskImg = slimeContainer.AddComponent<Image>();
            slimeContainer.AddComponent<Mask>().showMaskGraphic = false;
            
            GameObject slimeObj = new GameObject("Slime_Sprite");
            slimeObj.transform.SetParent(slimeContainer.transform, false);
            var slimeImg = slimeObj.AddComponent<Image>();
            Sprite slimeSprite = FindSpriteByName("slime");
            if (slimeSprite != null) 
            {
                slimeImg.sprite = slimeSprite;
                slimeImg.SetNativeSize();
            }
            
            var slimeRect = slimeObj.GetComponent<RectTransform>();
            slimeRect.localScale = new Vector3(0.8f, 0.8f, 1f);
            
            slimeObj.AddComponent<MenuSlimeIdle>(); 
            slimeContainer.AddComponent<UIMouseParallax>().amount = 30f;
            
            // Add a soft glow behind him
            GameObject glowObj = new GameObject("Slime_Glow");
            glowObj.transform.SetParent(slimeContainer.transform, false);
            glowObj.transform.SetAsFirstSibling();
            var glowImg = glowObj.AddComponent<Image>();
            glowImg.color = new Color(0.4f, 0.8f, 1f, 0.3f);
            var glowRect = glowObj.GetComponent<RectTransform>();
            glowRect.sizeDelta = new Vector2(800, 800);
            
            // Add a ground shadow
            GameObject shadowObj = new GameObject("Slime_Shadow");
            shadowObj.transform.SetParent(slimeContainer.transform, false);
            shadowObj.transform.SetAsFirstSibling();
            var shadowImg = shadowObj.AddComponent<Image>();
            shadowImg.color = new Color(0, 0, 0, 0.5f);
            var shadowRect = shadowObj.GetComponent<RectTransform>();
            shadowRect.anchoredPosition = new Vector2(0, -150);
            shadowRect.sizeDelta = new Vector2(400, 100);
            shadowRect.localScale = new Vector3(1, 0.5f, 1);

            // 8. EventSystem
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                new GameObject("EventSystem").AddComponent<EventSystem>().gameObject.AddComponent<StandaloneInputModule>();
            }

            Debug.Log("HEARTWELL: Atmospheric Overhaul Complete!");
        }

        private static void DestroyOldObjects()
        {
            string[] names = { "HEARTWELL_UI_ROOT", "Canvas", "Main Menu Canvas", "_MenuManager", "Menu Slime", "EventSystem" };
            foreach (var name in names)
            {
                GameObject obj = GameObject.Find(name);
                if (obj != null) DestroyImmediate(obj);
            }
            foreach (var c in Object.FindObjectsOfType<Canvas>()) DestroyImmediate(c.gameObject);
        }

        private static Sprite FindSpriteByName(string namePart)
        {
            string fileName = "";
            if (namePart.Contains("background")) fileName = "heartwell_candy_forest_bg.png";
            else if (namePart.Contains("slime")) fileName = "heartwell_grounded_slime.png";
            else if (namePart.Contains("logo")) fileName = "heartwell_slime_logo.png";
            else if (namePart.Contains("green")) fileName = "heartwell_btn_green.png";
            else if (namePart.Contains("blue")) fileName = "heartwell_btn_blue.png";
            else if (namePart.Contains("red")) fileName = "heartwell_btn_red.png";
            else fileName = "jelly_buttons.png";

            string path = "Assets/" + fileName;
            
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void CreatePolishedButton(string name, string label, Transform parent, UnityEngine.Events.UnityAction action, Sprite sprite)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(550, 220); // MASSIVE Hero buttons

            Image img = btnObj.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Simple; 
            img.preserveAspect = true;

            Button btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(action);
            btnObj.AddComponent<SquishButton>();
        }
    }
}
