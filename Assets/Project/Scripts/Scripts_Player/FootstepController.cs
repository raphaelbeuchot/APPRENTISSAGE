using UnityEngine;
public class FootstepController : MonoBehaviour
{
    [Header("Footstep Sounds")]
    [Tooltip("Sons par defaut : sol sans composant Surface, ou SurfaceType sans sons.")]
    [SerializeField] private AudioClip[] footstepSounds;
    [Header("Audio Settings")]
    [SerializeField] private float baseVolume = 0.5f;
    [SerializeField] private float volumeVariation = 0.1f;
    [SerializeField] private float pitchVariation = 0.1f;
    [SerializeField] private float minTimeBetweenSteps = 0.10f;
    [Header("Surfaces")]
    [Tooltip("Longueur du rayon qui cherche la surface sous les pieds (part de 0.2 m au-dessus des pieds).")]
    [SerializeField] private float surfaceRayLength = 0.5f;
    private const float SurfaceRayOriginHeight = 0.2f;
    private AudioSource audioSource;
    private float lastFootstepTime = 0f;
    private Rigidbody playerRb;
    private int surfaceMask;
    private readonly RaycastHit[] surfaceHits = new RaycastHit[8];
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
        // Le sol, pas les personnages ni les corps
        surfaceMask = ~LayerMask.GetMask("Human", "Zombie", "Corpse", "Swarm", "Ignore Raycast");
    }
    public void OnFootstep()
    {
        if (Time.time - lastFootstepTime < minTimeBetweenSteps)
            return;
        if (playerRb != null && playerRb.linearVelocity.magnitude < 0.1f)
            return;
        lastFootstepTime = Time.time;

        SurfaceType surface = FindSurfaceUnderFeet();
        AudioClip[] clips = surface != null && surface.footstepSounds != null && surface.footstepSounds.Length > 0
            ? surface.footstepSounds
            : footstepSounds;
        float volumeMultiplier = surface != null ? surface.volumeMultiplier : 1f;

        if (clips == null || clips.Length == 0)
        {
            Debug.LogWarning("[FootstepController] Aucun son de pas assigne !");
            return;
        }
        AudioClip randomClip = clips[Random.Range(0, clips.Length)];
        float randomVolume = (baseVolume + Random.Range(-volumeVariation, volumeVariation)) * volumeMultiplier;
        float randomPitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        audioSource.pitch = randomPitch;
        audioSource.PlayOneShot(randomClip, randomVolume);
    }

    // Collider le plus proche sous les pieds (hors joueur lui-meme), puis son Surface sur lui ou un parent
    private SurfaceType FindSurfaceUnderFeet()
    {
        Transform root = playerRb != null ? playerRb.transform : transform.root;
        Vector3 origin = root.position + Vector3.up * SurfaceRayOriginHeight;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, surfaceHits,
            SurfaceRayOriginHeight + surfaceRayLength, surfaceMask, QueryTriggerInteraction.Ignore);

        Collider ground = null;
        float closest = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = surfaceHits[i];
            if (hit.transform.IsChildOf(root)) continue;
            if (hit.distance < closest)
            {
                closest = hit.distance;
                ground = hit.collider;
            }
        }
        if (ground == null) return null;

        Surface surface = ground.GetComponentInParent<Surface>();
        return surface != null ? surface.Type : null;
    }
}
