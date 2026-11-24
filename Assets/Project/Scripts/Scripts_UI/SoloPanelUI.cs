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

    [Header("Choix de difficulte (apres New Game)")]
    [Tooltip("Lignes affichees a la place du menu apres New Game, dans l'ordre Easy / Normal / Hard. " +
             "Textes remplis par le code. Vides = New Game lance directement avec la difficulte deja enregistree.")]
    [SerializeField] private TextMeshProUGUI easyText;
    [SerializeField] private TextMeshProUGUI normalText;
    [SerializeField] private TextMeshProUGUI hardText;
    [Tooltip("Optionnel : legende de la difficulte en surbrillance (cles difficulty.easy/normal/hard.desc). " +
             "Visible seulement pendant le choix de difficulte.")]
    [SerializeField] private TextMeshProUGUI difficultyLegend;
    private static readonly string[] legendKeys = { "difficulty.easy.desc", "difficulty.normal.desc", "difficulty.hard.desc" };
    private int legendShown = -1;

    [Header("Audio")]
    [SerializeField] private UIAudioPlayer uiAudio;

    [Header("Visual Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private float selectedScale = 1.2f;
    [SerializeField] private float transitionSpeed = 10f;

    private List<TextMeshProUGUI> menuTexts = new List<TextMeshProUGUI>();
    // Easy / Normal / Hard, meme ordre que OptionsManager.Difficulty (index = valeur de l'enum)
    private List<TextMeshProUGUI> difficultyTexts = new List<TextMeshProUGUI>();
    private bool choosingDifficulty = false;
    private List<TextMeshProUGUI> CurrentTexts => choosingDifficulty ? difficultyTexts : menuTexts;
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
            // Masquee : le Vertical Layout Group du conteneur Lignes fait remonter Options
            continueText.gameObject.SetActive(false);
            menuTexts.Add(newGameText);
            menuTexts.Add(optionsText);
        }
        else
        {
            menuTexts.Add(newGameText);
            menuTexts.Add(continueText);
            menuTexts.Add(optionsText);
        }

        if (easyText != null && normalText != null && hardText != null)
        {
            difficultyTexts.Add(easyText);
            difficultyTexts.Add(normalText);
            difficultyTexts.Add(hardText);
        }
        ShowDifficultyChoice(false);

        if (soloPanel != null)
            soloPanel.SetActive(false);
    }

    void Update()
    {
        if (!isOpen) return;

        // Options ouvertes par-dessus : lignes masquees (sinon visibles a travers un fond d'options
        // transparent). Seul l'affichage est coupe : le Layout Group ne bouge pas.
        bool optionsOpen = OptionsOpen;
        foreach (TextMeshProUGUI t in CurrentTexts)
            if (t != null) t.enabled = !optionsOpen;
        if (optionsOpen) return;

        if (navigationCooldown > 0f)
        {
            navigationCooldown -= Time.unscaledDeltaTime;
        }
        else
        {
            HandleNavigation();
        }

        UpdateVisuals();
        UpdateLegend();
    }

    // Texte de la legende change seulement quand la selection change
    void UpdateLegend()
    {
        if (difficultyLegend == null || !choosingDifficulty) return;
        if (currentSelection == legendShown || currentSelection >= legendKeys.Length) return;
        legendShown = currentSelection;
        difficultyLegend.text = Loc.Get(legendKeys[currentSelection]);
    }

    void HandleNavigation()
    {
        Vector2 input = PlayerInputManager.Instance.MoveInput;

        int count = CurrentTexts.Count;

        // Choix de difficulte : lignes cote a cote, navigation gauche / droite
        if (choosingDifficulty)
        {
            if (input.x < -0.5f)
            {
                currentSelection = (currentSelection - 1 + count) % count;
                navigationCooldown = cooldownDuration;
                if (uiAudio != null) uiAudio.PlayLeft();
            }
            else if (input.x > 0.5f)
            {
                currentSelection = (currentSelection + 1) % count;
                navigationCooldown = cooldownDuration;
                if (uiAudio != null) uiAudio.PlayRight();
            }
        }
        else if (input.y > 0.5f)
        {
            currentSelection = (currentSelection - 1 + count) % count;
            navigationCooldown = cooldownDuration;
            if (uiAudio != null) uiAudio.PlayUp();
        }
        else if (input.y < -0.5f)
        {
            currentSelection = (currentSelection + 1) % count;
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
            if (choosingDifficulty)
            {
                ShowDifficultyChoice(false);
                currentSelection = 0;
            }
            else
            {
                Close();
            }
        }
    }

    void SelectCurrentOption()
    {
        if (choosingDifficulty)
        {
            StartNewGame((OptionsManager.Difficulty)currentSelection);
            return;
        }

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

    // New Game ouvre le choix de difficulte (sur place, a la place des lignes du menu) ;
    // la partie ne demarre qu'a la validation. Lignes non branchees : demarrage direct.
    void NewGame()
    {
        if (difficultyTexts.Count == 0)
        {
            Debug.LogWarning("[SoloPanelUI] Lignes Easy / Normal / Hard non branchees : New Game sans choix de difficulte.");
            StartNewGame(OptionsManager.Instance != null ? OptionsManager.Instance.difficulty : OptionsManager.Difficulty.Superbanco);
            return;
        }

        ShowDifficultyChoice(true);
        currentSelection = (int)OptionsManager.Difficulty.Superbanco;
    }

    void ShowDifficultyChoice(bool show)
    {
        choosingDifficulty = show && difficultyTexts.Count > 0;

        foreach (TextMeshProUGUI t in menuTexts)
            if (t != null) t.gameObject.SetActive(!choosingDifficulty);

        string[] keys = { "options.easy", "options.normal", "options.hard" };
        for (int i = 0; i < difficultyTexts.Count; i++)
        {
            difficultyTexts[i].gameObject.SetActive(choosingDifficulty);
            if (choosingDifficulty) difficultyTexts[i].text = Loc.Get(keys[i]);
        }

        if (difficultyLegend != null) difficultyLegend.gameObject.SetActive(choosingDifficulty);
        legendShown = -1;
    }

    // Difficulte figee dans la sauvegarde : seul endroit ou elle s'ecrit
    void StartNewGame(OptionsManager.Difficulty difficulty)
    {
        if (OptionsManager.Instance != null)
            OptionsManager.Instance.SetDifficulty(difficulty);

        if (uiAudio != null) uiAudio.PlayNewGameAndSurvive();

        if (LevelProgressionManager.Instance != null)
            LevelProgressionManager.Instance.ResetProgression();

        LoadingScreenManager.TargetSceneIndex = LevelProgressionManager.Instance.levels[0].sceneIndex;
        SceneManager.LoadScene(1);
    }

    // Continuer -> ecran LevelSelect (on voit ou on en est et on choisit), comme LevelManager.LoadLevelSelect :
    // ecran de chargement puis LevelSelect (index 2). Avant le 2026-11-23 : lancait le dernier niveau debloque.
    void Continue()
    {
        LoadingScreenManager.TargetSceneIndex = 2;
        SceneManager.LoadScene(1);
    }

    void OpenOptions()
    {
        if (optionsPanelUI != null)
            optionsPanelUI.Open();
    }

    void UpdateVisuals()
    {
        List<TextMeshProUGUI> texts = CurrentTexts;
        for (int i = 0; i < texts.Count; i++)
        {
            if (texts[i] == null) continue;

            Color targetColor = (i == currentSelection) ? selectedColor : normalColor;
            texts[i].color = Color.Lerp(texts[i].color, targetColor, Time.unscaledDeltaTime * transitionSpeed);

            Vector3 targetScale = (i == currentSelection) ? normalScale * selectedScale : normalScale;
            texts[i].transform.localScale = Vector3.Lerp(texts[i].transform.localScale, targetScale, Time.unscaledDeltaTime * transitionSpeed);
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
        ShowDifficultyChoice(false);
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

    // Lu par MainMenuUI pour masquer aussi le menu racine sous les options
    public bool OptionsOpen => optionsPanelUI != null && optionsPanelUI.IsOpen();
}
