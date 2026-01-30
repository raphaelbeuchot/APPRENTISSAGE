using UnityEngine;

public class FootstepController : MonoBehaviour
{
    [Header("Footstep Sounds")]
    [SerializeField] private AudioClip[] footstepSounds;

    [Header("Audio Settings")]
    [SerializeField] private float baseVolume = 0.5f;
    [SerializeField] private float volumeVariation = 0.1f;
    [SerializeField] private float pitchVariation = 0.1f;

    private AudioSource audioSource;

    void Start()
    {
        // Chercher ou creer un AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D sound
        }
    }

    // Methode appelee par les Animation Events
    public void OnFootstep()
    {
        if (footstepSounds == null || footstepSounds.Length == 0)
        {
            Debug.LogWarning("[FootstepController] Aucun son de pas assigne !");
            return;
        }

        // Choisir un son aleatoire
        AudioClip randomClip = footstepSounds[Random.Range(0, footstepSounds.Length)];

        // Variation volume et pitch pour naturalite
        float randomVolume = baseVolume + Random.Range(-volumeVariation, volumeVariation);
        float randomPitch = 1f + Random.Range(-pitchVariation, pitchVariation);

        audioSource.pitch = randomPitch;
        audioSource.PlayOneShot(randomClip, randomVolume);
    }
}