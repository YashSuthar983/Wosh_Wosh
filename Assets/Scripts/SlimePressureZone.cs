using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SlimePressureZone : MonoBehaviour
{
    [SerializeField] private float requiredResistance = 2f;
    [SerializeField] private float damagePerSecond = 0.4f;
    [SerializeField] private float pushForce = 12f;
    [SerializeField] private Vector3 pressureDirection = Vector3.down;

    private readonly HashSet<SlimePlayerAbilities> pressedThisFrame = new HashSet<SlimePlayerAbilities>();
    private int processedFrame = -1;

    private void OnTriggerStay(Collider other)
    {
        if (processedFrame != Time.frameCount)
        {
            pressedThisFrame.Clear();
            processedFrame = Time.frameCount;
        }

        SlimePlayerAbilities slime = other.GetComponentInParent<SlimePlayerAbilities>();
        if (slime == null || !pressedThisFrame.Add(slime))
            return;

        slime.HandlePressure(requiredResistance, damagePerSecond * Time.deltaTime, pressureDirection, pushForce);
    }
}
