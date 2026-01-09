using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class VictoryUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private CanvasGroup victoryCanvasGroup;
    [SerializeField] private TextMeshProUGUI victoryText;
    [SerializeField] private TextMeshProUGUI statsText;

    [Header("Menu Options")]
    [SerializeField] private TextMeshProUGUI nextLevelText;
    [SerializeField] private TextMeshProUGUI restartText;
    [SerializeField] private TextMeshProUGUI quitText;

    [Header("Visual Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private float selectedScale = 1.2f;
    [SerializeField] private float transitionSpeed = 10f;

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

    private List<TextMeshProUGUI> menuTexts = new List<TextMeshProUGUI>();
    private int currentSelection = 0;
    private Vector3 normalScale = Vector3.one;
    private float navigationCooldown = 0f;
    private float cooldownDuration = 0.2f;
    private bool isActive = false;

    void Start()
    {
        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Setup liste textes menu
        menuTexts.Add(nextLevelText);
        menuTexts.Add(restartText);
        menuTexts.Add(quitText);

        // Initialiser couleurs et scales
        foreach (var text in menuTexts)
        {
            if (text != null)
            {
                text.color = normalColor;
                text.transform.localScale = normalScale;
            }
        }

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
        }

        Hide();
    }

    void Update()
    {
        if (isActive)
        {
            HandleNavigation();
            UpdateVisuals();
        }
    }

    void HandleNavigation()
    {
        // Cooldown entre navigations
        if (navigationCooldown > 0f)
        {
            navigationCooldown -= Time.unscaledDeltaTime;
        }

        // Navigation haut/bas
        Vector2 moveInput = PlayerInputManager.Instance.MoveInput;
        bool upPressed = Input.GetKeyDown(KeyCode.UpArrow);
        bool downPressed = Input.GetKeyDown(KeyCode.DownArrow);

        // Manette : detecter mouvement stick avec cooldown
        if (navigationCooldown <= 0f)
        {
            if (moveInput.y > 0.5f)
                upPressed = true;
            else if (moveInput.y < -0.5f)
                downPressed = true;

            if (upPressed || downPressed)
                navigationCooldown = cooldownDuration;
        }

        if (upPressed)
        {
            currentSelection--;
            if (currentSelection < 0)
                currentSelection = menuTexts.Count - 1;
        }
        else if (downPressed)
        {
            currentSelection++;
            if (currentSelection >= menuTexts.Count)
                currentSelection = 0;
        }

        // Validation avec Entree ou A/X manette
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetButtonDown("Submit"))
        {
            SelectCurrentOption();
        }
    }

    void UpdateVisuals()
    {
        for (int i = 0; i < menuTexts.Count; i++)
        {
            if (menuTexts[i] == null) continue;

            // Couleur cible
            Color targetColor = (i == currentSelection) ? selectedColor : normalColor;
            menuTexts[i].color = Color.Lerp(menuTexts[i].color, targetColor, Time.unscaledDeltaTime * transitionSpeed);

            // Scale cible
            Vector3 targetScale = (i == currentSelection) ? normalScale * selectedScale : normalScale;
            menuTexts[i].transform.localScale = Vector3.Lerp(menuTexts[i].transform.localScale, targetScale, Time.unscaledDeltaTime * transitionSpeed);
        }
    }

    void SelectCurrentOption()
    {
        switch (currentSelection)
        {
            case 0: // Next Level
                OnNextLevelClicked();
                break;
            case 1: // Restart
                OnRestartClicked();
                break;
            case 2: // Quit
                OnQuitClicked();
                break;
        }
    }

    public void Show(PlayerHealth playerHealth = null)
    {
        isActive = true;

        // Freeze le temps
        Time.timeScale = 0f;

        if (victoryCanvasGroup != null)
        {
            victoryCanvasGroup.alpha = 1f;
            victoryCanvasGroup.interactable = true;
            victoryCanvasGroup.blocksRaycasts = true;
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

        // Animation
        StartCoroutine(AnimateVictoryText());

        currentSelection = 0;
        Debug.Log("=== VICTORY ===");
    }

    public void Hide()
    {
        isActive = false;

        if (victoryCanvasGroup != null)
        {
            victoryCanvasGroup.alpha = 0f;
            victoryCanvasGroup.interactable = false;
            victoryCanvasGroup.blocksRaycasts = false;
        }
    }

    void DisplayStats(PlayerHealth playerHealth)
    {
        if (statsText == null) return;

        string stats = "";

        // Stats player
        if (playerHealth != null)
        {
            float healthPercent = playerHealth.GetHealthPercentage();
            stats += $"Health Remaining: {Mathf.RoundToInt(healthPercent * 100)}%\n";
        }

        // Killcount
        GameManager gameManager = FindObjectOfType<GameManager>();
        if (gameManager != null)
        {
            stats += $"Killcount: {gameManager.GetEnemiesKilled()}/{gameManager.GetTotalEnemies()}\n";
        }
        else
        {
            stats += "Killcount: ?/?\n";
        }

        // Temps
        stats += $"Time: {FormatTime(Time.timeSinceLevelLoad)}\n";

        statsText.text = stats;
    }

    string FormatTime(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);
        return $"{minutes:00}:{seconds:00}";
    }

    IEnumerator AnimateVictoryText()
    {
        if (victoryText == null) yield break;

        Vector3 originalScale = victoryText.transform.localScale;
        victoryText.transform.localScale = Vector3.zero;

        float elapsed = 0f;
        while (elapsed < textAnimationDuration)
        {
            elapsed += Time.unscaledDeltaTime; // IMPORTANT: unscaledDeltaTime car time freeze
            float t = elapsed / textAnimationDuration;
            t = Mathf.Sin(t * Mathf.PI * 0.5f);
            victoryText.transform.localScale = Vector3.Lerp(Vector3.zero, originalScale * 1.2f, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < 0.2f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / 0.2f;
            victoryText.transform.localScale = Vector3.Lerp(originalScale * 1.2f, originalScale, t);
            yield return null;
        }

        victoryText.transform.localScale = originalScale;
    }

    void OnNextLevelClicked()
    {
        Debug.Log("Next Level button clicked!");
        Time.timeScale = 1f;

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
        Time.timeScale = 1f;

        LevelManager levelManager = FindObjectOfType<LevelManager>();
        if (levelManager != null)
        {
            levelManager.RestartLevel();
        }
    }

    void OnQuitClicked()
    {
        Debug.Log("Quit button clicked!");
        Time.timeScale = 1f;

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