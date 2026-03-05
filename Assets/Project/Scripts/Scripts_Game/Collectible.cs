using UnityEngine;

public class Collectible : MonoBehaviour
{
    [Header("Identifiant unique - ex: L1_Star_01")]
    public string collectibleID;

    [Header("Feedback")]
    public AudioClip collectSound;
    public GameObject collectParticlesPrefab;
    [Header("Animation")]
    public float rotationSpeed = 90f;

    [Header("References")]
    public ParticleSystem idleParticles;


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
    void Update()
    {
        transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f);
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

        CollectibleManager.GetOrCreate().PlayCollectSound(collectSound);

        if (idleParticles != null)
        {
            idleParticles.Stop();
        }

        if (collectParticlesPrefab != null)
        {
            Instantiate(collectParticlesPrefab, transform.position, Quaternion.identity);
        }
        Destroy(gameObject);
    }
}