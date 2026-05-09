using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Heartwell.UI
{
    /// <summary>
    /// Controls the cinematic opening cutscene, transitioning through a sequence of slides
    /// with fade effects and subtle camera movement.
    /// </summary>
    public class OpeningCutsceneController : MonoBehaviour
    {
        [Header("Assets")]
        [SerializeField] private List<Sprite> slides;
        [SerializeField] private string targetNextScene = "mosswake";

        [Header("Components")]
        [SerializeField] private Image displayImage;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Timing")]
        [SerializeField] private float fadeDuration = 0.5f;
        [SerializeField] private float displayDuration = 4.0f;
        [SerializeField] private float movementScale = 1.1f;

        private int currentSlideIndex = 0;
        private bool isTransitioning = false;

        private float startTime;

        private void Start()
        {
            startTime = Time.time;

            if (slides == null || slides.Count == 0)
            {
                Debug.LogError("No slides assigned to OpeningCutsceneController!");
                return;
            }

            if (displayImage == null || canvasGroup == null)
            {
                Debug.LogError("Missing component references on OpeningCutsceneController!");
                return;
            }

            StartCoroutine(PlayCutscene());
        }

        private void Update()
        {
            // Prevent accidental skipping right as the scene loads
            if (Time.time - startTime < 1.0f) return;

            // Allow skipping the cutscene
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(0))
            {
                SkipToNextScene();
            }
        }

        private IEnumerator PlayCutscene()
        {
            while (currentSlideIndex < slides.Count)
            {
                yield return StartCoroutine(ShowSlide(slides[currentSlideIndex]));
                currentSlideIndex++;
            }

            SkipToNextScene();
        }

        private IEnumerator ShowSlide(Sprite slideSprite)
        {
            isTransitioning = true;
            displayImage.sprite = slideSprite;
            
            // Reset transform for Ken Burns effect
            ((RectTransform)displayImage.transform).localScale = Vector3.one;
            ((RectTransform)displayImage.transform).anchoredPosition = Vector2.zero;

            float totalDuration = fadeDuration * 2 + displayDuration;
            
            // Fade In
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                
                // Subtle zoom during fade
                ApplyKenBurnsEffect(elapsed / totalDuration);
                
                yield return null;
            }
            canvasGroup.alpha = 1f;

            // Wait while displaying
            float displayElapsed = 0f;
            while (displayElapsed < displayDuration)
            {
                displayElapsed += Time.deltaTime;
                ApplyKenBurnsEffect((fadeDuration + displayElapsed) / totalDuration);
                yield return null;
            }

            // Fade Out
            elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Clamp01(1f - (elapsed / fadeDuration));
                
                // Continue zoom during fade out
                ApplyKenBurnsEffect((fadeDuration + displayDuration + elapsed) / totalDuration);
                
                yield return null;
            }
            canvasGroup.alpha = 0f;
            isTransitioning = false;
        }

        private void ApplyKenBurnsEffect(float normalizedTime)
        {
            // Slow zoom from 1.0 to movementScale
            float currentScale = Mathf.Lerp(1.0f, movementScale, normalizedTime);
            ((RectTransform)displayImage.transform).localScale = new Vector3(currentScale, currentScale, 1f);
            
            // Subtle upward drift
            float yOffset = Mathf.Lerp(0, 20f, normalizedTime);
            ((RectTransform)displayImage.transform).anchoredPosition = new Vector2(0, yOffset);
        }

        public void SkipToNextScene()
        {
            StopAllCoroutines();
            SceneManager.LoadScene(targetNextScene);
        }
    }
}
