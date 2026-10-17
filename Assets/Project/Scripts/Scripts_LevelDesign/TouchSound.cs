using UnityEngine;

// Joue un son quand un joueur (solo ou multi) touche cet objet : collider normal (contact) ou trigger (entree).
public class TouchSound : MonoBehaviour
{
    [SerializeField] private AudioClip[] sounds;
    [SerializeField] private float volume = 1f;
    [Tooltip("Variation de hauteur au hasard (0 = aucune)")]
    [Range(0f, 0.5f)]
    [SerializeField] private float pitchVariation = 0.05f;
    [Tooltip("Delai minimal entre deux sons, en secondes")]
    [SerializeField] private float cooldown = 0.5f;
    [Tooltip("Coche : ne joue qu'au premier contact")]
    [SerializeField] private bool playOnce = false;

    private AudioSource audioSource;
    private float lastPlayTime = -999f;
    private bool hasPlayed = false;

    void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
    }

    void OnCollisionEnter(Collision collision) => TryPlay(collision.collider);

    void OnTriggerEnter(Collider other) => TryPlay(other);

    private void TryPlay(Collider other)
    {
        if (sounds == null || sounds.Length == 0) return;
        if (playOnce && hasPlayed) return;
        if (Time.time - lastPlayTime < cooldown) return;
        if (other.GetComponentInParent<PlayerPhysicsMovement>() == null) return;

        lastPlayTime = Time.time;
        hasPlayed = true;
        audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        audioSource.PlayOneShot(sounds[Random.Range(0, sounds.Length)], volume);
    }
}
