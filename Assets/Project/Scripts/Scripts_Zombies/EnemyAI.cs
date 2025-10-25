using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class EnemyAI : MonoBehaviour
{
    [Header("Enemy Stats")]
    public ZombieStats stats; // On garde le m�me nom pour compatibilit� avec Zombie

    [Header("References")]
    protected Rigidbody rb;
    protected Transform targetHuman;
    protected GameManager gameManager;

    protected float lastWanderTime = 0f;
    protected float wanderTimer = 0f;

    protected float lastAttackTime = 0f;

    protected bool isStunnedByShot = false;
    protected bool isDead = false;

    protected Vector3 wanderDirection;
    protected float currentSpeed;

    protected enum State { Idle, Wandering, Chasing, Attacking, StunbySentinel, Dead }
    protected State currentState = State.Idle;

    protected virtual void Start()
    {
        if (stats == null)
        {
            Debug.LogError("Enemy stats non assign� sur " + gameObject.name);
            return;
        }

        rb = GetComponent<Rigidbody>();
        gameManager = FindObjectOfType<GameManager>();

        currentSpeed = stats.walkSpeed;

        StartCoroutine(DetectionLoop());
    }

    protected virtual void Update()
    {
        if (stats == null || isDead) return;
        if (gameManager != null && gameManager.zombieStunBySentinel) { StopMovement(); return; }

        switch (currentState)
        {
            case State.Idle: HandleIdleState(); break;
            case State.Wandering: HandleWanderingState(); break;
            case State.Chasing: HandleChasingState(); break;
            case State.Attacking: HandleAttackingState(); break;
            case State.StunbySentinel: HandleStunbySentinelState(); break; //état de stun après s'e^tre fait tirer dessus par la sentinelle
            case State.Dead: StopMovement(); break;
        }
    }

    protected IEnumerator DetectionLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(stats.detectionCheckInterval);
            if (isDead) continue;
            if (isStunnedByShot) continue;
            DetectHumans();
        }
    }

    protected virtual void DetectHumans()
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

    protected virtual void HandleStunbySentinelState()
    { 
     
       
    }
    
    protected virtual void HandleIdleState()
    {
        StopMovement();
        if (Time.time - lastWanderTime >= stats.wanderInterval)
        {
            lastWanderTime = Time.time;
            if (Random.value < stats.idleWanderChance)
                StartWandering();
        }
    }

    protected virtual void HandleWanderingState()
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

    protected virtual void HandleChasingState()
    {
        if (targetHuman == null) { currentState = State.Idle; return; }

        float distance = Vector3.Distance(transform.position, targetHuman.position);
        if (distance <= stats.grabRange) { currentState = State.Attacking; return; }

        Vector3 direction = (targetHuman.position - transform.position).normalized;
        MoveInDirection(direction, currentSpeed);
    }

    protected virtual void HandleAttackingState()
    {
        // Par d�faut, comportement d'attaque vide (surcharge dans ZombieAI)
        StopMovement();
    }

    protected virtual void StartWandering()
    {
        currentState = State.Wandering;
        wanderTimer = 0f;
        wanderDirection = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
    }

    protected virtual void MoveInDirection(Vector3 direction, float speed)
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

    protected void StopMovement()
    {
        rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
    }

    public void SetDead(bool value)
    {
        isDead = value;
        if (value) currentState = State.Dead;
    }

    protected virtual void OnDrawGizmosSelected()
    {
        if (stats == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, stats.detectionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stats.grabRange);
    }
}
