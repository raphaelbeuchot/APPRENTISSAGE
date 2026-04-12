using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class CyclePressureGaugeUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SentinelCycleManager cycleManager;
    [SerializeField] private Sprite pipSprite;

    [Header("Config")]
    [SerializeField] private int pipCount = 10;
    [SerializeField] private float pipDiameter = 25f;
    [SerializeField] private float releaseFillDuration = 0.5f;

    [Header("Colors")]
    [SerializeField] private Color colorInactive = Color.gray;
    [SerializeField] private Color colorActiveCalm = Color.green;
    [SerializeField] private Color colorActiveTense = Color.red;

    private List<Image> pips = new List<Image>();
    private float releaseTimer = 0f;
    private SentinelCycleManager.GameState lastState;

    void Start()
    {
        BuildPips();
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
            rt.sizeDelta = new Vector2(pipDiameter, pipDiameter);
            pips.Add(img);
        }
        for (int i = 0; i < pips.Count; i++)
            pips[i].transform.SetSiblingIndex(pips.Count - 1 - i);
    }

    void Update()
    {
        if (cycleManager == null || pips.Count == 0) return;
        if (OptionsManager.Instance != null && !OptionsManager.Instance.gaugeVisible)
        {
            for (int i = 0; i < pips.Count; i++)
                pips[i].color = colorInactive;
            return;
        }

        if (!cycleManager.IsGameStarted())
        {
            for (int i = 0; i < pips.Count; i++)
                pips[i].color = colorInactive;
            return;
        }

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