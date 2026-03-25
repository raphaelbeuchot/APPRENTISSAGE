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
    [SerializeField] private float baseContainerWidth = 300f;
    [SerializeField] private float baseContainerHeight = 15f;

    [Header("Damage Preview")]
    [SerializeField] private float damagePreviewDelay = 0.3f;
    [SerializeField] private float damagePreviewSpeed = 2f;

    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;

    private float targetGreenFill;
    private float targetBonusFill;
    private Coroutine damagePreviewCoroutine;
    private Coroutine bonusDamagePreviewCoroutine;
    private float baseMaxHealth;
    private float realMaxHealth;
    private bool initialized = false;

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

        if (bonusBarFill != null && hasBonus)
        {
            float newBonusFill = Mathf.Clamp01((currentHealth - baseMaxHealth) / bonusHealth);
            targetBonusFill = newBonusFill;
            bonusBarFill.fillAmount = newBonusFill;

            if (bonusBarDamage != null)
            {
                if (currentHealth <= baseMaxHealth)
                {
                    // Le degat traverse la frontiere : on snap la barre bonus a 0 immediatement
                    if (bonusDamagePreviewCoroutine != null)
                        StopCoroutine(bonusDamagePreviewCoroutine);
                    bonusBarDamage.fillAmount = 0f;
                }
                else
                {
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

        if (healthBarDamage != null)
        {
            if (!initialized)
                healthBarDamage.fillAmount = newGreenFill;
            if (damagePreviewCoroutine != null)
                StopCoroutine(damagePreviewCoroutine);
            damagePreviewCoroutine = StartCoroutine(DamagePreviewCoroutine());
        }

        initialized = true;
    }

    private System.Collections.IEnumerator DamagePreviewCoroutine()
    {
        yield return new WaitForSeconds(damagePreviewDelay);
        if (healthBarDamage != null)
        {
            while (healthBarDamage.fillAmount > targetGreenFill)
            {
                healthBarDamage.fillAmount = Mathf.Lerp(
                    healthBarDamage.fillAmount,
                    targetGreenFill,
                    Time.deltaTime * damagePreviewSpeed
                );
                if (Mathf.Abs(healthBarDamage.fillAmount - targetGreenFill) < 0.01f)
                {
                    healthBarDamage.fillAmount = targetGreenFill;
                    break;
                }
                yield return null;
            }
        }
    }

    private System.Collections.IEnumerator BonusDamagePreviewCoroutine()
    {
        yield return new WaitForSeconds(damagePreviewDelay);
        if (bonusBarDamage != null)
        {
            while (bonusBarDamage.fillAmount > targetBonusFill)
            {
                bonusBarDamage.fillAmount = Mathf.Lerp(
                    bonusBarDamage.fillAmount,
                    targetBonusFill,
                    Time.deltaTime * damagePreviewSpeed
                );
                if (Mathf.Abs(bonusBarDamage.fillAmount - targetBonusFill) < 0.01f)
                {
                    bonusBarDamage.fillAmount = targetBonusFill;
                    break;
                }
                yield return null;
            }
        }
    }

    void OnDestroy()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= UpdateHealthBar;
    }
}