using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class CyclePressureGaugeUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SentinelCycleManager cycleManager;
    [SerializeField] private Sprite pipSprite;

    [Header("Config")]
    [SerializeField] private int pipCount = 11;
    [SerializeField] private float pipDiameter = 25f;
    [SerializeField] private Color colorInactive = Color.gray;
    [SerializeField] private Color[] pipColors;

    private List<Image> pips = new List<Image>();

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
        float factor = cycleManager.GetCurrentPressureFactor();
        for (int i = 0; i < pips.Count; i++)
        {
            bool active = factor > (float)i / pipCount;
            if (active)
                pips[i].color = (pipColors != null && i < pipColors.Length) ? pipColors[i] : Color.white;
            else
                pips[i].color = colorInactive;
        }
    }
}