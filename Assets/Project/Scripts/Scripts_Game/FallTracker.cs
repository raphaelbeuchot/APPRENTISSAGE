using UnityEngine;

// Chutes du joueur et des ennemis. Etape 2 : sons, pas encore de mort (logs a la place).
// Hauteur de chute locale et reelle : depuis le dernier appui, pas un Y absolu (le Y absolu reste le filet KillBelowY).
// Pas de prediction : un saut au-dessus du vide ne descend pas sous son point de depart, il ne declenche rien.
//   soundHeight atteint  -> son de chute (coupe a l'atterrissage)
//   atterrissage avant lethalHeight -> bruit d'atterrissage, survit
//   atterrissage apres lethalHeight -> splat (plus tard : mort / Cleaned Up)
//   pas d'atterrissage (vide) -> son coupe apres maxFallingDuration (plus tard : Cleaned Up)
// Le pivot du perso doit etre a ses pieds (cas du Player et des ennemis).
public class FallTracker : MonoBehaviour
{
    [Header("Chute")]
    [Tooltip("Hauteur de chute (depuis le dernier appui) a partir de laquelle le son de chute demarre")]
    [SerializeField] private float soundHeight = 2f;
    [Tooltip("Hauteur de chute (depuis le dernier appui) a partir de laquelle la chute est mortelle")]
    [SerializeField] private float lethalHeight = 4f;
    [Tooltip("Dans le vide : duree du son de chute avant de le couper (fin de la chute)")]
    [SerializeField] private float maxFallingDuration = 2.5f;

    [Header("Sons (2D)")]
    [SerializeField] private AudioClip fallingClip;
    [Tooltip("Atterrissage d'une chute non mortelle. Vide = rien")]
    [SerializeField] private AudioClip landingClip;
    [Tooltip("Impact d'une chute mortelle contre un obstacle")]
    [SerializeField] private AudioClip splatClip;

    [Header("Detection")]
    [Tooltip("Distance sous les pieds dans laquelle un sol compte comme appui")]
    [SerializeField] private float groundCheckDistance = 0.3f;
    [Tooltip("Layers comptes comme appui. Les triggers sont ignores")]
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private const int MaxHits = 16;
    private readonly RaycastHit[] hits = new RaycastHit[MaxHits];

    private AudioSource audioSource;
    private float lastSupportY;
    private bool inAir;
    private bool soundStarted;
    private float soundStartTime;
    private bool lethal;
    private bool voidResolved;   // chute dans le vide deja terminee (son coupe)

    public float FallDistance => inAir ? Mathf.Max(0f, lastSupportY - transform.position.y) : 0f;
    public bool IsLethalFall => inAir && lethal;

    private void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f;
        audioSource.playOnAwake = false;
        audioSource.loop = false;
    }

    private void OnEnable()
    {
        ResetFall(transform.position.y);
    }

    private void OnDisable()
    {
        if (audioSource != null) audioSource.Stop();
    }

    private void FixedUpdate()
    {
        float feetY = transform.position.y;

        if (IsGrounded())
        {
            if (inAir && soundStarted && !voidResolved)
                Land(lastSupportY - feetY);

            ResetFall(feetY);
            return;
        }

        // En l'air : un saut monte au-dessus du dernier appui, la chute se compte depuis l'appui
        inAir = true;
        if (voidResolved) return;

        float drop = lastSupportY - feetY;

        if (!soundStarted && drop >= soundHeight)
        {
            soundStarted = true;
            soundStartTime = Time.time;
            if (fallingClip != null)
            {
                audioSource.clip = fallingClip;
                audioSource.Play();
            }
            Log(string.Format("{0:F2} m de chute depuis Y={1:F2} -> son de chute", drop, lastSupportY));
        }

        if (!lethal && drop >= lethalHeight)
        {
            lethal = true;
            Log(string.Format("{0:F2} m de chute -> chute MORTELLE", drop));
        }

        if (soundStarted && lethal && Time.time - soundStartTime >= maxFallingDuration)
        {
            voidResolved = true;
            audioSource.Stop();
            Log(string.Format("vide : son coupe apres {0:F1} s -> ici Cleaned Up", maxFallingDuration));
        }
    }

    private void Land(float drop)
    {
        audioSource.Stop();
        AudioClip clip = lethal ? splatClip : landingClip;
        if (clip != null) audioSource.PlayOneShot(clip);
        Log(string.Format("atterrissage apres {0:F2} m -> {1}", drop,
            lethal ? "splat (ici mort / Cleaned Up)" : "atterrissage, survit"));
    }

    private void ResetFall(float supportY)
    {
        lastSupportY = supportY;
        inAir = false;
        soundStarted = false;
        lethal = false;
        voidResolved = false;
    }

    // Sol juste sous les pieds, en ignorant les colliders du perso lui-meme (ragdoll, balai...)
    private bool IsGrounded()
    {
        const float originOffset = 0.1f;
        Vector3 origin = transform.position + Vector3.up * originOffset;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, hits, groundCheckDistance + originOffset,
            groundMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            if (!hits[i].collider.transform.IsChildOf(transform))
                return true;
        }
        return false;
    }

    private void Log(string message)
    {
        if (showDebugLogs)
            Debug.Log("[FallTracker] " + name + " : " + message);
    }
}
