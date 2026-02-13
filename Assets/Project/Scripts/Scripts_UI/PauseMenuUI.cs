using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

public class PauseMenuUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private CanvasGroup pauseCanvasGroup;
    [SerializeField] private Image backgroundOverlay;

    [Header("Menu Options")]
    [SerializeField] private TextMeshProUGUI continueText;
    [SerializeField] private TextMeshProUGUI restartText;
    [SerializeField] private TextMeshProUGUI optionsText;
    [SerializeField] private TextMeshProUGUI quitText;

    [Header("Visual Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private float selectedScale = 1.2f;
    [SerializeField] private float transitionSpeed = 10f;

    private bool isPaused = false;
    private List<TextMeshProUGUI> menuTexts = new List<TextMeshProUGUI>();
    private int currentSelection = 0;
    private Vector3 normalScale = Vector3.one;

    private float navigationCooldown = 0f;
    private float cooldownDuration = 0.2f;

    void Start()
    {
        // Setup liste textes menu
        menuTexts.Add(continueText);
        menuTexts.Add(restartText);
        menuTexts.Add(optionsText);
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

        // Cacher au demarrage
        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
        }
        Hide();
    }

    void Update()
    {
        // Toggle pause avec Echap ou Start (manette)
        if (Input.GetKeyDown(KeyCode.Escape) || PlayerInputManager.Instance.PausePressed)
        {
            if (isPaused)
                Resume();
            else
                Pause();
        }

        // Si pause active, gerer navigation
        if (isPaused)
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

        // Validation avec Entree ou A/X manette - LIRE DIRECTEMENT L'INPUT
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetButtonDown("Submit"))
        {
            SelectCurrentOption();
        }

        // Cancel avec B manette
        if (Input.GetButtonDown("Cancel"))
        {
            Resume();
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
            case 0: // Continue
                Resume();
                break;
            case 1: // Restart
                RestartLevel();
                break;
            case 2: // Options
                OpenOptions();
                break;
            case 3: // Quit
                QuitToMenu();
                break;
        }
    }

    public void Pause()
    {
        isPaused = true;
        Time.timeScale = 0f;

        if (pauseCanvasGroup != null)
        {
            pauseCanvasGroup.alpha = 1f;
            pauseCanvasGroup.interactable = true;
            pauseCanvasGroup.blocksRaycasts = true;
        }

        currentSelection = 0;

        // Pause ambient track
        CountdownManager countdown = FindObjectOfType<CountdownManager>();
        if (countdown != null) countdown.PauseAmbient();

        Debug.Log("Game paused");
    }

    public void Resume()
    {
        isPaused = false;
        Time.timeScale = 1f;
        Hide();

        // Resume ambient track
        CountdownManager countdown = FindObjectOfType<CountdownManager>();
        if (countdown != null) countdown.ResumeAmbient();

        Debug.Log("Game resumed");
    }
    void Hide()
    {
        if (pauseCanvasGroup != null)
        {
            pauseCanvasGroup.alpha = 0f;
            pauseCanvasGroup.interactable = false;
            pauseCanvasGroup.blocksRaycasts = false;
        }
    }

    void RestartLevel()
    {
        Time.timeScale = 1f;
        isPaused = false;

        // Stop ambient track avant restart
        CountdownManager countdown = FindObjectOfType<CountdownManager>();
        if (countdown != null) countdown.StopAmbient();

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

    void OpenOptions()
    {
        Debug.Log("Options menu - A implementer");
        // TODO : Ouvrir menu options
    }

    void QuitToMenu()
    {
        Time.timeScale = 1f;
        isPaused = false;

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

    public bool IsPaused()
    {
        return isPaused;
    }
}