using UnityEngine;
using System.Collections;

public class HitAttack : MonoBehaviour, IAttackBehavior
{
    private EnemyAI_AStar enemy;
    private EnemyHealth enemyHealth;
    private EnemyStats stats;
    private PlayerStats playerStats;
    private Rigidbody enemyRb;
    private PlayerPhysicsMovement player;
    private Rigidbody playerRb;
    private Animator animator;
    private Renderer enemyRenderer;
    private Material originalMaterial;
    private Material whiteMaterial;

    public bool isInWindup = false;
    private bool windupAnimComplete = false;
    private Coroutine windupCoroutine;

    public bool isLockedInIdle = false;
    private bool isAttacking = false;
    private AudioSource audioSource;

    public void Initialize(EnemyStats stats, PlayerStats playerStats, Transform enemyTransform, Rigidbody enemyRigidbody)
    {
        this.stats = stats;
        this.playerStats = playerStats;
        this.enemyRb = enemyRigidbody;

        enemy = GetComponent<EnemyAI_AStar>();
        enemyHealth = GetComponent<EnemyHealth>();
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        player = FindFirstObjectByType<PlayerPhysicsMovement>();
        if (player != null)
            playerRb = player.GetComponent<Rigidbody>();

        enemyRenderer = GetComponentInChildren<Renderer>();
        if (enemyRenderer != null)
        {
            originalMaterial = enemyRenderer.material;
            whiteMaterial = new Material(originalMaterial);
            whiteMaterial.color = Color.white;
        }
    }

    public bool CanAttack() => !isInWindup && !isAttacking && !isLockedInIdle;
    public bool IsAttacking() => isAttacking;
    public bool IsInSpecialState() => isInWindup || isAttacking;

    // Non utilise pour Hitter mais requis par IAttackBehavior
    public bool IsGrabbing() => false;

    public void AttemptAttack(GameObject target)
    {
        // Pas utilise directement, on passe par StartWindup
    }

    public void StartWindup()
    {
        if (isInWindup || isAttacking || isLockedInIdle) return;
        windupCoroutine = StartCoroutine(WindupCoroutine());
    }

    IEnumerator WindupCoroutine()
    {
        isInWindup = true;
        windupAnimComplete = false;

        if (animator != null)
            animator.SetTrigger("HitWindupTrigger");
        if (audioSource != null && stats.hitWindupSound != null)
            audioSource.PlayOneShot(stats.hitWindupSound);

        StartCoroutine(WindupWhiteEffect());

        while (!windupAnimComplete)
        {
            if (enemyHealth != null && enemyHealth.IsDead())
            {
                CancelWindup();
                yield break;
            }

            if (enemyHealth != null && enemyHealth.IsRecovering())
            {
                CancelWindup();
                yield break;
            }

            // AJOUT SENTINELLE
            if (enemy != null && enemy.isStunnedBySentinel)
            {
                CancelWindup();
                yield break;
            }

            yield return null;
        }

        isInWindup = false;
    }

    IEnumerator WindupWhiteEffect()
    {
        while (isInWindup)
        {
            if (enemyRenderer != null && whiteMaterial != null)
                enemyRenderer.material = whiteMaterial;
            yield return null;
        }
        RestoreVisual();
    }

    void RestoreVisual()
    {
        if (enemyRenderer != null && originalMaterial != null)
            enemyRenderer.material = originalMaterial;
    }

    // Appele par EnemyAnimationEvents via Animation Event
    public void OnHitLand()
    {
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.transform.position);
        if (dist > stats.attackRange)
        {
            FailHit();
            return;
        }

        Vector3 dirToPlayer = (player.transform.position - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, dirToPlayer);
        if (angle > stats.detectionAngle / 2f)
        {
            FailHit();
            return;
        }

        Vector3 losOrigin = transform.position + Vector3.up * 0.5f;
        Vector3 losTarget = player.transform.position + Vector3.up * 0.5f;
        Vector3 losDir = (losTarget - losOrigin).normalized;
        float losDist = Vector3.Distance(losOrigin, losTarget);

        if (Physics.Raycast(losOrigin, losDir, losDist, LayerMask.GetMask("Obstacle")))
        {
            FailHit();
            return;
        }

        // Hit valide
        isAttacking = true;
        windupAnimComplete = true;

        // Degats
        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        if (playerHealth != null)
            playerHealth.TakeDamage((int)stats.meleeDamage);

        // Recoil player
        Vector3 recoilDir = (player.transform.position - transform.position).normalized;
        recoilDir.y = 0f;
        player.ApplyKnockback(recoilDir * stats.meleeKnockbackForce, 0.3f);

        if (audioSource != null && stats.hitLandSound != null)
            audioSource.PlayOneShot(stats.hitLandSound);
        if (animator != null)
            animator.SetTrigger("HitEndTrigger");

        StartCoroutine(FinishAttack());
    }

    void FailHit()
    {
        windupAnimComplete = true;
        isInWindup = false;
        RestoreVisual();

        if (animator != null)
            animator.SetTrigger("HitFailTrigger");

        StartCoroutine(LockInIdleCoroutine(1.5f));
    }

    IEnumerator FinishAttack()
    {
        yield return new WaitForSeconds(stats.meleeAttackDuration);
        isAttacking = false;
        RestoreVisual();
        StartCoroutine(LockInIdleCoroutine(1f));
    }

    public void CancelWindup()
    {
        if (!isInWindup) return;

        isInWindup = false;
        windupAnimComplete = true;

        if (windupCoroutine != null)
        {
            StopCoroutine(windupCoroutine);
            windupCoroutine = null;
        }

        RestoreVisual();

        if (animator != null)
            animator.SetTrigger("HitFailTrigger");

        StartCoroutine(LockInIdleCoroutine(1.5f));
    }

    IEnumerator LockInIdleCoroutine(float duration)
    {
        isLockedInIdle = true;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (enemy != null)
            {
                enemy.currentState = EnemyAI_AStar.State.Idle;
                enemy.StopMovement();
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (enemy != null)
            enemy.ResetChaseState();
        isLockedInIdle = false;
    }
    public void ForceStop()
    {
        StopAllCoroutines();

        isInWindup = false;
        isAttacking = false;
        isLockedInIdle = false;
        windupAnimComplete = true;

        RestoreVisual();

        if (animator != null)
            animator.SetTrigger("HitFailTrigger");

        if (enemyRb != null)
            enemyRb.linearVelocity = Vector3.zero;
    }
}