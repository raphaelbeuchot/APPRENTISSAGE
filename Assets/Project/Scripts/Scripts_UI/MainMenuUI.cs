using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

public class MainMenuUI : MonoBehaviour
{
    [Header("Menu Options")]
    [SerializeField] private OptionsPanelUI optionsPanelUI;

    [SerializeField] private TextMeshProUGUI newGameText;
    [SerializeField] private TextMeshProUGUI continueText;
    [SerializeField] private TextMeshProUGUI optionsText;
    [SerializeField] private TextMeshProUGUI quitText;

    [Header("Audio")]
    [SerializeField] private UIAudioPlayer uiAudio;

    [Header("Visual Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private Color lockedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    [SerializeField] private float selectedScale = 1.2f;
    [SerializeField] private float transitionSpeed = 10f;

    private List<TextMeshProUGUI> menuTexts = new List<TextMeshProUGUI>();
    private int currentSelection = 0;
    private Vector3 normalScale = Vector3.one;
    private float navigationCooldown = 0f;
    private float cooldownDuration = 0.2f;
    private bool hasSave = false;

    void Start()
    {
        Time.timeScale = 1f;

        hasSave = LevelProgressionManager.Instance != null && LevelProgressionManager.Instance.HasSave();

        if (!hasSave)
        {
            continueText.gameObject.SetActive(false);
            optionsText.transform.position = continueText.transform.position;
            menuTexts.Add(newGameText);
            menuTexts.Add(optionsText);
            menuTexts.Add(quitText);
        }
        else
        {
            menuTexts.Add(newGameText);
            menuTexts.Add(continueText);
            menuTexts.Add(optionsText);
            menuTexts.Add(quitText);
        }

        UpdateVisuals();
    }

    void Update()
    {
        HandleNavigation();
        UpdateVisuals();
    }

    void HandleNavigation()
    {
        if (optionsPanelUI != null && optionsPanelUI.IsOpen()) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Screen.fullScreen = false;
            return;
        }

        if (navigationCooldown > 0f)
        {
            navigationCooldown -= Time.unscaledDeltaTime;
            return;
        }

        Vector2 input = PlayerInputManager.Instance.MoveInput;

        if (input.y > 0.5f)
        {
            currentSelection = (currentSelection - 1 + menuTexts.Count) % menuTexts.Count;
            navigationCooldown = cooldownDuration;
            if (uiAudio != null) uiAudio.PlayUp();
        }
        else if (input.y < -0.5f)
        {
            currentSelection = (currentSelection + 1) % menuTexts.Count;
            navigationCooldown = cooldownDuration;
            if (uiAudio != null) uiAudio.PlayDown();
        }

        if (AnyFaceButtonPressed())
        {
            if (uiAudio != null) uiAudio.PlayA();
            SelectCurrentOption();
        }
    }

    void SelectCurrentOption()
    {
        if (!hasSave)
        {
            switch (currentSelection)
            {
                case 0: NewGame(); break;
                case 1: OpenOptions(); break;
                case 2: Quit(); break;
            }
        }
        else
        {
            switch (currentSelection)
            {
                case 0: NewGame(); break;
                case 1: Continue(); break;
                case 2: OpenOptions(); break;
                case 3: Quit(); break;
            }
        }
    }

    void NewGame()
    {
        if (uiAudio != null) uiAudio.PlayNewGameAndSurvive();

        if (LevelProgressionManager.Instance != null)
            LevelProgressionManager.Instance.ResetProgression();

        LoadingScreenManager.TargetSceneIndex = LevelProgressionManager.Instance.levels[0].sceneIndex;
        SceneManager.LoadScene(1);
    }

    void Continue()
    {
        int idx = LevelProgressionManager.Instance.lastUnlockedLevelIndex;
        if (idx < 0 || idx >= LevelProgressionManager.Instance.levels.Count)
            idx = 0;
        LoadingScreenManager.TargetSceneIndex = LevelProgressionManager.Instance.levels[idx].sceneIndex;
        SceneManager.LoadScene(1);
    }

    void OpenOptions()
    {
        if (optionsPanelUI != null)
            optionsPanelUI.Open();
    }

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void UpdateVisuals()
    {
        for (int i = 0; i < menuTexts.Count; i++)
        {
            if (menuTexts[i] == null) continue;

            Color targetColor = (i == currentSelection) ? selectedColor : normalColor;
            menuTexts[i].color = Color.Lerp(menuTexts[i].color, targetColor, Time.unscaledDeltaTime * transitionSpeed);

            Vector3 targetScale = (i == currentSelection)
                ? normalScale * selectedScale
                : normalScale;
            menuTexts[i].transform.localScale = Vector3.Lerp(menuTexts[i].transform.localScale, targetScale, Time.unscaledDeltaTime * transitionSpeed);
        }
    }

    bool AnyFaceButtonPressed()
    {
        return Input.GetButtonDown("Submit");
    }
}