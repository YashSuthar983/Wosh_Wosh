using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SlimeHazard : MonoBehaviour
{
    [SerializeField] private float damagePerSecond = 0.35f;
    [SerializeField] private bool stretchCanCross = true;
    [SerializeField] private SlimeMaterialType immuneMaterial = SlimeMaterialType.Fireproof;

    private readonly HashSet<SlimePlayerAbilities> touchedThisFrame = new HashSet<SlimePlayerAbilities>();
    private Collider hazardCollider;
    private int processedFrame = -1;

    private void Awake()
    {
        hazardCollider = GetComponent<Collider>();
    }

    private void OnTriggerStay(Collider other)
    {
        DamageSlime(other);
    }

    private void OnCollisionStay(Collision collision)
    {
        DamageSlime(collision.collider);
    }

    private void DamageSlime(Collider other)
    {
        if (processedFrame != Time.frameCount)
        {
            touchedThisFrame.Clear();
            processedFrame = Time.frameCount;
        }

        SlimePlayerAbilities slime = other.GetComponentInParent<SlimePlayerAbilities>();
        if (slime == null || !touchedThisFrame.Add(slime))
            return;

        if ((stretchCanCross && slime.IsStretching) || slime.CurrentMaterial == immuneMaterial)
            return;

        slime.ApplyHazardDamage(damagePerSecond * Time.deltaTime);
    }
}
