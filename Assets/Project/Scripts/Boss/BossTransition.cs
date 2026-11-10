using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Entre-phases du boss de Rush Hour IV. Flow complet : BOSS_RUSH_HOUR_IV.md, « Flow du niveau ».
/// Begin (appele par le BossPhaseManager au dernier pupitre d'une phase, avec la cage de ce pupitre) :
/// - tout le monde est fige : le joueur sur place (plus de deplacement ni de commandes, la gravite continue),
///   les ennemis vivants et cadavres pour de bon (gel dedie : ils ne reviennent jamais) ;
/// - plan sentinelle (camera dediee) pendant "Sentinel Shot Duration", joueur pose dans la cage ;
/// - retour a la camera normale, la cage monte avec le joueur -> onCageRaised.
/// La suite s'enchaine par les evenements (Inspector) : RemoveEnemies (montee des ennemis figes),
/// pupitres et zombies de la phase qui descendent, puis LowerCage : la cage redescend, relache le joueur,
/// et la transition se termine (Ended -> reprise du cycle).
/// EndPlayerFreeze rend le joueur a la main (menu du composant), sans passer par la cage.
/// </summary>
public class BossTransition : MonoBehaviour
{
    [Header("Plan sentinelle")]
    [Tooltip("Camera qui regarde la sentinelle au dernier pupitre. Vide = pas de changement de plan.")]
    [SerializeField] private CinemachineCamera sentinelCamera;
    [Tooltip("Doit rester au-dessus de 20 (priorite de la vue active dans CameraPanningExtension).")]
    [SerializeField] private int cameraPriority = 30;
    [Tooltip("Duree du plan (s), fondu compris : laisser le temps de voir l'explosion du cercle (onSentinelShotLive). Puis retour a la camera normale et montee de la cage.")]
    [Min(0f)] [SerializeField] private float sentinelShotDuration = 2f;
    [Tooltip("Camera de la remise en place (fixe, en hauteur), montree apres le plan sentinelle, pendant la montee " +
             "de la cage, l'installation et la descente ; retiree quand le joueur reprend la main. Vide = retour a la camera normale.")]
    [SerializeField] private CinemachineCamera installCamera;

    [Header("Ennemis (remplacant provisoire des cages)")]
    [Tooltip("Hauteur de montee des ennemis figes (m).")]
    [SerializeField] private float liftHeight = 8f;
    [Tooltip("Duree de la montee (s). Les ennemis sont supprimes a la fin.")]
    [Min(0.01f)] [SerializeField] private float liftDuration = 3f;

    [Header("Arrivee des ennemis et pupitres de la phase suivante (remplacant provisoire des cages)")]
    [Tooltip("Hauteur d'ou ils descendent, au-dessus de l'endroit ou ils sont poses dans la scene (m).")]
    [SerializeField] private float dropHeight = 8f;
    [Tooltip("Duree de la descente (s). Ils sont inertes pendant la descente, rendus a l'arrivee.")]
    [Min(0.01f)] [SerializeField] private float dropDuration = 3f;

    [Header("Effets")]
    [Tooltip("Le plan sentinelle est seul a l'ecran (fondu fini), le joueur vient d'etre pose dans la cage. " +
             "Tout de suite s'il n'y a pas de camera sentinelle. Ex : BossCircleExploder.Explode.")]
    [SerializeField] private UnityEvent onSentinelShotLive;
    [Tooltip("La cage est en haut avec le joueur (tout de suite apres le plan sentinelle s'il n'y a pas de cage). " +
             "Ex : BossTransition.RemoveEnemies.")]
    [SerializeField] private UnityEvent onCageRaised;
    [Tooltip("Quand tous les ennemis, vivants et cadavres, sont partis (fin de leur sortie, supprimes). " +
             "Tout de suite s'il n'y en avait aucun. Ex : EnemyIconsUI.ClearAllIcons, " +
             "BossPhaseManager.BringInCurrentPhaseZombies.")]
    [SerializeField] private UnityEvent onEnemiesRemoved;
    [Tooltip("Quand tous les ennemis de la phase sont a terre. Tout de suite s'il n'y en avait aucun. " +
             "Ex : EnemyIconsUI.SpawnIconsForLivingEnemies, BossTransition.LowerCage.")]
    [SerializeField] private UnityEvent onEnemiesArrived;
    [Tooltip("Quand tous les pupitres de la phase sont poses. Tout de suite s'il n'y en avait aucun. " +
             "Ex : BossPhaseManager.BringInCurrentPhaseZombies (pupitres d'abord, zombies ensuite).")]
    [SerializeField] private UnityEvent onPupitresArrived;

    [Header("Debug")]
    [SerializeField] private bool playerFrozen = false;

    /// <summary>Fin de l'entre-phases (joueur rendu). Le BossPhaseManager y lance la reprise du cycle.</summary>
    public event System.Action Ended;

    public bool IsPlayerFrozen => playerFrozen;

    private PlayerPhysicsMovement player;
    private Rigidbody playerRb;
    private int pendingRemovals = 0;
    private BossCage currentCage;
    private bool lowering = false;
    // Ennemis figes au debut de la transition, enleves plus tard (RemoveEnemies)
    private readonly List<GameObject> frozenEnemies = new List<GameObject>();
    private int frozenCorpses = 0;

    private void Awake()
    {
        if (sentinelCamera != null) sentinelCamera.Priority = 0;
        if (installCamera != null) installCamera.Priority = 0;
    }

    private void Start()
    {
        GameObject go = GameObject.FindGameObjectWithTag("Player");
        if (go != null)
        {
            player = go.GetComponentInParent<PlayerPhysicsMovement>();
            if (player != null) playerRb = player.GetComponent<Rigidbody>();
        }
        if (player == null)
            Debug.LogWarning("[BOSS TRANSITION] Pas de PlayerPhysicsMovement (tag Player)");
    }

    [ContextMenu("Debut de transition (Begin, sans cage)")]
    private void BeginFromMenu() => Begin(null);

    /// <summary>
    /// Debut de l'entre-phases : tout le monde fige, plan sentinelle, puis la cage monte avec le joueur.
    /// cage : celle du dernier pupitre active (null = pas de cage, le joueur reste fige au sol).
    /// </summary>
    public void Begin(BossCage cage)
    {
        currentCage = cage;
        FreezePlayer();
        FreezeAllEnemies();
        StartCoroutine(SentinelShotThenCage());
    }

    private IEnumerator SentinelShotThenCage()
    {
        float shotStart = Time.time;

        // Le plan sentinelle cache la teleportation du joueur dans la cage : on attend que la camera
        // sentinelle soit seule a l'ecran (fondu termine), sinon on le verrait sauter pendant le fondu
        if (sentinelCamera != null)
        {
            sentinelCamera.Priority = cameraPriority;
            yield return WaitUntilCameraIsLive(sentinelCamera, sentinelShotDuration);
        }
        if (currentCage != null) currentCage.PlacePlayer();
        onSentinelShotLive?.Invoke();

        // La duree du plan compte depuis le changement de camera, fondu compris
        float remaining = sentinelShotDuration - (Time.time - shotStart);
        if (remaining > 0f) yield return new WaitForSeconds(remaining);

        // Plan d'installation (s'il y en a un) avant de rendre la camera sentinelle : pas d'aller-retour par la vue normale
        if (installCamera != null) installCamera.Priority = cameraPriority;
        if (sentinelCamera != null) sentinelCamera.Priority = 0;

        // La cage monte une fois le plan d'installation a l'ecran (fondu termine), pour qu'on la voie partir
        if (installCamera != null) yield return WaitUntilCameraIsLive(installCamera, MaxBlendWait);

        if (currentCage != null) currentCage.RaiseWithPlayer(CageRaised);
        else CageRaised();
    }

    // Securite : on n'attend jamais un fondu plus longtemps que ca (s)
    private const float MaxBlendWait = 5f;

    // Attend que le CinemachineBrain affiche cette camera seule (pas de fondu en cours). Limite : timeout (s).
    private static IEnumerator WaitUntilCameraIsLive(CinemachineCamera cam, float timeout)
    {
        // Le Brain prend en compte la nouvelle priorite a son prochain LateUpdate
        yield return null;

        float elapsed = 0f;
        while (elapsed < timeout)
        {
            CinemachineBrain brain = CinemachineBrain.ActiveBrainCount > 0 ? CinemachineBrain.GetActiveBrain(0) : null;
            if (brain == null) yield break;
            if (!brain.IsBlending && brain.ActiveVirtualCamera == (ICinemachineCamera)cam) yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void CageRaised()
    {
        Debug.Log("[BOSS TRANSITION] Cage en haut");
        onCageRaised?.Invoke();
    }

    /// <summary>
    /// Fin de l'entre-phases : la cage redescend, relache le joueur, puis la transition se termine.
    /// A brancher dans le flow (ex. sur onEnemiesArrived). Sans cage : le joueur est rendu tout de suite.
    /// </summary>
    [ContextMenu("Redescendre la cage")]
    public void LowerCage()
    {
        // Un deuxieme appel pendant la descente ne doit pas rendre le joueur avant que la cage soit posee
        if (lowering) return;

        if (currentCage == null)
        {
            EndPlayerFreeze();
            return;
        }

        BossCage cage = currentCage;
        currentCage = null;
        lowering = true;
        cage.LowerAndRelease(() =>
        {
            lowering = false;
            EndPlayerFreeze();
        });
    }

    // ============================================
    // JOUEUR
    // ============================================

    private void FreezePlayer()
    {
        if (player == null || playerFrozen) return;
        playerFrozen = true;

        player.isFrozen = true;
        // isFrozen ne coupe que le deplacement : le reste des commandes (crouch, balai, spray, tomates,
        // interaction) passe par le verrou du PlayerInputManager, celui de la pause
        if (PlayerInputManager.Instance != null) PlayerInputManager.Instance.IsLocked = true;
        if (playerRb != null && !playerRb.isKinematic)
        {
            // Garde la chute verticale : seul le deplacement horizontal est coupe
            Vector3 v = playerRb.linearVelocity;
            // Le mouvement ne tourne plus pendant le gel : sans remise a zero, son elan (vitesse lissee)
            // resterait en memoire et ressortirait au relachement
            player.ResetAllInputs();
            playerRb.linearVelocity = new Vector3(0f, Mathf.Min(v.y, 0f), 0f);
        }
        else
        {
            player.ResetAllInputs();
        }
        Debug.Log("[BOSS TRANSITION] Joueur fige");
    }

    [ContextMenu("Rendre le joueur")]
    public void EndPlayerFreeze()
    {
        if (player == null || !playerFrozen) return;
        playerFrozen = false;

        // Retour a la vue normale au moment ou le joueur reprend la main (fondu : Custom Blends du Brain)
        if (installCamera != null) installCamera.Priority = 0;
        if (sentinelCamera != null) sentinelCamera.Priority = 0;

        // Repart de l'arret : aucun elan d'avant le gel
        if (playerRb == null || !playerRb.isKinematic) player.ResetAllInputs();
        player.isFrozen = false;
        if (PlayerInputManager.Instance != null) PlayerInputManager.Instance.IsLocked = false;
        Debug.Log("[BOSS TRANSITION] Joueur rendu");
        Ended?.Invoke();
    }

    // ============================================
    // ENNEMIS
    // ============================================

    // Vivants et cadavres (un cadavre est le meme objet que l'ennemi, EnemyHealth mort, en ragdoll) :
    // tout le monde est fige des le dernier pupitre (plus personne ne frappe le joueur), et partira ensuite
    private void FreezeAllEnemies()
    {
        foreach (EnemyHealth health in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
        {
            if (health == null || frozenEnemies.Contains(health.gameObject)) continue;
            frozenEnemies.Add(health.gameObject);
            if (health.IsDead()) frozenCorpses++;
            FreezeEnemy(health.gameObject);
        }

        Debug.Log($"[BOSS TRANSITION] {frozenEnemies.Count - frozenCorpses} vivant(s) + {frozenCorpses} cadavre(s) fige(s)");
    }

    /// <summary>
    /// Enleve les ennemis figes au debut de la transition (montee puis suppression) : l'arene et la barre
    /// d'icones repartent a zero. A brancher dans le flow (ex. sur onCageRaised). Fin : onEnemiesRemoved.
    /// </summary>
    [ContextMenu("Enlever les ennemis")]
    public void RemoveEnemies()
    {
        List<GameObject> enemies = new List<GameObject>();
        foreach (GameObject enemy in frozenEnemies)
            if (enemy != null) enemies.Add(enemy);
        frozenEnemies.Clear();
        frozenCorpses = 0;

        pendingRemovals += enemies.Count;
        foreach (GameObject enemy in enemies)
            StartCoroutine(LiftAndRemove(enemy));

        Debug.Log($"[BOSS TRANSITION] {enemies.Count} ennemi(s) enleve(s)");
        if (pendingRemovals == 0) EnemiesRemoved();
    }

    private void OnEnemyRemoved()
    {
        pendingRemovals = Mathf.Max(0, pendingRemovals - 1);
        if (pendingRemovals == 0) EnemiesRemoved();
    }

    private void EnemiesRemoved()
    {
        Debug.Log("[BOSS TRANSITION] Ennemis enleves");
        onEnemiesRemoved?.Invoke();
    }

    // Gel definitif : scripts coupes (coroutines comprises : desactiver un script ne les arrete pas),
    // animations en pause, physique arretee, collisions coupees (ils traversent le decor en montant),
    // barres de vie et minuteurs d'etourdissement desinscrits (elements d'UI hors de l'ennemi).
    private static void FreezeEnemy(GameObject enemy)
    {
        if (EnemyHealthBarManager.Instance != null || StunTimerManager.Instance != null)
        {
            foreach (Transform t in enemy.GetComponentsInChildren<Transform>(true))
            {
                if (EnemyHealthBarManager.Instance != null) EnemyHealthBarManager.Instance.UnregisterEnemy(t);
                if (StunTimerManager.Instance != null) StunTimerManager.Instance.UnregisterEnemy(t);
            }
        }

        foreach (MonoBehaviour mb in enemy.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            mb.StopAllCoroutines();
            mb.enabled = false;
        }
        foreach (Animator animator in enemy.GetComponentsInChildren<Animator>(true))
            animator.speed = 0f;
        foreach (Rigidbody rb in enemy.GetComponentsInChildren<Rigidbody>(true))
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
        }
        foreach (Collider col in enemy.GetComponentsInChildren<Collider>(true))
            col.enabled = false;
    }

    private IEnumerator LiftAndRemove(GameObject enemy)
    {
        Vector3 start = enemy.transform.position;
        Vector3 end = start + Vector3.up * liftHeight;
        float t = 0f;

        while (t < 1f)
        {
            if (enemy == null) break;
            t += Time.deltaTime / liftDuration;
            enemy.transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        if (enemy != null) Destroy(enemy);
        OnEnemyRemoved();
    }

    // ============================================
    // ARRIVEE DES ENNEMIS DE LA PHASE
    // ============================================

    /// <summary>
    /// Fait descendre des ennemis desactives jusqu'a l'endroit ou ils sont poses dans la scene.
    /// Inertes pendant la descente (scripts, physique, collisions) ; leur demarrage (Start : ragdoll,
    /// barre de vie, IA) attend l'arrivee. Un ennemi deja actif est ignore. Appele par le BossPhaseManager.
    /// </summary>
    public void BringInEnemies(IList<GameObject> enemies)
    {
        DropGroup(enemies, "ennemi(s)", EnemiesArrived);
    }

    /// <summary>
    /// Fait descendre les pupitres d'une phase, comme les ennemis (inertes pendant la descente : pas
    /// activables en l'air). Appele par le BossPhaseManager. Fin : onPupitresArrived.
    /// </summary>
    public void BringInPupitres(IList<GameObject> pupitres)
    {
        DropGroup(pupitres, "pupitre(s)", PupitresArrived);
    }

    // Chaque groupe (ennemis, pupitres) compte ses arrivees de son cote : leurs evenements ne se melangent pas
    private class DropGroupCounter
    {
        public int remaining;
        public System.Action onAllArrived;

        public void OneArrived()
        {
            remaining--;
            if (remaining == 0) onAllArrived?.Invoke();
        }
    }

    private void DropGroup(IList<GameObject> objects, string label, System.Action onAllArrived)
    {
        List<GameObject> toDrop = new List<GameObject>();
        if (objects != null)
        {
            foreach (GameObject obj in objects)
            {
                if (obj == null || toDrop.Contains(obj)) continue;
                if (obj.activeInHierarchy)
                {
                    Debug.LogWarning($"[BOSS TRANSITION] {obj.name} deja actif : pas de descente");
                    continue;
                }
                toDrop.Add(obj);
            }
        }

        Debug.Log($"[BOSS TRANSITION] {toDrop.Count} {label} en descente");
        if (toDrop.Count == 0)
        {
            onAllArrived?.Invoke();
            return;
        }

        DropGroupCounter counter = new DropGroupCounter { remaining = toDrop.Count, onAllArrived = onAllArrived };
        foreach (GameObject obj in toDrop)
            StartCoroutine(DropIn(obj, counter));
    }

    private IEnumerator DropIn(GameObject enemy, DropGroupCounter counter)
    {
        Vector3 end = enemy.transform.position;
        Vector3 start = end + Vector3.up * dropHeight;

        // Coupe AVANT l'activation : Awake tourne, mais OnEnable et Start attendent qu'on rende les scripts
        List<MonoBehaviour> scripts = new List<MonoBehaviour>();
        foreach (MonoBehaviour mb in enemy.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null || !mb.enabled) continue;
            mb.enabled = false;
            scripts.Add(mb);
        }
        List<Rigidbody> bodies = new List<Rigidbody>();
        foreach (Rigidbody rb in enemy.GetComponentsInChildren<Rigidbody>(true))
        {
            if (rb.isKinematic) continue;
            rb.isKinematic = true;
            bodies.Add(rb);
        }
        List<Collider> colliders = new List<Collider>();
        foreach (Collider col in enemy.GetComponentsInChildren<Collider>(true))
        {
            if (!col.enabled) continue;
            col.enabled = false;
            colliders.Add(col);
        }

        enemy.transform.position = start;
        enemy.SetActive(true);

        float t = 0f;
        while (t < 1f)
        {
            if (enemy == null) break;
            t += Time.deltaTime / dropDuration;
            enemy.transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        if (enemy != null)
        {
            enemy.transform.position = end;
            foreach (Collider col in colliders) if (col != null) col.enabled = true;
            foreach (Rigidbody rb in bodies)
            {
                if (rb == null) continue;
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            foreach (MonoBehaviour mb in scripts) if (mb != null) mb.enabled = true;
        }

        counter.OneArrived();
    }

    private void EnemiesArrived()
    {
        Debug.Log("[BOSS TRANSITION] Ennemis arrives");
        onEnemiesArrived?.Invoke();
    }

    private void PupitresArrived()
    {
        Debug.Log("[BOSS TRANSITION] Pupitres arrives");
        onPupitresArrived?.Invoke();
    }
}
