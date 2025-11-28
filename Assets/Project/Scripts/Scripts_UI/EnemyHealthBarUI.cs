using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class EnemyHealthBarUI : MonoBehaviour
{
    [Header("Health Bar")]
    [SerializeField] private Image healthFill;
    [SerializeField] private Image damagePreviewFill;
    [SerializeField] private Image background; // Pour le lock-on outline

    [Header("Lock-On")]
    [SerializeField] private Color lockedColor = Color.white;
    private Color originalBackgroundColor;

    [Header("Damage Preview Settings")]
    [SerializeField] private float damagePreviewDelay = 0.2f;
    [SerializeField] private float damagePreviewSpeed = 2f;


    private float targetFillAmount;
    private Coroutine damagePreviewCoroutine;


    private void Start()
    {
        if (background != null)
        {
            originalBackgroundColor = background.color;
            Debug.Log($"Original background color saved: {originalBackgroundColor}");
        }
    }


    public void UpdateHealth(float currentHealth, float maxHealth)
    {
        if (healthFill == null) return;

        float healthPercent = currentHealth / maxHealth;
        targetFillAmount = healthPercent;

        healthFill.fillAmount = healthPercent;

        if (damagePreviewFill != null)
        {
            if (damagePreviewCoroutine != null)
                StopCoroutine(damagePreviewCoroutine);
            damagePreviewCoroutine = StartCoroutine(DamagePreviewCoroutine());
        }
    }

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

    private Coroutine fadeCoroutine;

    public void Show()
    {
        if (this == null || gameObject == null) return;

        Debug.Log($"[HealthBarUI] SHOW called on {gameObject.name}");

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        gameObject.SetActive(true);

        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 1f;
            Debug.Log($"[HealthBarUI] CanvasGroup alpha set to 1");
        }
    }

    public void Hide()
    {
        if (this == null || gameObject == null) return;

        gameObject.SetActive(false);  // Instantanément
    }


    private IEnumerator FadeOutCoroutine()
    {
        /*yield return new WaitForSeconds(2f);
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
        */
        yield break;
    }

    // === LOCK-ON OUTLINE ===
    public void SetLockedOutline(bool locked)
    {
        if (background == null) return;

        Debug.Log($"SetLockedOutline called on {gameObject.name}: {locked}");

        if (locked)
        {
            background.color = lockedColor;
        }
        else
        {
            background.color = originalBackgroundColor;
        }
    }
}