using UnityEngine;

// Mesure des chutes (joueur et ennemis), etape 1 : logs seulement, pas encore de son ni de mort.
// Hauteur de chute locale et reelle : depuis le dernier appui, pas un Y absolu (le Y absolu reste le filet KillBelowY).
// Pas de prediction : un saut au-dessus du vide ne descend pas sous son point de depart, il ne declenche rien.
//   soundHeight atteint  -> son de chute (coupe a l'atterrissage, bruit d'atterrissage si on survit)
//   lethalHeight atteint -> chute mortelle (au contact ou a la fin du son dans le vide)
// Le pivot du perso doit etre a ses pieds (cas du Player et des ennemis).
public class FallTracker : MonoBehaviour
{
    [Header("Chute")]
    [Tooltip("Hauteur de chute (depuis le dernier appui) a partir de laquelle le son de chute demarre")]
    [SerializeField] private float soundHeight = 2f;
    [Tooltip("Hauteur de chute (depuis le dernier appui) a partir de laquelle la chute est mortelle")]
    [SerializeField] private float lethalHeight = 4f;

    [Header("Detection")]
    [Tooltip("Distance sous les pieds dans laquelle un sol compte comme appui")]
    [SerializeField] private float groundCheckDistance = 0.3f;
    [Tooltip("Layers comptes comme appui. Les triggers sont ignores")]
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private const int MaxHits = 16;
    private readonly RaycastHit[] hits = new RaycastHit[MaxHits];

    private float lastSupportY;
    private bool inAir;
    private bool soundStarted;
    private bool lethal;

    public float FallDistance => inAir ? Mathf.Max(0f, lastSupportY - transform.position.y) : 0f;
    public bool IsLethalFall => inAir && lethal;

    private void OnEnable()
    {
        lastSupportY = transform.position.y;
        inAir = false;
        soundStarted = false;
        lethal = false;
    }

    private void FixedUpdate()
    {
        float feetY = transform.position.y;

        if (IsGrounded())
        {
            if (inAir && soundStarted)
                Log(string.Format("atterrissage apres {0:F2} m -> {1}", lastSupportY - feetY,
                    lethal ? "coupe le son, MORT" : "coupe le son, bruit d'atterrissage, survit"));

            lastSupportY = feetY;
            inAir = false;
            soundStarted = false;
            lethal = false;
            return;
        }

        // En l'air : un saut monte au-dessus du dernier appui, la chute se compte depuis l'appui
        inAir = true;
        float drop = lastSupportY - feetY;

        if (!soundStarted && drop >= soundHeight)
        {
            soundStarted = true;
            Log(string.Format("{0:F2} m de chute depuis Y={1:F2} -> son de chute", drop, lastSupportY));
        }

        if (!lethal && drop >= lethalHeight)
        {
            lethal = true;
            Log(string.Format("{0:F2} m de chute -> chute MORTELLE", drop));
        }
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
