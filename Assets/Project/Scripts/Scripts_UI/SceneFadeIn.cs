using UnityEngine;
using System.Collections;

public class SceneFadeIn : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeDuration = 10f;

    void Awake()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }
    }

    void Start()
    {
        if (canvasGroup != null)
            StartCoroutine(FadeInRoutine());
    }

    IEnumerator FadeInRoutine()
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
    }
}