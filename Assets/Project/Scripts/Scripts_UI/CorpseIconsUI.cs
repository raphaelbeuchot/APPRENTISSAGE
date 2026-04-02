using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class CorpseIconsUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject iconPrefab;
    [SerializeField] private Transform iconsContainer;
    [SerializeField] private AudioClip iconPopSound;
    [Header("Settings")]
    [SerializeField] private float delayBetweenIcons = 0.5f;
    private List<CorpseIconDisplay> corpseIcons = new List<CorpseIconDisplay>();
    private AudioSource audioSource;
    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }
    public void SpawnIconsForCorpses()
    {
        StartCoroutine(SpawnIconsCoroutine());
    }
    private IEnumerator SpawnIconsCoroutine()
    {
        CorpsePitHandler[] allCorpses = FindObjectsByType<CorpsePitHandler>(FindObjectsSortMode.None);
        foreach (CorpsePitHandler corpse in allCorpses)
        {
            GameObject iconGO = Instantiate(iconPrefab, iconsContainer);
            CorpseIconDisplay iconDisplay = iconGO.GetComponent<CorpseIconDisplay>();
            if (iconDisplay != null)
            {
                iconDisplay.Initialize(corpse);
                corpseIcons.Add(iconDisplay);
            }
            if (audioSource != null && iconPopSound != null)
                audioSource.PlayOneShot(iconPopSound);
            yield return new WaitForSeconds(delayBetweenIcons);
        }
    }
    public void SetIconCleaned(CorpsePitHandler corpse)
    {
        foreach (CorpseIconDisplay icon in corpseIcons)
        {
            if (icon != null && icon.GetTrackedCorpse() == corpse)
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