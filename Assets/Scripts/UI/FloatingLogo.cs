using UnityEngine;

namespace Heartwell.UI
{
    /// <summary>
    /// Adds a gentle "floating" motion to a UI element (like a Logo).
    /// Creates a dreamlike, atmospheric feel for the Main Menu.
    /// </summary>
    public class FloatingLogo : MonoBehaviour
    {
        [Header("Floating Settings")]
        [SerializeField] private float bobSpeed = 1.5f;
        [SerializeField] private float bobAmount = 10f;
        
        [Header("Rotation Settings")]
        [SerializeField] private float rotateSpeed = 1f;
        [SerializeField] private float rotateAmount = 2f;

        private Vector3 _initialPosition;
        private float _time;

        private void Start()
        {
            _initialPosition = transform.localPosition;
        }

        private void Update()
        {
            _time += Time.unscaledDeltaTime;

            // Bob up and down
            float newY = _initialPosition.y + Mathf.Sin(_time * bobSpeed) * bobAmount;
            transform.localPosition = new Vector3(_initialPosition.x, newY, _initialPosition.z);

            // Subtle tilt
            float tilt = Mathf.Sin(_time * rotateSpeed) * rotateAmount;
            transform.localRotation = Quaternion.Euler(0, 0, tilt);
        }
    }
}
