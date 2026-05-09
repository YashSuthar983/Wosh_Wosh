using UnityEngine;

[DisallowMultipleComponent]
public class SlimeImpactBodyPart : MonoBehaviour
{
    private SlimeMovementController owner;
    private Rigidbody body;

    public void Init(SlimeMovementController newOwner, Rigidbody newBody)
    {
        owner = newOwner;
        body = newBody != null ? newBody : GetComponent<Rigidbody>();
    }

    private void Awake()
    {
        if (body == null)
            body = GetComponent<Rigidbody>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (owner != null)
            owner.HandleBodyPartImpact(collision, body);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (owner != null)
            owner.HandleBodyPartImpact(collision, body);
    }
}
