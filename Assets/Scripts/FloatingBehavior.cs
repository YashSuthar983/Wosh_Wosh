using UnityEngine;

[DisallowMultipleComponent]
public class FloatingBehavior : MonoBehaviour
{
    [Header("Float")]
    [SerializeField, Min(0f)] private float amplitude = 0.05f;
    [SerializeField, Min(0f)] private float frequency = 0.50f;
    [SerializeField, Range(0f, 1f)] private float phaseOffset = 0f;

    private Vector3 baseLocalPosition;

    private void OnEnable()
    {
        baseLocalPosition = transform.localPosition;
    }

    private void Update()
    {
        float wave = Mathf.Sin((Time.time * frequency + phaseOffset) * Mathf.PI * 2f);
        transform.localPosition = baseLocalPosition + Vector3.up * (wave * amplitude);
    }
}
