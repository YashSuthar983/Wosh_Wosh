using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class SceneTransitionTrigger : MonoBehaviour
{
    [SerializeField] private string sceneName = "dungeon_enter";
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private bool requirePlayer = true;
    [SerializeField, Min(0f)] private float fadeDuration = 0.35f;
    [SerializeField, Min(0f)] private float loadDelay = 0.05f;

    private bool isTransitioning;

    private void Reset()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private void Awake()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isTransitioning && triggerOnce)
            return;

        if (requirePlayer && !IsPlayerCollider(other))
            return;

        if (string.IsNullOrWhiteSpace(sceneName))
            return;

        isTransitioning = true;
        StartCoroutine(TransitionRoutine());
    }

    private IEnumerator TransitionRoutine()
    {
        Heartwell.UI.InGameOverlayUI.ShowSceneTransition();

        if (loadDelay > 0f)
            yield return new WaitForSeconds(loadDelay);

        Image fadeImage = CreateFadeImage();
        if (fadeImage != null && fadeDuration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                Color color = fadeImage.color;
                color.a = Mathf.Clamp01(elapsed / fadeDuration);
                fadeImage.color = color;
                yield return null;
            }
        }

        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    private static bool IsPlayerCollider(Collider other)
    {
        if (other == null)
            return false;

        if (other.CompareTag("Player"))
            return true;

        return other.GetComponentInParent<SlimePlayerAbilities>() != null
            || other.GetComponentInParent<SlimeMovementController>() != null;
    }

    private static Image CreateFadeImage()
    {
        GameObject canvasObject = new GameObject("Scene Transition Fade", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject imageObject = new GameObject("Fade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = imageObject.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = false;
        return image;
    }
}
