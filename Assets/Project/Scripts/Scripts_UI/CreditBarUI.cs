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

    private void OnEnable()
    {
        CleaningCreditManager.OnCreditsChanged += HandleCreditsChanged;
    }

    private void OnDisable()
    {
        CleaningCreditManager.OnCreditsChanged -= HandleCreditsChanged;
    }

    private void Start()
    {
        RefreshDisplay(false);
    }

    private void HandleCreditsChanged()
    {
        if (CleaningCreditManager.Instance == null) return;

        int credits = CleaningCreditManager.Instance.GetCredits();

        // Depense immediate, gain avec delai + animation
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
            creditText.text = credits.ToString();

        if (animate && credits > 0)
            StartCoroutine(PulseAnimation());
    }

    private IEnumerator PulseAnimation()
    {
        if (creditText == null) yield break;

        // Flash couleur + pulse en meme temps
        creditText.color = flashColor;
        if (coinIcon != null) coinIcon.color = flashColor;

        // Scale up
        float elapsed = 0f;
        while (elapsed < pulseDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (pulseDuration / 2f);
            float scale = Mathf.Lerp(1f, pulseScale, t);
            creditText.transform.localScale = Vector3.one * scale;
            yield return null;
        }

        // Scale down
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

        // Retour couleur normale apres flashDuration
        yield return new WaitForSeconds(flashDuration);

        creditText.color = normalColor;
        if (coinIcon != null) coinIcon.color = normalColor;
    }
}