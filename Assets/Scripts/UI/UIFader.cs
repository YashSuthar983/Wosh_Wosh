using System.Collections;
using UnityEngine;

namespace Heartwell.UI
{
    /// <summary>
    /// Handles smooth alpha fading for UI elements using a CanvasGroup.
    /// Perfect for polished transitions between menus.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UIFader : MonoBehaviour
    {
        [SerializeField] private float fadeDuration = 0.5f;
        [SerializeField] private bool fadeOnStart = false;
        
        private CanvasGroup _canvasGroup;
        private Coroutine _fadeCoroutine;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        private void Start()
        {
            if (fadeOnStart)
            {
                _canvasGroup.alpha = 0f;
                FadeIn();
            }
        }

        public void FadeIn() => StartFade(1f);
        public void FadeOut() => StartFade(0f);

        private void StartFade(float targetAlpha)
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(AnimateFade(targetAlpha));
        }

        private IEnumerator AnimateFade(float targetAlpha)
        {
            float startAlpha = _canvasGroup.alpha;
            float time = 0;

            while (time < fadeDuration)
            {
                time += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / fadeDuration);
                yield return null;
            }

            _canvasGroup.alpha = targetAlpha;
            _canvasGroup.interactable = targetAlpha > 0.5f;
            _canvasGroup.blocksRaycasts = targetAlpha > 0.5f;
        }
    }
}
