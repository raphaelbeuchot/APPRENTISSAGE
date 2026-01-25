using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gère le déroulement complet du niveau :
/// - Détection de la victoire (GoalDoor)
/// - Détection de la défaite (PlayerHealth)
/// - Affichage des écrans UI
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

    [Header("Level End")]
    [SerializeField] private Collider levelEndTrigger; // Pour niveau tuto sans Victory UI
    [SerializeField] private bool useLevelEndTrigger = false; // Active/désactive ce système
    [SerializeField] private bool isTutorialLevel = false;

    private bool levelCompleted = false;
    private bool gameOver = false;

    void Start()
    {
        // Trouver automatiquement les références si non assignées
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

        // S'abonner aux événements
        if (goalDoor != null)
        {
            goalDoor.OnPlayerReached += OnPlayerReachedGoal;
        }
        else
        {
            Debug.LogWarning("LevelManager: Aucune GoalDoor trouvée dans la scène!");
        }

        if (playerHealth != null)
        {
            playerHealth.OnDeath += OnPlayerDeath;
        }
        else
        {
            Debug.LogWarning("LevelManager: PlayerHealth non trouvé!");
        }

        // S'assurer que le temps est normal
        Time.timeScale = 1f;
    }

    // ============================================
    // VICTOIRE
    // ============================================

    void OnPlayerReachedGoal(GameObject playerObject)
    {
        if (levelCompleted || gameOver) return;

        levelCompleted = true;
        Debug.Log("=== NIVEAU COMPLETÉ! ===");

        // Désactiver le mouvement du joueur
        if (player != null)
        {
            player.enabled = false;
        }

        // Afficher l'écran de victoire
        if (victoryUI != null)
        {
            victoryUI.Show(playerHealth);
        }
        else
        {
            Debug.LogWarning("VictoryUI non trouvé! Chargement automatique du niveau suivant.");
            Invoke(nameof(LoadNextLevel), delayBeforeNextLevel);
        }
    }

    // ============================================
    // DÉFAITE
    // ============================================

    void OnPlayerDeath()
    {
        if (gameOver || levelCompleted) return;

        gameOver = true;
        Debug.Log("=== GAME OVER ===");

        // Désactiver le mouvement du joueur
        if (player != null)
        {
            player.enabled = false;
        }

        // Afficher l'écran de Game Over
        if (gameOverUI != null)
        {
            gameOverUI.Show();
        }
        else
        {
            Debug.LogWarning("GameOverUI non trouvé! Redémarrage automatique.");
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

        // Vérifier s'il y a un niveau suivant
        if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
        {
            Debug.Log("Chargement du niveau " + nextSceneIndex + "...");
            Time.timeScale = 1f;
            SceneManager.LoadScene(nextSceneIndex);
        }
        else
        {
            // Plus de niveaux, retour au menu ou niveau 1
            Debug.Log("TOUS LES NIVEAUX COMPLÉTÉS! Recommencer...");
            Time.timeScale = 1f;
            SceneManager.LoadScene(0); // Retour au niveau 1
        }
    }

    /// <summary>
    /// Rejoue le niveau actuel
    /// </summary>
    public void RestartLevel()
    {
        Debug.Log("Redémarrage du niveau...");

        // NOUVEAU : Ne set le PlayerPrefs que si ce n'est PAS un niveau tuto
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


#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    /// <summary>
    /// Appelée quand le player touche le levelEndTrigger (passage direct sans Victory UI)
    /// </summary>
    public void OnPlayerReachedLevelEnd(GameObject playerObject)
    {
        if (levelCompleted || gameOver) return;

        levelCompleted = true;
        Debug.Log("=== NIVEAU COMPLETÉ (Level End Trigger) ===");

        // Désactiver le mouvement du joueur
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
        // Se désabonner des événements
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