using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Panneau de fin de demo (EndChoicesPanel, boss Rush Hour IV) : 3 options, navigation comme MainMenuUI
/// (stick haut / bas, Submit, texte selectionne en couleur et plus gros, sons UIAudioPlayer).
/// Cache au lancement ; affiche par BossEndRun a la fin du fondu au noir (Show).
/// Options : 0 = Rejouer un niveau (LevelSelect), 1 = Menu principal, 2 = Quitter.
/// </summary>
public class DemoEndPanel : MonoBehaviour
{
    [Header("Options (dans l'ordre)")]
    [SerializeField] private TextMeshProUGUI replayText;
    [SerializeField] private TextMeshProUGUI mainMenuText;
    [SerializeField] private TextMeshProUGUI quitText;

    [Header("Audio")]
    [SerializeField] private UIAudioPlayer uiAudio;

    [Header("Visuel")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private float selectedScale = 1.2f;
    [SerializeField] private float transitionSpeed = 10f;

    private readonly List<TextMeshProUGUI> options = new List<TextMeshProUGUI>();
    private int current;
    private float cooldown;
    private const float CooldownDuration = 0.2f;
    private bool chosen;
    private bool shown;

    private void Awake()
    {
        options.Add(replayText);
        options.Add(mainMenuText);
        options.Add(quitText);
        if (uiAudio == null) uiAudio = FindObjectOfType<UIAudioPlayer>();
        // Panneau laisse actif dans la scene : cache au lancement. S'il etait decoche, Awake ne passe
        // qu'au Show -> ne pas le recacher.
        if (!shown) gameObject.SetActive(false);
    }

    public void Show()
    {
        shown = true;
        current = 0;
        cooldown = CooldownDuration; // l'appui qui a pu rester enfonce ne valide pas tout de suite
        gameObject.SetActive(true);
    }

    private void Update()
    {
        UpdateVisuals();
        if (chosen) return;

        if (cooldown > 0f)
        {
            cooldown -= Time.unscaledDeltaTime;
            return;
        }

        Vector2 input = PlayerInputManager.Instance != null ? PlayerInputManager.Instance.MoveInput : Vector2.zero;
        if (input.y > 0.5f)
        {
            current = (current - 1 + options.Count) % options.Count;
            cooldown = CooldownDuration;
            if (uiAudio != null) uiAudio.PlayUp();
        }
        else if (input.y < -0.5f)
        {
            current = (current + 1) % options.Count;
            cooldown = CooldownDuration;
            if (uiAudio != null) uiAudio.PlayDown();
        }

        if (Input.GetButtonDown("Submit"))
        {
            if (uiAudio != null) uiAudio.PlayA();
            Choose();
        }
    }

    private void Choose()
    {
        chosen = true;
        Time.timeScale = 1f;
        if (PlayerInputManager.Instance != null) PlayerInputManager.Instance.IsLocked = false;

        switch (current)
        {
            case 0: // comme LevelManager.LoadLevelSelect : ecran de chargement puis LevelSelect
                LoadingScreenManager.TargetSceneIndex = 2;
                SceneManager.LoadScene(1);
                break;
            case 1:
                SceneManager.LoadScene(0);
                break;
            case 2:
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                break;
        }
    }

    private void UpdateVisuals()
    {
        for (int i = 0; i < options.Count; i++)
        {
            if (options[i] == null) continue;
            bool sel = i == current;
            options[i].color = Color.Lerp(options[i].color, sel ? selectedColor : normalColor,
                Time.unscaledDeltaTime * transitionSpeed);
            Vector3 target = sel ? Vector3.one * selectedScale : Vector3.one;
            options[i].transform.localScale = Vector3.Lerp(options[i].transform.localScale, target,
                Time.unscaledDeltaTime * transitionSpeed);
        }
    }
}
