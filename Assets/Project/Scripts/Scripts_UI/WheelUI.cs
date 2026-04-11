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
    [SerializeField] private float buttonSelectedScale = 1.2f;
    [SerializeField] private float buttonScaleSpeed = 10f;
    [SerializeField] private Color buttonNormalColor = Color.white;
    [SerializeField] private Color buttonSelectedColor = Color.yellow;

    [Header("Spin Config")]
    [SerializeField] private float fastPhaseInterval = 0.15f;

  

    [Header("Audio")]
    [SerializeField] private AudioClip jingleClip;
    [SerializeField] private AudioClip tickSound;
    [SerializeField] private AudioClip resultSound;
    private AudioSource audioSource;

    private WheelCell[] cells;

    private enum WheelState { Idle, FastPhase, Result }
    private WheelState state = WheelState.Idle;

    private int currentHighlightIndex = 0;
    private int resultIndex = -1;

    private int selectedButton = 0;
    private float navigationCooldown = 0f;
    private float cooldownDuration = 0.3f;

    private Coroutine spinCoroutine;
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

        if (state == WheelState.Result)
        {
            Vector3 selectedScale = Vector3.one * buttonSelectedScale;
            Vector3 normalScale = Vector3.one;

            if (continueText != null)
            {
                Vector3 target = selectedButton == 0 ? selectedScale : normalScale;
                continueText.transform.localScale = Vector3.Lerp(continueText.transform.localScale, target, Time.deltaTime * buttonScaleSpeed);
            }
            if (tryAgainText != null)
            {
                Vector3 target = selectedButton == 1 ? selectedScale : normalScale;
                tryAgainText.transform.localScale = Vector3.Lerp(tryAgainText.transform.localScale, target, Time.deltaTime * buttonScaleSpeed);
            }
        }

        bool confirmPressed = PlayerInputManager.Instance.InteractPressed
            || Input.GetButtonDown("Submit");

        if (state == WheelState.Idle)
        {
            if (confirmPressed)
                spinCoroutine = StartCoroutine(SpinCoroutine());
        }
        else if (state == WheelState.FastPhase)
        {
            if (confirmPressed)
            {
                if (spinCoroutine != null) StopCoroutine(spinCoroutine);
                audioSource.Stop();
                audioSource.loop = false;
                resultIndex = currentHighlightIndex;
                SetHighlight(resultIndex, true);
                StartCoroutine(ShowResultCoroutine());
            }
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

        bool canTryAgain = CleaningCreditManager.Instance != null && CleaningCreditManager.Instance.HasCredits();
        if (input.y < -0.5f || input.y > 0.5f)
        {
            if (canTryAgain)
            {
                selectedButton = (selectedButton + 1) % 2;
                UpdateButtonVisuals();
            }
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
            continueText.color = selectedButton == 0 ? buttonSelectedColor : buttonNormalColor;
        if (tryAgainText != null)
            tryAgainText.color = selectedButton == 1 ? buttonSelectedColor : buttonNormalColor;
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

            if (continueText != null) continueText.transform.localScale = Vector3.one;
            if (tryAgainText != null) tryAgainText.transform.localScale = Vector3.one;

            selectedButton = 0;
            if (continueText != null) { continueText.gameObject.SetActive(true); continueText.color = buttonSelectedColor; }
            bool canTryAgain = CleaningCreditManager.Instance != null && CleaningCreditManager.Instance.HasCredits();
            if (tryAgainText != null)
            {
                tryAgainText.gameObject.SetActive(canTryAgain);
                if (canTryAgain)
                {
                    tryAgainText.color = buttonNormalColor;
                    tryAgainText.text = "Try Again (" + CleaningCreditManager.Instance.GetCredits() + " credit(s))";
                }
            }
        }
    }

    IEnumerator SpinCoroutine()
    {
        SetState(WheelState.FastPhase);
        audioSource.clip = tickSound;
        audioSource.loop = true;
        audioSource.Play();
        while (true)
        {
            int randomIndex;
            do { randomIndex = Random.Range(0, cells.Length); }
            while (randomIndex == currentHighlightIndex);
            SetHighlight(randomIndex);
            yield return new WaitForSeconds(fastPhaseInterval);
        }
    }

    IEnumerator ShowResultCoroutine()
    {
        yield return new WaitForSeconds(fastPhaseInterval);
        SetState(WheelState.Result);
    }

    void SetHighlight(int index, bool isResult = false)
    {
        if (currentHighlightIndex >= 0 && currentHighlightIndex < cells.Length && cells[currentHighlightIndex] != null)
            cells[currentHighlightIndex].SetColor(normalColor);

        currentHighlightIndex = index;

        if (cells[currentHighlightIndex] != null)
            cells[currentHighlightIndex].SetColor(highlightColor);

        if (isResult && audioSource != null)
        {
            if (resultSound != null)
                audioSource.PlayOneShot(resultSound);
        }
    }

    void OnContinuePressed()
    {
        if (resultBlinkCoroutine != null) StopCoroutine(resultBlinkCoroutine);
        if (CleaningCreditManager.Instance != null)
            CleaningCreditManager.Instance.ResetCredits();
        if (SceneFader.Instance != null)
            SceneFader.Instance.FadeToScene(2);
    }

    void OnTryAgainPressed()
    {
        if (CleaningCreditManager.Instance == null || !CleaningCreditManager.Instance.SpendCredit()) return;

        if (resultBlinkCoroutine != null) { StopCoroutine(resultBlinkCoroutine); resultBlinkCoroutine = null; }

        foreach (var cell in cells)
            if (cell != null) { cell.SetColor(normalColor); cell.SetVisible(true); }

        spinCoroutine = StartCoroutine(SpinCoroutine());
    }
}