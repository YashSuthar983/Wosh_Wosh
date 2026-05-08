using UnityEngine;

[DisallowMultipleComponent]
public class SlimeBridgeSegment : MonoBehaviour
{
    private SlimePlayerAbilities owner;
    private float releaseVolume;
    private float endTime;
    private bool released;

    public void Init(SlimePlayerAbilities newOwner, float duration, float reservedVolume)
    {
        owner = newOwner;
        releaseVolume = reservedVolume;
        endTime = Time.time + Mathf.Max(0.05f, duration);
    }

    private void Update()
    {
        if (Time.time >= endTime)
            Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (released)
            return;

        released = true;

        if (owner != null)
            owner.ReleaseBridgeVolume(releaseVolume);
    }
}
