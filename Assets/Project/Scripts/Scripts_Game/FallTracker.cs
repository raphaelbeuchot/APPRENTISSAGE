using UnityEngine;

// Chutes du joueur, des ennemis et de leurs cadavres.
// Hauteur de chute locale et reelle : depuis le dernier appui, pas un Y absolu (le Y absolu reste le filet KillBelowY).
// Pas de prediction : un saut au-dessus du vide ne descend pas sous son point de depart, il ne declenche rien.
//   soundHeight atteint  -> son de chute (coupe a l'atterrissage)
//   atterrissage avant lethalHeight -> bruit d'atterrissage, survit
//   atterrissage apres lethalHeight -> splat, puis mort du joueur / Cleaned Up de l'ennemi
//   pas d'atterrissage (vide) -> son coupe apres maxFallingDuration, puis Cleaned Up de l'ennemi
//                                (joueur : rien ici, c'est KillBelowY qui le tue)
// Le pivot du perso doit etre a ses pieds (cas du Player et des ennemis). Cadavre ragdoll : la racine reste
// kinematique sur place, c'est la Hips qui tombe, on la suit a la place.
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

    [Header("Mort (joueur)")]
    [Tooltip("Mort transmise a PlayerHealth sur une chute mortelle contre un obstacle (ex. Death_Fall). Vide = mort generique")]
    [SerializeField] private DeathCause deathCause;

    [Header("Detection")]
    [Tooltip("Distance sous les pieds dans laquelle un sol compte comme appui")]
    [SerializeField] private float groundCheckDistance = 0.3f;
    [Tooltip("Pareil pour un cadavre ragdoll, mesure sous la Hips (plus haute que les pieds une fois couche)")]
    [SerializeField] private float corpseGroundCheckDistance = 0.5f;
    [Tooltip("Layers comptes comme appui. Les triggers sont ignores")]
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private const int MaxHits = 16;
    private readonly RaycastHit[] hits = new RaycastHit[MaxHits];

    private AudioSource audioSource;
    private PlayerHealth playerHealth;
    private EnemyHealth enemyHealth;
    private Rigidbody rootRb;

    private Transform tracked;    // racine, ou Hips d'un cadavre ragdoll
    private bool trackingHips;
    private bool playerWasDead;
    private bool cleaned;         // ennemi nettoye : plus rien a suivre

    private float lastSupportY;
    private bool inAir;
    private bool soundStarted;
    private float soundStartTime;
    private bool lethal;
    private bool voidResolved;    // chute dans le vide deja terminee (son coupe)

    public float FallDistance => inAir ? Mathf.Max(0f, lastSupportY - tracked.position.y) : 0f;
    public bool IsLethalFall => inAir && lethal;

    private void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f;
        audioSource.playOnAwake = false;
        audioSource.loop = false;

        playerHealth = GetComponent<PlayerHealth>();
        enemyHealth = GetComponent<EnemyHealth>();
        rootRb = GetComponent<Rigidbody>();
        tracked = transform;
    }

    private void OnEnable()
    {
        ResetFall(tracked.position.y);
    }

    private void OnDisable()
    {
        if (audioSource != null) audioSource.Stop();
    }

    private void FixedUpdate()
    {
        if (cleaned) return;

        // Joueur mort : on ne suit plus (sequence de mort). Au respawn (multi), on repart de zero sans son.
        if (playerHealth != null)
        {
            if (playerHealth.IsDead())
            {
                playerWasDead = true;
                // Tue par KillBelowY en plein vide : le son de chute s'arrete quand meme a sa duree
                if (soundStarted && !voidResolved && Time.time - soundStartTime >= maxFallingDuration)
                {
                    voidResolved = true;
                    audioSource.Stop();
                }
                return;
            }
            if (playerWasDead)
            {
                playerWasDead = false;
                audioSource.Stop();
                ResetFall(tracked.position.y);
            }
        }

        if (!trackingHips && enemyHealth != null && enemyHealth.IsDead())
            TrySwitchToHips();

        float feetY = tracked.position.y;

        if (IsGrounded())
        {
            if (inAir && soundStarted && !voidResolved)
                Land(lastSupportY - feetY);

            if (cleaned) return;
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
            Log(string.Format("vide : son coupe apres {0:F1} s", maxFallingDuration));
            if (enemyHealth != null)
                CleanUpEnemy();
        }
    }

    private void Land(float drop)
    {
        audioSource.Stop();
        AudioClip clip = lethal ? splatClip : landingClip;
        if (clip != null)
        {
            // Ennemi nettoye = detruit 0,5 s apres : le splat joue sur un objet a part pour ne pas etre coupe
            if (lethal && enemyHealth != null)
                PlayDetached(clip);
            else
                audioSource.PlayOneShot(clip);
        }
        Log(string.Format("atterrissage apres {0:F2} m -> {1}", drop, lethal ? "splat, MORT" : "survit"));

        if (!lethal) return;

        if (playerHealth != null)
            playerHealth.Kill(deathCause);
        else if (enemyHealth != null)
            CleanUpEnemy();
    }

    // Meme fin que la lave (SplinePitFillController.CheckAndCleanup) : mort par fosse si encore vivant,
    // puis CorpsePitHandler -> Cleaned Up (credit, icone, son, texte) et destruction
    private void CleanUpEnemy()
    {
        cleaned = true;

        if (!enemyHealth.IsDead())
        {
            enemyHealth.deathByPit = true;
            enemyHealth.TakeMeleeDamage(enemyHealth.GetMaxHealth() * 10f);
        }

        CorpsePitHandler handler = GetComponent<CorpsePitHandler>();
        if (handler == null)
            handler = gameObject.AddComponent<CorpsePitHandler>();
        handler.sourceEnemy = enemyHealth;
        handler.OnEnterPit(PitContentType.ContentCategory.InstantKill);
    }

    // Cadavre ragdoll (RagdollDeathEffect) : racine kinematique, la Hips porte la chute.
    // Une chute deja mortelle le reste (le tueur en plein vol ne la remet pas a zero).
    private void TrySwitchToHips()
    {
        if (rootRb == null || !rootRb.isKinematic) return;

        // Le modele s'appelle TPose ou TPose_BLDR selon l'ennemi : on cherche la Hips par son nom,
        // sinon le premier os a Rigidbody (meme repli que RagdollDeathEffect)
        Transform hips = null;
        Rigidbody firstBone = null;
        foreach (Rigidbody bone in GetComponentsInChildren<Rigidbody>())
        {
            if (bone == rootRb) continue;
            if (firstBone == null) firstBone = bone;
            if (bone.name.EndsWith("Hips")) { hips = bone.transform; break; }
        }
        if (hips == null && firstBone != null) hips = firstBone.transform;
        if (hips == null) return;

        tracked = hips;
        trackingHips = true;
        Log("mort en ragdoll : suivi de " + hips.name);
    }

    private static void PlayDetached(AudioClip clip)
    {
        GameObject go = new GameObject("FallTracker_Sound");
        AudioSource source = go.AddComponent<AudioSource>();
        source.spatialBlend = 0f;
        source.PlayOneShot(clip);
        Destroy(go, clip.length + 0.1f);
    }

    private void ResetFall(float supportY)
    {
        lastSupportY = supportY;
        inAir = false;
        soundStarted = false;
        lethal = false;
        voidResolved = false;
    }

    // Sol juste sous le point suivi, en ignorant les colliders du perso lui-meme (ragdoll, balai...)
    private bool IsGrounded()
    {
        const float originOffset = 0.1f;
        float checkDistance = trackingHips ? corpseGroundCheckDistance : groundCheckDistance;
        Vector3 origin = tracked.position + Vector3.up * originOffset;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, hits, checkDistance + originOffset,
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
