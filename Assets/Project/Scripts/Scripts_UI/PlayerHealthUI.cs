using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthUI : MonoBehaviour
{
    [Header("Health Bar")]
    [SerializeField] private Image healthBarFill;
    [SerializeField] private Image damagePreviewFill; // Pour l'effet rouge (optionnel)

    [Header("Colors")]
    [SerializeField] private Color healthyColor = Color.green;
    [SerializeField] private Color lowHealthColor = Color.yellow;
    [SerializeField] private Color criticalHealthColor = Color.red;

    [Header("Damage Preview (optionnel)")]
    [SerializeField] private float damagePreviewDelay = 0.3f;
    [SerializeField] private float damagePreviewSpeed = 2f;

    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;

    private float targetFillAmount;
    private Coroutine damagePreviewCoroutine;

    void Start()
    {
        if (playerHealth == null)
        {
            playerHealth = FindObjectOfType<PlayerHealth>();
        }

        if (playerHealth != null)
        {
            // S'abonner à l'event
            playerHealth.OnHealthChanged += UpdateHealthBar;

            // Initialiser l'affichage
            float initialHealth = playerHealth.GetCurrentHealth();
            float maxHealth = playerHealth.GetMaxHealth();
            UpdateHealthBar(initialHealth, maxHealth);
        }
        else
        {
            Debug.LogError("PlayerHealthUI: PlayerHealth non trouvé!");
        }
    }

    void UpdateHealthBar(float currentHealth, float maxHealth)
    {
        Debug.Log($"UPDATE HEALTHBAR: {currentHealth} / {maxHealth} = {currentHealth / maxHealth}");

        if (healthBarFill == null) return;

        float healthPercent = currentHealth / maxHealth;
        targetFillAmount = healthPercent;

        // La barre principale descend instantanément
        healthBarFill.fillAmount = healthPercent;

        // Changer la couleur selon le pourcentage
        if (healthPercent > 0.5f)
        {
            healthBarFill.color = healthyColor;
        }
        else if (healthPercent > 0.25f)
        {
            healthBarFill.color = lowHealthColor;
        }
        else
        {
            healthBarFill.color = criticalHealthColor;
        }

        // Effet damage preview (si activé)
        if (damagePreviewFill != null)
        {
            if (damagePreviewCoroutine != null)
                StopCoroutine(damagePreviewCoroutine);
            damagePreviewCoroutine = StartCoroutine(DamagePreviewCoroutine());
        }
    }

    private System.Collections.IEnumerator DamagePreviewCoroutine()
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

    void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= UpdateHealthBar;
        }
    }
}