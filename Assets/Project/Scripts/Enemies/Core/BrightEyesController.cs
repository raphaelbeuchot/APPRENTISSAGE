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

    private bool playerInRange = false;
    private bool isDead = false;

    private Material materialInstance;

    void Start()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        SetupHealthBar();
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
                    healthBarUI.UpdateHealth(stats.maxHealth, stats.maxHealth, stats.maxHealth);
                    healthBarUI.gameObject.SetActive(false);
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

        CheckPlayerDetection();
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
                NotifyBrightEyesNoLongerDetected();
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
                NotifyBrightEyesDetected();
            }
        }
        else
        {
            if (playerInRange)
            {
                playerInRange = false;
                RemoveAttractionFromPlayer();
                NotifyBrightEyesNoLongerDetected();
            }
        }
    }

    void NotifyBrightEyesDetected()
    {
        PlayerDetectionFeedback feedback = player.GetComponent<PlayerDetectionFeedback>();
        if (feedback != null)
            feedback.OnBrightEyesDetected();
    }

    void NotifyBrightEyesNoLongerDetected()
    {
        PlayerDetectionFeedback feedback = player.GetComponent<PlayerDetectionFeedback>();
        if (feedback != null)
            feedback.OnBrightEyesNoLongerDetected();
    }

    void ApplyAttractionToPlayer()
    {
        PlayerPhysicsMovement movement = player.GetComponent<PlayerPhysicsMovement>();
        if (movement != null)
            movement.ApplyBrightEyesAttraction(transform, stats.attractionForce, stats.playerSlowdownMultiplier);
    }

    void RemoveAttractionFromPlayer()
    {
        PlayerPhysicsMovement movement = player.GetComponent<PlayerPhysicsMovement>();
        if (movement != null)
            movement.RemoveBrightEyesAttraction();
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;
        Die();
    }

    void Die()
    {
        if (isDead) return;

        isDead = true;

        playerInRange = false;
        RemoveAttractionFromPlayer();
        NotifyBrightEyesNoLongerDetected();

        StartCoroutine(ShrinkAndDisableSpheres());

        if (flameRenderer != null)
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
                manager.UnregisterEnemy(transform);
        }

        Debug.Log($"{gameObject.name} DEAD!");
    }

    IEnumerator ShrinkAndDisableSpheres()
    {
        SphereAnim[] anims = transform.parent.GetComponentsInChildren<SphereAnim>(true);

        foreach (SphereAnim anim in anims)
            anim.enabled = false;

        Vector3[] initialScales = new Vector3[anims.Length];
        for (int i = 0; i < anims.Length; i++)
            initialScales[i] = anims[i].transform.localScale;

        float duration = 0.25f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            for (int i = 0; i < anims.Length; i++)
            {
                if (anims[i] != null)
                    anims[i].transform.localScale = Vector3.Lerp(initialScales[i], Vector3.zero, t);
            }
            yield return null;
        }

        foreach (SphereAnim anim in anims)
        {
            if (anim != null)
                anim.gameObject.SetActive(false);
        }
    }

    public bool IsAlive() => !isDead;
    public bool IsFlameExtinguished() => isDead;

    void OnDestroy()
    {
        if (playerInRange && player != null)
            RemoveAttractionFromPlayer();

        if (materialInstance != null)
            Destroy(materialInstance);
    }
}