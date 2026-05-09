using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SlimeSoftBody : MonoBehaviour
{
    [Header("Physics Settings")]
    public int segmentCount = 12;
    public float radius = 1.5f;
    public float stiffness = 100f;
    public float damping = 5f;
    public float mass = 0.5f;

    [Header("Visuals")]
    public int smoothingFactor = 3; // Smooths edges between physics points

    private List<Rigidbody2D> points = new List<Rigidbody2D>();
    private Rigidbody2D centerPoint;
    private Mesh mesh;
    private Vector3[] vertices;
    private int[] triangles;

    private Vector2[] uvs;

    void Start()
    {
        SetupBody();
    }

    void SetupBody()
    {
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;

        // 1. Create Center
        centerPoint = CreatePoint("Center", Vector2.zero, true);

        // 2. Create Outer Circle
        for (int i = 0; i < segmentCount; i++)
        {
            float angle = i * Mathf.PI * 2 / segmentCount;
            Vector2 pos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            var rb = CreatePoint($"Point_{i}", pos, false);
            points.Add(rb);

            // Connect to center
            Connect(rb, centerPoint);
        }

        // 3. Connect Neighbors (Outer Ring)
        for (int i = 0; i < segmentCount; i++)
        {
            int next = (i + 1) % segmentCount;
            Connect(points[i], points[next]);
        }

        SetupMesh();
    }

    Rigidbody2D CreatePoint(string name, Vector2 pos, bool isCenter)
    {
        GameObject go = new GameObject(name);
        go.transform.parent = transform;
        go.transform.localPosition = pos;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.mass = mass;
        rb.drag = 1f;
        rb.angularDrag = 1f;
        rb.gravityScale = 0f; // Floating slime

        go.AddComponent<CircleCollider2D>().radius = 0.1f;

        return rb;
    }

    void Connect(Rigidbody2D a, Rigidbody2D b)
    {
        var joint = a.gameObject.AddComponent<SpringJoint2D>();
        joint.connectedBody = b;
        joint.distance = Vector2.Distance(a.transform.localPosition, b.transform.localPosition);
        joint.frequency = stiffness / 100f;
        joint.dampingRatio = damping / 10f;
        joint.enableCollision = false;
    }

    void SetupMesh()
    {
        vertices = new Vector3[segmentCount + 1];
        uvs = new Vector2[segmentCount + 1];
        triangles = new int[segmentCount * 3];

        for (int i = 0; i < segmentCount; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = (i + 1 == segmentCount) ? 1 : i + 2;
            triangles[i * 3 + 2] = i + 1;
        }
    }

    void Update()
    {
        UpdateMesh();
    }

    void UpdateMesh()
    {
        vertices[0] = centerPoint.transform.localPosition;
        uvs[0] = new Vector2(0.5f, 0.5f);

        for (int i = 0; i < segmentCount; i++)
        {
            vertices[i + 1] = points[i].transform.localPosition;
            
            // Map position to 0-1 range for UVs
            float x = (vertices[i + 1].x / (radius * 2f)) + 0.5f;
            float y = (vertices[i + 1].y / (radius * 2f)) + 0.5f;
            uvs[i + 1] = new Vector2(x, y);
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}
