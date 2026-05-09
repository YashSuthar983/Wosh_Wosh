using UnityEngine;
using UnityEngine.UI;

namespace Heartwell.UI
{
    /// <summary>
    /// A physics-inspired custom cursor that follows the mouse with a viscous "drag" 
    /// and performs squash-and-stretch based on velocity.
    /// </summary>
    public class SlimeCursor : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float followSpeed = 20f;
        [SerializeField] private float rotationSpeed = 10f;
        
        [Header("Squash & Stretch")]
        [SerializeField] private float stretchAmount = 0.5f;
        [SerializeField] private float maxStretch = 1.5f;
        [SerializeField] private float damping = 5f;
        
        [Header("Interaction")]
        [SerializeField] private float clickStretchAmount = 1.8f;
        [SerializeField] private float dragStretchMultiplier = 1.5f;

        [Header("Animation Settings")]
        [SerializeField] private Sprite[] animationFrames;
        [SerializeField] private float frameRate = 0.1f;

        [Header("Effects")]
        [SerializeField] private float dropletInterval = 0.05f;
        [SerializeField] private float minDropletSpeed = 50f;

        private RectTransform _rectTransform;
        private Canvas _canvas;
        private Image _cursorImage;
        private Vector3 _originalScale;
        private Vector2 _velocity;
        private int _currentFrame;
        private float _animationTimer;
        private float _dropletTimer;
        private Vector2 _lastPos;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _cursorImage = GetComponent<Image>();
            _canvas = GetComponentInParent<Canvas>();
            
            // Fallback for scale if setup missed it
            _originalScale = _rectTransform.localScale;
            if (_originalScale.sqrMagnitude < 0.1f) _originalScale = Vector3.one;
            
            // Hide hardware cursor
            Cursor.visible = false;
        }

        private void Start()
        {
            _lastPos = Input.mousePosition;
            
#if UNITY_EDITOR
            // ULTIMATE FAILSAFE: If setup failed to assign frames, find them now
            if (animationFrames == null || animationFrames.Length == 0)
            {
                Debug.Log("HEARTWELL: No frames found, searching project for 'momo'...");
                string[] guids = UnityEditor.AssetDatabase.FindAssets("momo t:Sprite");
                if (guids.Length > 0)
                {
                    var framesList = new System.Collections.Generic.List<Sprite>();
                    foreach (var guid in guids)
                    {
                        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                        Sprite s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                        if (s != null) framesList.Add(s);
                    }
                    animationFrames = framesList.ToArray();
                }
            }
#endif

            // Ensure we have a sprite
            if (_cursorImage.sprite == null && animationFrames != null && animationFrames.Length > 0)
            {
                _cursorImage.sprite = animationFrames[0];
            }
            
            _cursorImage.color = Color.white;
            _cursorImage.raycastTarget = false;
            
            Debug.Log("HEARTWELL: Slime Cursor started at " + transform.position + " with " + (animationFrames != null ? animationFrames.Length : 0) + " frames.");
        }

        private void UpdatePosition()
        {
            Vector3 mousePos = Input.mousePosition;
            Vector2 currentPos = (Vector2)mousePos;
            
            _velocity = (currentPos - _lastPos) / Time.unscaledDeltaTime;
            transform.position = mousePos;
            _lastPos = currentPos;
        }

        private bool _hasLoggedStarted = false;
        private void Update()
        {
            if (!_hasLoggedStarted)
            {
                Debug.Log("HEARTWELL: Slime Cursor Update Loop Running!");
                _hasLoggedStarted = true;
            }

            UpdatePosition();
            ApplyPhysicsVisuals();
            UpdateAnimation();
            UpdateTrails();
            
            // Force hardware cursor hide every frame
            if (Cursor.visible) Cursor.visible = false;

            // Final safety: ensure we are visible
            if (_cursorImage.sprite == null && animationFrames != null && animationFrames.Length > 0)
                _cursorImage.sprite = animationFrames[0];
        }

        private void UpdateTrails()
        {
            float speed = _velocity.magnitude;
            bool isClicking = Input.GetMouseButton(0);

            if (speed > minDropletSpeed || isClicking)
            {
                _dropletTimer += Time.unscaledDeltaTime;
                
                // Adjust frequency based on speed
                float interval = isClicking ? dropletInterval * 0.5f : dropletInterval;
                if (_dropletTimer >= interval)
                {
                    _dropletTimer = 0;
                    SpawnDroplet();
                }
            }
        }

        private void SpawnDroplet()
        {
            GameObject dropObj = new GameObject("Goo_Droplet");
            dropObj.transform.SetParent(_canvas.transform, false);
            dropObj.transform.SetSiblingIndex(_rectTransform.GetSiblingIndex()); // Behind cursor
            
            var dropRect = dropObj.AddComponent<RectTransform>();
            dropRect.anchoredPosition = _rectTransform.anchoredPosition;
            
            dropObj.AddComponent<CanvasRenderer>();
            var dropImg = dropObj.AddComponent<Image>();
            
            var droplet = dropObj.AddComponent<GooDroplet>();
            
            // Pick a frame for the droplet
            Sprite s = (animationFrames != null && animationFrames.Length > 0) ? animationFrames[Random.Range(0, animationFrames.Length)] : _cursorImage.sprite;
            
            droplet.Initialize(
                s, 
                new Color(0.3f, 0.9f, 0.2f, 0.8f), // Bright slime green
                Random.Range(15f, 35f), 
                _velocity * -0.1f // Slight pushback
            );
        }

        private void UpdateAnimation()
        {
            if (animationFrames == null || animationFrames.Length == 0) return;

            // Only animate if moving significantly
            if (_velocity.magnitude > 5f || Input.GetMouseButton(0))
            {
                _animationTimer += Time.unscaledDeltaTime;
                if (_animationTimer >= frameRate)
                {
                    _animationTimer = 0;
                    _currentFrame = (_currentFrame + 1) % animationFrames.Length;
                    _cursorImage.sprite = animationFrames[_currentFrame];
                }
            }
            else
            {
                _currentFrame = 0;
                _cursorImage.sprite = animationFrames[0]; // Stay static on frame 0 when stopped
            }
        }

        private void ApplyPhysicsVisuals()
        {
            float speed = _velocity.magnitude;
            bool isClicking = Input.GetMouseButton(0);
            
            // 1. Rotation
            if (speed > 0.1f)
            {
                float angle = Mathf.Atan2(_velocity.y, _velocity.x) * Mathf.Rad2Deg;
                Quaternion targetRotation = Quaternion.Euler(0, 0, angle - 90f);
                _rectTransform.rotation = Quaternion.Lerp(_rectTransform.rotation, targetRotation, Time.unscaledDeltaTime * rotationSpeed);
            }
            else if (!isClicking)
            {
                _rectTransform.rotation = Quaternion.Lerp(_rectTransform.rotation, Quaternion.identity, Time.unscaledDeltaTime * damping);
            }

            // 2. Squash & Stretch
            float stretch = 1f + (speed / 1000f) * stretchAmount;
            
            // Apply click/drag multiplier
            if (isClicking) stretch *= clickStretchAmount;
            else if (speed > 500f) stretch *= dragStretchMultiplier;

            stretch = Mathf.Clamp(stretch, 0.5f, maxStretch * 2f);
            float squash = 1f / stretch;

            Vector3 targetScale = new Vector3(
                _originalScale.x * squash,
                _originalScale.y * stretch,
                _originalScale.z
            );
            
            _rectTransform.localScale = Vector3.Lerp(_rectTransform.localScale, targetScale, Time.unscaledDeltaTime * damping);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus) Cursor.visible = false;
        }
    }
}
