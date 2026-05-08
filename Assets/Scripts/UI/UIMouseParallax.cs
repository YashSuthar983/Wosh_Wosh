using UnityEngine;

namespace Heartwell.UI
{
    /// <summary>
    /// Adds a subtle "parallax" effect to UI elements, making them follow the cursor.
    /// This creates a high-end, responsive feel for the Main Menu.
    /// </summary>
    public class UIMouseParallax : MonoBehaviour
    {
        public float amount = 20f;
        [SerializeField] private float smoothSpeed = 5f;

        private Vector3 _initialPosition;

        private void Start()
        {
            _initialPosition = transform.localPosition;
        }

        private void Update()
        {
            // Get mouse position in normalized screen space (-1 to 1)
            Vector3 mousePos = Input.mousePosition;
            float mouseX = (mousePos.x / Screen.width) * 2f - 1f;
            float mouseY = (mousePos.y / Screen.height) * 2f - 1f;

            // Calculate target position
            Vector3 targetPos = _initialPosition + new Vector3(mouseX * amount, mouseY * amount, 0f);
            
            // Smoothly move toward it
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.unscaledDeltaTime * smoothSpeed);
        }
    }
}
