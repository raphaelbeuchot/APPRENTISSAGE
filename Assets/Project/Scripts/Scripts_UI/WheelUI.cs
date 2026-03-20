using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class WheelUI : MonoBehaviour
{
    [Header("Wheel")]
    [SerializeField] private Transform wheelTransform;
    [SerializeField] private float spinSpeed = 720f;

    [Header("Buttons")]
    [SerializeField] private TextMeshProUGUI launchText;
    [SerializeField] private TextMeshProUGUI stopText;
    [SerializeField] private TextMeshProUGUI continueText;

    [Header("Result")]
    [SerializeField] private TextMeshProUGUI resultText;

    [Header("Config")]
    [SerializeField] private float decelerationDuration = 1.5f;

    private enum WheelState { Idle, Spinning, Decelerating, Result }
    private WheelState state = WheelState.Idle;

    private float currentSpeed = 0f;
    private int resultIndex = -1;

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
        if (state == WheelState.Spinning || state == WheelState.Decelerating)
        {
            if (wheelTransform != null)
                wheelTransform.Rotate(0f, 0f, currentSpeed * Time.deltaTime);
        }

        if (PlayerInputManager.Instance == null) return;

        bool confirmPressed = (PlayerInputManager.Instance != null && PlayerInputManager.Instance.InteractPressed)
     || Input.GetButtonDown("Submit");

        if (state == WheelState.Idle && confirmPressed)
            OnLaunchPressed();
        else if (state == WheelState.Spinning && confirmPressed)
            OnStopPressed();
        else if (state == WheelState.Result && confirmPressed)
            OnContinuePressed();
    }

    void SetState(WheelState newState)
    {
        state = newState;

        if (launchText != null) launchText.gameObject.SetActive(newState == WheelState.Idle);
        if (stopText != null) stopText.gameObject.SetActive(newState == WheelState.Spinning);
        if (continueText != null) continueText.gameObject.SetActive(newState == WheelState.Result);

        if (resultText != null)
            resultText.gameObject.SetActive(newState == WheelState.Result);
    }

    public void OnLaunchPressed()
    {
        if (state != WheelState.Idle) return;
        currentSpeed = spinSpeed;
        SetState(WheelState.Spinning);
    }

    public void OnStopPressed()
    {
        if (state != WheelState.Spinning) return;
        SetState(WheelState.Decelerating);
        StartCoroutine(DecelerateCoroutine());
    }

    IEnumerator DecelerateCoroutine()
    {
        // Calculer le resultat maintenant
        resultIndex = Random.Range(0, slotNames.Length);

        float elapsed = 0f;
        float startSpeed = currentSpeed;

        // S'assurer de faire au moins un tour complet
        float minDuration = 360f / startSpeed;
        float duration = Mathf.Max(decelerationDuration, minDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            currentSpeed = Mathf.Lerp(startSpeed, 0f, elapsed / duration);
            yield return null;
        }

        currentSpeed = 0f;
        ShowResult();
    }

    void ShowResult()
    {
        SetState(WheelState.Result);

        if (resultText != null)
            resultText.text = slotNames[resultIndex];

        Debug.Log($"[WheelUI] Resultat : {slotNames[resultIndex]} (index {resultIndex})");
    }

    public void OnContinuePressed()
    {
        if (state != WheelState.Result) return;
        if (SceneFader.Instance != null)
            SceneFader.Instance.FadeToScene(2);
    }
}