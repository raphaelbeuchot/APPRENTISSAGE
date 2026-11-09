using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EnemyIconsUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject iconPrefab;
    [SerializeField] private Transform iconsContainer;
    [SerializeField] private AudioClip iconPopSound;

    [Header("Settings")]
    [SerializeField] private float delayBetweenIcons = 0.5f;
    [SerializeField] private float flashDuration = 1f;
    [Tooltip("Vider la barre : delai entre deux icones retirees, de la derniere a la premiere (0 = tout d'un coup).")]
    [SerializeField] private float clearDelayBetweenIcons = 0.1f;

    private List<EnemyIconDisplay> enemyIcons = new List<EnemyIconDisplay>();
    private AudioSource audioSource;
    public event System.Action OnAllIconsCleaned;
    private int cleanedCount = 0;
    private Coroutine spawnCoroutine;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    public void SpawnIconsForEnemies()
    {
        spawnCoroutine = StartCoroutine(SpawnIconsCoroutine());
    }

    /// <summary>
    /// Vide la barre (boss de Rush Hour IV, entre deux phases) : retire toutes les icones, vivants et
    /// morts, de la derniere affichee a la premiere. Ne compte pas comme un nettoyage (pas de
    /// OnAllIconsCleaned). Les autres niveaux ne l'appellent pas.
    /// </summary>
    [ContextMenu("Vider la barre")]
    public void ClearAllIcons()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        // La liste est videe tout de suite : un cadavre nettoye pendant le retrait ne retrouve plus son icone
        List<EnemyIconDisplay> toRemove = new List<EnemyIconDisplay>(enemyIcons);
        enemyIcons.Clear();
        cleanedCount = 0;

        StartCoroutine(ClearIconsCoroutine(toRemove));
    }

    private IEnumerator ClearIconsCoroutine(List<EnemyIconDisplay> icons)
    {
        for (int i = icons.Count - 1; i >= 0; i--)
        {
            if (icons[i] == null) continue;
            Destroy(icons[i].gameObject);
            if (clearDelayBetweenIcons > 0f && i > 0)
                yield return new WaitForSeconds(clearDelayBetweenIcons);
        }
    }
    public void OnIconCleaned(EnemyIconDisplay icon)
    {
        cleanedCount++;
        if (cleanedCount >= enemyIcons.Count)
            OnAllIconsCleaned?.Invoke();
    }
    private IEnumerator SpawnIconsCoroutine()
    {
        List<EnemyHealth> validEnemies = null;

        if (CleaningBonusManager.Instance != null)
            validEnemies = CleaningBonusManager.Instance.GetRegisteredEnemies();

        if (validEnemies == null || validEnemies.Count == 0)
        {
            Debug.LogWarning("[EnemyIconsUI] Aucun ennemi enregistre dans CleaningBonusManager.");
            yield break;
        }

        foreach (EnemyHealth enemy in validEnemies)
        {
            if (enemy == null)
            {
                Debug.Log("[EnemyIconsUI] Ennemi deja detruit, icone skippee.");
                yield return new WaitForSeconds(delayBetweenIcons);
                continue;
            }

            GameObject iconGO = Instantiate(iconPrefab, iconsContainer);
            EnemyIconDisplay iconDisplay = iconGO.GetComponent<EnemyIconDisplay>();
            if (iconDisplay != null)
            {
                iconDisplay.Initialize(enemy, this);
                enemyIcons.Add(iconDisplay);
            }

            if (audioSource != null && iconPopSound != null)
                audioSource.PlayOneShot(iconPopSound);

            yield return new WaitForSeconds(delayBetweenIcons);
        }
        spawnCoroutine = null;
    }

    public void FlashIconRed(EnemyIconDisplay icon)
    {
        if (icon == null) return;
        StartCoroutine(FlashRedCoroutine(icon));
    }

    private IEnumerator FlashRedCoroutine(EnemyIconDisplay icon)
    {
        if (icon == null) yield break;

        Image iconImage = icon.GetAliveImage();
        if (iconImage == null) yield break;

        Color originalColor = iconImage.color;
        float elapsed = 0f;

        while (elapsed < flashDuration / 2f)
        {
            if (icon == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / (flashDuration / 2f);
            iconImage.color = Color.Lerp(originalColor, Color.red, t);
            yield return null;
        }

        elapsed = 0f;

        while (elapsed < flashDuration / 2f)
        {
            if (icon == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / (flashDuration / 2f);
            iconImage.color = Color.Lerp(Color.red, originalColor, t);
            yield return null;
        }

        if (iconImage != null)
            iconImage.color = originalColor;
    }

    public void SetIconCleaned(EnemyHealth enemy)
    {
        if (enemy == null) return;

        foreach (EnemyIconDisplay icon in enemyIcons)
        {
            if (icon != null && icon.GetTrackedEnemy() == enemy)
            {
                icon.SetCleaned();
                return;
            }
        }
    }
    public bool IsLastIcon()
    {
        return cleanedCount + 1 >= enemyIcons.Count;
    }

    public void HideAllIcons()
    {
        gameObject.SetActive(false);
    }
}