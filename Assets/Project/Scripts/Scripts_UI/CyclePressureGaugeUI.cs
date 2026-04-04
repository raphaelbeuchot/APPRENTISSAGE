using UnityEngine;
using UnityEngine.UI;

public class CyclePressureGaugeUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SentinelCycleManager cycleManager;
    [SerializeField] private Image gaugeImage;

    [Header("Colors")]
    [SerializeField] private Color colorLow = Color.green;
    [SerializeField] private Color colorMid = Color.yellow;
    [SerializeField] private Color colorHigh = Color.red;

    void Update()
    {
        if (cycleManager == null || gaugeImage == null) return;

        float factor = cycleManager.GetCurrentPressureFactor();
        gaugeImage.fillAmount = factor;
        gaugeImage.color = GetGaugeColor(factor);
    }

    private Color GetGaugeColor(float t)
    {
        if (t < 0.5f)
            return Color.Lerp(colorLow, colorMid, t * 2f);
        else
            return Color.Lerp(colorMid, colorHigh, (t - 0.5f) * 2f);
    }
}