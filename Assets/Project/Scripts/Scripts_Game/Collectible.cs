using UnityEngine;

public class Collectible : MonoBehaviour
{
    [Header("Identifiant unique - ex: L1_Star_01")]
    public string collectibleID;

    [Header("Feedback")]
    public AudioClip collectSound;
    public GameObject collectParticlesPrefab;

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

        if (collectSound != null)
            AudioSource.PlayClipAtPoint(collectSound, transform.position);

        if (collectParticlesPrefab != null)
            Instantiate(collectParticlesPrefab, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }
}