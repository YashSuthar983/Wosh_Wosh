using UnityEngine;

[DisallowMultipleComponent]
public class SlimeAbsorbableContactRelay : MonoBehaviour
{
    private SlimeAbsorbable owner;

    public void Init(SlimeAbsorbable newOwner)
    {
        owner = newOwner;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (owner != null)
            owner.TryAbsorbFromCollider(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (owner != null)
            owner.TryAbsorbFromCollider(collision.collider);
    }
}
