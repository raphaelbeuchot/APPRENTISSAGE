using UnityEngine;
using System.Collections;

/// <summary>
/// Comportement d'attaque par Grab (pour les ennemis de type Grabber).
/// Extrait et refactorisé depuis ZombieGrabSystem.
/// </summary>
public class GrabAttack : MonoBehaviour, IAttackBehavior
{
    // Stats et références
    private EnemyStats stats;
    private Transform enemyTransform;
    private Rigidbody enemyRigidbody;
    private EnemyHealth enemyHealth;
    private GameManager gameManager;

    // État du grab
    private bool isGrabbing = false;
    private bool canGrab = true;
    private GameObject grabbedTarget;
    private PlayerPhysicsMovement targetMovement;
    private int currentMashes = 0;

    // État de la bourrade
    private bool isInBourradeDuration = false;
    private bool isInBourradeCooldown = false;

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
    }

    public bool CanAttack()
    {
        return canGrab && !isGrabbing && !IsInBourrade();
    }

    public void AttemptAttack(GameObject target)
    {
        if (stats == null) return;
        if (!CanAttack()) return;
        if (gameManager != null && gameManager.stunBySentinel) return;

        float distance = Vector3.Distance(enemyTransform.position, target.transform.position);

        if (distance <= stats.attackRange)
        {
            Vector3 directionToTarget = (target.transform.position - enemyTransform.position).normalized;
            float angle = Vector3.Angle(enemyTransform.forward, directionToTarget);

            if (angle <= 60f)
            {
                StartCoroutine(GrabSequence(target));
            }
            else
            {
                Debug.Log($"{gameObject.name}: Cible derrière l'ennemi, pas de grab! (angle: {angle}°)");
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
            Debug.Log($"{gameObject.name} grab forcibly stopped!");
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

    IEnumerator GrabSequence(GameObject target)
    {
        isGrabbing = true;
        grabbedTarget = target;
        currentMashes = 0;

        int mashesRequired = CalculateMashesToEscape();

        // Désactiver le mouvement du joueur
        targetMovement = target.GetComponent<PlayerPhysicsMovement>();
        if (targetMovement != null)
            targetMovement.enabled = false;

        // Informer le système d'attaque que le joueur est grabbed
        MeleeAttackSystem meleeSystem = target.GetComponent<MeleeAttackSystem>();
        if (meleeSystem != null)
            meleeSystem.OnGrabStart();

        // Désactiver l'AI de l'ennemi pendant le grab
        EnemyAI enemyAI = GetComponent<EnemyAI>();
        if (enemyAI != null)
            enemyAI.enabled = false;

        Debug.Log($"{gameObject.name} grabbed {target.name}! MASH Space to escape! ({mashesRequired} mashes needed)");

        float elapsed = 0f;
        bool escaped = false;

        // Boucle du grab
        while (elapsed < stats.grabDuration && !escaped)
        {
            elapsed += Time.deltaTime;

            // Détection du mashing
            if (Input.GetKeyDown(KeyCode.Space))
            {
                currentMashes++;
                Debug.Log($"Mash count: {currentMashes}/{mashesRequired}");

                if (currentMashes >= mashesRequired)
                {
                    escaped = true;
                    Debug.Log($"{target.name} escaped from grab!");
                }
            }

            yield return null;
        }

        // CAS 1 : Le joueur s'est échappé
        if (escaped)
        {
            Debug.Log($"CAS 1: {target.name} escaped! Applying bourrade to enemy.");
            ReleaseTargetWithBourrade(dealBiteDamage: false);
        }
        // CAS 2 : Le joueur n'a pas réussi à s'échapper
        else
        {
            Debug.Log($"CAS 2: {target.name} failed to escape! Bite + bourrade.");
            yield return StartCoroutine(BiteTarget(target));
            ReleaseTargetWithBourrade(dealBiteDamage: true);
        }
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
        Debug.Log($"{gameObject.name} is biting {target.name}!");
        yield return new WaitForSeconds(0.5f);

        PlayerHealth humanHealth = target.GetComponent<PlayerHealth>();
        if (humanHealth != null && !humanHealth.IsDead())
        {
            humanHealth.TakeDamage(stats.biteDamage);
            Debug.Log($"{target.name} took {stats.biteDamage} damage from bite!");
        }
    }

    // ============================================
    // LIBÉRATION AVEC BOURRADE
    // ============================================

    void ReleaseTargetWithBourrade(bool dealBiteDamage)
    {
        // 1. Arrêter le joueur
        if (targetMovement != null)
        {
            targetMovement.ForceStop();
        }

        // 2. Appliquer la bourrade À L'ENNEMI (pas au joueur !)
        ApplyBourradeToEnemy();

        // 3. Réactiver le mouvement du joueur
        if (targetMovement != null)
        {
            targetMovement.enabled = true;
        }

        // 4. Informer le système d'attaque que le joueur n'est plus grabbed
        if (grabbedTarget != null)
        {
            MeleeAttackSystem meleeSystem = grabbedTarget.GetComponent<MeleeAttackSystem>();
            if (meleeSystem != null)
                meleeSystem.OnGrabEnd();
        }

        // 5. Reset des variables
        isGrabbing = false;
        grabbedTarget = null;
        targetMovement = null;
        currentMashes = 0;

        Debug.Log($"Target released! Bourrade applied to enemy.");
    }

    // ============================================
    // BOURRADE (KNOCKBACK)
    // ============================================

    void ApplyBourradeToEnemy()
    {
        if (enemyRigidbody == null || grabbedTarget == null) return;

        // Direction : ennemi est projeté LOIN du joueur
        Vector3 bourradeDirection = (enemyTransform.position - grabbedTarget.transform.position).normalized;
        bourradeDirection.y = 0;

        // Application de la force
        Vector3 bourradeVelocity = bourradeDirection * stats.bourradeForce;
        enemyRigidbody.linearVelocity = bourradeVelocity;

        Debug.Log($"{gameObject.name} received bourrade! Knockback velocity: {bourradeVelocity}");

        // Démarrer la séquence de bourrade
        StartCoroutine(BourradeSequence());
    }

    IEnumerator BourradeSequence()
    {
        canGrab = false;

        // Désactiver l'AI pendant toute la bourrade
        EnemyAI enemyAI = GetComponent<EnemyAI>();
        if (enemyAI != null)
            enemyAI.enabled = false;

        // === PHASE 1 : BOURRADE DURATION ===
        isInBourradeDuration = true;
        isInBourradeCooldown = false;

        Debug.Log($"{gameObject.name} entering BourradeDuration ({stats.bourradeDuration}s)");
        yield return new WaitForSeconds(stats.bourradeDuration);

        // === PHASE 2 : BOURRADE COOLDOWN ===
        isInBourradeDuration = false;
        isInBourradeCooldown = true;

        // Arrêter le mouvement pendant le cooldown
        if (enemyRigidbody != null)
        {
            enemyRigidbody.linearVelocity = Vector3.zero;
        }

        Debug.Log($"{gameObject.name} entering BourradeCooldown ({stats.bourradeCooldown}s)");
        yield return new WaitForSeconds(stats.bourradeCooldown);

        // === FIN DE LA BOURRADE ===
        isInBourradeCooldown = false;

        // Réactiver l'AI
        if (enemyAI != null && enemyHealth != null && !enemyHealth.IsDead())
        {
            enemyAI.enabled = true;
        }

        canGrab = true;
        Debug.Log($"{gameObject.name} can grab again!");
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