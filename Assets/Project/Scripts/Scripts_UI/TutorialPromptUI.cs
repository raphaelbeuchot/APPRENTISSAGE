using UnityEngine;
using TMPro;
using System.Collections;

public class TutorialPromptUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI promptText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Settings")]
    [SerializeField] private float fadeDuration = 0.2f;

    private bool isVisible = false;
    private Coroutine fadeCoroutine;

    void Start()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
        isVisible = false;
    }

    public void Show()
    {
        // ENLEVE LE GUARD pour permettre changement de message
        // if (isVisible) return;   SUPPRIME

        isVisible = true;

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeCoroutine(1f));
    }

    public void Hide()
    {
        if (!isVisible) return;

        isVisible = false;

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeCoroutine(0f));
    }

    public void SetMessage(string message)
    {
        if (promptText != null)
        {
            promptText.text = message;
        }
    }

    IEnumerator FadeCoroutine(float targetAlpha)
    {
        if (canvasGroup == null) yield break;

        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }
}