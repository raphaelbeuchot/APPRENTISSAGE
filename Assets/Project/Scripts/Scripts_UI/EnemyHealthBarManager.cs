using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class EnemyHealthBarManager : MonoBehaviour
{
    public static EnemyHealthBarManager Instance;

    [Header("Prefab")]
    public GameObject healthBarPrefab;
    public Canvas canvas;

    [Header("Settings")]
    public float verticalOffset = 2f;
    public float maxDisplayDistance = 6f;

    [Header("UI Masking")]
    public Image tvFrameImage;
    public float alphaThreshold = 0.5f;
    private Texture2D vignetteTexture;

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

        // Recuperer la texture de la vignette
        if (tvFrameImage != null && tvFrameImage.sprite != null)
        {
            vignetteTexture = tvFrameImage.sprite.texture;

            if (!vignetteTexture.isReadable)
            {
                Debug.LogError("La texture de la vignette doit etre en Read/Write enabled dans les import settings!");
            }
        }
    }

    public void RegisterEnemy(Transform enemy, EnemyHealthBarUI bar)
    {
        if (!healthBars.ContainsKey(enemy))
        {
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

        foreach (var kvp in healthBars)
        {
            Transform enemy = kvp.Key;
            EnemyHealthBarUI bar = kvp.Value;

            if (enemy == null || bar == null) continue;

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

                // Check alpha de la vignette a cette position
                if (tvFrameImage != null && vignetteTexture != null && IsOccludedByVignette(screenPos))
                {
                    bar.Hide();
                    continue;
                }
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

    private bool IsOccludedByVignette(Vector3 screenPos)
    {
        if (vignetteTexture == null || tvFrameImage == null) return false;

        // Convertir position screen en coordonnees UV (0-1)
        float u = screenPos.x / Screen.width;
        float v = screenPos.y / Screen.height;

        // Clamper pour eviter out of bounds
        u = Mathf.Clamp01(u);
        v = Mathf.Clamp01(v);

        // Convertir UV en coordonnees pixel de la texture
        int x = Mathf.FloorToInt(u * vignetteTexture.width);
        int y = Mathf.FloorToInt(v * vignetteTexture.height);

        // Clamper aux limites de la texture
        x = Mathf.Clamp(x, 0, vignetteTexture.width - 1);
        y = Mathf.Clamp(y, 0, vignetteTexture.height - 1);

        // Sampler l'alpha a cette position
        Color pixelColor = vignetteTexture.GetPixel(x, y);

        // Si alpha > seuil, la vignette est opaque la = masquer
        return pixelColor.a > alphaThreshold;
    }
}