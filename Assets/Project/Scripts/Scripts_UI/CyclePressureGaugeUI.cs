using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
public class CyclePressureGaugeUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SentinelCycleManager cycleManager;
    [SerializeField] private Sprite pipSprite;

    [Header("Config")]
    [SerializeField] private int pipCount = 10;
    [SerializeField] private float radius = 80f;
    [SerializeField] private float releaseFillDuration = 0.5f;

    [Header("Pip Diameter")]
    [SerializeField] private float pipDiameterMin = 15f;
    [SerializeField] private float pipDiameterMax = 35f;
    [SerializeField] private float diameterLerpSpeed = 3f;

    [SerializeField] private float radiusMinMultiplier = 0.7f;

    [Header("Pressure Percentage")]
    [SerializeField] private TextMeshProUGUI pressurePercentageText;

    [Header("Kill Flash")]
    [SerializeField] private float killFlashDuration = 0.3f;

    [Header("Colors")]
    [SerializeField] private Color colorInactive = Color.gray;
    [SerializeField] private Color colorActiveCalm = Color.green;
    [SerializeField] private Color colorActiveTense = Color.red;

    private List<Image> pips = new List<Image>();
    private float releaseTimer = 0f;
    private SentinelCycleManager.GameState lastState;
    private float currentDiameter;
    private bool isFlashing = false;
    private float currentRadius;


    void Start()
    {

        currentRadius = radius; 
        currentDiameter = pipDiameterMin;
        BuildPips();
        if (pressurePercentageText == null)
            pressurePercentageText = GetComponentInChildren<TextMeshProUGUI>();
    }

    void OnEnable()
    {
        EnemyHealth.OnAnyEnemyDeath += OnEnemyKilled;
    }

    void OnDisable()
    {
        EnemyHealth.OnAnyEnemyDeath -= OnEnemyKilled;
    }

    void BuildPips()
    {
        if (pipSprite == null) return;
        pips.Clear();
        for (int i = 0; i < pipCount; i++)
        {
            GameObject go = new GameObject("Pip_" + i);
            go.transform.SetParent(transform, false);
            Image img = go.AddComponent<Image>();
            img.sprite = pipSprite;
            img.type = Image.Type.Simple;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(currentDiameter, currentDiameter);
            float t = (float)i / pipCount;
            float angle = Mathf.PI * 0.5f - t * Mathf.PI * 2f;
            rt.anchoredPosition = new Vector2(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle));
            pips.Add(img);
        }
    }

    void OnEnemyKilled()
    {
       //if (isFlashing) return;
        //StartCoroutine(KillFlashCoroutine());
    }

    private IEnumerator KillFlashCoroutine()
    {
        isFlashing = true;

        for (int i = 0; i < pips.Count; i++)
            pips[i].color = Color.white;

        yield return new WaitForSeconds(killFlashDuration);

        isFlashing = false;
    }

    void Update()
    {
        if (cycleManager == null || pips.Count == 0) return;

        if (OptionsManager.Instance != null && !OptionsManager.Instance.gaugeVisible)
        {
            for (int i = 0; i < pips.Count; i++)
                pips[i].color = Color.clear;
            return;
        }

        if (!cycleManager.IsGameStarted())
        {
            for (int i = 0; i < pips.Count; i++)
                pips[i].color = colorInactive;
            return;
        }

        float pressureFactor = cycleManager.GetCurrentPressureFactor();
        float targetDiameter = Mathf.Lerp(pipDiameterMin, pipDiameterMax, pressureFactor);
        currentDiameter = Mathf.Lerp(currentDiameter, targetDiameter, Time.deltaTime * diameterLerpSpeed);

        if (pressurePercentageText != null)
            pressurePercentageText.text = "Stress  " + Mathf.RoundToInt(pressureFactor * 100f) + "%";

        for (int i = 0; i < pips.Count; i++)
        {
            RectTransform rt = pips[i].rectTransform;
            rt.sizeDelta = new Vector2(currentDiameter, currentDiameter);
        }
        float targetRadius = radius * Mathf.Lerp(1f, radiusMinMultiplier, pressureFactor);
        currentRadius = Mathf.Lerp(currentRadius, targetRadius, Time.deltaTime * diameterLerpSpeed);

        for (int i = 0; i < pips.Count; i++)
        {
            float t = (float)i / pipCount;
            float angle = Mathf.PI * 0.5f - t * Mathf.PI * 2f;
            pips[i].rectTransform.anchoredPosition = new Vector2(currentRadius * Mathf.Cos(angle), currentRadius * Mathf.Sin(angle));
        }

        if (isFlashing) return;

        SentinelCycleManager.GameState state = cycleManager.GetCurrentState();
        float progress = cycleManager.GetCycleProgress();

        if (state == SentinelCycleManager.GameState.Release && lastState != SentinelCycleManager.GameState.Release)
            releaseTimer = 0f;
        if (state == SentinelCycleManager.GameState.Release)
            releaseTimer += Time.deltaTime;
        lastState = state;

        float fillLevel;
        Color activeColor;

        switch (state)
        {
            case SentinelCycleManager.GameState.GreenLight:
                fillLevel = 1f - progress;
                activeColor = colorActiveCalm;
                break;
            case SentinelCycleManager.GameState.Alert:
                fillLevel = progress;
                activeColor = colorActiveTense;
                break;
            case SentinelCycleManager.GameState.RedLight:
                fillLevel = 1f - progress;
                activeColor = colorActiveTense;
                break;
            case SentinelCycleManager.GameState.Release:
                fillLevel = Mathf.Clamp01(releaseTimer / releaseFillDuration);
                activeColor = colorActiveCalm;
                break;
            default:
                fillLevel = 0f;
                activeColor = colorInactive;
                break;
        }

        for (int i = 0; i < pips.Count; i++)
        {
            bool active = fillLevel > (float)i / pipCount;
            pips[i].color = active ? activeColor : colorInactive;
        }
    }
}