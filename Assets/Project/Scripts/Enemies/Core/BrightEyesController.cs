using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class BrightEyesController : MonoBehaviour
{
    [Header("Stats")]
    public BrightEyesStats stats;

    [Header("References")]
    public Renderer flameRenderer;

    [Header("Health Bar")]
    public EnemyHealthBarUI healthBarUI;

    private GameManager gameManager;
    private Transform player;
    private NavMeshAgent agent;

    private bool isAwake = false;
    private bool playerInRange = false;

    private float lastDamageTime;
    private float currentHealth;
    private bool isDead = false;

    private bool isFlameExtinguished = false;
    private bool wasExtinguishedThisCycle = false;
    private SentinelCycleManager.GameState lastState;

    private Material materialInstance;

    void Start()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        agent = GetComponent<NavMeshAgent>();

        if (stats != null)
        {
            currentHealth = stats.maxHealth;
            isFlameExtinguished = stats.startsExtinguished;
        }

        SetupHealthBar();

        if (agent != null)
        {
            agent.enabled = stats.canWander;
            if (stats.canWander)
            {
                agent.speed = stats.wanderSpeed;
                StartCoroutine(WanderRoutine());
            }
        }

        if (gameManager != null && gameManager.sentinelCycleManager != null)
        {
            lastState = gameManager.sentinelCycleManager.currentState;
        }
    }

    void SetupHealthBar()
    {
        if (healthBarUI == null)
        {
            EnemyHealthBarManager manager = FindFirstObjectByType<EnemyHealthBarManager>();
            if (manager != null && manager.healthBarPrefab != null)
            {
                GameObject barObj = Instantiate(manager.healthBarPrefab, manager.canvas.transform);
                healthBarUI = barObj.GetComponent<EnemyHealthBarUI>();

                if (healthBarUI != null)
                {
                    manager.RegisterEnemy(transform, healthBarUI);
                    healthBarUI.UpdateHealth(currentHealth, stats.maxHealth, stats.maxHealth); healthBarUI.gameObject.SetActive(false);
                }
            }
        }
    }

    void Update()
    {
        if (player == null || stats == null || isDead) return;

        if (healthBarUI != null)
        {
            float distToPlayer = Vector3.Distance(transform.position, player.position);
            bool shouldShow = distToPlayer <= 6f;

            if (shouldShow && !healthBarUI.gameObject.activeSelf)
                healthBarUI.Show();
            else if (!shouldShow && healthBarUI.gameObject.activeSelf)
                healthBarUI.Hide();
        }

        if (gameManager != null && gameManager.sentinelCycleManager != null && stats.canReignite && isFlameExtinguished && wasExtinguishedThisCycle)
        {
            SentinelCycleManager.GameState currentState = gameManager.sentinelCycleManager.currentState;

            if (currentState == SentinelCycleManager.GameState.Alert && lastState != SentinelCycleManager.GameState.Alert)
            {
                ReigniteFlame();
            }

            lastState = currentState;
        }

        if (isFlameExtinguished)
        {
            if (isAwake)
            {
                ForceDeactivate();
            }
            return;
        }

        UpdateMaterial();

        bool shouldBeAwake = ShouldBeAwake();

        if (shouldBeAwake != isAwake)
        {
            if (shouldBeAwake)
                WakeUp();
            else
                Sleep();
        }

        if (isAwake)
        {
            CheckPlayerDetection();
            CheckPlayerContact();
        }
    }

    bool ShouldBeAwake()
    {
        if (gameManager == null || gameManager.sentinelCycleManager == null) return false;
        if (isFlameExtinguished) return false;

        SentinelCycleManager.GameState state = gameManager.sentinelCycleManager.currentState;

        // Release suit le même comportement que GreenLight
        if ((state == SentinelCycleManager.GameState.GreenLight || state == SentinelCycleManager.GameState.Release) && stats.activeInGreenLight)
            return true;
        if (state == SentinelCycleManager.GameState.Alert && stats.activeInAlert)
            return true;
        if (state == SentinelCycleManager.GameState.RedLight && stats.activeInRedLight)
            return true;

        return false;
    }

    void WakeUp()
    {
        isAwake = true;
        Debug.Log($"{gameObject.name} comportement ACTIF");
    }

    void Sleep()
    {
        isAwake = false;
        playerInRange = false;

        if (player != null)
        {
            PlayerPhysicsMovement movement = player.GetComponent<PlayerPhysicsMovement>();
            if (movement != null)
            {
                movement.RemoveBrightEyesAttraction();

                float distToPlayer = Vector3.Distance(transform.position, player.position);
                if (distToPlayer <= stats.releaseRecoilRange)
                {
                    Vector3 recoilDirection = (player.position - transform.position).normalized;
                    movement.ApplyKnockback(recoilDirection * stats.releaseRecoilForce, 0.3f);
                    Debug.Log($"{gameObject.name} comportement INACTIF + recoil player");
                }
                else
                {
                    Debug.Log($"{gameObject.name} comportement INACTIF");
                }
            }
        }
    }

    void ForceDeactivate()
    {
        isAwake = false;
        playerInRange = false;

        if (player != null)
        {
            PlayerPhysicsMovement movement = player.GetComponent<PlayerPhysicsMovement>();
            if (movement != null)
            {
                movement.RemoveBrightEyesAttraction();
            }
        }
    }

    void UpdateMaterial()
    {
        if (flameRenderer == null || stats == null) return;

        

        if (isFlameExtinguished)
        {
            flameRenderer.enabled = false;
            return;
        }

        if (isAwake)
        {
            // Appliquer materialAwake avec pulse
            if (materialInstance == null || flameRenderer.sharedMaterial != stats.materialAwake)
            {
                if (materialInstance != null) Destroy(materialInstance);
                materialInstance = new Material(stats.materialAwake);
                flameRenderer.material = materialInstance;
            }

            // Pulse emission
            float pulse = Mathf.PingPong(Time.time * stats.awakePulseSpeed, 1f);
            float intensity = Mathf.Lerp(stats.awakePulseIntensityMin, stats.awakePulseIntensityMax, pulse);
            Color baseColor = stats.materialAwake.GetColor("_EmissionColor");
            materialInstance.SetColor("_EmissionColor", baseColor * intensity);

            flameRenderer.enabled = true;
        }
        else
        {
            // Appliquer materialAsleep sans pulse
            if (materialInstance == null || flameRenderer.sharedMaterial != stats.materialAsleep)
            {
                if (materialInstance != null) Destroy(materialInstance);
                materialInstance = new Material(stats.materialAsleep);
                flameRenderer.material = materialInstance;
            }

            flameRenderer.enabled = true;
        }
    }

    void CheckPlayerDetection()
    {
        float distance = Vector3.Distance(transform.position, player.position);

        if (distance > stats.detectionRange)
        {
            if (playerInRange)
            {
                playerInRange = false;
                RemoveAttractionFromPlayer();
            }
            return;
        }

        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        directionToPlayer.y = 0f;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        float angle = Vector3.Angle(forward, directionToPlayer);

        if (angle <= stats.detectionAngle / 2f)
        {
            if (!playerInRange)
            {
                playerInRange = true;
                ApplyAttractionToPlayer();
            }
        }
        else
        {
            if (playerInRange)
            {
                playerInRange = false;
                RemoveAttractionFromPlayer();
            }
        }
    }

    void CheckPlayerContact()
    {
        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= stats.contactDamageRange)
        {
            if (Time.time - lastDamageTime >= stats.contactDamageInterval)
            {
                PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
                if (playerHealth != null && !playerHealth.IsDead())
                {
                    playerHealth.TakeDamage(stats.contactDamage);
                    lastDamageTime = Time.time;
                }
            }
        }
    }

    void ApplyAttractionToPlayer()
    {
        PlayerPhysicsMovement movement = player.GetComponent<PlayerPhysicsMovement>();
        if (movement != null)
        {
            movement.ApplyBrightEyesAttraction(transform, stats.attractionForce, stats.playerSlowdownMultiplier);
        }
    }

    void RemoveAttractionFromPlayer()
    {
        PlayerPhysicsMovement movement = player.GetComponent<PlayerPhysicsMovement>();
        if (movement != null)
        {
            movement.RemoveBrightEyesAttraction();
        }
    }

    public void ExtinguishFlame()
    {
        if (isFlameExtinguished) return;

        isFlameExtinguished = true;
        wasExtinguishedThisCycle = true;

        ForceDeactivate();

        Debug.Log($"{gameObject.name} flame EXTINGUISHED!");
    }

    void ReigniteFlame()
    {
        if (!isFlameExtinguished) return;

        isFlameExtinguished = false;
        wasExtinguishedThisCycle = false;
        currentHealth = stats.maxHealth;

        if (healthBarUI != null)
        {
            healthBarUI.UpdateHealth(currentHealth, stats.maxHealth, stats.maxHealth);
        }

        Debug.Log($"{gameObject.name} flame REIGNITED!");
    }

    public void TakeDamage(float damage)
    {
        if (isDead || isFlameExtinguished) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0f, currentHealth);

        if (healthBarUI != null)
        {
            healthBarUI.UpdateHealth(currentHealth, stats.maxHealth, stats.maxHealth);
        }
        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    void Die()
    {
        if (isDead) return;

        isDead = true;

        ForceDeactivate();

        // Appliquer materialDead directement ici car Update() ne tourne plus
        if (flameRenderer != null && stats != null)
        {
            if (stats.materialDead != null)
            {
                if (materialInstance != null) Destroy(materialInstance);
                materialInstance = new Material(stats.materialDead);
                flameRenderer.material = materialInstance;
                flameRenderer.enabled = true;
            }
            else
            {
                flameRenderer.enabled = false;
            }
        }

        if (healthBarUI != null)
        {
            EnemyHealthBarManager manager = FindFirstObjectByType<EnemyHealthBarManager>();
            if (manager != null)
            {
                manager.UnregisterEnemy(transform);
            }
        }

        Debug.Log($"{gameObject.name} DEAD - permanent!");
        // Pas de Destroy : le bright eyes reste en scene mais inactif
    }

    public bool IsAlive() => !isDead && currentHealth > 0f;
    public bool IsAwake() => isAwake;
    public bool IsFlameExtinguished() => isFlameExtinguished;

    IEnumerator WanderRoutine()
    {
        while (stats.canWander && !isDead)
        {
            yield return new WaitForSeconds(Random.Range(3f, 6f));

            Vector3 randomDirection = Random.insideUnitSphere * stats.wanderRadius;
            randomDirection += transform.position;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDirection, out hit, stats.wanderRadius, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }
    }

    void OnDestroy()
    {
        if (playerInRange && player != null)
        {
            RemoveAttractionFromPlayer();
        }

        if (materialInstance != null)
        {
            Destroy(materialInstance);
        }
    }
}