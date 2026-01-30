using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.UI;

/// <summary>
/// Gere le deroulement complet du niveau :
/// - Detection de la victoire (GoalDoor)
/// - Detection de la defaite (PlayerHealth)
/// - Affichage des ecrans UI
/// - Chargement des niveaux
/// </summary>
public class LevelManager : MonoBehaviour
{
    [Header("References")]
    public GoalDoor goalDoor;
    public GameManager gameManager;
    public PlayerPhysicsMovement player;
    public PlayerHealth playerHealth;

    [Header("UI")]
    public VictoryUI victoryUI;
    public GameOverUI gameOverUI;

    [Header("Configuration")]
    public float delayBeforeNextLevel = 3f;
    public float delayBeforeRestart = 2f;

    [Header("Level Rules")]
    [Tooltip("Si coche, skip l'ecran Victory UI et passe direct au niveau suivant avec fade")]
    public bool skipVictoryUI = false;

    [Tooltip("Si coche, la porte reste fermee jusqu'a ce que tous les ennemis soient tues")]
    public bool requireAllEnemiesKilled = false;

    [Header("Fade Settings")]
    [SerializeField] private float fadeDuration = 1f;

    [Header("Level End (Legacy)")]
    [SerializeField] private Collider levelEndTrigger;
    [SerializeField] private bool useLevelEndTrigger = false;
    [SerializeField] private bool isTutorialLevel = false;

    private bool levelCompleted = false;
    private bool gameOver = false;
    private bool doorHasBeenActivated = false;

    // Fade canvas
    private Canvas fadeCanvas;
    private Image fadeImage;
    private CanvasGroup fadeCanvasGroup;

    void Start()
    {
        // Creer le canvas de fade
        CreateFadeCanvas();

        // Trouver automatiquement les references si non assignees
        if (goalDoor == null)
        {
            goalDoor = FindObjectOfType<GoalDoor>();
        }

        if (gameManager == null)
        {
            gameManager = FindObjectOfType<GameManager>();
        }

        if (player == null)
        {
            player = FindObjectOfType<PlayerPhysicsMovement>();
        }

        if (playerHealth == null && player != null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
        }

        if (victoryUI == null)
        {
            VictoryUI[] allVictoryUI = Resources.FindObjectsOfTypeAll<VictoryUI>();
            foreach (VictoryUI ui in allVictoryUI)
            {
                if (ui.gameObject.scene.name != null)
                {
                    victoryUI = ui;
                    break;
                }
            }
        }

        if (gameOverUI == null)
        {
            GameOverUI[] allGameOverUI = Resources.FindObjectsOfTypeAll<GameOverUI>();
            foreach (GameOverUI ui in allGameOverUI)
            {
                if (ui.gameObject.scene.name != null)
                {
                    gameOverUI = ui;
                    break;
                }
            }
        }

        // S'abonner aux evenements
        if (goalDoor != null)
        {
            goalDoor.OnPlayerReached += OnPlayerReachedGoal;

            // Si la regle require tous ennemis tues, desactiver la porte au debut
            if (requireAllEnemiesKilled)
            {
                goalDoor.SetActive(false);
            }
        }
        else
        {
            Debug.LogWarning("LevelManager: Aucune GoalDoor trouvee dans la scene!");
        }

        if (playerHealth != null)
        {
            playerHealth.OnDeath += OnPlayerDeath;
        }
        else
        {
            Debug.LogWarning("LevelManager: PlayerHealth non trouve!");
        }

        // S'assurer que le temps est normal
        Time.timeScale = 1f;
    }

    void Update()
    {
        // Check si la porte doit s'activer apres tous les kills
        if (requireAllEnemiesKilled && !doorHasBeenActivated && gameManager != null && goalDoor != null)
        {
            if (gameManager.enemiesKilled >= gameManager.totalEnemies && gameManager.totalEnemies > 0)
            {
                doorHasBeenActivated = true;
                goalDoor.ActivateDoor();
            }
        }
    }

    /// <summary>
    /// Cree le canvas de fade noir
    /// </summary>
    void CreateFadeCanvas()
    {
        GameObject fadeObj = new GameObject("FadeCanvas");
        fadeObj.transform.SetParent(transform);

        fadeCanvas = fadeObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 9999;

        fadeObj.AddComponent<CanvasScaler>();
        fadeObj.AddComponent<GraphicRaycaster>();

        fadeCanvasGroup = fadeObj.AddComponent<CanvasGroup>();
        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;

        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(fadeObj.transform);

        fadeImage = imageObj.AddComponent<Image>();
        fadeImage.color = Color.black;

        RectTransform rt = fadeImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    // ============================================
    // VICTOIRE
    // ============================================

    void OnPlayerReachedGoal(GameObject playerObject)
    {
        if (levelCompleted || gameOver) return;

        levelCompleted = true;
        Debug.Log("=== NIVEAU COMPLETE! ===");

        // Desactiver le mouvement du joueur
        if (player != null)
        {
            player.enabled = false;
        }

        // Branching selon skipVictoryUI
        if (skipVictoryUI)
        {
            // Fade noir puis chargement direct
            StartCoroutine(FadeAndLoadNextLevel());
        }
        else
        {
            // Afficher l'ecran de victoire
            if (victoryUI != null)
            {
                victoryUI.Show(playerHealth);
            }
            else
            {
                Debug.LogWarning("VictoryUI non trouve! Chargement automatique du niveau suivant.");
                Invoke(nameof(LoadNextLevel), delayBeforeNextLevel);
            }
        }
    }

    /// <summary>
    /// Fade noir puis charge le niveau suivant
    /// </summary>
    IEnumerator FadeAndLoadNextLevel()
    {
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.blocksRaycasts = true;

            // Fade in (vers noir)
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                fadeCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }

            fadeCanvasGroup.alpha = 1f;
        }

        // Petit delai au noir
        yield return new WaitForSeconds(0.3f);

        // Charger le niveau suivant
        LoadNextLevel();
    }

    // ============================================
    // DEFAITE
    // ============================================

    void OnPlayerDeath()
    {
        if (gameOver || levelCompleted) return;

        gameOver = true;
        Debug.Log("=== GAME OVER ===");

        // Desactiver le mouvement du joueur
        if (player != null)
        {
            player.enabled = false;
        }

        // Afficher l'ecran de Game Over
        if (gameOverUI != null)
        {
            gameOverUI.Show();
        }
        else
        {
            Debug.LogWarning("GameOverUI non trouve! Redemarrage automatique.");
            Invoke(nameof(RestartLevel), delayBeforeRestart);
        }
    }

    // ============================================
    // NAVIGATION DE NIVEAU
    // ============================================

    /// <summary>
    /// Charge le niveau suivant
    /// </summary>
    public void LoadNextLevel()
    {
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        int nextSceneIndex = currentSceneIndex + 1;

        // Verifier s'il y a un niveau suivant
        if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
        {
            Debug.Log("Chargement du niveau " + nextSceneIndex + "...");
            Time.timeScale = 1f;
            SceneManager.LoadScene(nextSceneIndex);
        }
        else
        {
            // Plus de niveaux, retour au menu ou niveau 1
            Debug.Log("TOUS LES NIVEAUX COMPLETES! Recommencer...");
            Time.timeScale = 1f;
            SceneManager.LoadScene(0);
        }
    }

    /// <summary>
    /// Rejoue le niveau actuel
    /// </summary>
    public void RestartLevel()
    {
        Debug.Log("Redemarrage du niveau...");

        // Ne set le PlayerPrefs que si ce n'est PAS un niveau tuto
        if (!isTutorialLevel)
        {
            PlayerPrefs.SetInt("AutoStartCountdown", 1);
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// Quitte le jeu
    /// </summary>
    public void QuitGame()
    {
        Debug.Log("Quitter le jeu...");

        Time.timeScale = 1f;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// Appelee quand le player touche le levelEndTrigger (passage direct sans Victory UI)
    /// </summary>
    public void OnPlayerReachedLevelEnd(GameObject playerObject)
    {
        if (levelCompleted || gameOver) return;

        levelCompleted = true;
        Debug.Log("=== NIVEAU COMPLETE (Level End Trigger) ===");

        // Desactiver le mouvement du joueur
        if (player != null)
        {
            player.enabled = false;
        }

        // Chargement direct du niveau suivant sans Victory UI
        Invoke(nameof(LoadNextLevel), 0.5f);
    }

    // ============================================
    // CLEANUP
    // ============================================

    void OnDestroy()
    {
        // Se desabonner des evenements
        if (goalDoor != null)
        {
            goalDoor.OnPlayerReached -= OnPlayerReachedGoal;
        }

        if (playerHealth != null)
        {
            playerHealth.OnDeath -= OnPlayerDeath;
        }
    }
}