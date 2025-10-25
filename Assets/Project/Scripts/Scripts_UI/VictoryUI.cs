using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Gère l'écran de victoire.
/// Affiche un message, les stats, et des boutons pour continuer.
/// </summary>
public class VictoryUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private TextMeshProUGUI victoryText;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private Button nextLevelButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button quitButton;

    [Header("Messages")]
    [SerializeField]
    private string[] victoryMessages = new string[]
    {
        "LEVEL COMPLETE!",
        "VICTORY!",
        "ESCAPED!",
        "SURVIVOR!",
        "WELL DONE!"
    };

    [Header("Audio")]
    [SerializeField] private AudioClip victorySound;
    private AudioSource audioSource;

    [Header("Animation")]
    [SerializeField] private float textAnimationDuration = 0.5f;

    void Start()
    {
        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Connecter les boutons
        if (nextLevelButton != null)
        {
            nextLevelButton.onClick.AddListener(OnNextLevelClicked);
        }

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
    /// Affiche l'écran de victoire avec les stats du joueur
    /// </summary>
    public void Show(PlayerHealth playerHealth = null)
    {
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
        }

        // Message aléatoire
        if (victoryText != null && victoryMessages.Length > 0)
        {
            string randomMessage = victoryMessages[Random.Range(0, victoryMessages.Length)];
            victoryText.text = randomMessage;
        }

        // Afficher les stats
        DisplayStats(playerHealth);

        // Son
        if (audioSource != null && victorySound != null)
        {
            audioSource.PlayOneShot(victorySound);
        }

        // Animation (optionnelle)
        StartCoroutine(AnimateVictoryText());

        Debug.Log("=== VICTORY ===");
    }

    /// <summary>
    /// Cache l'écran de victoire
    /// </summary>
    public void Hide()
    {
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Affiche les statistiques du niveau
    /// </summary>
    void DisplayStats(PlayerHealth playerHealth)
    {
        if (statsText == null) return;

        string stats = "";

        if (playerHealth != null)
        {
            float healthPercent = playerHealth.GetHealthPercentage();
            stats += $"Health Remaining: {Mathf.RoundToInt(healthPercent * 100)}%\n";

            if (playerHealth.IsCritical())
            {
                stats += "Status: CRITICAL\n";
            }
            else
            {
                stats += "Status: OK\n";
            }
        }

        // Ajouter d'autres stats si nécessaire
        // stats += $"Time: {Time.timeSinceLevelLoad:F1}s\n";
        // stats += $"Zombies Killed: {zombiesKilled}\n";

        statsText.text = stats;
    }

    /// <summary>
    /// Animation simple du texte de victoire
    /// </summary>
    IEnumerator AnimateVictoryText()
    {
        if (victoryText == null) yield break;

        // Scale animation
        Vector3 originalScale = victoryText.transform.localScale;
        victoryText.transform.localScale = Vector3.zero;

        float elapsed = 0f;
        while (elapsed < textAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / textAnimationDuration;

            // Easing
            t = Mathf.Sin(t * Mathf.PI * 0.5f);

            victoryText.transform.localScale = Vector3.Lerp(Vector3.zero, originalScale * 1.2f, t);

            yield return null;
        }

        // Retour à la taille normale
        elapsed = 0f;
        while (elapsed < 0.2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.2f;
            victoryText.transform.localScale = Vector3.Lerp(originalScale * 1.2f, originalScale, t);
            yield return null;
        }

        victoryText.transform.localScale = originalScale;
    }

    void OnNextLevelClicked()
    {
        Debug.Log("Next Level button clicked!");

        // Remettre le temps à la normale
        Time.timeScale = 1f;

        // Notifier le LevelManager
        LevelManager levelManager = FindObjectOfType<LevelManager>();
        if (levelManager != null)
        {
            levelManager.LoadNextLevel();
        }
        else
        {
            Debug.LogWarning("LevelManager not found!");
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
            Application.Quit();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}