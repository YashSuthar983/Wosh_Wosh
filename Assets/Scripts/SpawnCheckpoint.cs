using UnityEngine;

[DisallowMultipleComponent]
public sealed class SpawnCheckpoint : MonoBehaviour
{
    [SerializeField] private bool activateOnTouch = true;
    [SerializeField] private bool showActivationOverlay = true;

    private bool activated;

    public Vector3 SpawnPosition => transform.position;

    internal void ConfigureRuntimeMarker(bool makeTrigger)
    {
        if (!makeTrigger)
            return;

        Collider markerCollider = GetComponent<Collider>();
        if (markerCollider == null)
            markerCollider = gameObject.AddComponent<BoxCollider>();

        markerCollider.isTrigger = true;
        showActivationOverlay = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryActivate(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision != null)
            TryActivate(collision.collider);
    }

    private void TryActivate(Collider other)
    {
        if (!activateOnTouch || other == null || !IsPlayer(other))
            return;

        SceneRespawnManager.SetCheckpoint(gameObject.scene, SpawnPosition);
        if (!showActivationOverlay || activated)
            return;

        activated = true;
        Heartwell.UI.InGameOverlayUI.ShowCheckpoint();
    }

    private static bool IsPlayer(Collider other)
    {
        if (other.GetComponentInParent<SlimePlayerAbilities>() != null)
            return true;

        return other.GetComponentInChildren<SlimePlayerAbilities>() != null;
    }
}
