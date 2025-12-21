using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MarqueeLightController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform bulbsContainer;

    [Header("Pattern Settings")]
    [SerializeField] private float patternSpeed = 0.1f;
    [SerializeField] private bool playOnStart = true;

    private List<MarqueeLightBulb> bulbs = new List<MarqueeLightBulb>();
    private Coroutine patternCoroutine;

    void Start()
    {
        StartCoroutine(DelayedStart());
    }

    IEnumerator DelayedStart()
    {
        yield return new WaitForSeconds(0.1f); // Laisser le Spawner créer le container

        CollectBulbs();

        if (playOnStart)
        {
            StartChasePattern();
        }
    }

    void CollectBulbs()
    {
        // Auto-detecter BulbsContainer si pas assigne
        if (bulbsContainer == null)
        {
            bulbsContainer = transform.Find("BulbsContainer");
        }

        if (bulbsContainer == null)
        {
            Debug.LogError("[MarqueeLightController] BulbsContainer introuvable !");
            return;
        }

        // Recuperer tous les MarqueeLightBulb du container
        MarqueeLightBulb[] bulbArray = bulbsContainer.GetComponentsInChildren<MarqueeLightBulb>();
        bulbs.AddRange(bulbArray);

        Debug.Log($"[MarqueeLightController] {bulbs.Count} bulbs collectes");
    }

    public void StartChasePattern()
    {
        if (patternCoroutine != null)
            StopCoroutine(patternCoroutine);

        patternCoroutine = StartCoroutine(ChasePatternCoroutine());
    }

    public void StopPattern()
    {
        if (patternCoroutine != null)
        {
            StopCoroutine(patternCoroutine);
            patternCoroutine = null;
        }

        // Eteindre toutes les bulbs
        foreach (var bulb in bulbs)
        {
            bulb.TurnOff();
        }
    }

    IEnumerator ChasePatternCoroutine()
    {
        int currentIndex = 0;

        while (true)
        {
            // Eteindre toutes
            foreach (var bulb in bulbs)
            {
                bulb.TurnOff();
            }

            // Allumer la courante
            if (currentIndex < bulbs.Count)
            {
                bulbs[currentIndex].TurnOn();
            }

            // Passer a la suivante
            currentIndex = (currentIndex + 1) % bulbs.Count;

            yield return new WaitForSeconds(patternSpeed);
        }
    }
}