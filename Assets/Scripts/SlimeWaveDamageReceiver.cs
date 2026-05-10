using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class SlimeWaveDamageReceiver : MonoBehaviour
{
    [SerializeField] private float maxHealth = 1f;
    [SerializeField] private float damageMultiplier = 1f;
    [SerializeField] private MonoBehaviour[] disableBehavioursOnDefeat = null;
    [SerializeField] private Collider[] disableCollidersOnDefeat = null;
    [SerializeField] private bool scaleDownOnDefeat = true;
    [SerializeField] private float defeatScaleDuration = 0.35f;
    [SerializeField] private bool destroyOnDefeat = true;
    [SerializeField] private float defeatDestroyDelay = 0.45f;

    private Vector3 initialScale;
    private float currentHealth;
    private bool defeated;

    public bool IsDefeated => defeated;

    private void Awake()
    {
        initialScale = transform.localScale;
        currentHealth = Mathf.Max(0.01f, maxHealth);
    }

    public void ApplyWaveHit(float damage, Vector3 sourcePosition)
    {
        if (defeated)
            return;

        float appliedDamage = Mathf.Max(0f, damage) * Mathf.Max(0f, damageMultiplier);
        if (appliedDamage <= 0f)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - appliedDamage);
        if (currentHealth <= 0f)
            Defeat();
    }

    private void Defeat()
    {
        defeated = true;
        DisableDefeatedComponents();

        if (isActiveAndEnabled)
            StartCoroutine(DefeatRoutine());
        else if (destroyOnDefeat)
            Destroy(gameObject, Mathf.Max(0f, defeatDestroyDelay));
    }

    private void DisableDefeatedComponents()
    {
        if (disableBehavioursOnDefeat != null)
        {
            for (int i = 0; i < disableBehavioursOnDefeat.Length; i++)
            {
                if (disableBehavioursOnDefeat[i] != null)
                    disableBehavioursOnDefeat[i].enabled = false;
            }
        }

        if (disableCollidersOnDefeat != null)
        {
            for (int i = 0; i < disableCollidersOnDefeat.Length; i++)
            {
                if (disableCollidersOnDefeat[i] != null)
                    disableCollidersOnDefeat[i].enabled = false;
            }
        }
    }

    private IEnumerator DefeatRoutine()
    {
        float duration = scaleDownOnDefeat ? Mathf.Max(0.01f, defeatScaleDuration) : 0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = 1f - t;
            transform.localScale = initialScale * scale;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (scaleDownOnDefeat)
            transform.localScale = Vector3.zero;

        if (destroyOnDefeat)
            Destroy(gameObject, Mathf.Max(0f, defeatDestroyDelay - duration));
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(0.01f, maxHealth);
        damageMultiplier = Mathf.Max(0f, damageMultiplier);
        defeatScaleDuration = Mathf.Max(0.01f, defeatScaleDuration);
        defeatDestroyDelay = Mathf.Max(0f, defeatDestroyDelay);
    }
}
