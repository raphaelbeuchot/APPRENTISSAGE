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
    [HideInInspector]
    public GoalDoorNew goalDoor;
    private bool collected = false;
    void Start()
    {
        if (string.IsNullOrEmpty(collectibleID))
        {
            Debug.LogWarning("[Collectible] ID vide sur " + gameObject.name + " !");
            return;
        }
        // Désactivé temporairement : si la clé a été collectée en run précédente,
        // elle serait détruite avant que GoalDoorNew puisse s'injecter dedans.
        // À revoir quand on gère la persistance des clés de porte.
        //if (CollectibleManager.GetOrCreate().IsPermanentlyCollected(collectibleID))
        //{
        //    Destroy(transform.parent.gameObject);
        //    return;
        //}
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
        if (goalDoor != null)
            goalDoor.UnlockWithKey();
        if (idleParticles != null)
            idleParticles.Stop();
        if (collectParticlesPrefab != null)
            Instantiate(collectParticlesPrefab, transform.position, Quaternion.identity);
        Destroy(transform.parent.gameObject);
    }
}