using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Heartwell.UI;
using UnityEngine.EventSystems;

namespace Heartwell.Editor
{
    /// <summary>
    /// Automated setup tool to build the HEARTWELL Main Menu scene with one click.
    /// Access via: Heartwell > Setup Main Menu Scene
    /// </summary>
    public class HeartwellMenuSetup : EditorWindow
    {
        [MenuItem("Heartwell/Setup Main Menu Scene")]
        public static void SetupScene()
        {
            // 1. Setup Menu Manager
            GameObject manager = GameObject.Find("_MenuManager");
            if (manager == null)
            {
                manager = new GameObject("_MenuManager");
                manager.AddComponent<MainMenuController>();
            }
            MainMenuController controller = manager.GetComponent<MainMenuController>();

            // 2. Setup UI Canvas
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // 3. Setup EventSystem
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            // 4. Create Buttons
            CreateHeartwellButton("Embark Button", "EMBARK", new Vector2(0, 50), canvas.transform, controller.Embark);
            CreateHeartwellButton("Quit Button", "QUIT", new Vector2(0, -50), canvas.transform, controller.QuitGame);

            // 5. Setup Slime Diorama
            GameObject slime = GameObject.Find("Menu Slime");
            if (slime == null)
            {
                slime = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                slime.name = "Menu Slime";
                slime.transform.position = new Vector3(2, 1, 5); // Positioned in front of default HDRP camera
                slime.AddComponent<MenuSlimeIdle>();
                
                // Add a soft glow color
                var renderer = slime.GetComponent<Renderer>();
                renderer.sharedMaterial = new Material(Shader.Find("HDRP/Lit"));
                renderer.sharedMaterial.SetColor("_BaseColor", new Color(0.4f, 0.8f, 1f));
                renderer.sharedMaterial.SetColor("_EmissiveColor", new Color(0.1f, 0.3f, 0.5f));
                renderer.sharedMaterial.EnableKeyword("_EMISSIVE_COLOR");
            }

            Debug.Log("HEARTWELL: Main Menu Scene Setup Complete!");
        }

        private static void CreateHeartwellButton(string name, string label, Vector2 position, Transform parent, UnityEngine.Events.UnityAction action)
        {
            // Create Button Root
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200, 60);
            rect.anchoredPosition = position;

            // Add Image & Button
            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0.5f); // Semi-transparent white placeholder
            Button btn = btnObj.AddComponent<Button>();
            btn.transition = Button.Transition.None;
            btn.onClick.AddListener(action);

            // Add Squish Script
            btnObj.AddComponent<SquishButton>();

            // Add Text
            GameObject textObj = new GameObject("Label");
            textObj.transform.SetParent(btnObj.transform, false);
            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 24;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.black;
            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
        }
    }
}
