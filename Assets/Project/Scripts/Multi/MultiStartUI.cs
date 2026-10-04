using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

// Ecran de lancement multi (apres "Multi" depuis le menu principal) : choix du nombre de
// manches ("First to X") puis du niveau. Meme pattern de navigation que MainMenuUI/LevelSelectUI
// (PlayerInputManager.MoveInput, Input.GetButtonDown("Submit")/"Cancel"), juste horizontal pour
// le choix du nombre plutot que vertical. Une seule scene, trois etapes internes (pas de
// rechargement de scene entre elles) : le retour entre etapes est instantane.
public class MultiStartUI : MonoBehaviour
{
    private enum Step { Count, CustomEdit, Level }

    [Header("Etape 1 : nombre de manches (navigation horizontale)")]
    [Tooltip("Une seule ligne \"First to X\" : le contenu change en te deplacant horizontalement (5 -> 10 -> Custom -> 5...), pas trois textes separes.")]
    [SerializeField] private TextMeshProUGUI countDisplayText;
    [SerializeField] private int minCustom = 1;
    [SerializeField] private int maxCustom = 20;
    [SerializeField] private int defaultCustom = 10;

    [System.Serializable]
    private class LevelEntry
    {
        public TextMeshProUGUI label;
        public string sceneName;
    }

    [Header("Etape 2 : niveau (navigation verticale)")]
    [SerializeField] private GameObject levelRoot;
    [SerializeField] private List<LevelEntry> levelEntries;

    [Header("Audio (optionnel, meme pattern que LevelSelectUI)")]
    [SerializeField] private UIAudioPlayer uiAudio;

    [Header("Visuel")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private float selectedScale = 1.2f;
    [SerializeField] private float transitionSpeed = 10f;

    private Step currentStep = Step.Count;
    private int countSelection = 0; // 0 = 5, 1 = 10, 2 = Custom
    private int customValue;
    private int chosenTarget = 10;
    private int levelSelection = 0;

    private float navigationCooldown = 0f;
    private const float cooldownDuration = 0.2f;
    private Vector3 normalScale = Vector3.one;

    void Awake()
    {
        if (uiAudio == null) uiAudio = FindObjectOfType<UIAudioPlayer>();
        customValue = Mathf.Clamp(defaultCustom, minCustom, maxCustom);
    }

    void Start()
    {
        Time.timeScale = 1f;
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

        switch (currentStep)
        {
            case Step.Count: HandleCountNavigation(); break;
            case Step.CustomEdit: HandleCustomEditNavigation(); break;
            case Step.Level: HandleLevelNavigation(); break;
        }
    }

    private void HandleCountNavigation()
    {
        Vector2 input = PlayerInputManager.Instance.MoveInput;

        if (input.x < -0.5f)
        {
            countSelection = (countSelection - 1 + 3) % 3;
            navigationCooldown = cooldownDuration;
            if (uiAudio != null) uiAudio.PlayUp();
        }
        else if (input.x > 0.5f)
        {
            countSelection = (countSelection + 1) % 3;
            navigationCooldown = cooldownDuration;
            if (uiAudio != null) uiAudio.PlayDown();
        }

        if (AnyFaceButtonPressed())
        {
            if (uiAudio != null) uiAudio.PlayA();
            navigationCooldown = cooldownDuration;

            if (countSelection == 0) { chosenTarget = 5; currentStep = Step.Level; }
            else if (countSelection == 1) { chosenTarget = 10; currentStep = Step.Level; }
            else { currentStep = Step.CustomEdit; }
        }

        if (Input.GetButtonDown("Cancel"))
        {
            if (uiAudio != null) uiAudio.PlayB();
            SceneManager.LoadScene("MainMenu");
        }
    }

    private void HandleCustomEditNavigation()
    {
        Vector2 input = PlayerInputManager.Instance.MoveInput;

        if (input.x < -0.5f)
        {
            customValue = Mathf.Max(minCustom, customValue - 1);
            navigationCooldown = cooldownDuration;
            if (uiAudio != null) uiAudio.PlayUp();
        }
        else if (input.x > 0.5f)
        {
            customValue = Mathf.Min(maxCustom, customValue + 1);
            navigationCooldown = cooldownDuration;
            if (uiAudio != null) uiAudio.PlayDown();
        }

        if (AnyFaceButtonPressed())
        {
            if (uiAudio != null) uiAudio.PlayA();
            chosenTarget = customValue;
            currentStep = Step.Level;
            navigationCooldown = cooldownDuration;
        }

        if (Input.GetButtonDown("Cancel"))
        {
            if (uiAudio != null) uiAudio.PlayB();
            currentStep = Step.Count;
            navigationCooldown = cooldownDuration;
        }
    }

    private void HandleLevelNavigation()
    {
        if (levelEntries == null || levelEntries.Count == 0) return;

        Vector2 input = PlayerInputManager.Instance.MoveInput;

        if (input.y > 0.5f)
        {
            levelSelection = (levelSelection - 1 + levelEntries.Count) % levelEntries.Count;
            navigationCooldown = cooldownDuration;
            if (uiAudio != null) uiAudio.PlayUp();
        }
        else if (input.y < -0.5f)
        {
            levelSelection = (levelSelection + 1) % levelEntries.Count;
            navigationCooldown = cooldownDuration;
            if (uiAudio != null) uiAudio.PlayDown();
        }

        if (AnyFaceButtonPressed())
        {
            if (uiAudio != null) uiAudio.PlayLevelSelectedAndSurvive();
            LaunchLevel(levelEntries[levelSelection].sceneName);
        }

        if (Input.GetButtonDown("Cancel"))
        {
            if (uiAudio != null) uiAudio.PlayB();
            currentStep = Step.Count;
            navigationCooldown = cooldownDuration;
        }
    }

    // Passe par le LoadingScreen du solo (meme pattern que MainMenuUI/LevelSelectUI) : il ne
    // connait qu'un index de build, pas un nom de scene, donc on resout l'index a la volee
    // plutot que de toucher a ce fichier partage. Si la scene n'est pas dans les Build Settings
    // (oubli), repli sur un chargement direct sans ecran de chargement plutot qu'un crash.
    private void LaunchLevel(string sceneName)
    {
        MultiMatchScore.ResetMatch();
        MultiMatchScore.TargetScoreOverride = chosenTarget;

        int buildIndex = SceneUtility.GetBuildIndexByScenePath($"Assets/Project/Scenes/{sceneName}.unity");
        if (buildIndex >= 0)
        {
            LoadingScreenManager.TargetSceneIndex = buildIndex;
            SceneManager.LoadScene(1);
        }
        else
        {
            Debug.LogWarning($"[MultiStartUI] '{sceneName}' absente des Build Settings, chargement direct sans loading screen.");
            SceneManager.LoadScene(sceneName);
        }
    }

    void UpdateVisuals()
    {
        bool levelStep = currentStep == Step.Level;

        if (countDisplayText != null)
        {
            countDisplayText.gameObject.SetActive(!levelStep);
            countDisplayText.text = BuildCountDisplayText();
        }

        if (levelRoot != null) levelRoot.SetActive(levelStep);

        if (levelEntries != null)
        {
            for (int i = 0; i < levelEntries.Count; i++)
                SetTextVisual(levelEntries[i].label, levelStep && i == levelSelection);
        }
    }

    private string BuildCountDisplayText()
    {
        if (currentStep == Step.CustomEdit)
            return $"First to {customValue}";

        if (countSelection == 0) return "First to 5";
        if (countSelection == 1) return "First to 10";
        return "Custom";
    }

    void SetTextVisual(TextMeshProUGUI txt, bool isSelected)
    {
        if (txt == null) return;
        txt.color = Color.Lerp(txt.color, isSelected ? selectedColor : normalColor, Time.unscaledDeltaTime * transitionSpeed);
        txt.transform.localScale = Vector3.Lerp(txt.transform.localScale, isSelected ? normalScale * selectedScale : normalScale, Time.unscaledDeltaTime * transitionSpeed);
    }

    bool AnyFaceButtonPressed()
    {
        return Input.GetButtonDown("Submit");
    }
}
