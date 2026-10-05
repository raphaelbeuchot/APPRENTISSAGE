using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

// Ecran de lancement multi (apres "Multi" depuis le menu principal) : choix du mode (Race/Coin
// Race), puis des parametres propres a ce mode (nombre de manches ou duree du chrono), puis du
// niveau. Meme pattern de navigation que MainMenuUI/LevelSelectUI (PlayerInputManager.MoveInput,
// Input.GetButtonDown("Submit")/"Cancel"). Une seule scene, etapes internes (pas de rechargement
// de scene entre elles) : le retour entre etapes est instantane.
public class MultiStartUI : MonoBehaviour
{
    private enum Step { Mode, Count, CustomEdit, Level, Duration, DurationCustomEdit, CoinLevel }
    private enum GameMode { Race, CoinRace }

    [System.Serializable]
    private class ModeEntry
    {
        public TextMeshProUGUI label;
        public GameMode mode;
    }

    [Header("Etape 0 : mode (navigation verticale)")]
    [SerializeField] private GameObject modeRoot;
    [SerializeField] private List<ModeEntry> modeEntries;

    [Header("Etape 1 (Race) : nombre de manches (navigation verticale)")]
    [Tooltip("Parent des 3 lignes, actif uniquement pendant cette etape.")]
    [SerializeField] private GameObject countRoot;
    [Tooltip("3 lignes dans l'ordre : First to 5, First to 10, Custom. Le texte est ecrit par le script ; la ligne Custom affiche \"< First to X >\" pendant le reglage.")]
    [SerializeField] private List<TextMeshProUGUI> countEntries;
    [SerializeField] private int minCustom = 1;
    [SerializeField] private int maxCustom = 20;
    [SerializeField] private int defaultCustom = 10;

    [System.Serializable]
    private class LevelEntry
    {
        public TextMeshProUGUI label;
        public string sceneName;
    }

    [Header("Etape 2 (Race) : niveau (navigation verticale)")]
    [SerializeField] private GameObject levelRoot;
    [SerializeField] private List<LevelEntry> levelEntries;

    [Header("Etape 1 (Coin Race) : duree du chrono (navigation verticale)")]
    [Tooltip("Parent des 4 lignes, actif uniquement pendant cette etape.")]
    [SerializeField] private GameObject durationRoot;
    [Tooltip("4 lignes dans l'ordre : 1 min, 3 min, 5 min, Custom. Le texte est ecrit par le script ; la ligne Custom affiche \"< X min >\" pendant le reglage.")]
    [SerializeField] private List<TextMeshProUGUI> durationEntries;
    [SerializeField] private int minCustomDurationMinutes = 1;
    [SerializeField] private int maxCustomDurationMinutes = 20;
    [SerializeField] private int defaultCustomDurationMinutes = 3;

    [Header("Etape 2 (Coin Race) : niveau (navigation verticale)")]
    [SerializeField] private GameObject coinLevelRoot;
    [SerializeField] private List<LevelEntry> coinLevelEntries;

    [Header("Audio (optionnel, meme pattern que LevelSelectUI)")]
    [SerializeField] private UIAudioPlayer uiAudio;

    [Header("Visuel")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private float selectedScale = 1.2f;
    [SerializeField] private float transitionSpeed = 10f;

    private Step currentStep = Step.Mode;
    private GameMode chosenMode = GameMode.Race;
    private int modeSelection = 0;
    private int countSelection = 0; // 0 = 5, 1 = 10, 2 = Custom
    private int customValue;
    private int chosenTarget = 10;
    private int levelSelection = 0;
    private int durationSelection = 0; // 0 = 1 min, 1 = 3 min, 2 = 5 min, 3 = Custom
    private int customDurationMinutes;
    private int chosenDurationSeconds = 180;
    private int coinLevelSelection = 0;

    private float navigationCooldown = 0f;
    private const float cooldownDuration = 0.2f;
    private Vector3 normalScale = Vector3.one;

    void Awake()
    {
        if (uiAudio == null) uiAudio = FindObjectOfType<UIAudioPlayer>();
        customValue = Mathf.Clamp(defaultCustom, minCustom, maxCustom);
        customDurationMinutes = Mathf.Clamp(defaultCustomDurationMinutes, minCustomDurationMinutes, maxCustomDurationMinutes);
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
            case Step.Mode: HandleModeNavigation(); break;
            case Step.Count: HandleCountNavigation(); break;
            case Step.CustomEdit: HandleCustomEditNavigation(); break;
            case Step.Level: HandleLevelNavigation(); break;
            case Step.Duration: HandleDurationNavigation(); break;
            case Step.DurationCustomEdit: HandleDurationCustomEditNavigation(); break;
            case Step.CoinLevel: HandleCoinLevelNavigation(); break;
        }
    }

    private void HandleModeNavigation()
    {
        if (modeEntries == null || modeEntries.Count == 0) return;

        Vector2 input = PlayerInputManager.Instance.MoveInput;

        if (input.y > 0.5f)
        {
            modeSelection = (modeSelection - 1 + modeEntries.Count) % modeEntries.Count;
            navigationCooldown = cooldownDuration;
            if (uiAudio != null) uiAudio.PlayUp();
        }
        else if (input.y < -0.5f)
        {
            modeSelection = (modeSelection + 1) % modeEntries.Count;
            navigationCooldown = cooldownDuration;
            if (uiAudio != null) uiAudio.PlayDown();
        }

        if (AnyFaceButtonPressed())
        {
            if (uiAudio != null) uiAudio.PlayA();
            navigationCooldown = cooldownDuration;

            chosenMode = modeEntries[modeSelection].mode;
            currentStep = chosenMode == GameMode.Race ? Step.Count : Step.Duration;
        }

        if (Input.GetButtonDown("Cancel"))
        {
            if (uiAudio != null) uiAudio.PlayB();
            SceneManager.LoadScene("MainMenu");
        }
    }

    private void HandleCountNavigation()
    {
        Vector2 input = PlayerInputManager.Instance.MoveInput;

        if (input.y > 0.5f)
        {
            countSelection = (countSelection - 1 + 3) % 3;
            navigationCooldown = cooldownDuration;
            if (uiAudio != null) uiAudio.PlayUp();
        }
        else if (input.y < -0.5f)
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
            currentStep = Step.Mode;
            navigationCooldown = cooldownDuration;
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

    private void HandleDurationNavigation()
    {
        Vector2 input = PlayerInputManager.Instance.MoveInput;

        if (input.y > 0.5f)
        {
            durationSelection = (durationSelection - 1 + 4) % 4;
            navigationCooldown = cooldownDuration;
            if (uiAudio != null) uiAudio.PlayUp();
        }
        else if (input.y < -0.5f)
        {
            durationSelection = (durationSelection + 1) % 4;
            navigationCooldown = cooldownDuration;
            if (uiAudio != null) uiAudio.PlayDown();
        }

        if (AnyFaceButtonPressed())
        {
            if (uiAudio != null) uiAudio.PlayA();
            navigationCooldown = cooldownDuration;

            if (durationSelection == 0) { chosenDurationSeconds = 60; currentStep = Step.CoinLevel; }
            else if (durationSelection == 1) { chosenDurationSeconds = 180; currentStep = Step.CoinLevel; }
            else if (durationSelection == 2) { chosenDurationSeconds = 300; currentStep = Step.CoinLevel; }
            else { currentStep = Step.DurationCustomEdit; }
        }

        if (Input.GetButtonDown("Cancel"))
        {
            if (uiAudio != null) uiAudio.PlayB();
            currentStep = Step.Mode;
            navigationCooldown = cooldownDuration;
        }
    }

    private void HandleDurationCustomEditNavigation()
    {
        Vector2 input = PlayerInputManager.Instance.MoveInput;

        if (input.x < -0.5f)
        {
            customDurationMinutes = Mathf.Max(minCustomDurationMinutes, customDurationMinutes - 1);
            navigationCooldown = cooldownDuration;
            if (uiAudio != null) uiAudio.PlayUp();
        }
        else if (input.x > 0.5f)
        {
            customDurationMinutes = Mathf.Min(maxCustomDurationMinutes, customDurationMinutes + 1);
            navigationCooldown = cooldownDuration;
            if (uiAudio != null) uiAudio.PlayDown();
        }

        if (AnyFaceButtonPressed())
        {
            if (uiAudio != null) uiAudio.PlayA();
            chosenDurationSeconds = customDurationMinutes * 60;
            currentStep = Step.CoinLevel;
            navigationCooldown = cooldownDuration;
        }

        if (Input.GetButtonDown("Cancel"))
        {
            if (uiAudio != null) uiAudio.PlayB();
            currentStep = Step.Duration;
            navigationCooldown = cooldownDuration;
        }
    }

    private void HandleCoinLevelNavigation()
    {
        if (coinLevelEntries == null || coinLevelEntries.Count == 0) return;

        Vector2 input = PlayerInputManager.Instance.MoveInput;

        if (input.y > 0.5f)
        {
            coinLevelSelection = (coinLevelSelection - 1 + coinLevelEntries.Count) % coinLevelEntries.Count;
            navigationCooldown = cooldownDuration;
            if (uiAudio != null) uiAudio.PlayUp();
        }
        else if (input.y < -0.5f)
        {
            coinLevelSelection = (coinLevelSelection + 1) % coinLevelEntries.Count;
            navigationCooldown = cooldownDuration;
            if (uiAudio != null) uiAudio.PlayDown();
        }

        if (AnyFaceButtonPressed())
        {
            if (uiAudio != null) uiAudio.PlayLevelSelectedAndSurvive();
            LaunchCoinLevel(coinLevelEntries[coinLevelSelection].sceneName);
        }

        if (Input.GetButtonDown("Cancel"))
        {
            if (uiAudio != null) uiAudio.PlayB();
            currentStep = Step.Duration;
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
        LoadLevelScene(sceneName);
    }

    private void LaunchCoinLevel(string sceneName)
    {
        MultiCoinRaceManager.RoundDurationOverride = chosenDurationSeconds;
        LoadLevelScene(sceneName);
    }

    private void LoadLevelScene(string sceneName)
    {
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
        bool modeStep = currentStep == Step.Mode;
        bool countStep = currentStep == Step.Count || currentStep == Step.CustomEdit;
        bool levelStep = currentStep == Step.Level;
        bool durationStep = currentStep == Step.Duration || currentStep == Step.DurationCustomEdit;
        bool coinLevelStep = currentStep == Step.CoinLevel;

        if (modeRoot != null) modeRoot.SetActive(modeStep);

        if (modeEntries != null)
        {
            for (int i = 0; i < modeEntries.Count; i++)
                SetTextVisual(modeEntries[i].label, modeStep && i == modeSelection);
        }

        if (countRoot != null) countRoot.SetActive(countStep);

        if (countEntries != null)
        {
            for (int i = 0; i < countEntries.Count; i++)
            {
                if (countEntries[i] == null) continue;
                countEntries[i].text = BuildCountEntryText(i);
                SetTextVisual(countEntries[i], countStep && i == countSelection);
            }
        }

        if (levelRoot != null) levelRoot.SetActive(levelStep);

        if (levelEntries != null)
        {
            for (int i = 0; i < levelEntries.Count; i++)
                SetTextVisual(levelEntries[i].label, levelStep && i == levelSelection);
        }

        if (durationRoot != null) durationRoot.SetActive(durationStep);

        if (durationEntries != null)
        {
            for (int i = 0; i < durationEntries.Count; i++)
            {
                if (durationEntries[i] == null) continue;
                durationEntries[i].text = BuildDurationEntryText(i);
                SetTextVisual(durationEntries[i], durationStep && i == durationSelection);
            }
        }

        if (coinLevelRoot != null) coinLevelRoot.SetActive(coinLevelStep);

        if (coinLevelEntries != null)
        {
            for (int i = 0; i < coinLevelEntries.Count; i++)
                SetTextVisual(coinLevelEntries[i].label, coinLevelStep && i == coinLevelSelection);
        }
    }

    private string BuildCountEntryText(int index)
    {
        if (index == 0) return "First to 5";
        if (index == 1) return "First to 10";
        if (currentStep == Step.CustomEdit)
            return $"< First to {customValue} >";
        return "Custom";
    }

    private string BuildDurationEntryText(int index)
    {
        if (index == 0) return "1 min";
        if (index == 1) return "3 min";
        if (index == 2) return "5 min";
        if (currentStep == Step.DurationCustomEdit)
            return $"< {customDurationMinutes} min >";
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
