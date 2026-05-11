using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Heartwell.UI
{
    [DisallowMultipleComponent]
    public sealed class InGameOverlayUI : MonoBehaviour
    {
        private const string AssetRoot = "UI/InGameGenerated/";
        private const string MainMenuFontPath = "Fonts/NotoSerifDisplay-Regular";
        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        private static InGameOverlayUI instance;

        private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
        private readonly Queue<PopupRequest> popupQueue = new Queue<PopupRequest>();
        private readonly HashSet<string> shownOneShotKeys = new HashSet<string>();
        private readonly HashSet<string> shownSceneTitles = new HashSet<string>();

        private RectTransform root;
        private Font serifFont;

        private RectTransform popupRect;
        private CanvasGroup popupGroup;
        private Image popupImage;
        private Text popupText;
        private RectTransform popupTextRect;
        private Coroutine popupRoutine;

        private CanvasGroup hazardGroup;
        private Image hazardImage;
        private Coroutine hazardRoutine;

        private RectTransform respawnRect;
        private CanvasGroup respawnGroup;
        private Image respawnImage;
        private Text respawnText;
        private Coroutine respawnRoutine;

        private RectTransform sceneTitleRect;
        private CanvasGroup sceneTitleGroup;
        private Image sceneTitleImage;
        private Coroutine sceneTitleRoutine;

        private RectTransform optionsRect;
        private CanvasGroup optionsGroup;
        private Image optionsImage;
        private Text optionsText;
        private Coroutine optionsRoutine;
        private float optionsDismissTime;

        private RectTransform saveRect;
        private CanvasGroup saveGroup;
        private Image saveImage;
        private Text saveText;
        private Coroutine saveRoutine;

        private float nextBarrierHintTime;
        private float nextContextHintTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
        }

        public static void ShowAbsorbPopup(global::SlimeAbsorbable absorbable)
        {
            if (absorbable == null)
                return;

            InGameOverlayUI overlay = EnsureInstance();
            if (absorbable.GrantedAbility == global::SlimeAbsorbAbility.WaveAttack)
            {
                overlay.EnqueueOnce("ability_wave", "02_wave_unlock", "A pulse stirs.", new Vector2(700f, 467f), new Vector2(0f, 140f), new Vector2(76f, -132f), new Vector2(510f, 66f));
                return;
            }

            if (absorbable.MaterialType == global::SlimeMaterialType.Sticky)
            {
                overlay.EnqueueOnce("material_sticky", "01_sticky_unlock", "The walls answer.", new Vector2(700f, 467f), new Vector2(0f, 140f), new Vector2(76f, -132f), new Vector2(510f, 66f));
                return;
            }

            if (absorbable.MaterialType != global::SlimeMaterialType.Neutral)
            {
                overlay.EnqueueOnce("material_" + absorbable.MaterialType, "07_pickup_discovery", MaterialWhisper(absorbable.MaterialType), new Vector2(660f, 440f), new Vector2(0f, 136f), new Vector2(110f, -132f), new Vector2(470f, 58f));
                return;
            }

            overlay.EnqueueOnce("pickup_first", "07_pickup_discovery", "A quiet glow lingers.", new Vector2(660f, 440f), new Vector2(0f, 136f), new Vector2(110f, -132f), new Vector2(470f, 58f));
        }

        public static void ShowCheckpoint()
        {
            InGameOverlayUI overlay = EnsureInstance();
            overlay.EnqueuePopup("06_checkpoint", "A memory settles.", new Vector2(720f, 360f), new Vector2(0f, 160f), new Vector2(148f, -18f), new Vector2(480f, 76f), 2.1f);
            overlay.ShowSaveBadge();
        }

        public static void ShowEnemyEncounter()
        {
            EnsureInstance().EnqueuePopup("10_enemy_banner", "The grove tightens.", new Vector2(760f, 380f), new Vector2(0f, 124f), new Vector2(104f, -4f), new Vector2(520f, 70f), 2.2f);
        }

        public static void ShowBarrierHint()
        {
            InGameOverlayUI overlay = EnsureInstance();
            if (Time.unscaledTime < overlay.nextBarrierHintTime)
                return;

            overlay.nextBarrierHintTime = Time.unscaledTime + 14f;
            overlay.EnqueueOnce("barrier_" + SceneManager.GetActiveScene().name, "11_barrier_prompt", "Roots listen.", new Vector2(640f, 352f), new Vector2(0f, 122f), new Vector2(82f, -98f), new Vector2(420f, 56f));
        }

        public static void ShowContextHint()
        {
            InGameOverlayUI overlay = EnsureInstance();
            if (Time.unscaledTime < overlay.nextContextHintTime)
                return;

            overlay.nextContextHintTime = Time.unscaledTime + 20f;
            overlay.EnqueuePopup("04_context_prompt", "The echo is quiet.", new Vector2(560f, 373f), new Vector2(0f, 118f), new Vector2(0f, -24f), new Vector2(360f, 54f), 1.8f);
        }

        public static void ShowHazard(float health01)
        {
            EnsureInstance().FlashHazard(health01);
        }

        public static void ShowRespawn()
        {
            EnsureInstance().ShowRespawnCard();
        }

        public static void ShowOptionsShell()
        {
            EnsureInstance().ShowOptionsPanel();
        }

        public static bool IsOptionsShellVisible => instance != null
            && instance.optionsGroup != null
            && instance.optionsGroup.alpha > 0.01f;

        public static void DismissOptionsShell()
        {
            if (instance != null)
                instance.HideOptionsPanel();
        }

        public static void ShowSceneTransition()
        {
            EnsureInstance().EnqueuePopup("14_scene_transition", string.Empty, new Vector2(740f, 416f), new Vector2(0f, 332f), Vector2.zero, Vector2.zero, 1.4f);
        }

        private static InGameOverlayUI EnsureInstance()
        {
            if (instance != null)
                return instance;

            InGameOverlayUI existing = FindFirstObjectByType<InGameOverlayUI>();
            if (existing != null)
            {
                instance = existing;
                return instance;
            }

            GameObject overlayObject = new GameObject("Heartwell In-Game Overlay UI");
            instance = overlayObject.AddComponent<InGameOverlayUI>();
            DontDestroyOnLoad(overlayObject);
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            BuildUI();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void Start()
        {
            ShowSceneTitleFor(SceneManager.GetActiveScene().name, 0.85f);
        }

        private void Update()
        {
            if (optionsGroup != null && optionsGroup.alpha > 0.01f && Time.unscaledTime >= optionsDismissTime && Input.GetKeyDown(KeyCode.Escape))
                HideOptionsPanel();
        }

        private void BuildUI()
        {
            serifFont = Resources.Load<Font>(MainMenuFontPath);
            if (serifFont == null)
                serifFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject canvasObject = new GameObject("Overlay Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 9000;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            root = canvasObject.GetComponent<RectTransform>();

            BuildHazardOverlay();
            BuildSaveBadge();
            BuildPopupLayer();
            BuildSceneTitle();
            BuildRespawnCard();
            BuildOptionsPanel();
        }

        private void BuildHazardOverlay()
        {
            GameObject hazardObject = CreateChild("Hazard Overlay", root);
            hazardGroup = hazardObject.AddComponent<CanvasGroup>();
            hazardGroup.alpha = 0f;
            hazardGroup.interactable = false;
            hazardGroup.blocksRaycasts = false;

            hazardImage = hazardObject.AddComponent<Image>();
            hazardImage.sprite = LoadSprite("08_hazard_overlay");
            hazardImage.color = Color.white;
            hazardImage.raycastTarget = false;
            hazardImage.preserveAspect = false;

            RectTransform rect = hazardObject.GetComponent<RectTransform>();
            Stretch(rect);
        }

        private void BuildSaveBadge()
        {
            GameObject saveObject = CreateChild("Save Badge", root);
            saveGroup = saveObject.AddComponent<CanvasGroup>();
            saveGroup.alpha = 0f;
            saveGroup.interactable = false;
            saveGroup.blocksRaycasts = false;

            saveRect = saveObject.GetComponent<RectTransform>();
            saveRect.anchorMin = new Vector2(1f, 0f);
            saveRect.anchorMax = new Vector2(1f, 0f);
            saveRect.pivot = new Vector2(1f, 0f);
            saveRect.anchoredPosition = new Vector2(-28f, 28f);
            saveRect.sizeDelta = new Vector2(390f, 220f);

            saveImage = saveObject.AddComponent<Image>();
            saveImage.sprite = LoadSprite("15_save_indicator");
            saveImage.color = Color.white;
            saveImage.raycastTarget = false;
            saveImage.preserveAspect = true;

            saveText = CreateText("Text", saveRect, 24, TextAnchor.MiddleLeft);
            saveText.resizeTextMinSize = 10;
            saveText.resizeTextMaxSize = 20;
            saveText.text = "Remembered";
            RectTransform textRect = saveText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(136f, 86f);
            textRect.offsetMax = new Vector2(-48f, -84f);
        }

        private void BuildPopupLayer()
        {
            GameObject popupObject = CreateChild("Popup", root);
            popupGroup = popupObject.AddComponent<CanvasGroup>();
            popupGroup.alpha = 0f;
            popupGroup.interactable = false;
            popupGroup.blocksRaycasts = false;

            popupRect = popupObject.GetComponent<RectTransform>();
            popupRect.anchorMin = new Vector2(0.5f, 0f);
            popupRect.anchorMax = new Vector2(0.5f, 0f);
            popupRect.pivot = new Vector2(0.5f, 0f);
            popupRect.anchoredPosition = new Vector2(0f, 130f);
            popupRect.sizeDelta = new Vector2(680f, 454f);

            popupImage = popupObject.AddComponent<Image>();
            popupImage.color = Color.white;
            popupImage.raycastTarget = false;
            popupImage.preserveAspect = true;

            popupText = CreateText("Text", popupRect, 30, TextAnchor.MiddleCenter);
            popupTextRect = popupText.rectTransform;
        }

        private void BuildSceneTitle()
        {
            GameObject titleObject = CreateChild("Scene Title", root);
            sceneTitleGroup = titleObject.AddComponent<CanvasGroup>();
            sceneTitleGroup.alpha = 0f;
            sceneTitleGroup.interactable = false;
            sceneTitleGroup.blocksRaycasts = false;

            sceneTitleRect = titleObject.GetComponent<RectTransform>();
            sceneTitleRect.anchorMin = new Vector2(0.5f, 1f);
            sceneTitleRect.anchorMax = new Vector2(0.5f, 1f);
            sceneTitleRect.pivot = new Vector2(0.5f, 1f);
            sceneTitleRect.anchoredPosition = new Vector2(0f, -48f);
            sceneTitleRect.sizeDelta = new Vector2(1180f, 270f);

            sceneTitleImage = titleObject.AddComponent<Image>();
            sceneTitleImage.color = Color.white;
            sceneTitleImage.raycastTarget = false;
            sceneTitleImage.preserveAspect = true;
        }

        private void BuildRespawnCard()
        {
            GameObject respawnObject = CreateChild("Respawn Card", root);
            respawnGroup = respawnObject.AddComponent<CanvasGroup>();
            respawnGroup.alpha = 0f;
            respawnGroup.interactable = false;
            respawnGroup.blocksRaycasts = false;

            respawnRect = respawnObject.GetComponent<RectTransform>();
            respawnRect.anchorMin = new Vector2(0.5f, 0.5f);
            respawnRect.anchorMax = new Vector2(0.5f, 0.5f);
            respawnRect.pivot = new Vector2(0.5f, 0.5f);
            respawnRect.anchoredPosition = Vector2.zero;
            respawnRect.sizeDelta = new Vector2(610f, 407f);

            respawnImage = respawnObject.AddComponent<Image>();
            respawnImage.sprite = LoadSprite("09_respawn");
            respawnImage.color = Color.white;
            respawnImage.raycastTarget = false;
            respawnImage.preserveAspect = true;

            respawnText = CreateText("Text", respawnRect, 30, TextAnchor.MiddleCenter);
            respawnText.resizeTextMinSize = 12;
            respawnText.resizeTextMaxSize = 24;
            respawnText.text = "The light gathers you.";
            RectTransform textRect = respawnText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(122f, 64f);
            textRect.offsetMax = new Vector2(-84f, -262f);
        }

        private void BuildOptionsPanel()
        {
            GameObject optionsObject = CreateChild("Options Shell", root);
            optionsGroup = optionsObject.AddComponent<CanvasGroup>();
            optionsGroup.alpha = 0f;
            optionsGroup.interactable = false;
            optionsGroup.blocksRaycasts = false;

            optionsRect = optionsObject.GetComponent<RectTransform>();
            optionsRect.anchorMin = new Vector2(0.5f, 0.5f);
            optionsRect.anchorMax = new Vector2(0.5f, 0.5f);
            optionsRect.pivot = new Vector2(0.5f, 0.5f);
            optionsRect.anchoredPosition = Vector2.zero;
            optionsRect.sizeDelta = new Vector2(760f, 507f);

            optionsImage = optionsObject.AddComponent<Image>();
            optionsImage.sprite = LoadSprite("13_options_shell");
            optionsImage.color = Color.white;
            optionsImage.raycastTarget = false;
            optionsImage.preserveAspect = true;

            optionsText = CreateText("Title", optionsRect, 34, TextAnchor.MiddleCenter);
            optionsText.resizeTextMinSize = 14;
            optionsText.resizeTextMaxSize = 30;
            optionsText.text = "Options";
            RectTransform textRect = optionsText.rectTransform;
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = new Vector2(82f, 126f);
            textRect.sizeDelta = new Vector2(380f, 70f);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (string.Equals(scene.name, "MainMenu", System.StringComparison.OrdinalIgnoreCase))
            {
                popupQueue.Clear();
                shownOneShotKeys.Clear();
                shownSceneTitles.Clear();
            }

            ShowSceneTitleFor(scene.name, 0.85f);
        }

        private void EnqueueOnce(string key, string assetName, string message, Vector2 size, Vector2 position, Vector2 textPosition, Vector2 textSize)
        {
            if (!shownOneShotKeys.Add(key))
                return;

            EnqueuePopup(assetName, message, size, position, textPosition, textSize, 2f);
        }

        private void EnqueuePopup(string assetName, string message, Vector2 size, Vector2 position, Vector2 textPosition, Vector2 textSize, float holdSeconds)
        {
            popupQueue.Enqueue(new PopupRequest(assetName, message, size, position, textPosition, textSize, holdSeconds));
            if (popupRoutine == null)
                popupRoutine = StartCoroutine(PopupRoutine());
        }

        private IEnumerator PopupRoutine()
        {
            while (popupQueue.Count > 0)
            {
                PopupRequest request = popupQueue.Dequeue();
                Sprite sprite = LoadSprite(request.AssetName);
                if (sprite == null)
                    continue;

                popupImage.sprite = sprite;
                popupRect.sizeDelta = request.Size;
                popupRect.anchoredPosition = request.Position;
                popupText.text = request.Message;
                popupText.enabled = !string.IsNullOrWhiteSpace(request.Message);
                ApplyPopupTextLayout(request);

                yield return FadeGroup(popupGroup, 1f, 0.22f);
                yield return new WaitForSecondsRealtime(Mathf.Max(0.15f, request.HoldSeconds));
                yield return FadeGroup(popupGroup, 0f, 0.35f);
                yield return new WaitForSecondsRealtime(0.18f);
            }

            popupRoutine = null;
        }

        private void FlashHazard(float health01)
        {
            if (hazardImage.sprite == null)
                hazardImage.sprite = LoadSprite("08_hazard_overlay");

            if (hazardRoutine != null)
                StopCoroutine(hazardRoutine);

            hazardRoutine = StartCoroutine(HazardRoutine(health01));
        }

        private IEnumerator HazardRoutine(float health01)
        {
            float target = Mathf.Lerp(0.32f, 0.72f, 1f - Mathf.Clamp01(health01));
            hazardGroup.alpha = target;

            float elapsed = 0f;
            const float fadeTime = 0.8f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.unscaledDeltaTime;
                hazardGroup.alpha = Mathf.Lerp(target, 0f, elapsed / fadeTime);
                yield return null;
            }

            hazardGroup.alpha = 0f;
            hazardRoutine = null;
        }

        private void ShowRespawnCard()
        {
            if (respawnRoutine != null)
                StopCoroutine(respawnRoutine);

            respawnRoutine = StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            yield return FadeGroup(respawnGroup, 1f, 0.08f);
            yield return new WaitForSecondsRealtime(0.55f);
            yield return FadeGroup(respawnGroup, 0f, 0.35f);
            respawnRoutine = null;
        }

        private void ShowSceneTitleFor(string sceneName, float delay)
        {
            string assetName = ResolveSceneTitleAsset(sceneName);
            if (string.IsNullOrEmpty(assetName))
                return;

            string key = sceneName + ":" + assetName;
            if (!shownSceneTitles.Add(key))
                return;

            Sprite sprite = LoadSprite(assetName);
            if (sprite == null)
                return;

            if (sceneTitleRoutine != null)
                StopCoroutine(sceneTitleRoutine);

            sceneTitleImage.sprite = sprite;
            sceneTitleRoutine = StartCoroutine(SceneTitleRoutine(delay));
        }

        private IEnumerator SceneTitleRoutine(float delay)
        {
            sceneTitleGroup.alpha = 0f;
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);

            yield return FadeGroup(sceneTitleGroup, 1f, 0.32f);
            yield return new WaitForSecondsRealtime(3f);
            yield return FadeGroup(sceneTitleGroup, 0f, 0.8f);
            sceneTitleRoutine = null;
        }

        private static string ResolveSceneTitleAsset(string sceneName)
        {
            if (string.Equals(sceneName, "mosswake", System.StringComparison.OrdinalIgnoreCase))
                return "17_scene_title_outer_grove";

            if (string.Equals(sceneName, "OutdoorsScene", System.StringComparison.OrdinalIgnoreCase))
                return "16_scene_title_mosswake";

            if (string.Equals(sceneName, "dungeon_enter", System.StringComparison.OrdinalIgnoreCase))
                return "18_scene_title_dungeon_entrance";

            return string.Empty;
        }

        private void ShowOptionsPanel()
        {
            if (optionsRoutine != null)
                StopCoroutine(optionsRoutine);

            optionsDismissTime = Time.unscaledTime + 0.25f;
            optionsRoutine = StartCoroutine(FadeGroup(optionsGroup, 1f, 0.18f));
        }

        private void HideOptionsPanel()
        {
            if (optionsRoutine != null)
                StopCoroutine(optionsRoutine);

            optionsRoutine = StartCoroutine(FadeGroup(optionsGroup, 0f, 0.2f));
        }

        private void ShowSaveBadge()
        {
            if (saveRoutine != null)
                StopCoroutine(saveRoutine);

            saveRoutine = StartCoroutine(SaveRoutine());
        }

        private IEnumerator SaveRoutine()
        {
            yield return FadeGroup(saveGroup, 1f, 0.16f);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return FadeGroup(saveGroup, 0f, 0.35f);
            saveRoutine = null;
        }

        private IEnumerator FadeGroup(CanvasGroup group, float targetAlpha, float duration)
        {
            if (group == null)
                yield break;

            float startAlpha = group.alpha;
            if (duration <= 0f)
            {
                group.alpha = targetAlpha;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            group.alpha = targetAlpha;
        }

        private Sprite LoadSprite(string assetName)
        {
            if (string.IsNullOrEmpty(assetName))
                return null;

            if (spriteCache.TryGetValue(assetName, out Sprite cached))
                return cached;

            Texture2D texture = Resources.Load<Texture2D>(AssetRoot + assetName);
            if (texture == null)
                return null;

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.name = assetName;
            spriteCache[assetName] = sprite;
            return sprite;
        }

        private Text CreateText(string name, RectTransform parent, int fontSize, TextAnchor alignment)
        {
            GameObject textObject = CreateChild(name, parent);
            Text text = textObject.AddComponent<Text>();
            text.font = serifFont;
            text.fontSize = fontSize;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(10, fontSize - 16);
            text.resizeTextMaxSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.92f, 0.80f, 0.48f, 1f);
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            text.lineSpacing = 0.92f;
            text.alignByGeometry = true;
            return text;
        }

        private void ApplyPopupTextLayout(PopupRequest request)
        {
            popupTextRect.anchorMin = new Vector2(0.5f, 0.5f);
            popupTextRect.anchorMax = new Vector2(0.5f, 0.5f);
            popupTextRect.pivot = new Vector2(0.5f, 0.5f);
            popupTextRect.anchoredPosition = request.TextPosition;
            popupTextRect.sizeDelta = request.TextSize;

            int maxSize = ResolvePopupTextMaxSize(request.AssetName, request.Message);
            popupText.fontSize = maxSize;
            popupText.resizeTextMaxSize = maxSize;
            popupText.resizeTextMinSize = 10;
            popupText.alignment = TextAnchor.MiddleCenter;
            popupText.horizontalOverflow = HorizontalWrapMode.Wrap;
            popupText.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static int ResolvePopupTextMaxSize(string assetName, string message)
        {
            int maxSize;
            switch (assetName)
            {
                case "01_sticky_unlock":
                case "02_wave_unlock":
                    maxSize = 24;
                    break;
                case "06_checkpoint":
                case "10_enemy_banner":
                    maxSize = 26;
                    break;
                case "07_pickup_discovery":
                case "11_barrier_prompt":
                case "04_context_prompt":
                    maxSize = 23;
                    break;
                default:
                    maxSize = 24;
                    break;
            }

            if (!string.IsNullOrEmpty(message) && message.Length > 24)
                maxSize = Mathf.Min(maxSize, 21);

            return maxSize;
        }

        private static GameObject CreateChild(string name, Transform parent)
        {
            GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void Stretch(RectTransform rect)
        {
            if (rect == null)
                return;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static string MaterialWhisper(global::SlimeMaterialType material)
        {
            switch (material)
            {
                case global::SlimeMaterialType.Rubber:
                    return "The body springs.";
                case global::SlimeMaterialType.Stone:
                    return "Stone steadies the heart.";
                case global::SlimeMaterialType.Metal:
                    return "A heavy shine settles.";
                case global::SlimeMaterialType.Fireproof:
                    return "Warmth passes softly.";
                case global::SlimeMaterialType.Conductive:
                    return "A spark finds a path.";
                default:
                    return "Something bright lingers.";
            }
        }

        private readonly struct PopupRequest
        {
            public PopupRequest(string assetName, string message, Vector2 size, Vector2 position, Vector2 textPosition, Vector2 textSize, float holdSeconds)
            {
                AssetName = assetName;
                Message = message;
                Size = size;
                Position = position;
                TextPosition = textPosition;
                TextSize = textSize;
                HoldSeconds = holdSeconds;
            }

            public string AssetName { get; }
            public string Message { get; }
            public Vector2 Size { get; }
            public Vector2 Position { get; }
            public Vector2 TextPosition { get; }
            public Vector2 TextSize { get; }
            public float HoldSeconds { get; }
        }
    }
}
