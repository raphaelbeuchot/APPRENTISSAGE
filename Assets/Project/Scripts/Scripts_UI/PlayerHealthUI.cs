using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthUI : MonoBehaviour
{
    [Header("Health Bar")]
    [SerializeField] private Image healthBarFill;
    [SerializeField] private Image healthBarDamage;

    [Header("Bonus Bar")]
    [SerializeField] private Image bonusBarFill;
    [SerializeField] private Image bonusBarDamage;
    [SerializeField] private RectTransform bonusBarContainer;

    [Header("Config")]
    [SerializeField] private float baseContainerWidth = 150f;
    [SerializeField] private float baseContainerHeight = 10f;

    [Header("Damage Preview")]
    [SerializeField] private float damagePreviewDelay = 0.3f;
    [SerializeField] private float damagePreviewSpeed = 0.8f;

    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;

    private float targetGreenFill;
    private float targetBonusFill;
    private Coroutine damagePreviewCoroutine;
    private Coroutine bonusDamagePreviewCoroutine;
    private float baseMaxHealth;
    private float realMaxHealth;
    private bool initialized = false;
    private bool bonusDraining = false;

#if UNITY_EDITOR
    void Reset()
    {
        healthBarFill = transform.Find("HealthBarFill")?.GetComponent<Image>();
        healthBarDamage = transform.Find("HealthBarDamage")?.GetComponent<Image>();

        Transform bonus = transform.parent?.Find("BonusBarContainer");
        if (bonus != null)
        {
            bonusBarContainer = bonus.GetComponent<RectTransform>();
            bonusBarFill = bonus.Find("BonusFill")?.GetComponent<Image>();
            bonusBarDamage = bonus.Find("BonusDamage")?.GetComponent<Image>();
        }
    }
#endif
    void Start()
    {
        if (playerHealth == null)
            playerHealth = FindObjectOfType<PlayerHealth>();

        if (playerHealth != null)
        {
            baseMaxHealth = playerHealth.GetBaseMaxHealth();
            realMaxHealth = playerHealth.GetMaxHealth();
            SetupBonusContainer();
            playerHealth.OnHealthChanged += UpdateHealthBar;
            UpdateHealthBar(playerHealth.GetCurrentHealth(), realMaxHealth);
        }
        else
        {
            Debug.LogError("PlayerHealthUI: PlayerHealth non trouve!");
        }
    }

    void SetupBonusContainer()
    {
        if (bonusBarContainer == null) return;
        bool hasBonus = realMaxHealth > baseMaxHealth;
        bonusBarContainer.gameObject.SetActive(hasBonus);
        if (hasBonus)
        {
            float bonusWidth = baseContainerWidth * ((realMaxHealth / baseMaxHealth) - 1f);
            bonusBarContainer.sizeDelta = new Vector2(bonusWidth, baseContainerHeight);
        }
    }

    void UpdateHealthBar(float currentHealth, float maxHealth)
    {
        if (healthBarFill == null) return;

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
                    // La coroutine bonus va enchainer la normale directement
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

        float newGreenFill = Mathf.Clamp01(Mathf.Min(currentHealth, baseMaxHealth) / baseMaxHealth);
        targetGreenFill = newGreenFill;
        healthBarFill.fillAmount = newGreenFill;

        if (healthBarDamage != null && !isCrossing)
        {
            if (!initialized)
                healthBarDamage.fillAmount = newGreenFill;
            if (damagePreviewCoroutine != null)
                StopCoroutine(damagePreviewCoroutine);
            damagePreviewCoroutine = StartCoroutine(DamagePreviewCoroutine(false));
        }

        initialized = true;
    }

    private System.Collections.IEnumerator DamagePreviewCoroutine(bool skipDelay = false)
    {
        if (!skipDelay)
            yield return new WaitForSeconds(damagePreviewDelay);

        if (healthBarDamage != null)
        {
            while (healthBarDamage.fillAmount > targetGreenFill)
            {
                healthBarDamage.fillAmount = Mathf.MoveTowards(
                    healthBarDamage.fillAmount,
                    targetGreenFill,
                    Time.deltaTime * damagePreviewSpeed
                );
                yield return null;
            }
            healthBarDamage.fillAmount = targetGreenFill;
        }
    }

    private System.Collections.IEnumerator BonusDamagePreviewCoroutine()
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

    void OnDestroy()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= UpdateHealthBar;
    }
}