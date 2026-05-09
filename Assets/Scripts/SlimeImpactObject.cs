using UnityEngine;

[DisallowMultipleComponent]
public class SlimeImpactObject : MonoBehaviour
{
    [SerializeField] private bool canMoveFromSlimeImpact = true;
    [SerializeField, Min(0f)] private float groundedImpactThreshold = 2.4f;
    [SerializeField, Min(0f)] private float airImpactThreshold = 1.4f;
    [SerializeField, Min(0f)] private float impactStrength = 1f;
    [SerializeField, Min(0f)] private float maxImpactImpulse = 28f;
    [SerializeField, Range(0f, 1f)] private float upwardLift = 0.08f;
    [SerializeField, Min(0f)] private float slimeRecoilMultiplier = 1f;
    [SerializeField] private bool scaleWithSlimeVolume = true;
    [SerializeField] private bool scaleWithOwnMass = true;

    public bool CanMoveFromSlimeImpact => canMoveFromSlimeImpact;
    public float UpwardLift => Mathf.Clamp01(upwardLift);
    public float SlimeRecoilMultiplier => Mathf.Max(0f, slimeRecoilMultiplier);
    public bool ScaleWithSlimeVolume => scaleWithSlimeVolume;
    public bool ScaleWithOwnMass => scaleWithOwnMass;

    public float GetRequiredImpactSpeed(bool slimeGrounded)
    {
        return Mathf.Max(0f, slimeGrounded ? groundedImpactThreshold : airImpactThreshold);
    }

    public float ModifyImpactImpulse(float impulse)
    {
        float modifiedImpulse = Mathf.Max(0f, impulse) * Mathf.Max(0f, impactStrength);
        return maxImpactImpulse > 0f ? Mathf.Min(modifiedImpulse, maxImpactImpulse) : modifiedImpulse;
    }
}
