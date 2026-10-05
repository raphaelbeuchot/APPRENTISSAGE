using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

// Panneau "Solo" ouvert depuis MainMenuUI (meme pattern qu'OptionsPanelUI : Open/Close/IsOpen,
// ignore ses inputs si le panneau Options s'ouvre par-dessus). Porte Continue/New Game/Options
// (deplaces depuis MainMenuUI) : le menu racine ne garde que Solo/Multiplayer/Quit.
public class SoloPanelUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject soloPanel;
    [SerializeField] private OptionsPanelUI optionsPanelUI;

    [Header("Option Texts")]
    [SerializeField] private TextMeshProUGUI newGameText;
    [SerializeField] private TextMeshProUGUI continueText;
    [SerializeField] private TextMeshProUGUI optionsText;

    [Header("Audio")]
    [SerializeField] private UIAudioPlayer uiAudio;

    [Header("Visual Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private float selectedScale = 1.2f;
    [SerializeField] private float transitionSpeed = 10f;

    private List<TextMeshProUGUI> menuTexts = new List<TextMeshProUGUI>();
    private int currentSelection = 0;
    private Vector3 normalScale = Vector3.one;
    private bool isOpen = false;
    private bool hasSave = false;

    private float navigationCooldown = 0f;
    private const float cooldownDuration = 0.2f;

    void Awake()
    {
        if (uiAudio == null) uiAudio = FindObjectOfType<UIAudioPlayer>();

        hasSave = LevelProgressionManager.Instance != null && LevelProgressionManager.Instance.HasSave();

        if (!hasSave)
        {
            continueText.gameObject.SetActive(false);
            optionsText.transform.position = continueText.transform.position;
            menuTexts.Add(newGameText);
            menuTexts.Add(optionsText);
        }
        else
        {
            menuTexts.Add(newGameText);
            menuTexts.Add(continueText);
            menuTexts.Add(optionsText);
        }

        if (soloPanel != null)
            soloPanel.SetActive(false);
    }

    void Update()
    {
        if (!isOpen) return;
        if (optionsPanelUI != null && optionsPanelUI.IsOpen()) return;

        if (navigationCooldown > 0f)
        {
            navigationCooldown -= Time.unscaledDeltaTime;
        }
        else
        {
            HandleNavigation();
        }

        UpdateVisuals();
    }

    void HandleNavigation()
    {
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

        if (Input.GetButtonDown("Cancel"))
        {
            if (uiAudio != null) uiAudio.PlayB();
            Close();
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
            }
        }
        else
        {
            switch (currentSelection)
            {
                case 0: NewGame(); break;
                case 1: Continue(); break;
                case 2: OpenOptions(); break;
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

    void UpdateVisuals()
    {
        for (int i = 0; i < menuTexts.Count; i++)
        {
            if (menuTexts[i] == null) continue;

            Color targetColor = (i == currentSelection) ? selectedColor : normalColor;
            menuTexts[i].color = Color.Lerp(menuTexts[i].color, targetColor, Time.unscaledDeltaTime * transitionSpeed);

            Vector3 targetScale = (i == currentSelection) ? normalScale * selectedScale : normalScale;
            menuTexts[i].transform.localScale = Vector3.Lerp(menuTexts[i].transform.localScale, targetScale, Time.unscaledDeltaTime * transitionSpeed);
        }
    }

    bool AnyFaceButtonPressed()
    {
        return Input.GetButtonDown("Submit");
    }

    public void Open()
    {
        isOpen = true;
        currentSelection = 0;
        if (soloPanel != null)
            soloPanel.SetActive(true);
    }

    public void Close()
    {
        isOpen = false;
        if (soloPanel != null)
            soloPanel.SetActive(false);
    }

    public bool IsOpen() => isOpen;
}
