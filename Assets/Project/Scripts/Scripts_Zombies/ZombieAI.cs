using UnityEngine;
using System.Collections;
using NUnit.Framework.Constraints;

[RequireComponent(typeof(Rigidbody))]
public class ZombieAI : MonoBehaviour
{
    [Header("Zombie Stats")]
    public ZombieStats stats;

    [Header("References")]
    private Rigidbody rb;
    private ZombieHealth health;
    private ZombieGrabSystem grabSystem;

    private Transform targetHuman;

    private Vector3 wanderDirection;
    private float lastWanderTime = 0f;
    private float wanderTimer = 0f;

    private float lastAttackTime = 0f;

    private bool isStunnedByShot = false;

    private enum State { Idle, Wandering, Chasing, Attacking }
    private State currentState = State.Idle;

    private float currentSpeed;

    [Header("Références")]
    public GameManager gameManager;

    void Start()
    {
        if (stats == null)
        {
            Debug.LogError("ZombieStats non assigne sur " + gameObject.name);
            return;
        }

        rb = GetComponent<Rigidbody>();
        health = GetComponent<ZombieHealth>();
        grabSystem = GetComponent<ZombieGrabSystem>();

        currentSpeed = stats.walkSpeed;

        StartCoroutine(DetectionLoop());
    }

    void Update()
    {
        if (stats == null) return;
        if (health != null && health.IsDead()) { StopMovement(); return; }
        if (gameManager.zombieStunBySentinel == true) { StopMovement(); return; }
        if (grabSystem != null && grabSystem.IsGrabbing()) { StopMovement(); return; }

        switch (currentState)
        {
            case State.Idle: HandleIdleState(); break;
            case State.Wandering: HandleWanderingState(); break;
            case State.Chasing: HandleChasingState(); break;
            case State.Attacking: HandleAttackingState(); break;
        }
    }

    IEnumerator DetectionLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(stats.detectionCheckInterval);
            if (health != null && health.IsDead()) continue;
            if (isStunnedByShot) continue;
            if (grabSystem != null && grabSystem.IsGrabbing()) continue;
            DetectHumans();
        }
    }

    void DetectHumans()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, stats.detectionRadius, stats.targetLayer);
        Transform closestHuman = null;
        float closestDistance = Mathf.Infinity;

        foreach (Collider hit in hits)
        {
            PlayerHealth humanHealth = hit.GetComponent<PlayerHealth>();
            if (humanHealth != null && !humanHealth.IsDead())
            {
                float distance = Vector3.Distance(transform.position, hit.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestHuman = hit.transform;
                }
            }
        }

        if (closestHuman != null)
        {
            targetHuman = closestHuman;
            if (closestDistance <= stats.grabRange)
                currentState = State.Attacking;
            else
                currentState = State.Chasing;
        }
        else
        {
            targetHuman = null;
            if (currentState == State.Chasing || currentState == State.Attacking)
                currentState = State.Idle;
        }
    }

    void HandleIdleState()
    {
        StopMovement();
        if (Time.time - lastWanderTime >= stats.wanderInterval)
        {
            lastWanderTime = Time.time;
            if (UnityEngine.Random.value < stats.idleWanderChance)
                StartWandering();
        }
    }

    void HandleWanderingState()
    {
        wanderTimer += Time.deltaTime;
        if (wanderTimer >= stats.wanderDuration)
        {
            currentState = State.Idle;
            wanderTimer = 0f;
            StopMovement();
            return;
        }
        MoveInDirection(wanderDirection, currentSpeed);
    }

    void HandleChasingState()
    {
        if (targetHuman == null) { currentState = State.Idle; return; }

        float distance = Vector3.Distance(transform.position, targetHuman.position);
        if (distance <= stats.grabRange) { currentState = State.Attacking; return; }

        Vector3 direction = (targetHuman.position - transform.position).normalized;
        MoveInDirection(direction, currentSpeed);
    }

    void HandleAttackingState()
    {
        if (targetHuman == null) { currentState = State.Idle; return; }

        float distance = Vector3.Distance(transform.position, targetHuman.position);
        if (distance > stats.grabRange * 1.5f) { currentState = State.Chasing; return; }

        Vector3 direction = (targetHuman.position - transform.position).normalized;
        direction.y = 0;
        if (direction.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * stats.rotationSpeed);
        }

        if (Time.time - lastAttackTime >= stats.knockbackGracePeriod)
        {
            if (grabSystem != null && grabSystem.CanGrab())
                AttemptAttack();
        }
        else
        {
            StopMovement();
        }
    }

    void AttemptAttack()
    {
        if (targetHuman == null) return;

        lastAttackTime = Time.time;

        if (grabSystem != null && grabSystem.CanGrab())
        {
            grabSystem.AttemptGrab(targetHuman.gameObject);
        }
        else if (grabSystem == null)
        {
            PlayerHealth humanHealth = targetHuman.GetComponent<PlayerHealth>();
            if (humanHealth != null && !humanHealth.IsDead())
            {
                humanHealth.TakeDamage(stats.biteDamage);
                Debug.Log(gameObject.name + " attacked " + targetHuman.name + "!");
            }
        }
    }

    void StartWandering()
    {
        currentState = State.Wandering;
        wanderTimer = 0f;
        wanderDirection = new Vector3(UnityEngine.Random.Range(-1f, 1f), 0, UnityEngine.Random.Range(-1f, 1f)).normalized;
    }

    void MoveInDirection(Vector3 direction, float speed)
    {
        direction.y = 0;
        direction.Normalize();
        if (direction.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * stats.rotationSpeed);
            Vector3 velocity = direction * speed;
            rb.linearVelocity = new Vector3(velocity.x, rb.linearVelocity.y, velocity.z);
        }
    }

    void StopMovement()
    {
        rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
    }

    public void UpdateSpeed(float currentHealth, bool isCrawler)
    {
        if (stats == null) return;

        bool isChasing = (currentState == State.Chasing || currentState == State.Attacking);
        currentSpeed = stats.GetAdjustedSpeed(currentHealth, isChasing, isCrawler);
        Debug.Log(gameObject.name + " speed updated: " + currentSpeed);
    }

    void OnDrawGizmosSelected()
    {
        if (stats == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, stats.detectionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stats.grabRange);

        if (targetHuman != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, targetHuman.position);
        }

        if (grabSystem != null && !grabSystem.CanGrab())
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawCube(transform.position + Vector3.up * 2.5f, Vector3.one * 0.3f);
        }
    }
}
