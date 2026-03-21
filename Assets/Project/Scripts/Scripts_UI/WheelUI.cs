using UnityEngine;
using TMPro;
using System.Collections;

public class WheelUI : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private TextMeshProUGUI slotDisplayText;
    [SerializeField] private TextMeshProUGUI resultText;

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI launchText;
    [SerializeField] private TextMeshProUGUI stopText;
    [SerializeField] private TextMeshProUGUI continueText;

    [Header("Config")]
    [SerializeField] private float maxCyclesPerSecond = 10f;
    [SerializeField] private float decelerationDuration = 2f;

    private enum WheelState { Idle, Spinning, Decelerating, Result }
    private WheelState state = WheelState.Idle;

    private int resultIndex = -1;
    private int currentDisplayIndex = 0;
    private float timeSinceLastSlotChange = 0f;
    private float currentCyclesPerSecond = 0f;

    private string[] slotNames = new string[]
    {
        "+ Vie 50%",
        "+ Vie 50%",
        "+ Spray 50%",
        "+ Knockback Broom 50%",
        "+ Endurance 50%",
        "+ Furtivite",
        "- Vie 50%",
        "- Spray 50%",
        "- Endurance 50%",
        "+ Vie Ennemis 50%",
        "Rien",
        "Rien"
    };

    void Start()
    {
        SetState(WheelState.Idle);
    }

    void Update()
    {
        if (PlayerInputManager.Instance == null) return;

        bool confirmPressed = PlayerInputManager.Instance.InteractPressed
            || Input.GetButtonDown("Submit");

        if (state == WheelState.Idle && confirmPressed)
            OnLaunchPressed();
        else if (state == WheelState.Spinning && confirmPressed)
            OnStopPressed();
        else if (state == WheelState.Result && confirmPressed)
            OnContinuePressed();

        if (state == WheelState.Spinning || state == WheelState.Decelerating)
        {
            timeSinceLastSlotChange += Time.deltaTime;
            float interval = currentCyclesPerSecond > 0f ? 1f / currentCyclesPerSecond : 999f;

            if (timeSinceLastSlotChange >= interval)
            {
                timeSinceLastSlotChange = 0f;

                if (state == WheelState.Spinning)
                {
                    currentDisplayIndex = (currentDisplayIndex + 1) % slotNames.Length;
                }

                if (slotDisplayText != null)
                    slotDisplayText.text = slotNames[currentDisplayIndex];
            }
        }
    }

    void SetState(WheelState newState)
    {
        state = newState;

        if (launchText != null) launchText.gameObject.SetActive(newState == WheelState.Idle);
        if (stopText != null) stopText.gameObject.SetActive(newState == WheelState.Spinning);
        if (continueText != null) continueText.gameObject.SetActive(newState == WheelState.Result);
        if (slotDisplayText != null) slotDisplayText.gameObject.SetActive(newState == WheelState.Spinning || newState == WheelState.Decelerating);
        if (resultText != null) resultText.gameObject.SetActive(newState == WheelState.Result);
    }

    void OnLaunchPressed()
    {
        if (state != WheelState.Idle) return;
        currentCyclesPerSecond = maxCyclesPerSecond;
        currentDisplayIndex = 0;
        timeSinceLastSlotChange = 0f;
        SetState(WheelState.Spinning);
    }

    void OnStopPressed()
    {
        if (state != WheelState.Spinning) return;
        resultIndex = Random.Range(0, slotNames.Length);
        SetState(WheelState.Decelerating);
        StartCoroutine(DecelerateCoroutine());
    }

    IEnumerator DecelerateCoroutine()
    {
        float elapsed = 0f;
        float startSpeed = currentCyclesPerSecond;

        // Compter combien de slots restent avant le resultat
        // On s'assure de faire au moins un tour complet
        int slotsToResult = slotNames.Length;
        int currentPos = currentDisplayIndex;
        while (currentPos != resultIndex || slotsToResult < slotNames.Length)
        {
            currentPos = (currentPos + 1) % slotNames.Length;
            slotsToResult++;
            if (slotsToResult > slotNames.Length * 3) break; // securite max 3 tours
        }

        float totalTime = decelerationDuration;
        float timePerSlot = totalTime / slotsToResult;

        for (int i = 0; i < slotsToResult; i++)
        {
            float progress = (float)i / slotsToResult;
            float currentInterval = Mathf.Lerp(1f / startSpeed, timePerSlot * 3f, progress);

            currentDisplayIndex = (currentDisplayIndex + 1) % slotNames.Length;
            if (slotDisplayText != null)
                slotDisplayText.text = slotNames[currentDisplayIndex];

            yield return new WaitForSeconds(currentInterval);
        }

        // Forcer le resultat final
        currentDisplayIndex = resultIndex;
        if (slotDisplayText != null)
            slotDisplayText.text = slotNames[currentDisplayIndex];

        currentCyclesPerSecond = 0f;
        ShowResult();
    }

    void ShowResult()
    {
        SetState(WheelState.Result);

        if (resultText != null)
            resultText.text = slotNames[resultIndex];

        // Stocker le modifier
        if (LevelProgressionManager.Instance != null)
            LevelProgressionManager.Instance.SetActiveModifier((ModifierType)resultIndex);

        Debug.Log($"[WheelUI] Resultat : {slotNames[resultIndex]} (index {resultIndex})");
    }

    void OnContinuePressed()
    {
        if (state != WheelState.Result) return;
        if (SceneFader.Instance != null)
            SceneFader.Instance.FadeToScene(2);
    }
}