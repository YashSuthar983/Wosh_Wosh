using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Heartwell.UI;

namespace Heartwell.Editor
{
    public class SlimeCursorSetup : EditorWindow
    {
        public static void SetupCursor()
        {
            Debug.Log("HEARTWELL: Starting Slime Cursor Setup...");

            // 1. Cleanup
            GameObject old = GameObject.Find("SLIME_CURSOR_ROOT");
            if (old != null) DestroyImmediate(old);

            // 2. Create Root
            GameObject root = new GameObject("SLIME_CURSOR_ROOT");
            root.layer = 5;
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999; // EXTREME TOP
            
            root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            root.AddComponent<GraphicRaycaster>();

            // 3. Create Cursor
            GameObject cursorObj = new GameObject("Slime_Cursor");
            cursorObj.layer = 5;
            cursorObj.transform.SetParent(root.transform, false);
            
            RectTransform rect = cursorObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(64, 64);
            cursorObj.transform.localScale = Vector3.one;

            Image img = cursorObj.AddComponent<Image>();
            img.raycastTarget = false; 

            // 4. Asset Import & Slicing
            string spritePath = "Assets/Art/momo_slime.png";
            TextureImporter importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.filterMode = FilterMode.Point; // Pixel art style
                importer.textureCompression = TextureImporterCompression.Uncompressed;

                // Slice into 64x64 grid (5x3)
                var metaData = new System.Collections.Generic.List<SpriteMetaData>();
                for (int y = 0; y < 3; y++)
                {
                    for (int x = 0; x < 5; x++)
                    {
                        var meta = new SpriteMetaData();
                        meta.rect = new Rect(x * 64, (2 - y) * 64, 64, 64); // Unity Y is bottom-up
                        meta.name = $"momo_{y}_{x}";
                        meta.alignment = (int)SpriteAlignment.Center;
                        metaData.Add(meta);
                    }
                }
                importer.spritesheet = metaData.ToArray();
                importer.SaveAndReimport();
            }

            // 5. Add Script & Assign Animation
            var cursorScript = cursorObj.AddComponent<SlimeCursor>();
            
            // Force save and refresh to ensure slices are created
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 6. Assign Animation Frames
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(spritePath);
            Debug.Log($"HEARTWELL: Found {assets.Length} assets at {spritePath}");
            var frames = new System.Collections.Generic.List<Sprite>();
            foreach (var asset in assets)
            {
                if (asset is Sprite s) frames.Add(s);
            }

            if (frames.Count > 0)
            {
                img.sprite = frames[0];
                img.color = Color.white;
                
                var allFrames = frames.ToArray();
                var field = typeof(SlimeCursor).GetField("animationFrames", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null) field.SetValue(cursorScript, allFrames);
            }
            else
            {
                Debug.LogError("HEARTWELL: NO SPRITE SLICES FOUND! Check momo_slime.png import settings.");
            }

            Debug.Log("HEARTWELL: Slime Cursor Setup Complete with GOO TRAIL!");
        }
    }
}
