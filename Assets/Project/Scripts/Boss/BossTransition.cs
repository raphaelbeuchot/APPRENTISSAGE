using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Entre-phases du boss de Rush Hour IV, cote joueur et ennemis.
/// Begin (appele par le BossPhaseManager au dernier pupitre d'une phase) :
/// - le joueur est fige sur place ("coffre de BotW") : plus de deplacement ni de commandes,
///   la gravite continue (s'il etait en l'air, il retombe) ;
/// - les ennemis, vivants et cadavres, sont figes pour de bon (gel dedie : ils ne reviennent jamais),
///   montent (remplacant provisoire des cages) puis sont supprimes.
/// EndPlayerFreeze rend le joueur (menu du composant, evenement, signal de Timeline...).
/// </summary>
public class BossTransition : MonoBehaviour
{
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
    [Tooltip("Quand tous les ennemis, vivants et cadavres, sont partis (fin de leur sortie, supprimes). " +
             "Tout de suite s'il n'y en avait aucun. Ex : EnemyIconsUI.ClearAllIcons, " +
             "BossPhaseManager.BringInCurrentPhaseZombies.")]
    [SerializeField] private UnityEvent onEnemiesRemoved;
    [Tooltip("Quand tous les ennemis de la phase sont a terre. Tout de suite s'il n'y en avait aucun. " +
             "Ex : EnemyIconsUI.SpawnIconsForLivingEnemies.")]
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

    /// <summary>Debut de l'entre-phases : joueur fige, ennemis (vivants et cadavres) figes puis enleves.</summary>
    [ContextMenu("Debut de transition (Begin)")]
    public void Begin()
    {
        FreezePlayer();
        RemoveAllEnemies();
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
            playerRb.linearVelocity = new Vector3(0f, Mathf.Min(v.y, 0f), 0f);
        }
        Debug.Log("[BOSS TRANSITION] Joueur fige");
    }

    [ContextMenu("Rendre le joueur")]
    public void EndPlayerFreeze()
    {
        if (player == null || !playerFrozen) return;
        playerFrozen = false;
        player.isFrozen = false;
        if (PlayerInputManager.Instance != null) PlayerInputManager.Instance.IsLocked = false;
        Debug.Log("[BOSS TRANSITION] Joueur rendu");
        Ended?.Invoke();
    }

    // ============================================
    // ENNEMIS
    // ============================================

    // Vivants et cadavres (un cadavre est le meme objet que l'ennemi, EnemyHealth mort, en ragdoll) :
    // tout le monde part, l'arene et la barre d'icones repartent a zero
    private void RemoveAllEnemies()
    {
        List<GameObject> enemies = new List<GameObject>();
        int corpses = 0;
        foreach (EnemyHealth health in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
        {
            if (health == null || enemies.Contains(health.gameObject)) continue;
            enemies.Add(health.gameObject);
            if (health.IsDead()) corpses++;
        }

        pendingRemovals += enemies.Count;
        foreach (GameObject enemy in enemies)
        {
            FreezeEnemy(enemy);
            StartCoroutine(LiftAndRemove(enemy));
        }

        Debug.Log($"[BOSS TRANSITION] {enemies.Count - corpses} vivant(s) + {corpses} cadavre(s) fige(s) et enleve(s)");
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
