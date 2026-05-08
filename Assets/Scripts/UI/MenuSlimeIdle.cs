using UnityEngine;

namespace Heartwell.UI
{
    /// <summary>
    /// Adds life to the Slime in the Main Menu diorama. 
    /// Features include gentle idle breathing, mouse tracking, and subtle reactions.
    /// </summary>
    public class MenuSlimeIdle : MonoBehaviour
    {
        [Header("Idle Breathing")]
        [SerializeField] private float breatheSpeed = 2f;
        [SerializeField] private Vector3 breatheStrength = new Vector3(0.05f, -0.05f, 0.05f);

        [Header("Mouse Tracking")]
        [SerializeField] private float trackSpeed = 5f;
        [SerializeField] private float maxRotationAngle = 15f;
        [SerializeField] private bool followMouse = true;

        [Header("Curiosity")]
        [SerializeField] private float leanStrength = 0.5f;
        [SerializeField] private float leanSpeed = 2f;

        private Vector3 _initialPosition;
        private Quaternion _initialRotation;
        private Vector3 _initialScale;
        private float _time;

        private void Start()
        {
            _initialPosition = transform.localPosition;
            _initialRotation = transform.localRotation;
            _initialScale = transform.localScale;
        }

        private void Update()
        {
            _time += Time.deltaTime;

            ApplyBreathing();
            
            if (followMouse)
            {
                ApplyMouseTracking();
            }
        }

        private void ApplyBreathing()
        {
            float scaleOffset = Mathf.Sin(_time * breatheSpeed);
            transform.localScale = _initialScale + new Vector3(
                scaleOffset * breatheStrength.x,
                scaleOffset * breatheStrength.y,
                scaleOffset * breatheStrength.z
            );
        }

        private void ApplyMouseTracking()
        {
            // Get mouse position in normalized screen space (-1 to 1)
            Vector3 mousePos = Input.mousePosition;
            float mouseX = (mousePos.x / Screen.width) * 2f - 1f;
            float mouseY = (mousePos.y / Screen.height) * 2f - 1f;

            // Target rotation based on mouse
            Quaternion targetRot = _initialRotation * Quaternion.Euler(-mouseY * maxRotationAngle, mouseX * maxRotationAngle, 0);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, Time.deltaTime * trackSpeed);

            // Subtle "lean" towards the mouse
            Vector3 targetPos = _initialPosition + new Vector3(mouseX * leanStrength, mouseY * leanStrength, 0);
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.deltaTime * leanSpeed);
        }
    }
}
