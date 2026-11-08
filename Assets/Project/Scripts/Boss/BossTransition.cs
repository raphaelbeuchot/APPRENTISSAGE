using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Entre-phases du boss de Rush Hour IV, cote joueur et ennemis.
/// Begin (appele par le BossPhaseManager au dernier pupitre d'une phase) :
/// - le joueur est fige sur place ("coffre de BotW") : plus de deplacement ni de commandes,
///   la gravite continue (s'il etait en l'air, il retombe) ;
/// - les ennemis encore vivants sont figes pour de bon (gel dedie : ils ne reviennent jamais),
///   montent (remplacant provisoire des cages) puis sont supprimes.
/// Les cadavres ne sont pas touches.
/// EndPlayerFreeze rend le joueur (menu du composant, evenement, signal de Timeline...).
/// </summary>
public class BossTransition : MonoBehaviour
{
    [Header("Ennemis (remplacant provisoire des cages)")]
    [Tooltip("Hauteur de montee des ennemis figes (m).")]
    [SerializeField] private float liftHeight = 8f;
    [Tooltip("Duree de la montee (s). Les ennemis sont supprimes a la fin.")]
    [Min(0.01f)] [SerializeField] private float liftDuration = 3f;

    [Header("Debug")]
    [SerializeField] private bool playerFrozen = false;

    private PlayerPhysicsMovement player;
    private Rigidbody playerRb;

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

    /// <summary>Debut de l'entre-phases : joueur fige, ennemis vivants figes puis enleves.</summary>
    [ContextMenu("Debut de transition (Begin)")]
    public void Begin()
    {
        FreezePlayer();
        RemoveLivingEnemies();
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
    }

    // ============================================
    // ENNEMIS
    // ============================================

    private void RemoveLivingEnemies()
    {
        List<GameObject> enemies = new List<GameObject>();
        foreach (EnemyHealth health in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
        {
            if (health == null || health.IsDead()) continue;
            if (!enemies.Contains(health.gameObject)) enemies.Add(health.gameObject);
        }

        foreach (GameObject enemy in enemies)
        {
            FreezeEnemy(enemy);
            StartCoroutine(LiftAndRemove(enemy));
        }

        Debug.Log($"[BOSS TRANSITION] {enemies.Count} ennemi(s) vivant(s) fige(s) et enleve(s)");
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
            if (enemy == null) yield break;
            t += Time.deltaTime / liftDuration;
            enemy.transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        if (enemy != null) Destroy(enemy);
    }
}
