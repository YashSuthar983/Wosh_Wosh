using Heartwell.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenuController : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject pauseMenuPanel;
    public GameObject blurVolume;
    public RectTransform containerRect;

    [Header("Animation Settings")]
    public float animationSpeed = 10f;

    [Header("Button References")]
    public UnityEngine.UI.Button resumeButton;
    public UnityEngine.UI.Button optionsButton;
    public UnityEngine.UI.Button exitButton;

    [Header("Generated Button Sprites")]
    public Sprite resumeButtonSprite;
    public Sprite optionsButtonSprite;
    public Sprite mainMenuButtonSprite;
    public Vector2 generatedButtonSize = new Vector2(400f, 100f);
    public float generatedButtonSpacing = 30f;

    private bool isPaused = false;
    private Coroutine activeCoroutine;

    void Start()
    {
        // Auto-fix 1: Ensure Canvas can receive clicks and is sorting on top!
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            if (GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;
        }

        // Auto-fix 2: Prevent the blur volume from acting as an invisible shield blocking clicks
        if (blurVolume != null)
        {
            var img = blurVolume.GetComponent<UnityEngine.UI.Image>();
            if (img != null) img.raycastTarget = false;
        }

        InitializeButtons();
        ApplyGeneratedPausePanelArt();
    }

    private Canvas GetTargetCanvas()
    {
        Canvas c = GetComponentInParent<Canvas>(true);
        if (c != null) return c;
        if (pauseMenuPanel != null)
        {
            c = pauseMenuPanel.GetComponent<Canvas>();
            if (c != null) return c;

            c = pauseMenuPanel.GetComponentInParent<Canvas>(true);
            if (c != null) return c;
        }

        GameObject pc = GameObject.Find("Pause_Canvas");
        if (pc != null) return pc.GetComponent<Canvas>();
        return null;
    }

    private void InitializeButtons()
    {
        Canvas targetCanvas = GetTargetCanvas();
        Transform searchRoot = targetCanvas != null ? targetCanvas.transform : transform;
        if (pauseMenuPanel != null && targetCanvas == null) searchRoot = pauseMenuPanel.transform;

        // 1. Scan all Images to find the ones the user dragged in, and upgrade them to Buttons
        var images = searchRoot.GetComponentsInChildren<UnityEngine.UI.Image>(true);
        foreach (var img in images)
        {
            if (img.sprite == null) continue;
            string sName = img.sprite.name.ToLower();
            
            // Ignore the main menu buttons if they got swept up
            if (img.name == "Btn_Embark" || img.name == "Btn_Exit") continue;

            if (resumeButton == null && (sName.Contains("resume") || sName.Contains("start")))
            {
                resumeButton = img.GetComponent<UnityEngine.UI.Button>();
                if (resumeButton == null) resumeButton = img.gameObject.AddComponent<UnityEngine.UI.Button>();
            }
            else if (optionsButton == null && (sName.Contains("option") || sName.Contains("setting")))
            {
                optionsButton = img.GetComponent<UnityEngine.UI.Button>();
                if (optionsButton == null) optionsButton = img.gameObject.AddComponent<UnityEngine.UI.Button>();
            }
            else if (exitButton == null && (sName.Contains("main") || sName.Contains("exit") || sName.Contains("quit")))
            {
                exitButton = img.GetComponent<UnityEngine.UI.Button>();
                if (exitButton == null) exitButton = img.gameObject.AddComponent<UnityEngine.UI.Button>();
            }
        }

        EnsurePauseButtons(searchRoot);

        Debug.Log($"HEARTWELL: Pause Menu Buttons Auto-Linked - Resume: {(resumeButton != null ? resumeButton.name : "NULL")}, Options: {(optionsButton != null ? optionsButton.name : "NULL")}, Exit: {(exitButton != null ? exitButton.name : "NULL")}");

        // ALWAYS link listeners, overriding any old ones
        if (resumeButton != null) { resumeButton.onClick.RemoveAllListeners(); resumeButton.onClick.AddListener(Resume); }
        if (optionsButton != null) { optionsButton.onClick.RemoveAllListeners(); optionsButton.onClick.AddListener(OpenOptions); }
        if (exitButton != null) { exitButton.onClick.RemoveAllListeners(); exitButton.onClick.AddListener(ExitToMainMenu); }
    }

    private void EnsurePauseButtons(Transform searchRoot)
    {
        RectTransform buttonParent = ResolveButtonParent(searchRoot);
        if (buttonParent == null)
            return;

        ConfigureButtonContainer(buttonParent);
        resumeButton = EnsurePauseButton(resumeButton, "Btn_Resume", resumeButtonSprite, buttonParent);
        optionsButton = EnsurePauseButton(optionsButton, "Btn_Options", optionsButtonSprite, buttonParent);
        exitButton = EnsurePauseButton(exitButton, "Btn_MainMenu", mainMenuButtonSprite, buttonParent);
    }

    private RectTransform ResolveButtonParent(Transform searchRoot)
    {
        if (containerRect != null)
            return containerRect;

        if (pauseMenuPanel != null)
        {
            RectTransform[] childRects = pauseMenuPanel.GetComponentsInChildren<RectTransform>(true);
            foreach (RectTransform childRect in childRects)
            {
                if (childRect != null && childRect.name == "Button_Container")
                {
                    containerRect = childRect;
                    return childRect;
                }
            }

            GameObject containerObject = new GameObject("Button_Container", typeof(RectTransform));
            RectTransform rect = containerObject.GetComponent<RectTransform>();
            rect.SetParent(pauseMenuPanel.transform, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(Mathf.Max(450f, generatedButtonSize.x), 400f);
            containerRect = rect;
            return rect;
        }

        return searchRoot as RectTransform;
    }

    private void ConfigureButtonContainer(RectTransform buttonParent)
    {
        if (buttonParent == null)
            return;

        VerticalLayoutGroup layout = buttonParent.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            layout = buttonParent.gameObject.AddComponent<VerticalLayoutGroup>();

        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = generatedButtonSpacing;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
    }

    private Button EnsurePauseButton(Button button, string buttonName, Sprite sprite, RectTransform parent)
    {
        if (button == null && sprite == null)
            return null;

        GameObject buttonObject;
        if (button == null)
        {
            buttonObject = new GameObject(buttonName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(SquishButton));
            buttonObject.transform.SetParent(parent, false);
            button = buttonObject.GetComponent<Button>();
        }
        else
        {
            buttonObject = button.gameObject;
            if (buttonObject.GetComponent<SquishButton>() == null)
                buttonObject.AddComponent<SquishButton>();
        }

        buttonObject.name = buttonName;

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = generatedButtonSize;
            rect.localScale = Vector3.one;
        }

        Image image = buttonObject.GetComponent<Image>();
        if (image == null)
            image = buttonObject.AddComponent<Image>();

        if (sprite != null)
            image.sprite = sprite;

        image.color = Color.white;
        image.raycastTarget = true;
        image.preserveAspect = true;
        button.targetGraphic = image;
        return button;
    }

    private void ApplyGeneratedPausePanelArt()
    {
        if (pauseMenuPanel == null)
            return;

        Texture2D texture = Resources.Load<Texture2D>("UI/InGameGenerated/12_pause_panel");
        if (texture == null)
            return;

        Image image = pauseMenuPanel.GetComponent<Image>();
        if (image == null)
            image = pauseMenuPanel.AddComponent<Image>();

        image.sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (Heartwell.UI.InGameOverlayUI.IsOptionsShellVisible)
            {
                Heartwell.UI.InGameOverlayUI.DismissOptionsShell();
                return;
            }

            if (isPaused)
                Resume();
            else
                Pause();
        }
    }

    public void Resume()
    {
        if (activeCoroutine != null) StopCoroutine(activeCoroutine);
        activeCoroutine = StartCoroutine(AnimatePop(false));
    }

    void Pause()
    {
        if (resumeButton == null || exitButton == null) InitializeButtons();

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(true);
            pauseMenuPanel.transform.localScale = Vector3.one;
        }
        
        var canvas = GetTargetCanvas();
        if (canvas != null && !canvas.enabled) canvas.enabled = true;

        if (blurVolume != null) blurVolume.SetActive(true);
        
        Time.timeScale = 0f;
        isPaused = true;

        Cursor.visible = SceneManager.GetActiveScene().name != "MainMenu";
        Cursor.lockState = CursorLockMode.None;

        if (activeCoroutine != null) StopCoroutine(activeCoroutine);
        activeCoroutine = StartCoroutine(AnimatePop(true));
    }

    private System.Collections.IEnumerator AnimatePop(bool opening)
    {
        float targetScale = opening ? 1f : 0f;
        Vector3 initialScale = containerRect != null ? containerRect.localScale : Vector3.one;
        float t = 0;

        if (containerRect != null && animationSpeed > 0)
        {
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime * animationSpeed;
                containerRect.localScale = Vector3.Lerp(initialScale, Vector3.one * targetScale, t);
                yield return null;
            }
            containerRect.localScale = Vector3.one * targetScale;
        }

        if (!opening)
        {
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
            
            // FORCE the canvas to hide no matter what - this is the "kill switch"
            var canvas = GetTargetCanvas();
            if (canvas != null) canvas.enabled = false;

            if (blurVolume != null) blurVolume.SetActive(false);
            
            Time.timeScale = 1f;
            isPaused = false;

            if (SceneManager.GetActiveScene().name != "MainMenu")
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
            else
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.None;
            }
        }
        
        activeCoroutine = null;
    }

    public void OpenOptions()
    {
        Heartwell.UI.InGameOverlayUI.ShowOptionsShell();
    }

    public void ExitToMainMenu()
    {
        Time.timeScale = 1f;
        
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.None;
        
        SceneManager.LoadScene("MainMenu");
    }
}
