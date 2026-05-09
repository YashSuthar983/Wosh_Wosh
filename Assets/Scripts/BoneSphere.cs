using UnityEngine;

public class BoneSphere : MonoBehaviour
{
    
    [Header("Bones")]
    public GameObject root = null;
    public GameObject x = null;
    public GameObject x2 = null;
    public GameObject y = null;
    public GameObject y2 = null;
    public GameObject z = null;
    public GameObject z2 = null;
    [Header("Spring Joint Settings")]
    [Tooltip("Strength of spring")]
    public float Spring = 100f;
    [Tooltip("Higher the value the faster the spring oscillation stops")]
    public float Damper = 4f;
    [Header("Other Settings")]
    public Softbody.ColliderShape Shape = Softbody.ColliderShape.Sphere;
    public float ColliderSize = 0.002f;
    public float RigidbodyMass = 1f;
    public LineRenderer PrefabLine = null;
    public bool ViewLines = true;
    [Header("Contact Physics")]
    [Tooltip("Optional material for slime colliders. If empty, a low-friction runtime material is used.")]
    public PhysicMaterial SurfaceMaterial = null;
    [Tooltip("Contact offset as a fraction of collider size. Keeping this small prevents tiny bone colliders from becoming an invisible wider shell.")]
    public float ContactOffsetScale = 0.25f;
    public float MinContactOffset = 0.0002f;
    public float MaxContactOffset = 0.01f;
    public CollisionDetectionMode CollisionDetection = CollisionDetectionMode.ContinuousDynamic;
    public int SolverIterations = 12;
    public int SolverVelocityIterations = 4;

    private void Start()
    {
        Softbody.Init(Shape, ColliderSize, RigidbodyMass, Spring, Damper, RigidbodyConstraints.FreezeRotation, PrefabLine, ViewLines);
        Softbody.ConfigureContactPhysics(SurfaceMaterial, ContactOffsetScale, MinContactOffset, MaxContactOffset, CollisionDetection, SolverIterations, SolverVelocityIterations);

        Softbody.AddCollider(ref root, Softbody.ColliderShape.Sphere, 0.005f, 6f);
        Softbody.AddCollider(ref x);
        Softbody.AddCollider(ref x2);
        Softbody.AddCollider(ref y);
        Softbody.AddCollider(ref y2);
        Softbody.AddCollider(ref z);
        Softbody.AddCollider(ref z2);

        Softbody.AddSpring(ref x, ref root);
        Softbody.AddSpring(ref x2, ref root);
        Softbody.AddSpring(ref y, ref root);
        Softbody.AddSpring(ref y2, ref root);
        Softbody.AddSpring(ref z, ref root);
        Softbody.AddSpring(ref z2, ref root);
    }
}
