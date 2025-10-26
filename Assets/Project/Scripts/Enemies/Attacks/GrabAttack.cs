using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Comportement d'attaque par Grab (pour les ennemis de type Grabber).
/// Extrait et refactorisé depuis ZombieGrabSystem.
/// 
/// FIXES APPLIQUÉS :
/// - Bug 1 : OnGrabEnd() appelé AVANT le recoil pour éviter les problèmes d'invincibilité
/// - Bug 2 : Meilleure gestion de l'AI pendant bourrade + recovery + logs de debug
/// </summary>
public class GrabAttack : MonoBehaviour, IAttackBehavior
{
    // Stats et références
    private EnemyStats stats;
    private Transform enemyTransform;
    private Rigidbody enemyRigidbody;
    private EnemyHealth enemyHealth;
    private GameManager gameManager;

    private static GameObject currentlyGrabbedPlayer = null;
    private static GameObject primaryGrabber = null; // Celui qui compte les mashes
    private static List<GrabAttack> activeGrabbers = new List<GrabAttack>(); // Tous ceux qui grabent
    private static int sharedMashCount = 0;

    // État du grab
    private bool isGrabbing = false;
    private bool canGrab = true;
    private GameObject grabbedTarget;
    private PlayerPhysicsMovement targetMovement;
    private int currentMashes = 0;

    // État de la bourrade
    private bool isInBourradeDuration = false;
    private bool isInBourradeCooldown = false;

    // Player Recoil
    [Header("Player Recoil")]
    [SerializeField] private float playerEscapeRecoilForce = 50f;

    // ============================================
    // INTERFACE IAttackBehavior
    // ============================================

    public void Initialize(EnemyStats enemyStats, Transform transform, Rigidbody rigidbody)
    {
        stats = enemyStats;
        enemyTransform = transform;
        enemyRigidbody = rigidbody;
        enemyHealth = GetComponent<EnemyHealth>();
        gameManager = FindObjectOfType<GameManager>();

        Debug.Log($"[GRAB] {gameObject.name} initialized with GrabAttack behavior");
    }

    public bool CanAttack()
    {
        bool result = canGrab && !isGrabbing && !IsInBourrade();
        if (!result)
        {
            Debug.Log($"[GRAB] {gameObject.name} CanAttack=false (canGrab:{canGrab}, isGrabbing:{isGrabbing}, InBourrade:{IsInBourrade()})");
        }
        return result;
    }

    public void AttemptAttack(GameObject target)
    {
        if (stats == null) return;
        if (!CanAttack()) return;
        if (gameManager != null && gameManager.zombieStunBySentinel)
        {
            Debug.Log($"[GRAB] {gameObject.name} cannot attack - stunned by sentinel");
            return;
        }

        float distance = Vector3.Distance(enemyTransform.position, target.transform.position);

        if (distance <= stats.attackRange)
        {
            Vector3 directionToTarget = (target.transform.position - enemyTransform.position).normalized;
            float angle = Vector3.Angle(enemyTransform.forward, directionToTarget);

            if (angle <= 60f)
            {
                // Si personne ne grab ce joueur, devenir le primary
                if (currentlyGrabbedPlayer != target)
                {
                    Debug.Log($"[GRAB] {gameObject.name} starting PRIMARY grab on {target.name}");
                    StartCoroutine(GrabSequence(target, isPrimary: true));
                }
                else
                {
                    // Rejoindre le grab en cours
                    Debug.Log($"[GRAB] {gameObject.name} joining SECONDARY grab on {target.name}");
                    StartCoroutine(GrabSequence(target, isPrimary: false));
                }
            }
        }
    }

    public bool IsAttacking()
    {
        return isGrabbing;
    }

    public bool IsInSpecialState()
    {
        return IsInBourrade();
    }

    public void ForceStop()
    {
        if (isGrabbing)
        {
            Debug.Log($"[GRAB] {gameObject.name} grab forcibly stopped!");
            StopAllCoroutines();

            if (targetMovement != null)
            {
                targetMovement.enabled = true;
            }

            if (grabbedTarget != null)
            {
                MeleeAttackSystem meleeSystem = grabbedTarget.GetComponent<MeleeAttackSystem>();
                if (meleeSystem != null)
                    meleeSystem.OnGrabEnd();
            }

            isGrabbing = false;
            grabbedTarget = null;
            targetMovement = null;
            currentMashes = 0;
            canGrab = true;
        }
    }

    // ============================================
    // GETTERS PUBLICS
    // ============================================

    public bool IsGrabbing() => isGrabbing;
    public bool IsInBourrade() => isInBourradeDuration || isInBourradeCooldown;
    public bool IsInBourradeDuration() => isInBourradeDuration;
    public bool IsInBourradeCooldown() => isInBourradeCooldown;

    // ============================================
    // SÉQUENCE DE GRAB
    // ============================================

    IEnumerator GrabSequence(GameObject target, bool isPrimary)
    {
        isGrabbing = true;
        grabbedTarget = target;

        if (isPrimary)
        {
            // Setup primary grabber
            currentlyGrabbedPlayer = target;
            primaryGrabber = gameObject;
            sharedMashCount = 0;
            activeGrabbers.Clear();

            Debug.Log($"[GRAB] {gameObject.name} is PRIMARY grabber!");
        }

        activeGrabbers.Add(this);
        Debug.Log($"[GRAB] {gameObject.name} joined grab! Total grabbers: {activeGrabbers.Count}");

        int mashesRequired = CalculateMashesToEscape();

        // Désactiver le mouvement (une seule fois)
        if (isPrimary)
        {
            targetMovement = target.GetComponent<PlayerPhysicsMovement>();
            if (targetMovement != null)
                targetMovement.enabled = false;

            MeleeAttackSystem meleeSystem = target.GetComponent<MeleeAttackSystem>();
            if (meleeSystem != null)
                meleeSystem.OnGrabStart();
        }

        // Désactiver l'AI
        EnemyAI enemyAI = GetComponent<EnemyAI>();
        if (enemyAI != null)
        {
            enemyAI.enabled = false;
            Debug.Log($"[GRAB] {gameObject.name} AI disabled for grab");
        }

        float elapsed = 0f;
        bool escaped = false;

        // Boucle - SEULEMENT le primary compte les mashes
        while (elapsed < stats.grabDuration && !escaped)
        {
            elapsed += Time.deltaTime;

            if (isPrimary && Input.GetKeyDown(KeyCode.Space))
            {
                sharedMashCount++;
                Debug.Log($"[GRAB] Mash count: {sharedMashCount}/{mashesRequired} ({activeGrabbers.Count} zombies grabbing)");

                if (sharedMashCount >= mashesRequired)
                {
                    escaped = true;
                    Debug.Log($"[GRAB] {target.name} ESCAPED from {activeGrabbers.Count} zombies!");
                }
            }

            // Les secondaires vérifient le compteur partagé
            if (!isPrimary && sharedMashCount >= mashesRequired)
            {
                escaped = true;
            }

            yield return null;
        }

        // CAS 1 : Escape
        if (escaped)
        {
            Debug.Log($"[GRAB] [{Time.frameCount}] {gameObject.name} (Primary:{isPrimary}): ESCAPED");

            // SEULEMENT le primary appelle ReleaseAllGrabbers
            if (isPrimary)
            {
                ReleaseAllGrabbers(dealBiteDamage: false);
            }
            // Les secondaries se nettoient juste localement
            else
            {
                CleanupGrab();
                // Pas besoin de toucher l'AI, le primary s'en occupe via ApplyBourradeToEnemy
            }
        }
        // CAS 2 : Échec - SEULEMENT le primary mord
        else if (isPrimary)
        {
            Debug.Log($"[GRAB] {gameObject.name}: Primary grabber biting!");
            yield return StartCoroutine(BiteTarget(target));
            ReleaseAllGrabbers(dealBiteDamage: true);
        }
        // Les secondaires attendent que le primary finisse
        else
        {
            // Attendre que le primary finisse (sera libéré par ReleaseAllGrabbers)
            while (isGrabbing)
            {
                yield return null;
            }
        }
    }

    static void ReleaseAllGrabbers(bool dealBiteDamage)
    {
        Debug.Log($"[GRAB] ========== RELEASING {activeGrabbers.Count} GRABBERS (bite: {dealBiteDamage}) ==========");

        GameObject target = currentlyGrabbedPlayer;

        // ============================================
        // FIX BUG 1 : RÉACTIVER JOUEUR EN PREMIER
        // ============================================
        // Le joueur doit être "released" AVANT le recoil pour que la sentinelle
        // puisse lui infliger des dégâts correctement

        if (target != null)
        {
            PlayerPhysicsMovement movement = target.GetComponent<PlayerPhysicsMovement>();
            if (movement != null)
            {
                movement.enabled = true;
                Debug.Log($"[GRAB] Player movement RE-ENABLED");
            }

            MeleeAttackSystem meleeSystem = target.GetComponent<MeleeAttackSystem>();
            if (meleeSystem != null)
            {
                meleeSystem.OnGrabEnd();
                Debug.Log($"[GRAB] Player OnGrabEnd() called - isGrabbed = false");
            }
        }

        // ============================================
        // RECOIL JOUEUR (après OnGrabEnd)
        // ============================================
        // Recoil joueur UNE SEULE FOIS (par le primary) - SEULEMENT si escape
        if (!dealBiteDamage && target != null && primaryGrabber != null)
        {
            Rigidbody playerRb = target.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                // Trouver le primary grabber
                GrabAttack primaryGrab = primaryGrabber.GetComponent<GrabAttack>();
                if (primaryGrab != null)
                {
                    Vector3 recoilDir = (target.transform.position - primaryGrabber.transform.position).normalized;
                    recoilDir.y = 0;
                    playerRb.AddForce(recoilDir * primaryGrab.playerEscapeRecoilForce, ForceMode.VelocityChange);
                    Debug.Log($"[GRAB] Player RECOIL applied! Force: {primaryGrab.playerEscapeRecoilForce}, Direction: {recoilDir}, Mode: VelocityChange");
                }
            }
        }

        // ============================================
        // BOURRADE POUR CHAQUE ZOMBIE
        // ============================================
        foreach (GrabAttack grabber in activeGrabbers)
        {
            if (grabber != null)
            {
                grabber.ApplyBourradeToEnemy();
                grabber.CleanupGrab();
            }
        }

        // Reset global
        currentlyGrabbedPlayer = null;
        primaryGrabber = null;
        sharedMashCount = 0;
        activeGrabbers.Clear();

        Debug.Log($"[GRAB] ========== RELEASE COMPLETE ==========");
    }

    void CleanupGrab()
    {
        isGrabbing = false;
        grabbedTarget = null;
        targetMovement = null;
        Debug.Log($"[GRAB] {gameObject.name} cleaned up grab state");
    }

    // ============================================
    // CALCUL DU MASHING REQUIS
    // ============================================

    int CalculateMashesToEscape()
    {
        if (enemyHealth == null) return stats.mashesToEscape;

        int armCount = enemyHealth.GetArmCount();

        if (armCount == 2)
        {
            return stats.mashesToEscape;
        }
        else if (armCount == 1)
        {
            return Mathf.RoundToInt(stats.mashesToEscape * (1f - stats.oneArmMashReduction));
        }
        else
        {
            return 0; // Pas de bras = échappement instantané
        }
    }

    // ============================================
    // MORSURE
    // ============================================

    IEnumerator BiteTarget(GameObject target)
    {
        Debug.Log($"[GRAB] {gameObject.name} is biting {target.name}!");

        float elapsed = 0f;
        float biteDuration = 0.5f;

        // Attendre MAIS vérifier si toujours valide
        while (elapsed < biteDuration)
        {
            // Si le grab est interrompu, annuler la morsure
            if (!isGrabbing || grabbedTarget == null)
            {
                Debug.Log("[GRAB] Bite cancelled - grab interrupted!");
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Morsure finale
        PlayerHealth humanHealth = target.GetComponent<PlayerHealth>();
        if (humanHealth != null && !humanHealth.IsDead())
        {
            humanHealth.TakeDamage(stats.biteDamage);
            Debug.Log($"[GRAB] {target.name} took {stats.biteDamage} damage from bite!");
        }
    }

    // ============================================
    // BOURRADE (KNOCKBACK)
    // ============================================

    void ApplyBourradeToEnemy()
    {
        Debug.Log($"[BOURRADE] [{Time.frameCount}] {gameObject.name}: ApplyBourradeToEnemy called");

        if (enemyRigidbody == null || grabbedTarget == null)
        {
            Debug.LogWarning($"[BOURRADE] {gameObject.name}: Cannot apply bourrade - missing rigidbody or target");
            return;
        }

        // Direction : ennemi est projeté LOIN du joueur
        Vector3 bourradeDirection = (enemyTransform.position - grabbedTarget.transform.position).normalized;
        bourradeDirection.y = 0;

        // Application de la force
        Vector3 bourradeVelocity = bourradeDirection * stats.bourradeForce;
        enemyRigidbody.linearVelocity = bourradeVelocity;

        Debug.Log($"[BOURRADE] {gameObject.name} received bourrade! Knockback velocity: {bourradeVelocity}");

        // Démarrer la séquence de bourrade
        StartCoroutine(BourradeSequence());
    }

    IEnumerator BourradeSequence()
    {
        Debug.Log($"[BOURRADE] {gameObject.name}: BourradeSequence START (canGrab will be disabled)");
        canGrab = false;

        // Désactiver l'AI pendant toute la bourrade
        EnemyAI enemyAI = GetComponent<EnemyAI>();
        if (enemyAI != null)
        {
            enemyAI.enabled = false;
            Debug.Log($"[BOURRADE] {gameObject.name}: AI DISABLED for bourrade");
        }

        // === PHASE 1 : BOURRADE DURATION ===
        isInBourradeDuration = true;
        isInBourradeCooldown = false;

        Debug.Log($"[BOURRADE] {gameObject.name}: Entering BourradeDuration ({stats.bourradeDuration}s)");
        yield return new WaitForSeconds(stats.bourradeDuration);

        // === PHASE 2 : BOURRADE COOLDOWN ===
        isInBourradeDuration = false;
        isInBourradeCooldown = true;

        // Arrêter le mouvement pendant le cooldown
        if (enemyRigidbody != null)
        {
            enemyRigidbody.linearVelocity = Vector3.zero;
        }

        Debug.Log($"[BOURRADE] {gameObject.name}: Entering BourradeCooldown ({stats.bourradeCooldown}s)");
        yield return new WaitForSeconds(stats.bourradeCooldown);

        // === FIN DE LA BOURRADE ===
        isInBourradeCooldown = false;

        // ============================================
        // FIX BUG 2 : VÉRIFICATIONS AVANT DE RÉACTIVER L'AI
        // ============================================
        // On vérifie que le zombie n'est pas mort et n'est pas en recovery
        bool canReactivateAI = true;

        if (enemyHealth != null)
        {
            if (enemyHealth.IsDead())
            {
                Debug.Log($"[BOURRADE] {gameObject.name}: Zombie is DEAD - AI will NOT be reactivated");
                canReactivateAI = false;
            }
            else if (enemyHealth.IsRecovering())
            {
                Debug.Log($"[BOURRADE] {gameObject.name}: Zombie is RECOVERING from sentinel shot - AI will NOT be reactivated yet");
                canReactivateAI = false;
                // Démarrer une coroutine pour réactiver l'AI après le recovery
                StartCoroutine(WaitForRecoveryThenReactivateAI());
            }
        }

        // Réactiver l'AI si possible
        if (canReactivateAI && enemyAI != null)
        {
            enemyAI.enabled = true;
            Debug.Log($"[BOURRADE] {gameObject.name}: AI RE-ENABLED after bourrade");
        }

        canGrab = true;
        Debug.Log($"[BOURRADE] {gameObject.name}: BourradeSequence COMPLETE - can grab again!");
    }

    /// <summary>
    /// FIX BUG 2 : Attendre la fin du recovery avant de réactiver l'AI
    /// </summary>
    IEnumerator WaitForRecoveryThenReactivateAI()
    {
        Debug.Log($"[BOURRADE] {gameObject.name}: Waiting for recovery to end...");

        // Attendre que le recovery soit terminé
        while (enemyHealth != null && enemyHealth.IsRecovering())
        {
            yield return new WaitForSeconds(0.1f);
        }

        // Vérifier qu'on est toujours vivant
        if (enemyHealth != null && !enemyHealth.IsDead())
        {
            EnemyAI enemyAI = GetComponent<EnemyAI>();
            if (enemyAI != null && !enemyAI.enabled)
            {
                enemyAI.enabled = true;
                Debug.Log($"[BOURRADE] {gameObject.name}: AI RE-ENABLED after recovery ended!");
            }
        }
        else
        {
            Debug.Log($"[BOURRADE] {gameObject.name}: Zombie died during recovery - AI not reactivated");
        }
    }

    // ============================================
    // GIZMOS (DEBUG)
    // ============================================

    void OnDrawGizmosSelected()
    {
        if (stats == null) return;

        // Portée du grab
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, stats.attackRange);

        // Indicateur si ne peut pas grab
        if (!canGrab)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, Vector3.one * 0.5f);
        }

        // Indicateur si en bourrade
        if (IsInBourrade())
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.3f);
        }
    }
}