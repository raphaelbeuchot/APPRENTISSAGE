using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Gere l'ecran de Game Over.
/// Affiche un message et des boutons pour rejouer ou quitter.
/// </summary>
public class GameOverUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private CanvasGroup gameOverCanvasGroup; // NOUVEAU
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

    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;

    void Start()
    {
        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // NOUVEAU : S'abonner a la mort du joueur
        if (playerHealth == null)
        {
            playerHealth = FindObjectOfType<PlayerHealth>();
        }

        if (playerHealth != null)
        {
            playerHealth.OnDeath += Show;
            Debug.Log("GameOverUI subscribed to PlayerHealth.OnDeath");
        }
        else
        {
            Debug.LogError("GameOverUI: PlayerHealth not found!");
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

        // Garder le panel actif mais invisible
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        // Cacher au demarrage
        Hide();
    }

    /// <summary>
    /// Affiche l'ecran de Game Over
    /// </summary>
    public void Show()
    {
        // NOUVEAU : Utiliser CanvasGroup pour afficher
        if (gameOverCanvasGroup != null)
        {
            gameOverCanvasGroup.alpha = 1f;
            gameOverCanvasGroup.interactable = true;
            gameOverCanvasGroup.blocksRaycasts = true;
        }

        // Message aleatoire
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

        Debug.Log("=== GAME OVER ===");
    }

    /// <summary>
    /// Cache l'ecran de Game Over
    /// </summary>
    public void Hide()
    {
        // NOUVEAU : Utiliser CanvasGroup pour cacher
        if (gameOverCanvasGroup != null)
        {
            gameOverCanvasGroup.alpha = 0f;
            gameOverCanvasGroup.interactable = false;
            gameOverCanvasGroup.blocksRaycasts = false;
        }
    }

    void OnRestartClicked()
    {
        Debug.Log("Restart button clicked!");

        // Remettre le temps a la normale
        Time.timeScale = 1f;

        // Notifier le LevelManager
        LevelManager levelManager = FindObjectOfType<LevelManager>();
        if (levelManager != null)
        {
            levelManager.RestartLevel();
        }
        else
        {
            // Fallback: recharger la scene manuellement
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

    void OnDestroy()
    {
        // Se desabonner pour eviter les leaks
        if (playerHealth != null)
        {
            playerHealth.OnDeath -= Show;
        }
    }
}