using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody))]
public class SwarmController : MonoBehaviour
{
    [Header("References")]
    public SwarmStats stats;
    private EnemyHealthBarUI healthBarUI;
    private bool isPlayerInRange = false;

    [Header("Runtime State")]
    public float currentHealth;

    // Components
    private Rigidbody rb;
    private NavMeshAgent agent;
    private Transform targetPlayer;
    private PlayerPhysicsMovement playerMovement;

    // State flags
    private bool isInitialized = false;
    private bool isInContact = false;
    private bool isInBourrade = false;

    // Detection
    private float detectionCheckInterval = 0.3f;
    private float lastDetectionCheck = 0f;

    // Damage tick
    private Coroutine damageTickCoroutine;

    // Visual
    private GameObject particleSystemInstance;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Setup Rigidbody
        rb.isKinematic = false;
        rb.mass = 0.1f;
        rb.linearDamping = 5f;
        rb.angularDamping = 10f;

        // Try to get NavMeshAgent
        agent = GetComponent<NavMeshAgent>();
    }

    public void Initialize(SwarmStats swarmStats)
    {
        stats = swarmStats;
        currentHealth = stats.maxHealth;

        // Setup Rigidbody constraints
        if (stats.useNavMesh && !stats.canCrossObstacles)
        {
            // Asticots : freeze Y position
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            rb.useGravity = true;
        }
        else
        {
            // Mouches : pas de gravit�, rotation freeze
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.useGravity = false;
        }

        // Setup NavMeshAgent
        if (stats.useNavMesh && agent != null)
        {
            agent.enabled = true;
            agent.speed = stats.moveSpeed;
            agent.stoppingDistance = 0.5f;
            agent.acceleration = 20f;
            agent.autoBraking = false;
        }
        else if (agent != null)
        {
            agent.enabled = false;
        }

        // Spawn particle system
        if (stats.particleSystemPrefab != null)
        {
            particleSystemInstance = Instantiate(stats.particleSystemPrefab, transform.position, Quaternion.identity, transform);
        }

        // Find player
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            targetPlayer = playerObject.transform;
            playerMovement = playerObject.GetComponent<PlayerPhysicsMovement>();
        }

        SetupHealthBar();

        isInitialized = true;
        Debug.Log($"SwarmController initialized: {stats.swarmType}, Health: {currentHealth}");
    }

    void SetupHealthBar()
    {
        EnemyHealthBarManager manager = FindObjectOfType<EnemyHealthBarManager>();
        if (manager != null)
        {
            GameObject barGO = Instantiate(manager.healthBarPrefab, manager.transform);
            healthBarUI = barGO.GetComponent<EnemyHealthBarUI>();
            manager.RegisterEnemy(transform, healthBarUI);
            healthBarUI.UpdateHealth(currentHealth, stats.maxHealth);
            healthBarUI.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (!isInitialized || stats == null) return;

        // Don't move during bourrade
        if (isInBourrade) return;

        // Detection and movement
        if (Time.time - lastDetectionCheck > detectionCheckInterval)
        {
            lastDetectionCheck = Time.time;
            DetectAndChasePlayer();
        }
    }

    void DetectAndChasePlayer()
    {
        if (targetPlayer == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);

        if (distanceToPlayer <= stats.detectionRange)
        {
            MoveTowardsPlayer();
        }
        else
        {
            StopMovement();
        }

        // Health bar visibility
        bool wasInRange = isPlayerInRange;
        isPlayerInRange = (distanceToPlayer <= 6f);

        if (isPlayerInRange && !wasInRange)
        {
            healthBarUI?.Show();
        }
        else if (!isPlayerInRange && wasInRange)
        {
            healthBarUI?.Hide();
        }
    }

    void MoveTowardsPlayer()
    {
        if (targetPlayer == null) return;

        Vector3 targetPosition = targetPlayer.position;

        // For flies, maintain hover height
        if (!stats.useNavMesh && stats.canCrossObstacles)
        {
            targetPosition.y = targetPlayer.position.y + stats.hoverHeight;
        }

        // Use NavMesh if available
        if (stats.useNavMesh && agent != null && agent.enabled)
        {
            agent.SetDestination(targetPosition);
        }
        else
        {
            // Direct movement for flies
            Vector3 direction = (targetPosition - transform.position).normalized;
            rb.linearVelocity = direction * stats.moveSpeed;
        }
    }

    void StopMovement()
    {
        if (stats.useNavMesh && agent != null && agent.enabled)
        {
            agent.ResetPath();
        }
        else
        {
            rb.linearVelocity = Vector3.zero;
        }
    }

    // ============================================
    // CONTACT EFFECTS (OnTriggerStay)
    // ============================================

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isInContact)
        {
            isInContact = true;
            ApplyContactEffects();
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player") && !isInContact)
        {
            isInContact = true;
            ApplyContactEffects();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && isInContact)
        {
            isInContact = false;
            RemoveContactEffects();
        }
    }

    void ApplyContactEffects()
    {
        if (playerMovement == null) return;

        Debug.Log($"Swarm contact! Applying effects: {stats.swarmType}");

        // Slowdown
        playerMovement.ApplySwarmSlowdown(stats.slowdownMultiplier);

        // Vision obscurity (mouches)
        if (stats.obscuresVision)
        {
            playerMovement.ApplySwarmVision(true);
        }

        // Damage over time (asticots)
        if (stats.dealsDamage)
        {
            if (damageTickCoroutine != null)
                StopCoroutine(damageTickCoroutine);
            damageTickCoroutine = StartCoroutine(DamageTickCoroutine());
        }
    }

    void RemoveContactEffects()
    {
        if (playerMovement == null) return;

        Debug.Log($"Swarm contact lost! Removing effects");

        // Remove slowdown
        playerMovement.RemoveSwarmSlowdown();

        // Remove vision effect
        if (stats.obscuresVision)
        {
            playerMovement.ApplySwarmVision(false);
        }

        // Stop damage tick
        if (damageTickCoroutine != null)
        {
            StopCoroutine(damageTickCoroutine);
            damageTickCoroutine = null;
        }
    }

    IEnumerator DamageTickCoroutine()
    {
        while (isInContact)
        {
            // Apply damage
            PlayerHealth playerHealth = targetPlayer.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                float damage = stats.damagePerSecond * stats.damageTick;
                playerHealth.TakeDamage(damage);
                Debug.Log($"Swarm deals {damage} damage!");
            }

            yield return new WaitForSeconds(stats.damageTick);
        }
    }

    // ============================================
    // DAMAGE & DEATH
    // ============================================

    public void TakeDamage(float damage)
    {
        // Reduce damage (pour 2-3 hits)
        damage *= 1f;

        currentHealth -= damage;

        healthBarUI?.UpdateHealth(currentHealth, stats.maxHealth);

        Debug.Log($"Swarm took {damage} damage. Health: {currentHealth}/{stats.maxHealth}");

        // Bourrade on hit
        if (!isInBourrade)
        {
            StartCoroutine(BourradeOnHit());
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    IEnumerator BourradeOnHit()
    {
        isInBourrade = true;

        // Remove contact effects temporarily
        if (isInContact)
        {
            RemoveContactEffects();
            isInContact = false;
        }

        // Push back
        if (targetPlayer != null)
        {
            Vector3 pushDirection = (transform.position - targetPlayer.position).normalized;

            // Disable agent during bourrade
            if (agent != null && agent.enabled)
            {
                agent.enabled = false;
            }

            rb.linearVelocity = pushDirection * stats.bourradeDistance * 2f;

            Debug.Log($"Swarm pushed back!");

            yield return new WaitForSeconds(0.5f);

            // Re-enable agent
            if (stats.useNavMesh && agent != null)
            {
                agent.enabled = true;
            }
        }

        isInBourrade = false;
    }

    void Die()
    {
        Debug.Log($"Swarm died!");
        EnemyHealthBarManager manager = FindObjectOfType<EnemyHealthBarManager>();
        manager?.UnregisterEnemy(transform);

        // Remove effects if in contact
        if (isInContact)
        {
            RemoveContactEffects();
        }

        Destroy(gameObject);
    }

    void OnDestroy()
    {
        // Cleanup
        if (particleSystemInstance != null)
        {
            Destroy(particleSystemInstance);
        }

        // Make sure effects are removed
        if (isInContact && playerMovement != null)
        {
            playerMovement.RemoveSwarmSlowdown();
            playerMovement.ApplySwarmVision(false);
        }
    }
}