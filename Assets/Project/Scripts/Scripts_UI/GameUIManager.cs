using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameUIManager : MonoBehaviour
{
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
    private CountdownTextConfig countdownConfig;

    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private GrabAttack[] zombies;
    [SerializeField] private GameObject pressureGauge;
    [SerializeField] private GameObject creditBar;
    [SerializeField] private GameObject enemyIconsContainer;

    private GameObject healthBar;
    private GameObject staminaBar;
    private GameObject sprayBar;

    private bool isGrabbed = false;

    public void HideGameplayBars()
    {
        if (healthBar != null) healthBar.SetActive(false);
        if (staminaBar != null) staminaBar.SetActive(false);
        if (sprayBar != null) sprayBar.SetActive(false);
        if (pressureGauge != null) pressureGauge.SetActive(false);
        if (creditBar != null) creditBar.SetActive(false);
        if (enemyIconsContainer != null) enemyIconsContainer.SetActive(false);
    }

    public void ShowGameplayBars()
    {
        if (healthBar != null) healthBar.SetActive(true);
        if (staminaBar != null) staminaBar.SetActive(true);
        if (pressureGauge != null) pressureGauge.SetActive(true);
        if (enemyIconsContainer != null) enemyIconsContainer.SetActive(true);
    }

    void Start()
    {
        zombies = FindObjectsOfType<GrabAttack>();
        SprayAmmoUI sprayAmmoUI = FindObjectOfType<SprayAmmoUI>(true);
        if (sprayAmmoUI != null) sprayBar = sprayAmmoUI.gameObject;

        StaminaBarFollower staminaBarFollower = FindObjectOfType<StaminaBarFollower>(true);
        if (staminaBarFollower != null) staminaBar = staminaBarFollower.gameObject;

        PlayerHealthUI playerHealthUI = FindObjectOfType<PlayerHealthUI>(true);
        if (playerHealthUI != null) healthBar = playerHealthUI.gameObject;

        // === FIX : Trouver PlayerHealth automatiquement si non assign� ===
        if (playerHealth == null)
        {
            playerHealth = FindObjectOfType<PlayerHealth>();
            if (playerHealth == null)
            {
                Debug.LogError("GameUIManager: PlayerHealth non trouv�!");
            }
        }

        // Setup initial
        if (damageVignette != null)
        {
            Color c = damageVignette.color;
            c.a = 0f;
            damageVignette.color = c;
        }
        else
        {
            Debug.LogError("GameUIManager: damageVignette non assign�e!");
        }

        if (mashText != null)
        {
            mashText.gameObject.SetActive(false);
        }

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }
        // NOUVEAU : R�cup�rer la config
        countdownConfig = countdownText.GetComponent<CountdownTextConfig>();
        if (countdownConfig == null)
        {
            Debug.LogWarning("CountdownTextConfig non trouve sur CountdownText !");
        }

        // S'abonner UNIQUEMENT au damage flash
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += OnPlayerDamaged;
            Debug.Log("GameUIManager: Abonn� aux events PlayerHealth");
        }

        HideGameplayBars();
    }
    void Update()
    {
        CheckGrabStatus();
    }

    void OnPlayerDamaged(float currentHealth, float maxHealth)
    {
        // Flash de degats seulement
        StartCoroutine(DamageFlash());
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

        Color finalColor = damageVignette.color;
        finalColor.a = 0f;
        damageVignette.color = finalColor;
    }

    void CheckGrabStatus()
    {
        if (zombies == null || zombies.Length == 0) return;

        bool currentlyGrabbed = false;

        foreach (GrabAttack zombie in zombies)
        {
            if (zombie != null && zombie.IsGrabbing())
            {
                currentlyGrabbed = true;
                break;
            }
        }

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

        Debug.Log("MASH PROMPT STARTED");
        mashText.gameObject.SetActive(true);

        while (true)
        {
            bool stillGrabbed = false;
            foreach (GrabAttack zombie in zombies)
            {
                if (zombie != null && zombie.IsGrabbing())
                {
                    stillGrabbed = true;
                    break;
                }
            }

            if (!stillGrabbed) break;

            float cycleTime = 1f / mashBlinkSpeed;
            mashText.enabled = (Time.time % cycleTime) < (cycleTime * 0.5f);

            yield return null;
        }

        Debug.Log("MASH PROMPT STOPPED");
        HideMashPrompt();
    }

    void HideMashPrompt()
    {
        if (mashText != null)
        {
            mashText.gameObject.SetActive(false);
        }
    }

    public void ShowCountdown(bool skipText = false)
    {
        Debug.Log($"[GameUIManager] ShowCountdown appele avec skipText = {skipText}"); // NOUVEAU

        if (!skipText)
        {
            Debug.Log("[GameUIManager] Lancement CountdownSequence"); // NOUVEAU
            StartCoroutine(CountdownSequence());
        }
        else
        {
            Debug.Log("[GameUIManager] Skip CountdownSequence (restart)"); // NOUVEAU
        }
    }

    IEnumerator CountdownSequence()
    {
        if (countdownText == null) yield break;

        countdownText.gameObject.SetActive(true);
        countdownText.fontSize = countdownFontSize;

        // R�cup�rer config
        string titleText = countdownConfig != null
            ? countdownConfig.GetTitle()
            : "Super Panopticon!";

        float rotationDuration = countdownConfig != null
            ? countdownConfig.GetRotationDuration()
            : 2f;

        float fadeOutDuration = countdownConfig != null
            ? countdownConfig.GetFadeOutDuration()
            : 1f;

        // Afficher le texte
        countdownText.text = titleText;
        countdownText.transform.localScale = Vector3.one;
        Color c = countdownText.color;
        c.a = 1f;
        countdownText.color = c;

        // PHASE 1 : Rotation 360 degr�s
        float elapsed = 0f;
        Quaternion startRotation = countdownText.transform.localRotation;

        // NOUVEAU : Direction de rotation
        bool clockwise = countdownConfig != null ? countdownConfig.IsClockwise() : true;
        float rotationDirection = clockwise ? 360f : -360f;

        while (elapsed < rotationDuration)
        {
            elapsed += Time.deltaTime;
            float angle = Mathf.Lerp(0f, rotationDirection, elapsed / rotationDuration); // Modifi�
            countdownText.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }

        // Reset rotation
        countdownText.transform.localRotation = startRotation;

        // PHASE 2 : Fade out
        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
            c = countdownText.color;
            c.a = alpha;
            countdownText.color = c;
            yield return null;
        }

        // Cleanup
        countdownText.gameObject.SetActive(false);
        countdownText.transform.localRotation = startRotation;
        c = countdownText.color;
        c.a = 1f;
        countdownText.color = c;
    }

    public void UpdateZombiesList()
    {
        zombies = FindObjectsOfType<GrabAttack>();
    }

    public void RegisterGrab(GrabAttack grab)
    {
        if (!zombies.Contains(grab))
        {
            var newList = zombies.ToList();
            newList.Add(grab);
            zombies = newList.ToArray();
        }
    }

    void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= OnPlayerDamaged;
        }
    }
}