using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class EnemyHealthBarUI : MonoBehaviour
{
    [SerializeField] private Image healthFill;           // Barre orange (vie actuelle)
    [SerializeField] private Image damageFill;           // Barre rouge (dégât preview)

    [Header("Damage Preview Settings")]
    [SerializeField] private float damagePreviewDelay = 0.3f;    // Délai avant que le rouge commence à se vider
    [SerializeField] private float damagePreviewSpeed = 2f;      // Vitesse de vidage du rouge

    private float currentFillAmount;
    private float targetFillAmount;
    private Coroutine damagePreviewCoroutine;

    public void UpdateHealth(float currentHealth, float maxHealth)
    {
        if (healthFill != null)
        {
            targetFillAmount = currentHealth / maxHealth;

            // La barre orange descend instantanément
            healthFill.fillAmount = targetFillAmount;

            // Lancer l'effet de vidage progressif du rouge
            if (damagePreviewCoroutine != null)
                StopCoroutine(damagePreviewCoroutine);
            damagePreviewCoroutine = StartCoroutine(DamagePreviewCoroutine());
        }
    }

    private IEnumerator DamagePreviewCoroutine()
    {
        // Attendre un peu avant de commencer à vider le rouge
        yield return new WaitForSeconds(damagePreviewDelay);

        // Vider progressivement la barre rouge jusqu'à la valeur orange
        if (damageFill != null)
        {
            while (damageFill.fillAmount > targetFillAmount)
            {
                damageFill.fillAmount = Mathf.Lerp(
                    damageFill.fillAmount,
                    targetFillAmount,
                    Time.deltaTime * damagePreviewSpeed
                );

                // Si très proche, snap direct pour éviter le lerp infini
                if (Mathf.Abs(damageFill.fillAmount - targetFillAmount) < 0.01f)
                {
                    damageFill.fillAmount = targetFillAmount;
                    break;
                }

                yield return null;
            }
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