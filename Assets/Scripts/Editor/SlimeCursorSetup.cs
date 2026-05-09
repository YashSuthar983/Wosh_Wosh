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
            // Removed GraphicRaycaster so the cursor layer never blocks clicks!

            // 3. Create Cursor
            GameObject cursorObj = new GameObject("Slime_Cursor");
            cursorObj.layer = 5;
            cursorObj.transform.SetParent(root.transform, false);
            
            RectTransform rect = cursorObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(32, 32);
            cursorObj.transform.localScale = Vector3.one;

            Image img = cursorObj.AddComponent<Image>();
            img.raycastTarget = false; 

            // 4. Asset Import & Slicing
            string spritePath = "Assets/Art/UI/cursor_transparent.png";
            
            // Force import first
            AssetDatabase.ImportAsset(spritePath, ImportAssetOptions.ForceUpdate);
            
            TextureImporter importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.filterMode = FilterMode.Bilinear; // AI generated art is usually high res, not pixel art
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.isReadable = true; // Required for automatic slicing
                
                importer.SaveAndReimport();
                
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(spritePath);
                if (tex != null)
                {
                    Rect[] rects = UnityEditorInternal.InternalSpriteUtility.GenerateAutomaticSpriteRectangles(tex, 10, 0);
                    var metaData = new System.Collections.Generic.List<SpriteMetaData>();
                    
                    for (int i = 0; i < rects.Length; i++)
                    {
                        var meta = new SpriteMetaData();
                        meta.rect = rects[i];
                        meta.name = "cursor_" + i;
                        meta.alignment = 9; // Custom pivot
                        meta.pivot = new Vector2(0.5f, 0.5f);
                        metaData.Add(meta);
                    }
                    
                    importer.spritesheet = metaData.ToArray();
                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport();
                }
            }

            // Force Unity to acknowledge the newly sliced sprites before we try to load them!
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 5. Add Script & Assign Animation
            SlimeCursor cursor = cursorObj.AddComponent<SlimeCursor>();
            
            // Assign frames dynamically from imported asset
            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(spritePath);
            var allFrames = new System.Collections.Generic.List<Sprite>();

            foreach (var asset in allAssets)
            {
                if (asset is Sprite s)
                {
                    allFrames.Add(s);
                }
            }
            
            if (allFrames.Count > 0)
            {
                var framesArray = allFrames.ToArray();
                var field = typeof(SlimeCursor).GetField("animationFrames", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null) field.SetValue(cursor, framesArray);
            }
            
            EditorUtility.SetDirty(cursorObj);
            
            Debug.Log("HEARTWELL: Slime Cursor Setup Complete!");
        }
    }
}
