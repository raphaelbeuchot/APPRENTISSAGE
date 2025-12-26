using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MarqueeLightController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform bulbsContainer;

    [Header("GreenLight Settings")]
    [SerializeField] private float greenLightInterval = 2f;
    [SerializeField] private float greenLightRatio = 0.25f;

    [Header("Alert Settings")]
    [SerializeField] private int alertFlashCount = 5;

    [Header("RedLight Settings")]
    [SerializeField] private int redLightChaseLength = 5;
    [SerializeField] private float redLightRotationDuration = 6f;
    [SerializeField] private float redLightFadeDuration = 1f;
    [SerializeField] private int redLightOffsetPerCircle = 2;

    private List<List<MarqueeLightBulb>> circles = new List<List<MarqueeLightBulb>>();
    private Coroutine currentPatternCoroutine;

    void Start()
    {
        StartCoroutine(DelayedStart());
    }

    IEnumerator DelayedStart()
    {
        yield return new WaitForSeconds(0.5f);
        CollectBulbsByCircle();
    }

    void CollectBulbsByCircle()
    {
        bulbsContainer = transform.Find("CirclesRuntimeContainer");

        if (bulbsContainer == null)
        {
            Debug.LogError("[MarqueeLightController] CirclesRuntimeContainer introuvable !");
            return;
        }

        circles.Clear();

        for (int i = 0; i < bulbsContainer.childCount; i++)
        {
            Transform circleTransform = bulbsContainer.GetChild(i);
            MarqueeLightBulb[] circleBulbs = circleTransform.GetComponentsInChildren<MarqueeLightBulb>();

            if (circleBulbs.Length > 0)
            {
                List<MarqueeLightBulb> circleList = new List<MarqueeLightBulb>(circleBulbs);
                circles.Add(circleList);
                Debug.Log($"[MarqueeLightController] Cercle {i} : {circleBulbs.Length} bulbs");
            }
        }

        Debug.Log($"[MarqueeLightController] {circles.Count} cercles collectes");
    }

    // ========== PUBLIC METHODS ==========

    public void StartGreenLightPattern()
    {
        StopCurrentPattern();
        currentPatternCoroutine = StartCoroutine(GreenLightCoroutine());
    }

    public void StartAlertPattern(float alertDuration)
    {
        StopCurrentPattern();
        currentPatternCoroutine = StartCoroutine(AlertCoroutine(alertDuration));
    }

    public void StartRedLightPattern()
    {
        StopCurrentPattern();
        currentPatternCoroutine = StartCoroutine(RedLightCoroutine());
    }

    public void StartReleaseFade(float duration)
    {
        StopCurrentPattern();
        currentPatternCoroutine = StartCoroutine(ReleaseFadeCoroutine(duration));
    }

    public void StopAll()
    {
        StopCurrentPattern();
        TurnOffAllBulbs();
    }

    // ========== PATTERN COROUTINES ==========

    IEnumerator GreenLightCoroutine()
    {
        Debug.Log("[MarqueeLights] GreenLight pattern started");

        // Allumer toutes en mode GreenLight
        foreach (var circle in circles)
        {
            foreach (var bulb in circle)
            {
                bulb.SetGreenLightMode();
            }
        }

        // Rester allume jusqu'a changement
        while (true)
        {
            yield return null;
        }
    }

    IEnumerator AlertCoroutine(float totalDuration)
    {
        Debug.Log($"[MarqueeLights] Alert pattern - duration: {totalDuration}s, {alertFlashCount} flashes");

        float flashInterval = totalDuration / alertFlashCount;

        for (int i = 0; i < alertFlashCount; i++)
        {
            // Allumer en mode Alert
            foreach (var circle in circles)
            {
                foreach (var bulb in circle)
                {
                    bulb.SetAlertMode();
                }
            }
            yield return new WaitForSeconds(flashInterval * 0.5f);

            // Eteindre
            TurnOffAllBulbs();
            yield return new WaitForSeconds(flashInterval * 0.5f);
        }

        Debug.Log("[MarqueeLights] Alert pattern finished");
    }

    IEnumerator RedLightCoroutine()
    {
        Debug.Log("[MarqueeLights] RedLight pattern started");

        if (circles.Count == 0)
        {
            Debug.LogWarning("[MarqueeLights] No circles to animate");
            yield break;
        }

        // Allumer toutes en mode RedLight
        foreach (var circle in circles)
        {
            foreach (var bulb in circle)
            {
                bulb.SetRedLightMode();
            }
        }

        // Rester allume jusqu'a Release
        while (true)
        {
            yield return null;
        }
    }

    IEnumerator ReleaseFadeCoroutine(float duration)
    {
        Debug.Log($"[MarqueeLights] Release fade started - {duration}s");

        // Changer les materiaux tout de suite
        foreach (var circle in circles)
        {
            foreach (var bulb in circle)
            {
                bulb.SetGreenLightMode();
            }
        }

        // Fade progressif des Point Lights ET emissions RedLight -> GreenLight
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float linear = elapsed / duration;
            float t = 1f - Mathf.Pow(1f - linear, 3f); // Exposant 3 = EaseOut doux
            foreach (var circle in circles)
            {
                foreach (var bulb in circle)
                {
                    // Fade Point Light
                    float intensity = Mathf.Lerp(bulb.GetRedLightIntensity(), bulb.GetGreenLightIntensity(), t);
                    Color color = Color.Lerp(bulb.GetRedLightColor(), bulb.GetGreenLightColor(), t);
                    bulb.SetLightIntensity(intensity);
                    bulb.SetLightColor(color);

                    // Fade Emission
                    float emissionIntensity = Mathf.Lerp(bulb.GetRedEmissionIntensity(), bulb.GetGreenEmissionIntensity(), t);
                    bulb.SetEmissionIntensity(emissionIntensity);
                }
            }

            yield return null;
        }

        // S'assurer qu'on finit exactement sur GreenLight
        foreach (var circle in circles)
        {
            foreach (var bulb in circle)
            {
                bulb.SetLightIntensity(bulb.GetGreenLightIntensity());
                bulb.SetLightColor(bulb.GetGreenLightColor());
                bulb.SetEmissionIntensity(bulb.GetGreenEmissionIntensity());
            }
        }

        // Rester en GreenLight
        while (true)
        {
            yield return null;
        }
    }

    // ========== HELPERS ==========

    void StopCurrentPattern()
    {
        if (currentPatternCoroutine != null)
        {
            StopCoroutine(currentPatternCoroutine);
            currentPatternCoroutine = null;
        }
    }

    void TurnOffAllBulbs()
    {
        foreach (var circle in circles)
        {
            foreach (var bulb in circle)
            {
                bulb.TurnOff();
            }
        }
    }
}