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

        // Trouver le player
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

        // Check global si le rideau existe
        bool shutterExists = FindObjectOfType<MetalShutter>() != null;

        foreach (var kvp in healthBars)
        {
            Transform enemy = kvp.Key;
            EnemyHealthBarUI bar = kvp.Value;

            if (enemy == null || bar == null) continue;

            // SI LE RIDEAU EXISTE, NE PAS AFFICHER LES BARRES
            if (shutterExists)
            {
                bar.Hide();
                continue;
            }

            // Position world space
            Vector3 worldPos = enemy.position + Vector3.up * verticalOffset;
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

            // Conversion en local canvas
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

            // Check distance pour affichage
            float distance = Vector3.Distance(playerTransform.position, enemy.position);

            // Exceptions
            bool shouldIgnoreDistance = false;

            // Exception 1 : Blinder custom range
            ChargeAttack blinder = enemy.GetComponent<ChargeAttack>();
            if (blinder != null)
            {
                float blinderRange = 10f;
                if (distance <= blinderRange)
                {
                    bar.Show();
                    continue;
                }
                else
                {
                    bar.Hide();
                    continue;
                }
            }

            // Exception 2 : Deep empty pit
            EnemyPitInteractable pitInt = enemy.GetComponent<EnemyPitInteractable>();
            if (pitInt != null && pitInt.shouldIgnoreHealthbarDistance)
            {
                shouldIgnoreDistance = true;
            }

            // Exception 3 : Ennemi recemment touche par sentinelle
            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
            if (enemyHealth != null && enemyHealth.recentlyHitBySentinel)
            {
                shouldIgnoreDistance = true;
            }

            // Affichage normal selon distance
            if (shouldIgnoreDistance || distance <= maxDisplayDistance)
            {
                bar.Show();
            }
            else
            {
                bar.Hide();
            }
        }
    }

    
}