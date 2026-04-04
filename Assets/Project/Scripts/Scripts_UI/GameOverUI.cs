using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

public class GameOverUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private CanvasGroup gameOverCanvasGroup;
    [SerializeField] private TextMeshProUGUI gameOverText;
    [SerializeField] private Image backgroundOverlay;

    [Header("Menu Options")]
    [SerializeField] private TextMeshProUGUI restartText;
    [SerializeField] private TextMeshProUGUI quitText;

    [Header("Visual Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private float selectedScale = 1.2f;
    [SerializeField] private float transitionSpeed = 10f;

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

    private List<TextMeshProUGUI> menuTexts = new List<TextMeshProUGUI>();
    private int currentSelection = 0;
    private Vector3 normalScale = Vector3.one;
    private float navigationCooldown = 0f;
    private float cooldownDuration = 0.2f;
    private bool isActive = false;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

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

        // Setup liste textes menu
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

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
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

        // Cancel direct vers Quit
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetButtonDown("Cancel"))
        {
            OnQuitClicked();
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
            case 0: // Restart
                OnRestartClicked();
                break;
            case 1: // Quit
                OnQuitClicked();
                break;
        }
    }

    public void Show()
    {
        isActive = true;

        // NOUVEAU : Freeze le temps comme en pause
        Time.timeScale = 0f;


        SentinelCycleManager cycle = FindObjectOfType<SentinelCycleManager>();
        if (cycle != null) cycle.PauseMusic();

        if (gameOverCanvasGroup != null)
        {
            gameOverCanvasGroup.alpha = 1f;
            gameOverCanvasGroup.interactable = true;
            gameOverCanvasGroup.blocksRaycasts = true;
        }

        if (gameOverText != null && deathMessages.Length > 0)
        {
            string randomMessage = deathMessages[Random.Range(0, deathMessages.Length)];
            gameOverText.text = randomMessage;
        }

        if (audioSource != null && gameOverSound != null)
        {
            audioSource.PlayOneShot(gameOverSound);
        }

        currentSelection = 0;
        Debug.Log("=== GAME OVER ===");
    }

    public void Hide()
    {
        isActive = false;

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
        Time.timeScale = 1f;


        LevelManager levelManager = FindObjectOfType<LevelManager>();
        if (levelManager != null)
        {
            levelManager.RestartLevel();
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            );
        }
    }

    void OnQuitClicked()
    {
        Debug.Log("Quit button clicked!");

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

    void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.OnDeath -= Show;
        }
    }
}