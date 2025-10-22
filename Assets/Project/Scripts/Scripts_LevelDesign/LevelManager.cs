using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour
{
    [Header("References")]
    public GoalDoor goalDoor;
    public GameManager gameManager;
    public PlayerPhysicsMovement player;

    [Header("Configuration")]
    public float delayBeforeNextLevel = 2f;

    [Header("UI (Optionnel)")]
    public GameObject victoryUI;

    private bool levelCompleted = false;

    void Start()
    {
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

        // S'abonner a l'evenement de la porte
        if (goalDoor != null)
        {
            goalDoor.OnPlayerReached += OnPlayerReachedGoal;
        }
        else
        {
            Debug.LogWarning("LevelManager: Aucune GoalDoor trouvee dans la scene!");
        }

        // Cacher l'UI de victoire au debut
        if (victoryUI != null)
        {
            victoryUI.SetActive(false);
        }

        // S'assurer que le temps est normal
        Time.timeScale = 1f;
    }

    void OnPlayerReachedGoal(GameObject playerObject)
    {
        if (levelCompleted) return;

        levelCompleted = true;
        Debug.Log("=== NIVEAU COMPLETE! ===");

        // Afficher l'UI de victoire
        if (victoryUI != null)
        {
            victoryUI.SetActive(true);
        }

        // Calculer des stats (optionnel)
        DisplayStats();

        // Charger le niveau suivant apres un delai
        Invoke("LoadNextLevel", delayBeforeNextLevel);
    }

    void DisplayStats()
    {
        // Stats du joueur
        PlayerHealth playerHealth = player != null ? player.GetComponent<PlayerHealth>() : null;
        if (playerHealth != null)
        {
            float healthPercent = playerHealth.GetHealthPercentage();

            Debug.Log("Statistiques du niveau:");
            Debug.Log("- Sante restante: " + (healthPercent * 100f) + "%");
            Debug.Log("- Etat: " + (playerHealth.IsCritical() ? "CRITIQUE" : "OK"));
        }
    }

    void LoadNextLevel()
    {
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        int nextSceneIndex = currentSceneIndex + 1;

        // Verifier s'il y a un niveau suivant
        if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
        {
            Debug.Log("Chargement du niveau " + nextSceneIndex + "...");
            SceneManager.LoadScene(nextSceneIndex);
        }
        else
        {
            // Plus de niveaux, on recommence ou on affiche un ecran de fin
            Debug.Log("TOUS LES NIVEAUX COMPLETES! Recommencer...");
            SceneManager.LoadScene(0); // Retour au niveau 1
        }
    }

    /// <summary>
    /// Rejouer le niveau actuel
    /// </summary>
    public void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// Quitter le jeu
    /// </summary>
    public void QuitGame()
    {
        Debug.Log("Quitter le jeu");
        Application.Quit();
    }

    void OnDestroy()
    {
        // Se desabonner de l'evenement
        if (goalDoor != null)
        {
            goalDoor.OnPlayerReached -= OnPlayerReachedGoal;
        }
    }
}