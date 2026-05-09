using UnityEngine;

public class SlimeInteraction : MonoBehaviour
{
    public float forceAmount = 10f;
    public float dragAmount = 5f;

    private Rigidbody2D selectedPoint;
    private Camera mainCam;

    void Start()
    {
        mainCam = Camera.main;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            TrySelectPoint();
        }

        if (Input.GetMouseButtonUp(0))
        {
            selectedPoint = null;
        }

        if (selectedPoint != null)
        {
            DragPoint();
        }
        else
        {
            PulseOnHover();
        }
    }

    void TrySelectPoint()
    {
        Vector2 mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);

        if (hit.collider != null && hit.collider.GetComponent<Rigidbody2D>())
        {
            selectedPoint = hit.collider.GetComponent<Rigidbody2D>();
        }
    }

    void DragPoint()
    {
        Vector2 mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 direction = mousePos - (Vector2)selectedPoint.transform.position;
        
        // Smoother drag with a spring-like force
        selectedPoint.velocity = Vector2.Lerp(selectedPoint.velocity, direction * dragAmount, Time.deltaTime * 10f);
    }

    void PulseOnHover()
    {
        Vector2 mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        Collider2D[] colliders = Physics2D.OverlapCircleAll(mousePos, 0.8f);

        foreach (var col in colliders)
        {
            var rb = col.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                Vector2 diff = (Vector2)col.transform.position - mousePos;
                float dist = diff.magnitude;
                float force = (1f - Mathf.Clamp01(dist / 0.8f)) * forceAmount;
                rb.AddForce(diff.normalized * force * Time.deltaTime, ForceMode2D.Impulse);
            }
        }
    }
}
