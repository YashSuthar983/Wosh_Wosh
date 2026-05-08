using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Heartwell.UI
{
    /// <summary>
    /// Adds a "squishy" tactile feel to UI buttons by manipulating their scale on hover and click.
    /// Designed to match the soft, elastic slime aesthetic of HEARTWELL.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class SquishButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Squish Settings")]
        [SerializeField] private float hoverScaleMultiplier = 1.05f;
        [SerializeField] private float downScaleMultiplier = 0.9f;
        [SerializeField] private float animationSpeed = 15f;
        [SerializeField] private Vector3 squishStrength = new Vector3(0.1f, -0.1f, 0f); // X expands, Y compresses

        [Header("Idle Wobble")]
        [SerializeField] private bool useIdleWobble = true;
        [SerializeField] private float idleSpeed = 2f;
        [SerializeField] private float idleAmount = 0.02f;

        private Vector3 _originalScale;
        private Vector3 _targetScale;
        private Coroutine _animationCoroutine;
        private float _idleTime;

        private void Awake()
        {
            _originalScale = transform.localScale;
            _targetScale = _originalScale;
        }        private void Update()
        {
            if (useIdleWobble && _targetScale == _originalScale)
            {
                _idleTime += Time.unscaledDeltaTime;
                float wobble = Mathf.Sin(_idleTime * idleSpeed) * idleAmount;
                transform.localScale = _originalScale + new Vector3(wobble, -wobble, 0);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _targetScale = _originalScale * hoverScaleMultiplier;
            StartAnimation();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _targetScale = _originalScale;
            StartAnimation();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // Apply a "squish" effect (flattening)
            _targetScale = new Vector3(
                _originalScale.x * (1f + squishStrength.x),
                _originalScale.y * (1f + squishStrength.y),
                _originalScale.z
            ) * downScaleMultiplier;
            StartAnimation();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _targetScale = _originalScale * hoverScaleMultiplier;
            StartAnimation();
        }

        private void StartAnimation()
        {
            if (_animationCoroutine != null) StopCoroutine(_animationCoroutine);
            _animationCoroutine = StartCoroutine(AnimateScale());
        }

        private IEnumerator AnimateScale()
        {
            while (Vector3.Distance(transform.localScale, _targetScale) > 0.001f)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.unscaledDeltaTime * animationSpeed);
                yield return null;
            }
            transform.localScale = _targetScale;
        }

        private void OnDisable()
        {
            transform.localScale = _originalScale;
        }
    }
}
