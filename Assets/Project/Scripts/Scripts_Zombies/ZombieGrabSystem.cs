using UnityEngine;
using System.Collections;

public class ZombieGrabSystem : MonoBehaviour
{
    [Header("Zombie Stats")]
    public ZombieStats stats; // Reference au ScriptableObject

    [Header("References")]
    private ZombieAI zombieAI;
    private ZombieHealth zombieHealth;
    private Rigidbody zombieRigidbody;
    private GameManager gameManager;

    // State
    private bool isGrabbing = false;
    private bool canGrab = true;
    private bool isInKnockbackGrace = false;
    private GameObject grabbedTarget;
    private PlayerPhysicsMovement targetMovement;
    private int currentMashes = 0;

    void Start()
    {
        if (stats == null)
        {
            Debug.LogError("ZombieStats non assigne sur " + gameObject.name);
            return;
        }

        zombieAI = GetComponent<ZombieAI>();
        zombieHealth = GetComponent<ZombieHealth>();
        zombieRigidbody = GetComponent<Rigidbody>();
        gameManager = FindObjectOfType<GameManager>();

        // Si pas de Rigidbody, on en ajoute un pour le knockback
        if (zombieRigidbody == null)
        {
            zombieRigidbody = gameObject.AddComponent<Rigidbody>();
            zombieRigidbody.mass = 50f;
            zombieRigidbody.linearDamping = 5f;
            zombieRigidbody.angularDamping = 5f;
            zombieRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
        }
    }

    // ===============================================
    // GETTERS
    // ===============================================

    public bool IsGrabbing() => isGrabbing;
    public bool CanGrab() => canGrab && !isGrabbing;
    public bool IsInKnockbackGracePeriod() => isInKnockbackGrace;

    // ===============================================
    // GRAB SYSTEM
    // ===============================================

    public void AttemptGrab(GameObject target)
    {
        if (stats == null) return;
        if (isGrabbing || !canGrab) return;

        float distance = Vector3.Distance(transform.position, target.transform.position);

        if (distance <= stats.grabRange)
        {
            // Verifier que la cible est devant le zombie
            Vector3 directionToTarget = (target.transform.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, directionToTarget);

            // Grab seulement si dans un arc de 120 degres devant
            if (angle <= 60f)
            {
                StartCoroutine(GrabSequence(target));
            }
            else
            {
                Debug.Log($"Cible derriere le zombie, pas de grab! (angle: {angle} deg)");
            }
        }
    }

    IEnumerator GrabSequence(GameObject target)
    {
        isGrabbing = true;
        grabbedTarget = target;
        currentMashes = 0;

        // Calculer mashes to escape selon les bras du zombie
        int mashesRequired = CalculateMashesToEscape();

        // Desactiver le mouvement du joueur
        targetMovement = target.GetComponent<PlayerPhysicsMovement>();
        if (targetMovement != null)
        {
            targetMovement.enabled = false;
        }

        // Desactiver temporairement l'IA du zombie
        if (zombieAI != null)
        {
            zombieAI.enabled = false;
        }

        Debug.Log($"{gameObject.name} grabbed {target.name}! MASH Space to escape! ({mashesRequired} mashes needed)");

        float elapsed = 0f;
        bool escaped = false;

        // Phase de grab avec possibilite de mashing
        while (elapsed < stats.grabRange && !escaped) // Utilise grabRange comme duree (tu peux creer un grabDuration dans stats si tu preferes)
        {
            elapsed += Time.deltaTime;

            // Verifier le mashing
            if (Input.GetKeyDown(KeyCode.Space))
            {
                currentMashes++;
                Debug.Log($"Mash count: {currentMashes}/{mashesRequired}");

                if (currentMashes >= mashesRequired)
                {
                    escaped = true;
                    Debug.Log($"{target.name} escaped!");
                }
            }

            yield return null;
        }

        // Si pas echappe, infliger des degats
        if (!escaped)
        {
            yield return StartCoroutine(BiteTarget(target));
        }

        // Liberer la cible avec knockback
        ReleaseTarget(escaped);
    }

    /// <summary>
    /// Calcule le nombre de mashes necessaires selon les bras du zombie
    /// </summary>
    int CalculateMashesToEscape()
    {
        if (zombieHealth == null)
            return 10; // Valeur par defaut

        int armCount = zombieHealth.GetArmCount();

        if (armCount == 2)
        {
            // Deux bras : mashes normaux
            return 10; // Tu peux mettre ca dans stats si tu veux
        }
        else if (armCount == 1)
        {
            // Un bras : reduction de 50%
            return Mathf.RoundToInt(10 * (1f - stats.oneArmMashReduction));
        }
        else
        {
            // Pas de bras : ne devrait pas pouvoir grab
            return 0;
        }
    }

    IEnumerator BiteTarget(GameObject target)
    {
        Debug.Log($"{gameObject.name} is biting {target.name}!");

        // Animation de morsure
        yield return new WaitForSeconds(0.5f);

        // Infliger des degats
        PlayerHealth humanHealth = target.GetComponent<PlayerHealth>();
        if (humanHealth != null && !humanHealth.IsDead())
        {
            humanHealth.TakeDamage(stats.biteDamage);
            Debug.Log($"{target.name} took {stats.biteDamage} damage from bite!");
        }
    }

    public void ForceRelease()
    {
        if (isGrabbing)
        {
            Debug.Log($"{gameObject.name} was forced to release (probably shot)!");
            StopAllCoroutines();
            ReleaseTarget(true);
        }
    }

    void ReleaseTarget(bool applyKnockback = false)
    {
        // Verifier si on est en RedLight AVANT de faire le knockback
        bool isRedLight = gameManager != null && gameManager.IsInRedLight();

        if (grabbedTarget != null)
        {
            // Appliquer le knockback au zombie SEULEMENT si pas en RedLight
            if (applyKnockback && !isRedLight)
            {
                ApplyKnockback();
            }
            else if (applyKnockback && isRedLight)
            {
                // En RedLight, on fait juste une separation sans knockback violent
                Debug.Log("Release en RedLight - pas de knockback pour eviter la mort!");
                StartCoroutine(GentleReleaseRecovery());
            }

            // Reactiver le mouvement du joueur
            if (targetMovement != null)
            {
                targetMovement.enabled = true;

                // Donner une petite impulsion au joueur SEULEMENT si pas en RedLight
                if (!isRedLight)
                {
                    Rigidbody playerRb = grabbedTarget.GetComponent<Rigidbody>();
                    if (playerRb != null)
                    {
                        Vector3 pushDirection = (grabbedTarget.transform.position - transform.position).normalized;
                        pushDirection.y = 0.2f;
                        playerRb.AddForce(pushDirection * (stats.knockbackResistance * 5f), ForceMode.Impulse);
                    }

                    // Activer la grace period pour le joueur
                    PlayerHealth playerHealth = grabbedTarget.GetComponent<PlayerHealth>();
                    if (playerHealth != null)
                    {
                        playerHealth.StartKnockbackGracePeriod(stats.knockbackGracePeriod);
                    }
                }
            }
        }

        if (zombieAI != null)
        {
            zombieAI.enabled = true;
        }

        isGrabbing = false;
        grabbedTarget = null;
        targetMovement = null;
        currentMashes = 0;

        Debug.Log("Target released!");
    }

    void ApplyKnockback()
    {
        if (zombieRigidbody != null && grabbedTarget != null)
        {
            // Calculer la direction du knockback (opposee au joueur)
            Vector3 knockbackDirection = (transform.position - grabbedTarget.transform.position).normalized;
            knockbackDirection.y = 0;

            // Appliquer la force (depuis stats)
            Vector3 knockbackVelocity = knockbackDirection * 10f + Vector3.up * stats.upwardForce;
            zombieRigidbody.linearVelocity = knockbackVelocity;

            Debug.Log($"{gameObject.name} knocked back!");

            // Demarrer le cooldown et la recuperation
            StartCoroutine(KnockbackRecovery());
        }
    }

    IEnumerator KnockbackRecovery()
    {
        canGrab = false;
        isInKnockbackGrace = true;

        // Desactiver temporairement l'IA pendant le knockback
        if (zombieAI != null)
        {
            zombieAI.enabled = false;
        }

        // Grace period pour eviter la detection
        yield return new WaitForSeconds(stats.knockbackGracePeriod);
        isInKnockbackGrace = false;

        // Attendre la fin du knockback
        yield return new WaitForSeconds(stats.knockbackDuration - stats.knockbackGracePeriod);

        // Reactiver l'IA
        if (zombieAI != null)
        {
            zombieAI.enabled = true;
        }

        // Attendre le cooldown avant de pouvoir re-grab
        yield return new WaitForSeconds(stats.grabCooldown - stats.knockbackDuration);

        canGrab = true;
        Debug.Log($"{gameObject.name} can grab again!");
    }

    IEnumerator GentleReleaseRecovery()
    {
        canGrab = false;

        // Juste un cooldown sans mouvement violent
        yield return new WaitForSeconds(stats.grabCooldown);

        canGrab = true;
        Debug.Log($"{gameObject.name} can grab again after gentle release!");
    }

    void OnDrawGizmosSelected()
    {
        if (stats == null) return;

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, stats.grabRange);

        // Afficher l'etat du cooldown
        if (!canGrab)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, Vector3.one * 0.5f);
        }
    }
}