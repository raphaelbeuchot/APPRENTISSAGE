using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class BrightEyesController : MonoBehaviour
{
    [Header("Stats")]
    public BrightEyesStats stats;
    
    [Header("References")]
    public Renderer flameRenderer;
    public Material flameMaterial;
    
    [Header("Health Bar")]
    public EnemyHealthBarUI healthBarUI;
    
    private GameManager gameManager;
    private Transform player;
    private NavMeshAgent agent;
    
    private bool isAwake = false;
    private bool playerInRange = false;
    private Material flameMaterialInstance;
    private Color originalEmission;
    
    private float lastDamageTime;
    private float currentHealth;
    private bool isDead = false;
    
    // Flame system
    private bool isFlameExtinguished = false;
    private bool wasExtinguishedThisCycle = false;
    private GameManager.GameState lastState;
    
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
        
        if (flameRenderer != null && flameMaterial != null)
        {
            flameMaterialInstance = new Material(flameMaterial);
            flameRenderer.material = flameMaterialInstance;
            originalEmission = flameMaterialInstance.GetColor("_EmissionColor");
        }
        
        // Start eteint
        if (!isFlameExtinguished)
        {
            SetFlameGlow(false);
        }
        else
        {
            SetFlameExtinguished();
        }
        
        if (agent != null)
        {
            agent.enabled = stats.canWander;
            if (stats.canWander)
            {
                agent.speed = stats.wanderSpeed;
                StartCoroutine(WanderRoutine());
            }
        }
        
        if (gameManager != null)
        {
            lastState = gameManager.currentState;
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
                    healthBarUI.UpdateHealth(currentHealth, stats.maxHealth);
                    healthBarUI.gameObject.SetActive(false);
                }
            }
        }
    }
    
    void Update()
    {
        if (player == null || stats == null || isDead) return;
        
        // Health bar visibility (6m range)
        if (healthBarUI != null)
        {
            float distToPlayer = Vector3.Distance(transform.position, player.position);
            bool shouldShow = distToPlayer <= 6f;
            
            if (shouldShow && !healthBarUI.gameObject.activeSelf)
                healthBarUI.Show();
            else if (!shouldShow && healthBarUI.gameObject.activeSelf)
                healthBarUI.Hide();
        }
        
        // Flame reignite system
        if (gameManager != null && stats.canReignite && isFlameExtinguished && wasExtinguishedThisCycle)
        {
            GameManager.GameState currentState = gameManager.currentState;
            
            // Si on entre dans Alert apres avoir ete eteint
            if (currentState == GameManager.GameState.Alert && lastState != GameManager.GameState.Alert)
            {
                ReigniteFlame();
            }
            
            lastState = currentState;
        }
        
        // Si flamme eteinte, aucune activite
        if (isFlameExtinguished)
        {
            if (isAwake)
            {
                ForceDeactivate();
            }
            return;
        }
        
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
        if (gameManager == null) return false;
        if (isFlameExtinguished) return false;
        
        GameManager.GameState state = gameManager.currentState;
        
        if (state == GameManager.GameState.GreenLight && stats.activeInGreenLight)
            return true;
        if (state == GameManager.GameState.Alert && stats.activeInAlert)
            return true;
        if (state == GameManager.GameState.RedLight && stats.activeInRedLight)
            return true;
            
        return false;
    }
    
    void WakeUp()
    {
        isAwake = true;
        SetFlameGlow(true);
        Debug.Log($"{gameObject.name} flame LIT");
    }
    
    void Sleep()
    {
        isAwake = false;
        SetFlameGlow(false);
        playerInRange = false;
        
        if (player != null)
        {
            PlayerPhysicsMovement movement = player.GetComponent<PlayerPhysicsMovement>();
            if (movement != null)
            {
                movement.RemoveBrightEyesAttraction();
                
                // Recoil seulement si player proche (range recoil)
                float distToPlayer = Vector3.Distance(transform.position, player.position);
                if (distToPlayer <= stats.releaseRecoilRange)
                {
                    Vector3 recoilDirection = (player.position - transform.position).normalized;
                    movement.ApplyKnockback(recoilDirection * stats.releaseRecoilForce, 0.3f);
                    Debug.Log($"{gameObject.name} flame SLEEP + recoil player");
                }
                else
                {
                    Debug.Log($"{gameObject.name} flame SLEEP (no recoil, player too far)");
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
                    Debug.Log($"{gameObject.name} damaged player for {stats.contactDamage}");
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
    
    void SetFlameGlow(bool glow)
    {
        if (flameMaterialInstance == null) return;
        
        if (glow)
        {
            Color emissionColor = stats.flameColorLit * stats.flameIntensity;
            flameMaterialInstance.SetColor("_EmissionColor", emissionColor);
            flameMaterialInstance.EnableKeyword("_EMISSION");
        }
        else
        {
            flameMaterialInstance.SetColor("_EmissionColor", originalEmission);
            flameMaterialInstance.DisableKeyword("_EMISSION");
        }
    }
    
    void SetFlameExtinguished()
    {
        if (flameMaterialInstance == null) return;
        
        Color darkColor = stats.flameColorExtinguished * 0.1f;
        flameMaterialInstance.SetColor("_EmissionColor", darkColor);
        flameMaterialInstance.DisableKeyword("_EMISSION");
    }
    
    // === FLAME SYSTEM ===
    
    public void ExtinguishFlame()
    {
        if (isFlameExtinguished) return;
        
        isFlameExtinguished = true;
        wasExtinguishedThisCycle = true;
        
        ForceDeactivate();
        SetFlameExtinguished();
        
        Debug.Log($"{gameObject.name} flame EXTINGUISHED!");
    }
    
    void ReigniteFlame()
    {
        if (!isFlameExtinguished) return;
        
        isFlameExtinguished = false;
        wasExtinguishedThisCycle = false;
        
        Debug.Log($"{gameObject.name} flame REIGNITED!");
    }
    
    // === HEALTH SYSTEM ===
    
    public void TakeDamage(float damage)
    {
        if (isDead) return;
        
        currentHealth -= damage;
        currentHealth = Mathf.Max(0f, currentHealth);
        
        if (healthBarUI != null)
        {
            healthBarUI.UpdateHealth(currentHealth, stats.maxHealth);
        }
        
        Debug.Log($"{gameObject.name} took {damage} damage, health: {currentHealth}/{stats.maxHealth}");
        
        if (currentHealth <= 0f)
        {
            Die();
        }
    }
    
    void Die()
    {
        if (isDead) return;
        
        isDead = true;
        
        if (playerInRange)
        {
            RemoveAttractionFromPlayer();
        }
        
        if (healthBarUI != null)
        {
            EnemyHealthBarManager manager = FindFirstObjectByType<EnemyHealthBarManager>();
            if (manager != null)
            {
                manager.UnregisterEnemy(transform);
            }
        }
        
        Debug.Log($"{gameObject.name} died!");
        Destroy(gameObject);
    }
    
    public bool IsAlive() => !isDead && currentHealth > 0f;
    public bool IsAwake() => isAwake;
    public bool IsFlameExtinguished() => isFlameExtinguished;
    public EnemyHealthBarUI GetHealthBarUI() => healthBarUI;
    
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
    }
}