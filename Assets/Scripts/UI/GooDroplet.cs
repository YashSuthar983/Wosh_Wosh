using UnityEngine;
using UnityEngine.UI;

namespace Heartwell.UI
{
    public class GooDroplet : MonoBehaviour
    {
        private Image _image;
        private RectTransform _rect;
        private float _lifetime;
        private float _maxLifetime = 1.0f;
        private Vector3 _velocity;
        private Color _startColor;

        public void Initialize(Sprite sprite, Color color, float size, Vector2 velocity)
        {
            _image = GetComponent<Image>();
            _rect = GetComponent<RectTransform>();
            
            _image.sprite = sprite;
            _image.color = color;
            _startColor = color;
            _rect.sizeDelta = new Vector2(size, size);
            _velocity = velocity;
            _lifetime = 0;
            
            // Randomize slightly
            _velocity += new Vector3(Random.Range(-50f, 50f), Random.Range(-50f, 50f), 0);
        }

        private void Update()
        {
            _lifetime += Time.unscaledDeltaTime;
            float t = _lifetime / _maxLifetime;

            // Move with "gravity"
            _velocity.y -= 500f * Time.unscaledDeltaTime; 
            _rect.anchoredPosition += (Vector2)_velocity * Time.unscaledDeltaTime;

            // Fade and shrink
            _image.color = Color.Lerp(_startColor, new Color(_startColor.r, _startColor.g, _startColor.b, 0), t);
            _rect.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);

            if (t >= 1.0f) Destroy(gameObject);
        }
    }
}
