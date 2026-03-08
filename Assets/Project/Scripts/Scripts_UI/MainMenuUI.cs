using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

public class MainMenuUI : MonoBehaviour
{
    [Header("Menu Options")]
    [SerializeField] private TextMeshProUGUI newGameText;
    [SerializeField] private TextMeshProUGUI continueText;
    [SerializeField] private TextMeshProUGUI optionsText;

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

        menuTexts.Add(newGameText);
        menuTexts.Add(continueText);
        menuTexts.Add(optionsText);

        UpdateVisuals();
    }

    void Update()
    {
        HandleNavigation();
        UpdateVisuals();
    }

    void HandleNavigation()
    {
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
        }
        else if (input.y < -0.5f)
        {
            currentSelection = (currentSelection + 1) % menuTexts.Count;
            navigationCooldown = cooldownDuration;
        }

        if (AnyFaceButtonPressed())
            SelectCurrentOption();
    }

    void SelectCurrentOption()
    {
        switch (currentSelection)
        {
            case 0:
                NewGame();
                break;
            case 1:
                if (hasSave) Continue();
                break;
            case 2:
                OpenOptions();
                break;
        }
    }

    void NewGame()
    {
        if (LevelProgressionManager.Instance != null)
            LevelProgressionManager.Instance.ResetProgression();

        LoadingScreenManager.TargetSceneIndex = 18;
        SceneManager.LoadScene(16);
    }

    void Continue()
    {
        LoadingScreenManager.TargetSceneIndex = 18;
        SceneManager.LoadScene(16);
    }

    void OpenOptions()
    {
        Debug.Log("Options - a implementer");
    }

    void UpdateVisuals()
    {
        for (int i = 0; i < menuTexts.Count; i++)
        {
            if (menuTexts[i] == null) continue;

            Color targetColor;
            if (i == 1 && !hasSave)
                targetColor = lockedColor;
            else
                targetColor = (i == currentSelection) ? selectedColor : normalColor;

            menuTexts[i].color = Color.Lerp(menuTexts[i].color, targetColor, Time.unscaledDeltaTime * transitionSpeed);

            Vector3 targetScale = (i == currentSelection && !(i == 1 && !hasSave))
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