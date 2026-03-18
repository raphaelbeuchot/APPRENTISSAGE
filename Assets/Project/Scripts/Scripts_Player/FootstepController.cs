using UnityEngine;
public class FootstepController : MonoBehaviour
{
    [Header("Footstep Sounds")]
    [SerializeField] private AudioClip[] footstepSounds;
    [Header("Audio Settings")]
    [SerializeField] private float baseVolume = 0.5f;
    [SerializeField] private float volumeVariation = 0.1f;
    [SerializeField] private float pitchVariation = 0.1f;
    [SerializeField] private float minTimeBetweenSteps = 0.10f;
    private AudioSource audioSource;
    private float lastFootstepTime = 0f;
    private Rigidbody playerRb;
    void Start()
    {
        playerRb = GetComponentInParent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }
    }
    public void OnFootstep()
    {
        if (Time.time - lastFootstepTime < minTimeBetweenSteps)
            return;
        if (playerRb != null && playerRb.linearVelocity.magnitude < 0.1f)
            return;
        lastFootstepTime = Time.time;
        if (footstepSounds == null || footstepSounds.Length == 0)
        {
            Debug.LogWarning("[FootstepController] Aucun son de pas assigne !");
            return;
        }
        AudioClip randomClip = footstepSounds[Random.Range(0, footstepSounds.Length)];
        float randomVolume = baseVolume + Random.Range(-volumeVariation, volumeVariation);
        float randomPitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        audioSource.pitch = randomPitch;
        audioSource.PlayOneShot(randomClip, randomVolume);
    }
}