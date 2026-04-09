using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class CreditBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject creditDisplay;
    [SerializeField] private TextMeshProUGUI creditText;
    [SerializeField] private Image coinIcon;

    [Header("Animation")]
    [SerializeField] private float spawnDelay = 2f;
    [SerializeField] private float pulseDuration = 0.5f;
    [SerializeField] private float pulseScale = 1.4f;
    [SerializeField] private float flashDuration = 1f;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color flashColor = Color.yellow;

    private Coroutine pendingUpdateCoroutine;
    private Coroutine countdownCoroutine;
    private bool isDead = false;

    private void Start()
    {
        if (creditDisplay != null)
            creditDisplay.SetActive(false);
    }

    private void OnEnable()
    {
        CleaningCreditManager.OnCreditsChanged += HandleCreditsChanged;
    }

    private void OnDisable()
    {
        CleaningCreditManager.OnCreditsChanged -= HandleCreditsChanged;
    }

    private void HandleCreditsChanged()
    {
        if (isDead) return;
        if (CleaningCreditManager.Instance == null) return;
        int credits = CleaningCreditManager.Instance.GetCredits();
        if (credits < GetDisplayedCount())
        {
            RefreshDisplay(false);
        }
        else
        {
            if (pendingUpdateCoroutine != null)
                StopCoroutine(pendingUpdateCoroutine);
            pendingUpdateCoroutine = StartCoroutine(DelayedCreditUpdate());
        }
    }

    private int GetDisplayedCount()
    {
        if (creditText == null) return 0;
        if (int.TryParse(creditText.text, out int val))
            return val;
        return 0;
    }

    private IEnumerator DelayedCreditUpdate()
    {
        yield return new WaitForSeconds(spawnDelay);
        RefreshDisplay(true);
    }

    private void RefreshDisplay(bool animate)
    {
        if (CleaningCreditManager.Instance == null) return;
        int credits = CleaningCreditManager.Instance.GetCredits();

        if (creditDisplay != null)
            creditDisplay.SetActive(credits > 0);
        if (creditText != null)
            creditText.text = credits.ToString() + "x";
        if (animate && credits > 0)
            StartCoroutine(PulseAnimation());
    }

    public void CountdownToZero(float duration)
    {
        Debug.Log("[CreditBarUI] CountdownToZero appele, duration : " + duration);
        isDead = true;
        if (pendingUpdateCoroutine != null)
            StopCoroutine(pendingUpdateCoroutine);
        if (countdownCoroutine != null)
            StopCoroutine(countdownCoroutine);
        countdownCoroutine = StartCoroutine(CountdownCoroutine(duration));
    }

    private IEnumerator CountdownCoroutine(float duration)
    {
        if (CleaningCreditManager.Instance == null) yield break;
        int startCount = CleaningCreditManager.Instance.GetCredits();
        if (startCount <= 0) yield break;

        if (creditDisplay != null)
            creditDisplay.SetActive(true);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            int current = Mathf.RoundToInt(Mathf.Lerp(startCount, 0, t));
            if (creditText != null)
                creditText.text = current.ToString() + "x";
            yield return null;
        }

        if (creditText != null)
            creditText.text = "0x";
        
    }

    private IEnumerator PulseAnimation()
    {
        if (creditText == null) yield break;
        creditText.color = flashColor;
        float elapsed = 0f;
        while (elapsed < pulseDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (pulseDuration / 2f);
            float scale = Mathf.Lerp(1f, pulseScale, t);
            creditText.transform.localScale = Vector3.one * scale;
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < pulseDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (pulseDuration / 2f);
            float scale = Mathf.Lerp(pulseScale, 1f, t);
            creditText.transform.localScale = Vector3.one * scale;
            yield return null;
        }
        creditText.transform.localScale = Vector3.one;
        yield return new WaitForSeconds(flashDuration);
        creditText.color = normalColor;
    }
}