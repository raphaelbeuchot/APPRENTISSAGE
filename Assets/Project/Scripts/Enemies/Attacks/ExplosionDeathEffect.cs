using UnityEngine;
using System.Collections;

public class ExplosionDeathEffect : MonoBehaviour, IDeathEffect
{
    [HideInInspector] public GameObject explosionVFX;
    [HideInInspector] public AudioClip explosionSound;
    [HideInInspector] public float soundVolume = 1f;
    [HideInInspector] public float vfxScale = 1f;
    [HideInInspector] public float swellDuration = 1.5f;
    [HideInInspector] public float swellScale = 1.5f;

    private Vector3 originalScale;
    private bool isSwelling = false;

    void Start()
    {
        originalScale = transform.localScale;
    }

    public void OnDeath(Vector3 deathPosition, DeathContext context)
    {
        if (!isSwelling)
        {
            StartCoroutine(SwellAndExplodeCoroutine(deathPosition));
        }
    }

    private IEnumerator SwellAndExplodeCoroutine(Vector3 deathPosition)
    {
        isSwelling = true;

        // Phase 1 : Gonflement progressif
        float elapsed = 0f;
        Vector3 targetScale = originalScale * swellScale;

        while (elapsed < swellDuration)
        {
            float t = elapsed / swellDuration;
            transform.localScale = Vector3.Lerp(originalScale, targetScale, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localScale = targetScale;

        // Phase 2 : Explosion (VFX + son)
        if (explosionVFX != null)
        {
            GameObject vfx = Instantiate(explosionVFX, deathPosition, Quaternion.identity);
            vfx.transform.localScale = Vector3.one * vfxScale;
            Destroy(vfx, 3f);
        }

        if (explosionSound != null)
        {
            AudioSource.PlayClipAtPoint(explosionSound, deathPosition, soundVolume);
        }

        Debug.Log("BOOM! Bloated exploded after swelling!");

        // Phase 3 : Notifier les comportements de mort (spawn swarm)
        IOnDeathBehavior[] deathBehaviors = GetComponents<IOnDeathBehavior>();
        if (deathBehaviors != null && deathBehaviors.Length > 0)
        {
            foreach (IOnDeathBehavior behavior in deathBehaviors)
            {
                behavior.OnEnemyDeath(deathPosition);
            }
        }

        // Detruire le GameObject du Bloated
        Destroy(gameObject);
    }
}