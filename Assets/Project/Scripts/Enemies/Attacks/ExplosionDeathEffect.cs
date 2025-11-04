using UnityEngine;

public class ExplosionDeathEffect : MonoBehaviour, IDeathEffect
{
    [HideInInspector] public GameObject explosionVFX;
    [HideInInspector] public AudioClip explosionSound;
    [HideInInspector] public float soundVolume = 1f;
    [HideInInspector] public float vfxScale = 1f;

    public void OnDeath(Vector3 deathPosition, DeathContext context)
    {
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

        Debug.Log("BOOM! Bloated exploded!");
    }
}