using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameUIManager : MonoBehaviour
{
    [Header("Damage Vignette")]
    [SerializeField] private Image damageVignette;
    [SerializeField] private float damageFlashDuration = 0.3f;
    [SerializeField] private Color damageColor = new Color(1f, 0f, 0f, 0.5f);

    [Header("Countdown UI")]
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private float countdownFontSize = 100f;
    private CountdownTextConfig countdownConfig;

    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private GameObject pressureGauge;
    [SerializeField] private GameObject creditBar;
    [SerializeField] private GameObject enemyIconsContainer;

    private GameObject healthBar;
    private GameObject staminaBar;
    private GameObject sprayBar;

    public void HideGameplayBars()
    {
        if (healthBar != null) healthBar.SetActive(false);
        if (staminaBar != null) staminaBar.SetActive(false);
        if (sprayBar != null) sprayBar.SetActive(false);
        if (pressureGauge != null) pressureGauge.SetActive(false);
        if (creditBar != null) creditBar.SetActive(false);
        if (enemyIconsContainer != null) enemyIconsContainer.SetActive(false);
    }

    void Start()
    {
       

        StaminaBarFollower staminaBarFollower = FindObjectOfType<StaminaBarFollower>();
        if (staminaBarFollower != null) staminaBar = staminaBarFollower.gameObject;

        PlayerHealthUI playerHealthUI = FindObjectOfType<PlayerHealthUI>();
        if (playerHealthUI != null) healthBar = playerHealthUI.gameObject;

        if (playerHealth == null)
        {
            playerHealth = FindObjectOfType<PlayerHealth>();
            if (playerHealth == null)
            {
                Debug.LogError("GameUIManager: PlayerHealth non trouve!");
            }
        }

        if (damageVignette != null)
        {
            Color c = damageVignette.color;
            c.a = 0f;
            damageVignette.color = c;
        }
        else
        {
            Debug.LogError("GameUIManager: damageVignette non assignee!");
        }

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }

        countdownConfig = countdownText.GetComponent<CountdownTextConfig>();
        if (countdownConfig == null)
        {
            Debug.LogWarning("CountdownTextConfig non trouve sur CountdownText !");
        }

        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += OnPlayerDamaged;
            Debug.Log("GameUIManager: Abonne aux events PlayerHealth");
        }
    }

    void OnPlayerDamaged(float currentHealth, float maxHealth)
    {
        StartCoroutine(DamageFlash());
    }

    IEnumerator DamageFlash()
    {
        if (damageVignette == null) yield break;

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

    public void ShowCountdown(bool skipText = false)
    {
        Debug.Log($"[GameUIManager] ShowCountdown appele avec skipText = {skipText}");

        if (!skipText)
        {
            Debug.Log("[GameUIManager] Lancement CountdownSequence");
            StartCoroutine(CountdownSequence());
        }
        else
        {
            Debug.Log("[GameUIManager] Skip CountdownSequence (restart)");
        }
    }

    IEnumerator CountdownSequence()
    {
        if (countdownText == null) yield break;

        countdownText.gameObject.SetActive(true);
        countdownText.fontSize = countdownFontSize;

        string titleText = countdownConfig != null
            ? countdownConfig.GetTitle()
            : "Super Panopticon!";

        float rotationDuration = countdownConfig != null
            ? countdownConfig.GetRotationDuration()
            : 2f;

        float fadeOutDuration = countdownConfig != null
            ? countdownConfig.GetFadeOutDuration()
            : 1f;

        countdownText.text = titleText;
        countdownText.transform.localScale = Vector3.one;
        Color c = countdownText.color;
        c.a = 1f;
        countdownText.color = c;

        float elapsed = 0f;
        Quaternion startRotation = countdownText.transform.localRotation;

        bool clockwise = countdownConfig != null ? countdownConfig.IsClockwise() : true;
        float rotationDirection = clockwise ? 360f : -360f;

        while (elapsed < rotationDuration)
        {
            elapsed += Time.deltaTime;
            float angle = Mathf.Lerp(0f, rotationDirection, elapsed / rotationDuration);
            countdownText.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }

        countdownText.transform.localRotation = startRotation;

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

        countdownText.gameObject.SetActive(false);
        countdownText.transform.localRotation = startRotation;
        c = countdownText.color;
        c.a = 1f;
        countdownText.color = c;
    }

    void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= OnPlayerDamaged;
        }
    }
}