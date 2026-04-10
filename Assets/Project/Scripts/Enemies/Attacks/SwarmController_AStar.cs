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

    private Rigidbody rb;
    private AIPath aiPath;
    private Transform targetPlayer;
    private PlayerPhysicsMovement playerMovement;

    private bool isInitialized = false;
    private bool isGrounded = false;
    private bool isInContact = false;
    private bool isInBourrade = false;
    private PulseNoise pulseNoise;

    private float detectionCheckInterval = 0.3f;
    private float lastDetectionCheck = 0f;

    private Coroutine damageTickCoroutine;
    private GameObject particleSystemInstance;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.mass = 0.1f;
        rb.linearDamping = 5f;
        rb.angularDamping = 10f;
        aiPath = GetComponent<AIPath>();

        if (aiPath != null)
            aiPath.enabled = false;

        Collider col = GetComponent<Collider>();
        if (col != null)
            col.enabled = false;

        if (stats == null)
            enabled = false;
    }

    void Start()
    {
        if (stats != null && !isInitialized)
            Initialize(stats);
    }

    public void SetHealthBarUI(EnemyHealthBarUI bar) { healthBarUI = bar; }
    public EnemyHealthBarUI GetHealthBarUI() { return healthBarUI; }
    public bool IsAlive() { return currentHealth > 0f; }

    public void Initialize(SwarmStats swarmStats)
    {
        enabled = true;
        rb.isKinematic = false;
        pulseNoise = GetComponent<PulseNoise>();

        stats = swarmStats;
        currentHealth = stats.maxHealth;

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;

        if (!stats.canCrossObstacles && aiPath != null)
        {
            aiPath.maxSpeed = stats.moveSpeed;
            aiPath.rotationSpeed = 200f;
            aiPath.endReachedDistance = 0.1f;
            aiPath.canMove = false;
            aiPath.orientation = OrientationMode.ZAxisForward;
            aiPath.updatePosition = true;
            aiPath.updateRotation = false;
            aiPath.enabled = false;
        }
        else if (aiPath != null)
        {
            aiPath.canMove = false;
        }

        if (col != null) col.enabled = true;

        isGrounded = false;
        StartCoroutine(WaitUntilGrounded());

        if (stats.particleSystemPrefab != null)
            particleSystemInstance = Instantiate(stats.particleSystemPrefab, transform.position, Quaternion.identity, transform);

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            targetPlayer = playerObject.transform;
            playerMovement = playerObject.GetComponent<PlayerPhysicsMovement>();
        }

        SetupHealthBar();
        isInitialized = true;
    }

    IEnumerator WaitUntilGrounded()
    {
        float startY = transform.position.y;
        float targetY = startY - 0.8f;

        while (transform.position.y > targetY)
        {
            float newY = transform.position.y - 5f * Time.deltaTime;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
            yield return null;
        }

        transform.position = new Vector3(transform.position.x, targetY, transform.position.z);
        rb.linearVelocity = Vector3.zero;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;

        if (!stats.canCrossObstacles && aiPath != null)
        {
            aiPath.enabled = true;
            aiPath.canMove = true;
        }

        if (pulseNoise != null) pulseNoise.enabled = true;

        isGrounded = true;
    }

    void SetupHealthBar()
    {
        EnemyHealthBarManager manager = FindObjectOfType<EnemyHealthBarManager>();
        if (manager != null)
        {
            GameObject barGO = Instantiate(manager.healthBarPrefab, manager.transform);
            healthBarUI = barGO.GetComponent<EnemyHealthBarUI>();
            manager.RegisterEnemy(transform, healthBarUI);
            healthBarUI.UpdateHealth(currentHealth, stats.maxHealth, stats.maxHealth);
            healthBarUI.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (!isInitialized || stats == null || !isGrounded) return;
        if (isInBourrade) return;

        if (Time.time - lastDetectionCheck > detectionCheckInterval)
        {
            lastDetectionCheck = Time.time;
            DetectAndChasePlayer();
        }
    }

    void DetectAndChasePlayer()
    {
        if (targetPlayer == null) return;

        MoveTowardsPlayer();

        bool wasInRange = isPlayerInRange;
        float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);
        isPlayerInRange = distanceToPlayer <= 6f;

        if (isPlayerInRange && !wasInRange)
            healthBarUI?.Show();
        else if (!isPlayerInRange && wasInRange)
            healthBarUI?.Hide();
    }

    void MoveTowardsPlayer()
    {
        if (targetPlayer == null) return;

        Vector3 targetPosition = targetPlayer.position;

        if (stats.canCrossObstacles)
            targetPosition.y = targetPlayer.position.y + stats.hoverHeight;

        if (!stats.canCrossObstacles && aiPath != null && aiPath.canMove)
        {
            aiPath.destination = targetPosition;
        }
        else
        {
            Vector3 direction = (targetPosition - transform.position).normalized;
            rb.linearVelocity = direction * stats.moveSpeed;
        }
    }

    void StopMovement()
    {
        if (!stats.canCrossObstacles && aiPath != null)
            aiPath.canMove = false;
        else
            rb.linearVelocity = Vector3.zero;
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

        playerMovement.ApplySwarmSlowdown(stats.slowdownMultiplier);

        if (stats.obscuresVision)
            playerMovement.ApplySwarmVision(true);

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

        playerMovement.RemoveSwarmSlowdown();

        if (stats.obscuresVision)
            playerMovement.ApplySwarmVision(false);

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
            }
            yield return new WaitForSeconds(stats.damageTick);
        }
    }

    public void TakeDamage(float damage, bool triggerBourrade = true)
    {
        currentHealth -= damage;
        healthBarUI.UpdateHealth(currentHealth, stats.maxHealth, stats.maxHealth);

        if (triggerBourrade && !isInBourrade)
            StartCoroutine(BourradeOnHit());

        if (currentHealth <= 0)
            Die();
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
            Vector3 pushDirection = (transform.position - targetPlayer.position);
            pushDirection.y = 0f;
            pushDirection.Normalize();

            if (aiPath != null)
                aiPath.canMove = false;

            rb.linearVelocity = pushDirection * stats.bourradeDistance * 2f;

            yield return new WaitForSeconds(0.5f);

            if (!stats.canCrossObstacles && aiPath != null)
                aiPath.canMove = true;
        }

        isInBourrade = false;
    }

    void Die()
    {
        EnemyHealthBarManager manager = FindObjectOfType<EnemyHealthBarManager>();
        manager?.UnregisterEnemy(transform);

        if (isInContact)
            RemoveContactEffects();

        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (particleSystemInstance != null)
            Destroy(particleSystemInstance);

        if (isInContact && playerMovement != null)
        {
            playerMovement.RemoveSwarmSlowdown();
            playerMovement.ApplySwarmVision(false);
        }
    }
}