using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class EnemyHealthBarUI : MonoBehaviour
{
    [Header("Health Bar")]
    [SerializeField] private Image healthFill;        // Barre principale
    [SerializeField] private Image damagePreviewFill; // Barre rouge pour l'effet damage preview

    [Header("Damage Preview Settings")]
    [SerializeField] private float damagePreviewDelay = 0.2f;
    [SerializeField] private float damagePreviewSpeed = 2f;

    private float targetFillAmount;
    private Coroutine damagePreviewCoroutine;

    // Mise à jour de la barre
    public void UpdateHealth(float currentHealth, float maxHealth)
    {
        if (healthFill == null) return;

        float healthPercent = currentHealth / maxHealth;
        targetFillAmount = healthPercent;

        // Mise à jour instantanée de la barre principale
        healthFill.fillAmount = healthPercent;

        // Lancer le damage preview si activé
        if (damagePreviewFill != null)
        {
            if (damagePreviewCoroutine != null)
                StopCoroutine(damagePreviewCoroutine);
            damagePreviewCoroutine = StartCoroutine(DamagePreviewCoroutine());
        }
    }

    // Coroutine pour faire descendre la barre rouge progressivement
    private IEnumerator DamagePreviewCoroutine()
    {
        yield return new WaitForSeconds(damagePreviewDelay);

        if (damagePreviewFill != null)
        {
            while (damagePreviewFill.fillAmount > targetFillAmount)
            {
                damagePreviewFill.fillAmount = Mathf.Lerp(
                    damagePreviewFill.fillAmount,
                    targetFillAmount,
                    Time.deltaTime * damagePreviewSpeed
                );

                if (Mathf.Abs(damagePreviewFill.fillAmount - targetFillAmount) < 0.01f)
                {
                    damagePreviewFill.fillAmount = targetFillAmount;
                    break;
                }

                yield return null;
            }
        }
    }

    // Affichage de la barre
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
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

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
