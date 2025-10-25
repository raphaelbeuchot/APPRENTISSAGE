using UnityEngine;
using System.Collections;

public class ZombieGrabSystem : MonoBehaviour
{
    [Header("Zombie Stats")]
    public ZombieStats stats;

    [Header("References")]
    private ZombieAI zombieAI;
    private ZombieHealth zombieHealth;
    private Rigidbody zombieRigidbody;
    private GameManager gameManager;

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

        if (zombieRigidbody == null)
        {
            zombieRigidbody = gameObject.AddComponent<Rigidbody>();
            zombieRigidbody.mass = 50f;
            zombieRigidbody.linearDamping = 5f;
            zombieRigidbody.angularDamping = 5f;
            zombieRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
        }
    }

    public bool IsGrabbing() => isGrabbing;
    public bool CanGrab() => canGrab && !isGrabbing;
    public bool IsInKnockbackGracePeriod() => isInKnockbackGrace;

    public void AttemptGrab(GameObject target)
    {
        if (stats == null) return;
        if (isGrabbing || !canGrab || gameManager.stunBySentinel || isInKnockbackGrace) return;

        float distance = Vector3.Distance(transform.position, target.transform.position);

        if (distance <= stats.grabRange)
        {
            Vector3 directionToTarget = (target.transform.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, directionToTarget);

            if (angle <= 60f)
            {
                StartCoroutine(GrabSequence(target));
            }
            else
            {
                Debug.Log("Cible derriere le zombie, pas de grab! (angle: " + angle + " deg)");
            }
        }
    }

    IEnumerator GrabSequence(GameObject target)
    {
        isGrabbing = true;
        grabbedTarget = target;
        currentMashes = 0;

        int mashesRequired = CalculateMashesToEscape();

        targetMovement = target.GetComponent<PlayerPhysicsMovement>();
        if (targetMovement != null)
            targetMovement.enabled = false;

        if (zombieAI != null)
            zombieAI.enabled = false;

        Debug.Log(gameObject.name + " grabbed " + target.name + "! MASH Space to escape! (" + mashesRequired + " mashes needed)");

        float elapsed = 0f;
        bool escaped = false;

        while (elapsed < stats.grabDuration && !escaped)
        {
            elapsed += Time.deltaTime;

            if (Input.GetKeyDown(KeyCode.Space))
            {
                currentMashes++;
                Debug.Log("Mash count: " + currentMashes + "/" + mashesRequired);
                if (currentMashes >= mashesRequired)
                {
                    escaped = true;
                    Debug.Log(target.name + " escaped!");
                }
            }

            yield return null;
        }

        if (!escaped)
        {
            yield return StartCoroutine(BiteTarget(target));
        }

        ReleaseTarget(escaped);
    }

    int CalculateMashesToEscape()
    {
        if (zombieHealth == null) return 10;

        int armCount = zombieHealth.GetArmCount();

        if (armCount == 2)
        {
            return 10;
        }
        else if (armCount == 1)
        {
            return Mathf.RoundToInt(10 * (1f - stats.oneArmMashReduction));
        }
        else
        {
            return 0;
        }
    }

    IEnumerator BiteTarget(GameObject target)
    {
        Debug.Log(gameObject.name + " is biting " + target.name + "!");
        yield return new WaitForSeconds(0.5f);

        PlayerHealth humanHealth = target.GetComponent<PlayerHealth>();
        if (humanHealth != null && !humanHealth.IsDead())
        {
            humanHealth.TakeDamage(stats.biteDamage);
            Debug.Log(target.name + " took " + stats.biteDamage + " damage from bite!");
        }
    }

    public void ForceRelease()
    {
        /*
        if (isGrabbing)
        {
            Debug.Log(gameObject.name + " was forced to release (probably shot)!");
            StopAllCoroutines();
            ReleaseTarget(true);
        }*/
    }

    void ReleaseTarget(bool applyKnockback = false)
    {
        targetMovement.ForceStop();
        ApplyKnockback();

        if (grabbedTarget != null)
        {
            if (applyKnockback)
            {
                ApplyKnockback();
            }

            targetMovement.enabled = true;
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
            Vector3 knockbackDirection = (transform.position - grabbedTarget.transform.position).normalized;
            knockbackDirection.y = 0;

            Vector3 knockbackVelocity = knockbackDirection * stats.knockbackForce;
            zombieRigidbody.linearVelocity = knockbackVelocity;

            Debug.Log(gameObject.name + " knocked back!");
            StartCoroutine(KnockbackRecovery());
        }
    }

    IEnumerator KnockbackRecovery()
    {
        canGrab = false;
        isInKnockbackGrace = true;

        if (zombieAI != null)
            zombieAI.enabled = false;

        yield return new WaitForSeconds(stats.knockbackGracePeriod);

        isInKnockbackGrace = false;
        yield return new WaitForSeconds(stats.knockbackDuration - stats.knockbackGracePeriod);

        if (zombieAI != null)
            zombieAI.enabled = true;

        canGrab = true;
        Debug.Log(gameObject.name + " can grab again!");
    }

    void OnDrawGizmosSelected()
    {
        if (stats == null) return;

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, stats.grabRange);

        if (!canGrab)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, Vector3.one * 0.5f);
        }
    }
}
