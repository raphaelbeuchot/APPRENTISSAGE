using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class EnemyHealthBarManager : MonoBehaviour
{
    public static EnemyHealthBarManager Instance;

    [Header("Prefab")]
    public GameObject healthBarPrefab;
    public Canvas canvas;
    public RectTransform healthBarsContainer;

    [Header("Settings")]
    public float verticalOffset = 2f;
    public float maxDisplayDistance = 6f;

    private Transform playerTransform;
    private Dictionary<Transform, EnemyHealthBarUI> healthBars = new Dictionary<Transform, EnemyHealthBarUI>();
    private Camera mainCamera;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindObjectOfType<Camera>();
        }

        PlayerPhysicsMovement player = FindObjectOfType<PlayerPhysicsMovement>();
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    public void RegisterEnemy(Transform enemy, EnemyHealthBarUI bar)
    {
        if (!healthBars.ContainsKey(enemy))
        {
            bar.transform.SetParent(healthBarsContainer, false);
            healthBars.Add(enemy, bar);
            Debug.Log($"[HealthBarManager] Registered {enemy.name}");
        }
    }

    public void UnregisterEnemy(Transform enemy)
    {
        if (healthBars.ContainsKey(enemy))
        {
            if (healthBars[enemy] != null)
            {
                Destroy(healthBars[enemy].gameObject);
            }
            healthBars.Remove(enemy);
            Debug.Log($"[HealthBarManager] Unregistered {enemy.name}");
        }
    }

    public void ClearAllOutlines()
    {
        foreach (var kvp in healthBars)
        {
            if (kvp.Value != null)
            {
                kvp.Value.SetLockedOutline(false);
            }
        }
    }

    private void LateUpdate()
    {
        if (canvas == null || mainCamera == null || playerTransform == null) return;

        bool shutterExists = FindObjectOfType<MetalShutter>() != null;

        foreach (var kvp in healthBars)
        {
            Transform enemy = kvp.Key;
            EnemyHealthBarUI bar = kvp.Value;

            if (enemy == null || bar == null) continue;

            if (shutterExists)
            {
                bar.Hide();
                continue;
            }

            Vector3 worldPos = enemy.position + Vector3.up * verticalOffset;
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

            RectTransform barRect = bar.GetComponent<RectTransform>();
            if (barRect != null)
            {
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvas.transform as RectTransform,
                    screenPos,
                    null,
                    out localPoint);
                barRect.localPosition = localPoint;
            }

            float distance = Vector3.Distance(playerTransform.position, enemy.position);

            EnemyAI_AStar ai = enemy.GetComponent<EnemyAI_AStar>();
            EnemyStats stats = ai != null ? ai.stats : null;

            if (stats != null && stats.attackType == EnemyStats.AttackType.Blinder)
            {
                if (distance <= stats.blinderHealthBarRange)
                    bar.Show();
                else
                    bar.Hide();
                continue;
            }

            bool shouldIgnoreDistance = false;

            EnemyPitInteractable pitInt = enemy.GetComponent<EnemyPitInteractable>();
            if (pitInt != null && pitInt.shouldIgnoreHealthbarDistance)
                shouldIgnoreDistance = true;

            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
            if (enemyHealth != null && enemyHealth.recentlyHitBySentinel)
                shouldIgnoreDistance = true;

            float displayRadius = stats != null ? stats.detectionRadius : maxDisplayDistance;

            if (shouldIgnoreDistance || distance <= displayRadius)
                bar.Show();
            else
                bar.Hide();
        }
    }
}