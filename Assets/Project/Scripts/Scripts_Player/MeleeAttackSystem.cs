using UnityEngine;
using System.Collections;

public class MeleeAttackSystem : MonoBehaviour
{
    [Header("Attack Settings")]
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackKnockbackForce = 35f;
    [SerializeField] private float attackDuration = 0.3f;
    [SerializeField] private float comboWindow = 0.8f;
    [SerializeField] private float knockdownDuration = 3f;
    [SerializeField] private int maxComboCount = 3;

    [Header("Stun Settings")]
    [SerializeField] private float staggerDuration = 0.5f;
    [SerializeField] private float fallDuration = 1f;
    [SerializeField] private float getUpDuration = 1.5f;

    [Header("Visual Feedback")]
    [SerializeField] private float armRotationAngle = 90f;

    private Rigidbody rb;
    private HumanHealth health;
    private PlayerPhysicsMovement movement;

    private int comboCount = 0;
    private float lastAttackTime = 0f;
    private bool isAttacking = false;
    private bool isStunned = false;

    private enum AttackArm { Left, Right }
    private AttackArm currentArm = AttackArm.Left;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        health = GetComponent<HumanHealth>();
        movement = GetComponent<PlayerPhysicsMovement>();
    }

    void OnDisable()
    {
        // Réactiver le movement si le script est désactivé
        if (movement != null && !movement.enabled)
        {
            movement.enabled = true;
        }

        // Reset les états
        isAttacking = false;
        isStunned = false;
    }

    void Update()
    {
        if (Time.time - lastAttackTime > comboWindow && comboCount > 0)
        {
            ResetCombo();
        }

        if (Input.GetMouseButtonDown(0) && CanAttack())
        {
            StartCoroutine(PerformAttack());
        }
    }

    bool CanAttack()
    {
        if (isAttacking)
        {
            Debug.Log("Cannot attack: isAttacking = true");
            return false;
        }

        if (isStunned)
        {
            Debug.Log("Cannot attack: isStunned = true");
            return false;
        }

        if (health != null && health.IsDead())
        {
            Debug.Log("Cannot attack: player is dead");
            return false;
        }

        if (movement != null && !movement.enabled)
        {
            Debug.Log("Cannot attack: movement disabled, trying to re-enable...");
            if (!isAttacking && !isStunned)
            {
                movement.enabled = true;
            }
        }

        Debug.Log("CAN ATTACK!");
        return true;
    }

    IEnumerator PerformAttack()
    {
        isAttacking = true;
        comboCount++;
        lastAttackTime = Time.time;

        if (movement != null) movement.enabled = false;

        StartCoroutine(RotateArmVisual());

        yield return new WaitForSeconds(0.1f);
        DetectAndHitTargets();

        yield return new WaitForSeconds(attackDuration - 0.1f);

        currentArm = (currentArm == AttackArm.Left) ? AttackArm.Right : AttackArm.Left;

        isAttacking = false;

        if (comboCount >= maxComboCount)
        {
            yield return StartCoroutine(EnterStunSequence());
        }
        else
        {
            if (movement != null) movement.enabled = true;
        }
    }

    IEnumerator RotateArmVisual()
    {
        float elapsed = 0f;
        float rotationDirection = (currentArm == AttackArm.Left) ? -1f : 1f;
        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = startRotation * Quaternion.Euler(0, rotationDirection * armRotationAngle, 0);

        while (elapsed < attackDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (attackDuration / 2f);
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < attackDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (attackDuration / 2f);
            transform.rotation = Quaternion.Slerp(targetRotation, startRotation, t);
            yield return null;
        }

        transform.rotation = startRotation;
    }

    void DetectAndHitTargets()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            attackRange,
            LayerMask.GetMask("Zombie")
        );

        foreach (Collider hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            Vector3 directionToTarget = (hit.transform.position - transform.position).normalized;
            float angleToTarget = Vector3.Angle(transform.forward, directionToTarget);

            if (angleToTarget <= 90f)
            {
                Rigidbody targetRb = hit.GetComponent<Rigidbody>();

                if (targetRb != null)
                {
                    Vector3 knockbackDir = (hit.transform.position - transform.position).normalized;
                    knockbackDir.y = 0;

                    targetRb.AddForce(knockbackDir * attackKnockbackForce, ForceMode.Impulse);

                    StartCoroutine(KnockdownTarget(hit.gameObject));

                    Debug.Log($"{gameObject.name} melee hit: {hit.gameObject.name}!");
                }
            }
        }
    }

    IEnumerator KnockdownTarget(GameObject target)
    {
        ZombieAI zombieAI = target.GetComponent<ZombieAI>();
        if (zombieAI != null)
        {
            zombieAI.enabled = false;
        }

        yield return new WaitForSeconds(knockdownDuration);

        if (zombieAI != null)
        {
            zombieAI.enabled = true;
        }
    }

    IEnumerator EnterStunSequence()
    {
        isStunned = true;
        ResetCombo();

        yield return StartCoroutine(StaggerPhase());
        yield return StartCoroutine(FallPhase());
        yield return StartCoroutine(GetUpPhase());

        isStunned = false;
        if (movement != null) movement.enabled = true;
    }

    IEnumerator StaggerPhase()
    {
        float elapsed = 0f;
        Vector3 originalPos = transform.position;

        while (elapsed < staggerDuration)
        {
            elapsed += Time.deltaTime;

            float randomRotation = Mathf.Sin(elapsed * 20f) * 30f;
            transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y + randomRotation * Time.deltaTime, 0);

            Vector3 randomOffset = new Vector3(
                Mathf.Sin(elapsed * 15f) * 0.3f,
                0,
                Mathf.Cos(elapsed * 15f) * 0.3f
            );
            transform.position = originalPos + randomOffset;

            yield return null;
        }

        transform.position = originalPos;
    }

    IEnumerator FallPhase()
    {
        yield return new WaitForSeconds(fallDuration);
    }

    IEnumerator GetUpPhase()
    {
        float elapsed = 0f;
        Quaternion targetRot = Quaternion.Euler(0, transform.eulerAngles.y, 0);

        while (elapsed < getUpDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / getUpDuration;

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);

            yield return null;
        }

        transform.rotation = targetRot;
    }

    void ResetCombo()
    {
        comboCount = 0;
        currentArm = AttackArm.Left;
    }

    public bool IsStunned() => isStunned;
    public bool IsAttacking() => isAttacking;
    public int GetComboCount() => comboCount;
}