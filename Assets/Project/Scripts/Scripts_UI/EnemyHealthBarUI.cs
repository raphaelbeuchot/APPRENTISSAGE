using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class EnemyHealthBarUI : MonoBehaviour
{
    [SerializeField] private Image healthFill;

    public void UpdateHealth(float currentHealth, float maxHealth)
    {
        if (healthFill != null)
        {
            healthFill.fillAmount = currentHealth / maxHealth;
        }
    }

    private Coroutine fadeCoroutine;

    public void Show()
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);
        gameObject.SetActive(true);
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = 1f;
    }

    public void Hide()
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeOutCoroutine());
    }

    private IEnumerator FadeOutCoroutine()
    {
        yield return new WaitForSeconds(2f);

        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = gameObject.AddComponent<CanvasGroup>();
        }

        float elapsed = 0f;
        float fadeDuration = 0.5f;

        while (elapsed < fadeDuration)
        {
            cg.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        gameObject.SetActive(false);
    }
}