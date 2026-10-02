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

    public float verticalOffset = 2f;

    private List<Transform> playerTransforms = new List<Transform>();
    private Dictionary<Transform, EnemyHealthBarUI> healthBars = new Dictionary<Transform, EnemyHealthBarUI>();
    private Camera mainCamera;
    private float displayRadius;

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

        PlayerPhysicsMovement[] players = FindObjectsOfType<PlayerPhysicsMovement>();
        foreach (PlayerPhysicsMovement player in players)
        {
            playerTransforms.Add(player.transform);
        }

        if (players.Length > 0)
        {
            PlayerStats playerStats = players[0].stats;
            displayRadius = playerStats != null ? playerStats.lockOnRange : 6f;
        }
    }

    // Distance au joueur le plus proche : en solo il n'y en a qu'un, en multi on ne veut pas
    // qu'une pip bar reste eteinte parce que le joueur suivi par hasard (le premier trouve par
    // FindObjectsOfType) ne s'approche jamais, pendant que l'autre joueur est juste a cote.
    private float GetClosestPlayerDistance(Vector3 enemyPosition)
    {
        float closest = float.MaxValue;
        foreach (Transform player in playerTransforms)
        {
            if (player == null) continue;
            float d = Vector3.Distance(player.position, enemyPosition);
            if (d < closest) closest = d;
        }
        return closest;
    }

    public void RegisterEnemy(Transform enemy, EnemyHealthBarUI bar)
    {
        if (!healthBars.ContainsKey(enemy))
        {
            bar.transform.SetParent(healthBarsContainer, false);
            healthBars.Add(enemy, bar);
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
        if (canvas == null || mainCamera == null || playerTransforms.Count == 0) return;

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

            float distance = GetClosestPlayerDistance(enemy.position);

            EnemyAI_AStar ai = enemy.GetComponent<EnemyAI_AStar>();
            EnemyStats stats = ai != null ? ai.stats : null;

            EnemyHealthBarPipsUI pipsUI = bar as EnemyHealthBarPipsUI;
            if (pipsUI != null && ai != null)
            {
                bool isChasing = ai.currentState == EnemyAI_AStar.State.Chasing
                              || ai.currentState == EnemyAI_AStar.State.Attacking;
                pipsUI.SetChaseOutline(isChasing);
            }

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

            if (shouldIgnoreDistance || distance <= displayRadius)
                bar.Show();
            else
                bar.Hide();
        }
    }
}