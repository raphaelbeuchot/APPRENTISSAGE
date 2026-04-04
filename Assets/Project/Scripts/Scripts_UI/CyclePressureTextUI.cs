using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CyclePressureTextUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SentinelCycleManager cycleManager;
    [SerializeField] private RectMask2D fillMask;
    [SerializeField] private TextMeshProUGUI filledText;

    [Header("Colors")]
    [SerializeField] private Color colorLow = Color.green;
    [SerializeField] private Color colorMid = Color.yellow;
    [SerializeField] private Color colorHigh = Color.red;

    private float fullWidth = 0f;

    void Start()
    {
        if (fillMask != null)
            fullWidth = fillMask.rectTransform.rect.width;
    }

    void Update()
    {
        if (cycleManager == null || fillMask == null || filledText == null) return;

        if (fullWidth == 0f)
            fullWidth = fillMask.rectTransform.rect.width;

        float factor = cycleManager.GetCurrentPressureFactor();

        float maskRight = fullWidth * (1f - factor);
        fillMask.padding = new Vector4(0, 0, maskRight, 0);

        filledText.color = GetGaugeColor(factor);
    }

    private Color GetGaugeColor(float t)
    {
        if (t < 0.5f)
            return Color.Lerp(colorLow, colorMid, t * 2f);
        else
            return Color.Lerp(colorMid, colorHigh, (t - 0.5f) * 2f);
    }
}