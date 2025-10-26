using UnityEngine;

public class SentinelTarget : MonoBehaviour
{
    [HideInInspector] public bool wasInLOS = false;
    [HideInInspector] public float lostLOSTime = 0f;
    [HideInInspector] public bool isTracked = false;

    private TargetCircle targetCircle;

    void Awake()
    {
        targetCircle = GetComponent<TargetCircle>();
        if (targetCircle == null)
        {
            targetCircle = gameObject.AddComponent<TargetCircle>();
        }
    }

    /*public void ShowCircle()
    {
        if (targetCircle != null)
            targetCircle.ShowRedCircle();
    }*/

    public void HideCircle()
    {
        if (targetCircle != null)
            targetCircle.HideCircle();
    }

    public void FlashWhite()
    {
        if (targetCircle != null)
            targetCircle.FlashWhite();
    }

    public void FadeOutCircle(float duration)
    {
        if (targetCircle != null)
            StartCoroutine(targetCircle.FadeOut(duration));
    }
}