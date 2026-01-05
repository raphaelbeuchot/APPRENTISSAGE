using UnityEngine;
using UnityEngine.UI;

public class StunTimerUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image fillImage;

    private float maxTheoreticalTime;

    public void Initialize(float maxTime)
    {
        maxTheoreticalTime = maxTime;
        if (fillImage != null)
        {
            fillImage.fillAmount = 0f;
        }
    }

    public void UpdateTimer(float currentTime)
    {
        if (fillImage != null && maxTheoreticalTime > 0)
        {
            fillImage.fillAmount = Mathf.Clamp01(currentTime / maxTheoreticalTime);
        }
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}