using UnityEngine;
using System.Collections;

public class ZombieGrabSystem : MonoBehaviour
{
    [Header("Grab Settings")]
    [SerializeField] private float grabDuration = 3f;
    [SerializeField] private float grabRange = 1.5f;
    [SerializeField] private int mashesToEscape = 10;
    [SerializeField] private KeyCode escapeKey = KeyCode.Space;

    [Header("Bite Settings")]
    [SerializeField] private float biteAnimationDuration = 0.5f;

    [Header("Knockback Settings")]
    [SerializeField] private float knockbackForce = 10f;
    [SerializeField] private float knockbackUpwardForce = 2f;
    [SerializeField] private float knockbackDuration = 0.5f;
    [SerializeField] private float grabCooldown = 1.5f; // Temps avant que le zombie puisse re-grab
    [SerializeField] private float knockbackGracePeriod = 0.3f; // Grace period pour éviter la détection pendant le knockback

    private ZombieAI zombieAI;
    private Rigidbody zombieRigidbody;
    private GameManager gameManager;
    private bool isGrabbing = false;
    private bool canGrab = true; // Nouveau flag pour le cooldown
    private bool isInKnockbackGrace = false;
    private GameObject grabbedTarget;
    private PlayerPhysicsMovement targetMovement;
    private int currentMashes = 0;

    void Start()
    {
        zombieAI = GetComponent<ZombieAI>();
        zombieRigidbody = GetComponent<Rigidbody>();
        gameManager = FindObjectOfType<GameManager>();

        // Si pas de Rigidbody, on en ajoute un pour le knockback
        if (zombieRigidbody == null)
        {
            zombieRigidbody = gameObject.AddComponent<Rigidbody>();
            zombieRigidbody.mass = 50f; // Masse assez élevée pour que le zombie ne vole pas trop
            zombieRigidbody.linearDamping = 5f;
            zombieRigidbody.angularDamping = 5f;
            zombieRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
        }
    }

    public bool IsGrabbing() => isGrabbing;
    public bool CanGrab() => canGrab && !isGrabbing; // Nouveau check pour le cooldown
    public bool IsInKnockbackGracePeriod() => isInKnockbackGrace; // Pour le GameManager

    public void AttemptGrab(GameObject target)
    {
        if (isGrabbing || !canGrab) return; // Vérifier aussi le cooldown

        float distance = Vector3.Distance(transform.position, target.transform.position);

        if (distance <= grabRange)
        {
            // NOUVEAU : Vérifier que la cible est devant le zombie
            Vector3 directionToTarget = (target.transform.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, directionToTarget);

            // Seulement grab si la cible est dans un arc de 120° devant
            if (angle <= 60f) // 60° de chaque côté = 120° total
            {
                StartCoroutine(GrabSequence(target));
            }
            else
            {
                Debug.Log($"Cible derrière le zombie, pas de grab! (angle: {angle}°)");
            }
        }
    }

    IEnumerator GrabSequence(GameObject target)
    {
        isGrabbing = true;
        grabbedTarget = target;
        currentMashes = 0;

        // Désactiver le mouvement du joueur
        targetMovement = target.GetComponent<PlayerPhysicsMovement>();
        if (targetMovement != null)
        {
            targetMovement.enabled = false;
        }

        // Désactiver temporairement l'IA du zombie
        if (zombieAI != null)
        {
            zombieAI.enabled = false;
        }

        Debug.Log($"{gameObject.name} grabbed {target.name}! MASH {escapeKey} to escape!");

        float elapsed = 0f;
        bool escaped = false;

        // Phase de grab avec possibilité de mashing
        while (elapsed < grabDuration && !escaped)
        {
            elapsed += Time.deltaTime;

            // Vérifier le mashing
            if (Input.GetKeyDown(escapeKey))
            {
                currentMashes++;
                Debug.Log($"Mash count: {currentMashes}/{mashesToEscape}");

                if (currentMashes >= mashesToEscape)
                {
                    escaped = true;
                    Debug.Log($"{target.name} escaped!");
                }
            }

            yield return null;
        }

        // Si pas échappé, infliger des dégâts
        if (!escaped)
        {
            yield return StartCoroutine(BiteTarget(target));
        }

        // Libérer la cible avec knockback
        ReleaseTarget(escaped);
    }

    IEnumerator BiteTarget(GameObject target)
    {
        Debug.Log($"{gameObject.name} is biting {target.name}!");

        // Animation de morsure (ici juste un délai)
        yield return new WaitForSeconds(biteAnimationDuration);

        // Infliger des dégâts
        HumanHealth humanHealth = target.GetComponent<HumanHealth>();
        if (humanHealth != null && !humanHealth.IsDead())
        {
            humanHealth.TakeDamage();
            Debug.Log($"{target.name} took damage from bite!");
        }
    }

    public void ForceRelease()
    {
        if (isGrabbing)
        {
            Debug.Log($"{gameObject.name} was forced to release (probably shot)!");
            StopAllCoroutines();
            ReleaseTarget(true); // Knockback même en cas de force release
        }
    }

    void ReleaseTarget(bool applyKnockback = false)
    {
        // Vérifier si on est en RedLight AVANT de faire le knockback
        bool isRedLight = gameManager != null && gameManager.IsInRedLight();

        if (grabbedTarget != null)
        {
            // Appliquer le knockback au zombie SEULEMENT si pas en RedLight
            // (ou si c'est un ForceRelease par tir)
            if (applyKnockback && !isRedLight)
            {
                ApplyKnockback();
            }
            else if (applyKnockback && isRedLight)
            {
                // En RedLight, on fait juste une séparation sans knockback violent
                Debug.Log("Release en RedLight - pas de knockback pour éviter la mort!");
                StartCoroutine(GentleReleaseRecovery());
            }

            // Réactiver le mouvement du joueur
            if (targetMovement != null)
            {
                targetMovement.enabled = true;
                targetMovement.ResetMovementState();

                // Donner une petite impulsion au joueur SEULEMENT si pas en RedLight
                if (!isRedLight)
                {
                    Rigidbody playerRb = grabbedTarget.GetComponent<Rigidbody>();
                    if (playerRb != null)
                    {
                        Vector3 pushDirection = (grabbedTarget.transform.position - transform.position).normalized;
                        pushDirection.y = 0.2f; // Petite composante verticale
                        playerRb.AddForce(pushDirection * (knockbackForce * 0.5f), ForceMode.Impulse);
                    }

                    // Activer la grace period pour le joueur
                    HumanHealth playerHealth = grabbedTarget.GetComponent<HumanHealth>();
                    if (playerHealth != null)
                    {
                        playerHealth.StartKnockbackGracePeriod(knockbackGracePeriod);
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
            // Calculer la direction du knockback (opposée au joueur)
            Vector3 knockbackDirection = (transform.position - grabbedTarget.transform.position).normalized;
            knockbackDirection.y = 0; // Garder le knockback principalement horizontal

            // Appliquer la force
            Vector3 knockbackVelocity = knockbackDirection * knockbackForce + Vector3.up * knockbackUpwardForce;
            zombieRigidbody.linearVelocity = knockbackVelocity;

            Debug.Log($"{gameObject.name} knocked back!");

            // Démarrer le cooldown et la récupération
            StartCoroutine(KnockbackRecovery());
        }
    }

    IEnumerator KnockbackRecovery()
    {
        canGrab = false;
        isInKnockbackGrace = true; // Activer la grace period

        // Désactiver temporairement l'IA pendant le knockback
        if (zombieAI != null)
        {
            zombieAI.enabled = false;
        }

        // Grace period pour éviter la détection
        yield return new WaitForSeconds(knockbackGracePeriod);
        isInKnockbackGrace = false;

        // Attendre la fin du knockback
        yield return new WaitForSeconds(knockbackDuration - knockbackGracePeriod);

        // Réactiver l'IA
        if (zombieAI != null)
        {
            zombieAI.enabled = true;
        }

        // Attendre le cooldown avant de pouvoir re-grab
        yield return new WaitForSeconds(grabCooldown - knockbackDuration);

        canGrab = true;
        Debug.Log($"{gameObject.name} can grab again!");
    }

    IEnumerator GentleReleaseRecovery()
    {
        canGrab = false;

        // Juste un cooldown sans mouvement violent
        yield return new WaitForSeconds(grabCooldown);

        canGrab = true;
        Debug.Log($"{gameObject.name} can grab again after gentle release!");
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, grabRange);

        // Afficher l'état du cooldown
        if (!canGrab)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, Vector3.one * 0.5f);
        }
    }
}