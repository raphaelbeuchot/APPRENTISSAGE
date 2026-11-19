using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Icones instanciees au lancement dans iconsContainer. En editeur (hors jeu) : apercu de
// previewIconCount icones, jamais sauvegardees (HideFlags.DontSave), pour poser la barre sans lancer le jeu.
[ExecuteAlways]
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
    [Tooltip("Editeur seulement : nombre d'icones de l'apercu hors jeu")]
    [SerializeField] private int previewIconCount = 15;

    private List<EnemyIconDisplay> enemyIcons = new List<EnemyIconDisplay>();
    private AudioSource audioSource;
    public event System.Action OnAllIconsCleaned;
    private int cleanedCount = 0;
    private Coroutine spawnCoroutine;

    private void Awake()
    {
        if (!Application.isPlaying) return;

        // Apercu editeur encore present (Enter Play Mode sans rechargement de scene)
        if (iconsContainer != null)
            for (int i = iconsContainer.childCount - 1; i >= 0; i--)
            {
                GameObject child = iconsContainer.GetChild(i).gameObject;
                if ((child.hideFlags & HideFlags.DontSaveInEditor) != 0)
                    Destroy(child);
            }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

#if UNITY_EDITOR
    private void OnEnable()
    {
        if (!Application.isPlaying) QueuePreview();
    }

    private void OnDisable()
    {
        // Pas de destruction directe ici : interdit pendant la desactivation du parent
        if (!Application.isPlaying) QueuePreview();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying) QueuePreview();
    }

    void QueuePreview()
    {
        UnityEditor.EditorApplication.delayCall -= RefreshPreview;
        UnityEditor.EditorApplication.delayCall += RefreshPreview;
    }

    void RefreshPreview()
    {
        if (this == null || Application.isPlaying || iconsContainer == null) return;

        // Ne detruit que les icones d'apercu, jamais un enfant pose a la main
        for (int i = iconsContainer.childCount - 1; i >= 0; i--)
        {
            GameObject child = iconsContainer.GetChild(i).gameObject;
            if ((child.hideFlags & HideFlags.DontSaveInEditor) != 0)
                DestroyImmediate(child);
        }
        if (!isActiveAndEnabled || iconPrefab == null) return;

        for (int n = 0; n < previewIconCount; n++)
        {
            GameObject iconGO = Instantiate(iconPrefab, iconsContainer);
            iconGO.name = "PreviewIcon_" + n;
            foreach (Transform t in iconGO.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.hideFlags = HideFlags.DontSave;
                foreach (Component c in t.GetComponents<Component>())
                    c.hideFlags = HideFlags.DontSave;
            }
        }
    }
#endif

    public void SpawnIconsForEnemies()
    {
        List<EnemyHealth> validEnemies = null;
        if (CleaningBonusManager.Instance != null)
            validEnemies = CleaningBonusManager.Instance.GetRegisteredEnemies();
        spawnCoroutine = StartCoroutine(SpawnIconsCoroutine(validEnemies));
    }

    /// <summary>
    /// Remplit la barre (boss de Rush Hour IV, nouvelle phase) avec les ennemis vivants presents dans la
    /// scene, sans passer par la liste du CleaningBonusManager (figee au lancement). Memes exclusions que
    /// lui (BrightEyes). A appeler sur une barre videe (ClearAllIcons). Les autres niveaux ne l'appellent pas.
    /// </summary>
    [ContextMenu("Remplir la barre")]
    public void SpawnIconsForLivingEnemies()
    {
        List<EnemyHealth> living = new List<EnemyHealth>();
        foreach (EnemyHealth enemy in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.InstanceID))
        {
            if (enemy == null || enemy.IsDead()) continue;
            if (enemy.GetComponent<BrightEyesController>() != null) continue;
            living.Add(enemy);
        }

        if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);
        spawnCoroutine = StartCoroutine(SpawnIconsCoroutine(living));
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
    private IEnumerator SpawnIconsCoroutine(List<EnemyHealth> validEnemies)
    {
        if (validEnemies == null || validEnemies.Count == 0)
        {
            Debug.LogWarning("[EnemyIconsUI] Aucun ennemi a afficher.");
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