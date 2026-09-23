using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Multi : vignette de degats plein ecran, scopee a ce joueur (image et PlayerHealth locaux,
// pas de recherche globale). Pose a la main sur chaque joueur avec l'Image de son cote
// (Canvas Overlay partage, deja utilise pour l'ecran "Get ready"/"YOU LOSE").
// Meme logique de flash que GameUIManager.DamageFlash (solo), mais anime seulement l'alpha
// de l'image (deja coloree/degradee dans l'asset) au lieu de teinter une couleur.
public class PlayerDamageVignette : MonoBehaviour
{
    [SerializeField] private Image vignetteImage;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.5f;
    [SerializeField] private float flashDuration = 0.3f;

    private float lastHealth;
    private Coroutine flashCoroutine;

    private void Awake()
    {
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
    }

    private void OnEnable()
    {
        if (playerHealth == null) return;

        lastHealth = playerHealth.GetCurrentHealth();
        playerHealth.OnHealthChanged += OnHealthChanged;
        SetAlpha(0f);
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= OnHealthChanged;
    }

    private void OnHealthChanged(float currentHealth, float maxHealth)
    {
        // Seule une perte de vie declenche le flash : Revive()/Heal() invoquent aussi
        // cet event (remontee de vie), pas une vignette de degats.
        bool tookDamage = currentHealth < lastHealth - 0.01f;
        lastHealth = currentHealth;
        if (!tookDamage) return;

        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashCoroutine());
    }

    private IEnumerator FlashCoroutine()
    {
        if (vignetteImage == null) yield break;

        float elapsed = 0f;
        while (elapsed < flashDuration * 0.3f)
        {
            elapsed += Time.deltaTime;
            SetAlpha(Mathf.Lerp(0f, maxAlpha, elapsed / (flashDuration * 0.3f)));
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < flashDuration * 0.7f)
        {
            elapsed += Time.deltaTime;
            SetAlpha(Mathf.Lerp(maxAlpha, 0f, elapsed / (flashDuration * 0.7f)));
            yield return null;
        }

        SetAlpha(0f);
        flashCoroutine = null;
    }

    private void SetAlpha(float alpha)
    {
        if (vignetteImage == null) return;
        Color c = vignetteImage.color;
        c.a = alpha;
        vignetteImage.color = c;
    }
}
