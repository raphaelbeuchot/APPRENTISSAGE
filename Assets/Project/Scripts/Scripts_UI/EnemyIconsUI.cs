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

    private List<EnemyIconDisplay> enemyIcons = new List<EnemyIconDisplay>();
    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    public void SpawnIconsForEnemies()
    {
        StartCoroutine(SpawnIconsCoroutine());
    }

    private IEnumerator SpawnIconsCoroutine()
    {
        GameManager gameManager = FindObjectOfType<GameManager>();
        if (gameManager == null)
        {
            Debug.LogError("[EnemyIconsUI] GameManager introuvable !");
            yield break;
        }

        // Récupérer tous les EnemyHealth (sauf BrightEyes)
        EnemyHealth[] allEnemies = FindObjectsOfType<EnemyHealth>();
        List<EnemyHealth> validEnemies = new List<EnemyHealth>();

        foreach (EnemyHealth enemy in allEnemies)
        {
            if (enemy.GetComponent<BrightEyesController>() == null)
            {
                validEnemies.Add(enemy);
            }
        }

        // Créer une icône pour chaque ennemi avec délai
        foreach (EnemyHealth enemy in validEnemies)
        {
            GameObject iconGO = Instantiate(iconPrefab, iconsContainer);
            EnemyIconDisplay iconDisplay = iconGO.GetComponent<EnemyIconDisplay>();

            if (iconDisplay != null)
            {
                iconDisplay.Initialize(enemy, this);
                enemyIcons.Add(iconDisplay);
            }

            // Son de pop
            if (audioSource != null && iconPopSound != null)
            {
                audioSource.PlayOneShot(iconPopSound);
            }

            yield return new WaitForSeconds(delayBetweenIcons);
        }
    }

    public void FlashIconRed(EnemyIconDisplay icon)
    {
        StartCoroutine(FlashRedCoroutine(icon));
    }

    private IEnumerator FlashRedCoroutine(EnemyIconDisplay icon)
    {
        if (icon == null) yield break;

        Image iconImage = icon.GetAliveImage();
        if (iconImage == null) yield break;

        Color originalColor = iconImage.color;
        float elapsed = 0f;

        // Fade vers rouge
        while (elapsed < flashDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (flashDuration / 2f);
            iconImage.color = Color.Lerp(originalColor, Color.red, t);
            yield return null;
        }

        elapsed = 0f;

        // Fade retour couleur originale
        while (elapsed < flashDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (flashDuration / 2f);
            iconImage.color = Color.Lerp(Color.red, originalColor, t);
            yield return null;
        }

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
    public void HideAllIcons()
    {
        gameObject.SetActive(false);
    }
}