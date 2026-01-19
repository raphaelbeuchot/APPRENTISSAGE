using UnityEngine;
using UnityEngine.Rendering;

public class RedLightVolumeController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Volume redLightVolume;

    [Header("Transition Settings")]
    [SerializeField] private float fadeInDuration = 0.5f;
    [SerializeField] private float fadeOutDuration = 1f;

    private Coroutine currentFadeCoroutine;

    void Start()
    {
        if (redLightVolume != null)
        {
            redLightVolume.weight = 0f;
        }
    }

    public void FadeIn()
    {
        if (currentFadeCoroutine != null)
            StopCoroutine(currentFadeCoroutine);

        currentFadeCoroutine = StartCoroutine(FadeCoroutine(1f, fadeInDuration));
    }

    public void FadeOut()
    {
        if (currentFadeCoroutine != null)
            StopCoroutine(currentFadeCoroutine);

        currentFadeCoroutine = StartCoroutine(FadeCoroutine(0f, fadeOutDuration));
    }

    private System.Collections.IEnumerator FadeCoroutine(float targetWeight, float duration)
    {
        if (redLightVolume == null) yield break;

        float startWeight = redLightVolume.weight;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            redLightVolume.weight = Mathf.Lerp(startWeight, targetWeight, t);
            yield return null;
        }

        redLightVolume.weight = targetWeight;
    }
}