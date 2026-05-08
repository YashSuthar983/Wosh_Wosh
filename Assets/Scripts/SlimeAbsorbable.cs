using UnityEngine;

[DisallowMultipleComponent]
public class SlimeAbsorbable : MonoBehaviour
{
    [Header("Material")]
    [SerializeField] private SlimeMaterialType materialType = SlimeMaterialType.Neutral;
    [SerializeField] private Color tint = Color.white;

    [Header("Gains")]
    [SerializeField] private float volumeGain = 0.2f;
    [SerializeField] private float pressureResistanceBonus = 0.25f;
    [SerializeField] private float stretchMultiplierBonus = 0f;
    [SerializeField] private float bridgeDurationBonus = 0f;

    [Header("Pickup")]
    [SerializeField] private bool destroyOnAbsorb = true;

    private bool absorbed;

    public SlimeMaterialType MaterialType => materialType;
    public Color Tint => tint;
    public float VolumeGain => volumeGain;
    public float PressureResistanceBonus => pressureResistanceBonus;
    public float StretchMultiplierBonus => stretchMultiplierBonus;
    public float BridgeDurationBonus => bridgeDurationBonus;

    private void OnTriggerEnter(Collider other)
    {
        TryAbsorb(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryAbsorb(collision.collider);
    }

    private void TryAbsorb(Collider other)
    {
        if (absorbed)
            return;

        SlimePlayerAbilities slime = other.GetComponentInParent<SlimePlayerAbilities>();
        if (slime == null || !slime.TryAbsorb(this))
            return;

        absorbed = true;

        if (destroyOnAbsorb)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }
}
