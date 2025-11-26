using System.Collections;
using UnityEngine;
using Pathfinding;

[RequireComponent(typeof(Rigidbody))]
public class SwarmController_AStar : MonoBehaviour
{
    [Header("References")]
    public SwarmStats stats;
    private EnemyHealthBarUI healthBarUI;
    private bool isPlayerInRange = false;

    [Header("Runtime State")]
    public float currentHealth;

    // Components
    private Rigidbody rb;
    private AIPath aiPath;
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

        // Try to get AIPath
        aiPath = GetComponent<AIPath>();
    }

    public void SetHealthBarUI(EnemyHealthBarUI bar)
    {
        healthBarUI = bar;
    }

    public EnemyHealthBarUI GetHealthBarUI()
    {
        return healthBarUI;
    }

    public bool IsAlive()
    {
        return currentHealth > 0f;
    }

    public void Initialize(SwarmStats swarmStats)
    {
        stats = swarmStats;
        currentHealth = stats.maxHealth;

        transform.position = new Vector3(transform.position.x, 0f, transform.position.z);


        // Setup Rigidbody constraints
        if (stats.useNavMesh && !stats.canCrossObstacles)
        {
            // Asticots : freeze Y position
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            rb.useGravity = true;
        }
        else
        {
            // Mouches : pas de gravite, rotation freeze
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.useGravity = false;
        }

        // Setup AIPath
        if (stats.useNavMesh && aiPath != null)
        {
            aiPath.maxSpeed = stats.moveSpeed;
            aiPath.rotationSpeed = 200f;
            aiPath.endReachedDistance = 0.5f;
            aiPath.canMove = true;
            aiPath.orientation = OrientationMode.ZAxisForward;

            // AJOUT CRITIQUE :
            aiPath.updatePosition = true;
            aiPath.updateRotation = false; // Pas besoin pour swarm
        }
        else if (aiPath != null)
        {
            aiPath.canMove = false;
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
        Debug.Log($"SwarmController_AStar initialized: {stats.swarmType}, Health: {currentHealth}");
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
        Debug.Log("=== MOVE TOWARDS PLAYER ===");

        if (targetPlayer == null)
        {
            Debug.LogError("targetPlayer is NULL!");
            return;
        }

        Debug.Log($"targetPlayer: {targetPlayer.name}");

        Vector3 targetPosition = targetPlayer.position;

        if (!stats.useNavMesh && stats.canCrossObstacles)
        {
            targetPosition.y = targetPlayer.position.y + stats.hoverHeight;
        }

        Debug.Log($"useNavMesh: {stats.useNavMesh}, aiPath null: {aiPath == null}");

        if (stats.useNavMesh && aiPath != null && aiPath.canMove)
        {
            Debug.Log($"Setting destination to: {targetPosition}");
            aiPath.destination = targetPosition;
        }
        else
        {
            Debug.Log("Using direct movement (flies)");
            Vector3 direction = (targetPosition - transform.position).normalized;
            rb.linearVelocity = direction * stats.moveSpeed;
        }
    }

    void StopMovement()
    {
        if (stats.useNavMesh && aiPath != null)
        {
            aiPath.canMove = false;
        }
        else
        {
            rb.linearVelocity = Vector3.zero;
        }
    }

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

        playerMovement.ApplySwarmSlowdown(stats.slowdownMultiplier);

        if (stats.obscuresVision)
        {
            playerMovement.ApplySwarmVision(true);
        }

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

        playerMovement.RemoveSwarmSlowdown();

        if (stats.obscuresVision)
        {
            playerMovement.ApplySwarmVision(false);
        }

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

    public void TakeDamage(float damage)
    {
        damage *= 1f;

        currentHealth -= damage;

        healthBarUI?.UpdateHealth(currentHealth, stats.maxHealth);

        Debug.Log($"Swarm took {damage} damage. Health: {currentHealth}/{stats.maxHealth}");

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

        if (isInContact)
        {
            RemoveContactEffects();
            isInContact = false;
        }

        if (targetPlayer != null)
        {
            Vector3 pushDirection = (transform.position - targetPlayer.position).normalized;

            // Disable AIPath during bourrade
            if (aiPath != null)
            {
                aiPath.canMove = false;
            }

            rb.linearVelocity = pushDirection * stats.bourradeDistance * 2f;

            Debug.Log($"Swarm pushed back!");

            yield return new WaitForSeconds(0.5f);

            // Re-enable AIPath
            if (stats.useNavMesh && aiPath != null)
            {
                aiPath.canMove = true;
            }
        }

        isInBourrade = false;
    }

    void Die()
    {
        Debug.Log($"Swarm died!");
        EnemyHealthBarManager manager = FindObjectOfType<EnemyHealthBarManager>();
        manager?.UnregisterEnemy(transform);

        if (isInContact)
        {
            RemoveContactEffects();
        }

        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (particleSystemInstance != null)
        {
            Destroy(particleSystemInstance);
        }

        if (isInContact && playerMovement != null)
        {
            playerMovement.RemoveSwarmSlowdown();
            playerMovement.ApplySwarmVision(false);
        }
    }
}