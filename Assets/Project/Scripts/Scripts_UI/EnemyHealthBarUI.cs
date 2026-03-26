using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class EnemyHealthBarUI : MonoBehaviour
{
    [Header("Health Bar")]
    [SerializeField] private Image healthFill;
    [SerializeField] private Image damagePreviewFill;
    [SerializeField] private Image background;

    [Header("Bonus Bar")]
    [SerializeField] private Image bonusBarFill;
    [SerializeField] private Image bonusBarDamage;
    [SerializeField] private RectTransform bonusBarContainer;
    [SerializeField] private RectTransform mainBarContainer;

    [Header("Config")]
    [SerializeField] private float baseContainerWidth = 200f;
    [SerializeField] private float baseContainerHeight = 10f;

    [Header("Lock-On")]
    [SerializeField] private Image outline;

    [Header("Damage Preview Settings")]
    [SerializeField] private float damagePreviewDelay = 0.2f;
    [SerializeField] private float damagePreviewSpeed = 0.8f;

    private float targetFill;
    private float targetBonusFill;
    private Coroutine damagePreviewCoroutine;
    private Coroutine bonusDamagePreviewCoroutine;
    private float baseMaxHealth;
    private float realMaxHealth;
    private bool initialized = false;
    private bool bonusDraining = false;

    public void Initialize(float baseMax, float realMax)
    {
        baseMaxHealth = baseMax;
        realMaxHealth = realMax;

        bool hasBonus = realMaxHealth > baseMaxHealth;
        if (bonusBarContainer != null)
        {
            bonusBarContainer.gameObject.SetActive(hasBonus);
            if (hasBonus)
            {
                float actualWidth = mainBarContainer != null ? mainBarContainer.rect.width : baseContainerWidth;
                float bonusWidth = actualWidth * ((realMaxHealth / baseMaxHealth) - 1f);
                bonusBarContainer.sizeDelta = new Vector2(bonusWidth, baseContainerHeight);

                if (outline != null)
                {
                    RectTransform outlineRect = outline.GetComponent<RectTransform>();
                    if (outlineRect != null)
                    {
                        outlineRect.offsetMin = new Vector2(-3.5f, -3.5f);
                        outlineRect.offsetMax = new Vector2(bonusWidth + 3.5f, 3.5f);
                    }
                }
                else
                {
                    if (outline != null)
                    {
                        RectTransform outlineRect = outline.GetComponent<RectTransform>();
                        if (outlineRect != null)
                        {
                            outlineRect.offsetMin = new Vector2(-3.5f, -3.5f);
                            outlineRect.offsetMax = new Vector2(3.5f, 3.5f);
                        }
                    }
                }
            }
        }
    }

    private void UpdateOutlineWidth(float currentHealth)
    {
        if (outline == null) return;
        RectTransform outlineRect = outline.GetComponent<RectTransform>();
        if (outlineRect == null) return;

        bool hasBonus = realMaxHealth > baseMaxHealth;
        if (!hasBonus)
        {
            outlineRect.offsetMin = new Vector2(-3.5f, -3.5f);
            outlineRect.offsetMax = new Vector2(3.5f, 3.5f);
            return;
        }

        float bonusHealth = realMaxHealth - baseMaxHealth;
        float bonusRatio = Mathf.Clamp01((currentHealth - baseMaxHealth) / bonusHealth);
        float actualWidth = mainBarContainer != null ? mainBarContainer.rect.width : baseContainerWidth;
        float maxBonusWidth = actualWidth * ((realMaxHealth / baseMaxHealth) - 1f);
        float currentBonusWidth = maxBonusWidth * bonusRatio;

        outlineRect.offsetMin = new Vector2(-3.5f, -3.5f);
        outlineRect.offsetMax = new Vector2(currentBonusWidth + 3.5f, 3.5f);
    }

    public void UpdateHealth(float currentHealth, float baseMax, float realMax)
    {
        if (healthFill == null) return;

        baseMaxHealth = baseMax;
        realMaxHealth = realMax;

        bool hasBonus = realMaxHealth > baseMaxHealth;
        float bonusHealth = realMaxHealth - baseMaxHealth;
        bool isCrossing = hasBonus && currentHealth <= baseMaxHealth;

        if (bonusBarFill != null && hasBonus)
        {
            float newBonusFill = Mathf.Clamp01((currentHealth - baseMaxHealth) / bonusHealth);
            targetBonusFill = isCrossing ? 0f : newBonusFill;
            bonusBarFill.fillAmount = newBonusFill;

            if (bonusBarDamage != null)
            {
                if (isCrossing)
                {
                    bonusDraining = true;
                    if (bonusDamagePreviewCoroutine != null)
                        StopCoroutine(bonusDamagePreviewCoroutine);
                    bonusDamagePreviewCoroutine = StartCoroutine(BonusDamagePreviewCoroutine());
                    if (damagePreviewCoroutine != null)
                        StopCoroutine(damagePreviewCoroutine);
                    damagePreviewCoroutine = null;
                }
                else
                {
                    bonusDraining = false;
                    bonusBarDamage.fillAmount = Mathf.Max(bonusBarDamage.fillAmount, newBonusFill);
                    if (bonusDamagePreviewCoroutine != null)
                        StopCoroutine(bonusDamagePreviewCoroutine);
                    bonusDamagePreviewCoroutine = StartCoroutine(BonusDamagePreviewCoroutine());
                }
            }
        }

        float newFill = Mathf.Clamp01(Mathf.Min(currentHealth, baseMaxHealth) / baseMaxHealth);
        targetFill = newFill;
        healthFill.fillAmount = newFill;

        if (damagePreviewFill != null && !isCrossing)
        {
            if (!gameObject.activeInHierarchy)
            {
                damagePreviewFill.fillAmount = targetFill;
                return;
            }
            if (!initialized)
                damagePreviewFill.fillAmount = newFill;
            if (damagePreviewCoroutine != null)
                StopCoroutine(damagePreviewCoroutine);
            damagePreviewCoroutine = StartCoroutine(DamagePreviewCoroutine(false));
        }
        UpdateOutlineWidth(currentHealth);
        initialized = true;
    }

    private IEnumerator DamagePreviewCoroutine(bool skipDelay = false)
    {
        if (!skipDelay)
            yield return new WaitForSeconds(damagePreviewDelay);

        if (damagePreviewFill != null)
        {
            while (damagePreviewFill.fillAmount > targetFill)
            {
                damagePreviewFill.fillAmount = Mathf.MoveTowards(
                    damagePreviewFill.fillAmount,
                    targetFill,
                    Time.deltaTime * damagePreviewSpeed
                );
                yield return null;
            }
            damagePreviewFill.fillAmount = targetFill;
        }
    }

    private IEnumerator BonusDamagePreviewCoroutine()
    {
        yield return new WaitForSeconds(damagePreviewDelay);

        if (bonusBarDamage != null)
        {
            while (bonusBarDamage.fillAmount > targetBonusFill)
            {
                bonusBarDamage.fillAmount = Mathf.MoveTowards(
                    bonusBarDamage.fillAmount,
                    targetBonusFill,
                    Time.deltaTime * damagePreviewSpeed
                );
                yield return null;
            }
            bonusBarDamage.fillAmount = targetBonusFill;
        }

        if (bonusDraining)
        {
            bonusDraining = false;
            damagePreviewCoroutine = StartCoroutine(DamagePreviewCoroutine(true));
        }
    }

    private Coroutine fadeCoroutine;

    public void Show()
    {
        if (this == null || gameObject == null) return;
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);
        gameObject.SetActive(true);
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg != null)
            cg.alpha = 1f;
    }

    public void Hide()
    {
        if (this == null || gameObject == null) return;
        gameObject.SetActive(false);
    }

    public void SetLockedOutline(bool locked)
    {
        if (outline != null)
            outline.gameObject.SetActive(locked);
    }
}