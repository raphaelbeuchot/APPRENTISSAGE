using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class GameUIManager : MonoBehaviour
{
    [Header("Health UI")]
    [SerializeField] private Image healthBarFill; // Remplacer les segments par une barre
    [SerializeField] private Color healthyColor = Color.green;
    [SerializeField] private Color lowHealthColor = Color.yellow;
    [SerializeField] private Color criticalHealthColor = Color.red;

    [Header("Damage Vignette")]
    [SerializeField] private Image damageVignette;
    [SerializeField] private float damageFlashDuration = 0.3f;
    [SerializeField] private Color damageColor = new Color(1f, 0f, 0f, 0.5f);

    [Header("Grab UI")]
    [SerializeField] private TextMeshProUGUI mashText;
    [SerializeField] private float mashBlinkSpeed = 2f;

    [Header("Countdown UI")]
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private float countdownFontSize = 100f;

    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private GrabAttack[] zombies; // CHANGÉ: ZombieGrabSystem en GrabAttack

    private bool isGrabbed = false;

    void Start()
    {
        // Setup initial
        if (damageVignette != null)
        {
            Color c = damageVignette.color;
            c.a = 0f;
            damageVignette.color = c;
        }

        if (mashText != null)
        {
            mashText.gameObject.SetActive(false);
        }

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }

        // S'abonner aux evenements
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += UpdateHealthUI;

            // Initialiser l'affichage
            UpdateHealthDisplay(playerHealth.GetCurrentHealth(), playerHealth.GetMaxHealth());
        }
    }

    void Update()
    {
        CheckGrabStatus();
    }

    void UpdateHealthUI(float currentHealth, float maxHealth)
    {
        UpdateHealthDisplay(currentHealth, maxHealth);

        // Flash de degats
        StartCoroutine(DamageFlash());
    }

    void UpdateHealthDisplay(float currentHealth, float maxHealth)
    {
        // Mettre a jour la barre de vie
        if (healthBarFill != null)
        {
            float healthPercent = currentHealth / maxHealth;
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
        }
    }

    IEnumerator DamageFlash()
    {
        if (damageVignette == null) yield break;

        // Apparition rapide
        float elapsed = 0f;
        while (elapsed < damageFlashDuration * 0.3f)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, damageColor.a, elapsed / (damageFlashDuration * 0.3f));
            Color c = damageColor;
            c.a = alpha;
            damageVignette.color = c;
            yield return null;
        }

        // Disparition lente
        elapsed = 0f;
        while (elapsed < damageFlashDuration * 0.7f)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(damageColor.a, 0f, elapsed / (damageFlashDuration * 0.7f));
            Color c = damageColor;
            c.a = alpha;
            damageVignette.color = c;
            yield return null;
        }

        // S'assurer que c'est transparent a la fin
        Color finalColor = damageVignette.color;
        finalColor.a = 0f;
        damageVignette.color = finalColor;
    }

    void CheckGrabStatus()
    {
        if (zombies == null || zombies.Length == 0) return;

        bool currentlyGrabbed = false;

        // CHANGÉ: ZombieGrabSystem en GrabAttack
        foreach (GrabAttack zombie in zombies)
        {
            if (zombie != null && zombie.IsGrabbing())
            {
                currentlyGrabbed = true;
                break;
            }
        }

        // Afficher/cacher le texte de mashing
        if (currentlyGrabbed && !isGrabbed)
        {
            StartCoroutine(ShowMashPrompt());
        }
        else if (!currentlyGrabbed && isGrabbed)
        {
            HideMashPrompt();
        }

        isGrabbed = currentlyGrabbed;
    }

    IEnumerator ShowMashPrompt()
    {
        if (mashText == null) yield break;

        mashText.gameObject.SetActive(true);

        while (isGrabbed)
        {
            // Effet de clignotement
            float alpha = Mathf.PingPong(Time.time * mashBlinkSpeed, 1f);
            Color c = mashText.color;
            c.a = alpha;
            mashText.color = c;

            yield return null;
        }
    }

    void HideMashPrompt()
    {
        if (mashText != null)
        {
            mashText.gameObject.SetActive(false);
        }
    }

    public void ShowCountdown()
    {
        StartCoroutine(CountdownSequence());
    }

    IEnumerator CountdownSequence()
    {
        if (countdownText == null) yield break;

        countdownText.gameObject.SetActive(true);
        countdownText.fontSize = countdownFontSize;

        string[] countdownNumbers = { "3", "2", "1", "GO!" };

        foreach (string number in countdownNumbers)
        {
            countdownText.text = number;

            // Animation de scale
            float elapsed = 0f;
            float duration = number == "GO!" ? 0.5f : 1f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float scale = Mathf.Lerp(2f, 1f, elapsed / duration);
                countdownText.transform.localScale = Vector3.one * scale;

                float alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
                Color c = countdownText.color;
                c.a = alpha;
                countdownText.color = c;

                yield return null;
            }

            // Reset pour le prochain nombre
            Color resetColor = countdownText.color;
            resetColor.a = 1f;
            countdownText.color = resetColor;
            countdownText.transform.localScale = Vector3.one;
        }

        countdownText.gameObject.SetActive(false);
    }

    // Methode publique pour mettre a jour manuellement la liste des zombies
    // CHANGÉ: ZombieGrabSystem en GrabAttack
    public void UpdateZombiesList()
    {
        zombies = FindObjectsOfType<GrabAttack>();
    }

    void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= UpdateHealthUI;
        }
    }
}