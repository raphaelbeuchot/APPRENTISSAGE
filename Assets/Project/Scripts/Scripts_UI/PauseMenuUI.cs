using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using System.Collections;

public class PauseMenuUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private CanvasGroup pauseCanvasGroup;
    [SerializeField] private Image backgroundOverlay;

    [Header("Audio")]
    [SerializeField] private UIAudioPlayer uiAudio;

    [Header("Menu Options")]
    [SerializeField] private OptionsPanelUI optionsPanelUI;
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

    void Awake()
    {
        if (uiAudio == null) uiAudio = FindObjectOfType<UIAudioPlayer>();
    }

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
        if (optionsPanelUI != null && optionsPanelUI.IsOpen()) return;

        if (navigationCooldown > 0f)
        {
            navigationCooldown -= Time.unscaledDeltaTime;
        }

        Vector2 moveInput = PlayerInputManager.Instance.MoveInput;
        bool upPressed = Input.GetKeyDown(KeyCode.UpArrow);
        bool downPressed = Input.GetKeyDown(KeyCode.DownArrow);

        if (navigationCooldown <= 0f)
        {
            if (moveInput.y > 0.5f) upPressed = true;
            else if (moveInput.y < -0.5f) downPressed = true;

            if (upPressed || downPressed)
                navigationCooldown = cooldownDuration;
        }

        if (upPressed)
        {
            currentSelection = (currentSelection - 1 + menuTexts.Count) % menuTexts.Count;
            if (uiAudio != null) uiAudio.PlayUp();
        }
        else if (downPressed)
        {
            currentSelection = (currentSelection + 1) % menuTexts.Count;
            if (uiAudio != null) uiAudio.PlayDown();
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetButtonDown("Submit"))
        {
            if (uiAudio != null) uiAudio.PlayA();
            SelectCurrentOption();
        }

        if (Input.GetButtonDown("Cancel"))
        {
            if (uiAudio != null) uiAudio.PlayB();
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
        PlayerInputManager.Instance.IsLocked = true;
        isPaused = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;


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
        SentinelCycleManager cycle = FindObjectOfType<SentinelCycleManager>();
        if (cycle != null) cycle.PauseMusic();

    }

    public void Resume()
    {
        isPaused = false;
        StartCoroutine(UnlockNextFrame());
        PlayerInputManager.Instance.FlushInputs();
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Hide();

        CountdownManager countdown = FindObjectOfType<CountdownManager>();
        if (countdown != null) countdown.ResumeAmbient();
        SentinelCycleManager cycle = FindObjectOfType<SentinelCycleManager>();
        if (cycle != null) cycle.ResumeMusic();
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
        AudioListener.pause = false;
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
        if (optionsPanelUI != null)
            optionsPanelUI.Open();
    }

    void QuitToMenu()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        isPaused = false;

        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }

    public bool IsPaused()
    {
        return isPaused;
    }
    IEnumerator UnlockNextFrame()
    {
        yield return null;
        PlayerInputManager.Instance.IsLocked = false;
    }
}