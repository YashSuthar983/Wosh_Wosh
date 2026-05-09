using UnityEngine;
using UnityEngine.SceneManagement;

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
    }

    private Canvas GetTargetCanvas()
    {
        Canvas c = GetComponentInParent<Canvas>();
        if (c != null) return c;
        if (pauseMenuPanel != null)
        {
            c = pauseMenuPanel.GetComponentInParent<Canvas>();
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

        Debug.Log($"HEARTWELL: Pause Menu Buttons Auto-Linked - Resume: {(resumeButton != null ? resumeButton.name : "NULL")}, Options: {(optionsButton != null ? optionsButton.name : "NULL")}, Exit: {(exitButton != null ? exitButton.name : "NULL")}");

        // ALWAYS link listeners, overriding any old ones
        if (resumeButton != null) { resumeButton.onClick.RemoveAllListeners(); resumeButton.onClick.AddListener(Resume); }
        if (optionsButton != null) { optionsButton.onClick.RemoveAllListeners(); optionsButton.onClick.AddListener(OpenOptions); }
        if (exitButton != null) { exitButton.onClick.RemoveAllListeners(); exitButton.onClick.AddListener(ExitToMainMenu); }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
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

        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
        
        var canvas = GetTargetCanvas();
        if (canvas != null && !canvas.enabled) canvas.enabled = true;

        if (blurVolume != null) blurVolume.SetActive(true);
        
        Time.timeScale = 0f;
        isPaused = true;
        
        Cursor.visible = true;
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
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }
        
        activeCoroutine = null;
    }

    public void OpenOptions()
    {
    }

    public void ExitToMainMenu()
    {
        Time.timeScale = 1f;
        
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        
        SceneManager.LoadScene("MainMenu");
    }
}
