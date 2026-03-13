using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MarqueeLightController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform bulbsContainer;

    [Header("Alert Settings")]
    [SerializeField] private int alertFlashCount = 3;

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
            }
        }


        foreach (var circle in circles)
        {
            foreach (var bulb in circle)
            {
                bulb.SetGreenLightMode();
            }
        }
    }

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

    IEnumerator GreenLightCoroutine()
    {
        Debug.Log("[MarqueeLights] GreenLight pattern started");

        foreach (var circle in circles)
        {
            foreach (var bulb in circle)
            {
                bulb.SetGreenLightMode();
            }
        }

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
            foreach (var circle in circles)
            {
                foreach (var bulb in circle)
                {
                    bulb.SetAlertMode();
                }
            }
            yield return new WaitForSeconds(flashInterval * 0.5f);

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

        foreach (var circle in circles)
        {
            foreach (var bulb in circle)
            {
                bulb.SetRedLightMode();
            }
        }

        while (true)
        {
            yield return null;
        }
    }

    IEnumerator ReleaseFadeCoroutine(float duration)
    {
        Debug.Log($"[MarqueeLights] Release fade started - {duration}s");

        foreach (var circle in circles)
        {
            foreach (var bulb in circle)
            {
                bulb.SetGreenLightMode();
            }
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float linear = elapsed / duration;
            float t = 1f - Mathf.Pow(1f - linear, 3f);

            foreach (var circle in circles)
            {
                foreach (var bulb in circle)
                {
                    float emissionIntensity = Mathf.Lerp(bulb.GetRedEmissionIntensity(), bulb.GetGreenEmissionIntensity(), t);
                    bulb.SetEmissionIntensity(emissionIntensity);
                }
            }

            yield return null;
        }

        foreach (var circle in circles)
        {
            foreach (var bulb in circle)
            {
                bulb.SetEmissionIntensity(bulb.GetGreenEmissionIntensity());
            }
        }

        while (true)
        {
            yield return null;
        }
    }

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