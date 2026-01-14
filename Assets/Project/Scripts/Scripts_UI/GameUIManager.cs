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

    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private GrabAttack[] zombies;

    private bool isGrabbed = false;

    void Start()
    {
        zombies = FindObjectsOfType<GrabAttack>();

        // === FIX : Trouver PlayerHealth automatiquement si non assigné ===
        if (playerHealth == null)
        {
            playerHealth = FindObjectOfType<PlayerHealth>();
            if (playerHealth == null)
            {
                Debug.LogError("GameUIManager: PlayerHealth non trouvé!");
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
            Debug.LogError("GameUIManager: damageVignette non assignée!");
        }

        if (mashText != null)
        {
            mashText.gameObject.SetActive(false);
        }

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }

        // S'abonner UNIQUEMENT au damage flash
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += OnPlayerDamaged;
            Debug.Log("GameUIManager: Abonné aux events PlayerHealth");
        }
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

        string[] countdownNumbers = { "Let's", "Play", "Super Panopticon !", "" };

        foreach (string number in countdownNumbers)
        {
            countdownText.text = number;

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

            Color resetColor = countdownText.color;
            resetColor.a = 1f;
            countdownText.color = resetColor;
            countdownText.transform.localScale = Vector3.one;
        }

        countdownText.gameObject.SetActive(false);
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