using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Gère l'écran de Game Over.
/// Affiche un message et des boutons pour rejouer ou quitter.
/// </summary>
public class GameOverUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI gameOverText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button quitButton;

    [Header("Messages")]
    [SerializeField]
    private string[] deathMessages = new string[]
    {
        "YOU DIED",
        "GAME OVER",
        "CAUGHT!",
        "ELIMINATED",
        "TERMINATED"
    };

    [Header("Audio")]
    [SerializeField] private AudioClip gameOverSound;
    private AudioSource audioSource;

    void Start()
    {
        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Connecter les boutons
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(OnRestartClicked);
        }

        if (quitButton != null)
        {
            quitButton.onClick.AddListener(OnQuitClicked);
        }

        // Cacher le panel au démarrage
        Hide();
    }

    /// <summary>
    /// Affiche l'écran de Game Over
    /// </summary>
    public void Show()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        // Message aléatoire
        if (gameOverText != null && deathMessages.Length > 0)
        {
            string randomMessage = deathMessages[Random.Range(0, deathMessages.Length)];
            gameOverText.text = randomMessage;
        }

        // Son
        if (audioSource != null && gameOverSound != null)
        {
            audioSource.PlayOneShot(gameOverSound);
        }

        // Arrêter le temps (optionnel)
        // Time.timeScale = 0f;

        Debug.Log("=== GAME OVER ===");
    }

    /// <summary>
    /// Cache l'écran de Game Over
    /// </summary>
    public void Hide()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }

    void OnRestartClicked()
    {
        Debug.Log("Restart button clicked!");

        // Remettre le temps à la normale
        Time.timeScale = 1f;

        // Notifier le LevelManager
        LevelManager levelManager = FindObjectOfType<LevelManager>();
        if (levelManager != null)
        {
            levelManager.RestartLevel();
        }
        else
        {
            // Fallback: recharger la scène manuellement
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            );
        }
    }

    void OnQuitClicked()
    {
        Debug.Log("Quit button clicked!");

        // Notifier le LevelManager
        LevelManager levelManager = FindObjectOfType<LevelManager>();
        if (levelManager != null)
        {
            levelManager.QuitGame();
        }
        else
        {
            // Fallback
            Application.Quit();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}