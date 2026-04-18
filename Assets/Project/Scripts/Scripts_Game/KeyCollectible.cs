using UnityEngine;

[RequireComponent(typeof(Collider))]
public class KeyCollectible : MonoBehaviour
{
    [Header("References")]
    public GoalDoorNew goalDoor;

    [Header("Feedback")]
    public AudioClip collectSound;
    public GameObject collectParticlesPrefab;

    [Header("Animation")]
    public float rotationSpeed = 90f;

    private bool collected = false;
    private AudioSource audioSource;

    void Start()
    {
        GetComponent<Collider>().isTrigger = true;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        if (goalDoor == null)
            Debug.LogWarning("[KeyCollectible] GoalDoor non assignee!");
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

        if (goalDoor != null)
            goalDoor.UnlockWithKey();

        if (collectParticlesPrefab != null)
            Instantiate(collectParticlesPrefab, transform.position, Quaternion.identity);

        if (collectSound != null)
        {
            GameObject tempAudio = new GameObject("TempAudio_KeyCollect");
            tempAudio.transform.position = transform.position;
            AudioSource tempAS = tempAudio.AddComponent<AudioSource>();
            tempAS.clip = collectSound;
            tempAS.spatialBlend = 0f;
            tempAS.Play();
            Destroy(tempAudio, collectSound.length + 0.1f);
        }

        Destroy(gameObject);
    }
}