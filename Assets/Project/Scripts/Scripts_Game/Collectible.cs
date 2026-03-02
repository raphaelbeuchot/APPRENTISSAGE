using UnityEngine;

public class Collectible : MonoBehaviour
{
    [Header("Identifiant unique - ex: L1_Star_01")]
    public string collectibleID;

    [Header("Feedback")]
    public AudioClip collectSound;
    public GameObject collectParticlesPrefab;

    [Header("Audio")]
    public AudioSource audioSource2D; // AudioSource non spatialise (2D)

    private bool collected = false;

    void Start()
    {
        if (string.IsNullOrEmpty(collectibleID))
        {
            Debug.LogWarning("[Collectible] ID vide sur " + gameObject.name + " !");
            return;
        }

        if (CollectibleManager.GetOrCreate().IsPermanentlyCollected(collectibleID))
        {
            Destroy(gameObject);
            return;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        if (other.gameObject.layer != LayerMask.NameToLayer("Human")) return;
        Collect();
    }

    void Collect()
    {
        collected = true;

        CollectibleManager.GetOrCreate().CollectThisRun(collectibleID);

        if (collectSound != null && audioSource2D != null)
        {
            audioSource2D.PlayOneShot(collectSound);
        }

        if (collectParticlesPrefab != null)
        {
            Instantiate(collectParticlesPrefab, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}