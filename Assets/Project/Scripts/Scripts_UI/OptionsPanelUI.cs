using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class OptionsPanelUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject optionsPanel;
    [Header("Audio")]
    [SerializeField] private UIAudioPlayer uiAudio;

    [Header("Option Texts")]
    [SerializeField] private TextMeshProUGUI gaugeText;
    [SerializeField] private TextMeshProUGUI difficultyText;
    [SerializeField] private TextMeshProUGUI audioText;

    [Header("Visual Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private float selectedScale = 1.2f;
    [SerializeField] private float transitionSpeed = 10f;

    private List<TextMeshProUGUI> optionTexts = new List<TextMeshProUGUI>();
    private int currentSelection = 0;
    private bool isOpen = false;

    private float navCooldown = 0f;
    private float cooldownDuration = 0.2f;

    void Awake()
    {
        if (uiAudio == null) uiAudio = FindObjectOfType<UIAudioPlayer>();

        optionTexts.Add(gaugeText);
        optionTexts.Add(difficultyText);
        optionTexts.Add(audioText);

        if (optionsPanel != null)
            optionsPanel.SetActive(false);
    }

    void Update()
    {
        if (!isOpen) return;

        if (navCooldown > 0f)
            navCooldown -= Time.unscaledDeltaTime;

        HandleNavigation();
        UpdateTexts();
        UpdateVisuals();
    }

    void HandleNavigation()
    {
        Vector2 input = PlayerInputManager.Instance.MoveInput;
        bool up = Input.GetKeyDown(KeyCode.UpArrow);
        bool down = Input.GetKeyDown(KeyCode.DownArrow);
        bool left = Input.GetKeyDown(KeyCode.LeftArrow);
        bool right = Input.GetKeyDown(KeyCode.RightArrow);

        if (navCooldown <= 0f)
        {
            if (input.y > 0.5f) up = true;
            else if (input.y < -0.5f) down = true;
            if (input.x < -0.5f) left = true;
            else if (input.x > 0.5f) right = true;

            if (up || down || left || right)
                navCooldown = cooldownDuration;
        }

        if (up)
        {
            currentSelection = (currentSelection - 1 + optionTexts.Count) % optionTexts.Count;
            if (uiAudio != null) uiAudio.PlayUp();
        }
        else if (down)
        {
            currentSelection = (currentSelection + 1) % optionTexts.Count;
            if (uiAudio != null) uiAudio.PlayDown();
        }

        if (left)
        {
            HandleHorizontal(true);
            if (uiAudio != null) uiAudio.PlayLeft();
        }
        else if (right)
        {
            HandleHorizontal(false);
            if (uiAudio != null) uiAudio.PlayRight();
        }

        if (Input.GetButtonDown("Cancel"))
        {
            if (uiAudio != null) uiAudio.PlayB();
            Close();
        }
    }

    void HandleHorizontal(bool goLeft)
    {
        if (OptionsManager.Instance == null) return;

        switch (currentSelection)
        {
            case 0:
                OptionsManager.Instance.SetGaugeVisible(!OptionsManager.Instance.gaugeVisible);
                break;
            case 1:
                int diffCount = System.Enum.GetValues(typeof(OptionsManager.Difficulty)).Length;
                int current = (int)OptionsManager.Instance.difficulty;
                int next = goLeft
                    ? (current - 1 + diffCount) % diffCount
                    : (current + 1) % diffCount;
                OptionsManager.Instance.SetDifficulty((OptionsManager.Difficulty)next);
                break;
            case 2:
                // Audio - a implementer
                break;
        }
    }

    void UpdateTexts()
    {
        if (OptionsManager.Instance == null) return;

        bool gaugeOn = OptionsManager.Instance.gaugeVisible;
        gaugeText.text = "Timer display     " + (gaugeOn ? "On" : "Off");

        bool isBanco = OptionsManager.Instance.difficulty == OptionsManager.Difficulty.Banco;
        difficultyText.text = "Difficulty     " + (isBanco ? "Easy" : "Normal");

        audioText.text = "Audio";
    }

    void UpdateVisuals()
    {
        for (int i = 0; i < optionTexts.Count; i++)
        {
            if (optionTexts[i] == null) continue;
            Color target = i == currentSelection ? selectedColor : normalColor;
            optionTexts[i].color = Color.Lerp(optionTexts[i].color, target, Time.unscaledDeltaTime * transitionSpeed);
            Vector3 targetScale = i == currentSelection ? Vector3.one * selectedScale : Vector3.one;
            optionTexts[i].transform.localScale = Vector3.Lerp(optionTexts[i].transform.localScale, targetScale, Time.unscaledDeltaTime * transitionSpeed);
        }
    }

    public void Open()
    {
        isOpen = true;
        currentSelection = 0;
        if (optionsPanel != null)
            optionsPanel.SetActive(true);
    }

    public void Close()
    {
        isOpen = false;
        if (optionsPanel != null)
            optionsPanel.SetActive(false);
    }

    public bool IsOpen() => isOpen;
}