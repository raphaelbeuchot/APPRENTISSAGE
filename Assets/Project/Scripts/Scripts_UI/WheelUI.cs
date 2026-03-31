using UnityEngine;
using TMPro;
using System.Collections;

public class WheelUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI spinPromptText;
    [SerializeField] private TextMeshProUGUI continueText;
    [SerializeField] private TextMeshProUGUI tryAgainText;

    [Header("Couleurs")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color highlightColor = Color.yellow;
    [SerializeField] private Color resultColor = Color.green;
    [SerializeField] private Color eliminatedColor = Color.gray;

    [Header("Spin Config")]
    [SerializeField] private float fastPhaseDuration = 3f;
    [SerializeField] private float fastPhaseInterval = 0.25f;
  

    [Header("Blink Config")]

    [Header("Try Again Cost")]
    [SerializeField] private int tryAgainCost = 3;

    [Header("Audio")]
    [SerializeField] private AudioClip jingleClip;
    private AudioSource audioSource;

    private WheelCell[] cells;

    private enum WheelState { Idle, FastPhase, Decelerating, Result }
    private WheelState state = WheelState.Idle;

    private int currentHighlightIndex = 0;
    private int resultIndex = -1;

    private int selectedButton = 0; // 0 = continue, 1 = tryagain
    private float navigationCooldown = 0f;
    private float cooldownDuration = 0.3f;

    private Coroutine blinkPromptCoroutine;
    private Coroutine resultBlinkCoroutine;

    void Start()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        cells = GetComponentsInChildren<WheelCell>();
        SetState(WheelState.Idle);
    }

    void Update()
    {
        if (PlayerInputManager.Instance == null) return;

        if (navigationCooldown > 0f)
            navigationCooldown -= Time.deltaTime;

        if (state == WheelState.Idle)
        {
            bool confirmPressed = PlayerInputManager.Instance.InteractPressed
                || Input.GetButtonDown("Submit");
            if (confirmPressed)
                StartCoroutine(SpinCoroutine());
        }
        else if (state == WheelState.Result)
        {
            HandleResultNavigation();
        }
    }

    void HandleResultNavigation()
    {
        if (navigationCooldown > 0f) return;

        Vector2 input = PlayerInputManager.Instance.MoveInput;

        if (input.y < -0.5f)
        {
            selectedButton = (selectedButton + 1) % 2;
            UpdateButtonVisuals();
            navigationCooldown = cooldownDuration;
        }
        else if (input.y > 0.5f)
        {
            selectedButton = (selectedButton + 1) % 2;
            UpdateButtonVisuals();
            navigationCooldown = cooldownDuration;
        }

        bool confirmPressed = PlayerInputManager.Instance.InteractPressed
            || Input.GetButtonDown("Submit");

        if (confirmPressed)
        {
            if (selectedButton == 0)
                OnContinuePressed();
            else
                OnTryAgainPressed();
        }
    }

    void UpdateButtonVisuals()
    {
        if (continueText != null)
            continueText.color = selectedButton == 1 ? highlightColor : normalColor;
        if (tryAgainText != null)
            tryAgainText.color = selectedButton == 0 ? highlightColor : normalColor;
    }

    void SetState(WheelState newState)
    {
        state = newState;

        if (newState == WheelState.Idle)
        {
            foreach (var cell in cells)
                if (cell != null) { cell.SetColor(normalColor); cell.SetVisible(true); }

            if (spinPromptText != null) spinPromptText.gameObject.SetActive(true);
            if (continueText != null) continueText.gameObject.SetActive(false);
            if (tryAgainText != null) tryAgainText.gameObject.SetActive(false);

            
        }
        else if (newState == WheelState.FastPhase)
        {
            if (blinkPromptCoroutine != null) { StopCoroutine(blinkPromptCoroutine); blinkPromptCoroutine = null; }
            if (spinPromptText != null) { spinPromptText.gameObject.SetActive(true); spinPromptText.enabled = true; }
            if (continueText != null) continueText.gameObject.SetActive(false);
            if (tryAgainText != null) tryAgainText.gameObject.SetActive(false);
        }
        else if (newState == WheelState.Result)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] == null) continue;
                if (i == resultIndex)
                {
                    cells[i].SetColor(resultColor);
                    cells[i].SetVisible(true);
                    
                }
                else
                {
                    cells[i].SetColor(eliminatedColor);
                }
            }

            if (jingleClip != null && audioSource != null)
                audioSource.PlayOneShot(jingleClip);

            if (resultIndex >= 0 && resultIndex < cells.Length && cells[resultIndex] != null)
            {
                if (LevelProgressionManager.Instance != null)
                    LevelProgressionManager.Instance.SetActiveModifier(cells[resultIndex].modifierType);

                Debug.Log($"[WheelUI] Resultat : {cells[resultIndex].GetLabel()} ({cells[resultIndex].modifierType})");
            }

            selectedButton = 0;
            if (continueText != null) { continueText.gameObject.SetActive(true); continueText.color = highlightColor; }
            if (tryAgainText != null) { tryAgainText.gameObject.SetActive(true); tryAgainText.color = normalColor; tryAgainText.text = $"Try Again ({tryAgainCost} credits)"; }
        }
    }

    IEnumerator SpinCoroutine()
    {
        resultIndex = Random.Range(0, cells.Length);

        SetState(WheelState.FastPhase);
        float elapsed = 0f;
        while (elapsed < fastPhaseDuration)
        {
            int randomIndex;
            do { randomIndex = Random.Range(0, cells.Length); }
            while (randomIndex == currentHighlightIndex);
            SetHighlight(randomIndex);
            yield return new WaitForSeconds(fastPhaseInterval);
            elapsed += fastPhaseInterval;
        }

        SetHighlight(resultIndex);
        yield return new WaitForSeconds(0.3f);
        SetState(WheelState.Result);
    }

    void SetHighlight(int index)
    {
        if (currentHighlightIndex >= 0 && currentHighlightIndex < cells.Length && cells[currentHighlightIndex] != null)
            cells[currentHighlightIndex].SetColor(normalColor);

        currentHighlightIndex = index;

        if (cells[currentHighlightIndex] != null)
            cells[currentHighlightIndex].SetColor(highlightColor);
    }

   

    void OnContinuePressed()
    {
        if (resultBlinkCoroutine != null) StopCoroutine(resultBlinkCoroutine);
        if (SceneFader.Instance != null)
            SceneFader.Instance.FadeToScene(2);
    }

    void OnTryAgainPressed()
    {
        if (resultBlinkCoroutine != null) { StopCoroutine(resultBlinkCoroutine); resultBlinkCoroutine = null; }

        foreach (var cell in cells)
            if (cell != null) { cell.SetColor(normalColor); cell.SetVisible(true); }

        StartCoroutine(SpinCoroutine());
    }
}