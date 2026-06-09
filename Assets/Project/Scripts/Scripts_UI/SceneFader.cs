using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System;
using System.Collections;

public class SceneFader : MonoBehaviour
{
    public static SceneFader Instance { get; private set; }

    [SerializeField] private float fadeDuration = 0.4f;

    public event Action OnFadeOutComplete;

    public bool IsFading => isFading;

    private Image fadeImage;
    private bool isFading = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Creer le canvas et l'image en code
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();

        GameObject imgGO = new GameObject("FadeImage");
        imgGO.transform.SetParent(transform, false);

        fadeImage = imgGO.AddComponent<Image>();
        fadeImage.color = new Color(0f, 0f, 0f, 0f);

        RectTransform rt = fadeImage.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        fadeImage.raycastTarget = false;
    }

    public void FadeToScene(int sceneIndex)
    {
        if (isFading) return;
        StartCoroutine(FadeToSceneCoroutine(sceneIndex));
    }

    /// <summary>
    /// Charge la scene en async (allowSceneActivation=false), l'active quand prete,
    /// puis fade out. Fire OnFadeOutComplete quand la transition est terminee.
    /// </summary>
    public void FadeToSceneAsync(int sceneIndex)
    {
        if (isFading) return;
        StartCoroutine(FadeToSceneAsyncCoroutine(sceneIndex));
    }

    private IEnumerator FadeToSceneAsyncCoroutine(int sceneIndex)
    {
        isFading = true;

        yield return StartCoroutine(Fade(0f, 1f));

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneIndex);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
            yield return null;

        op.allowSceneActivation = true;

        while (!op.isDone)
            yield return null;

        yield return null; // frame pour que Start() s'execute

        yield return StartCoroutine(Fade(1f, 0f));

        isFading = false;
        OnFadeOutComplete?.Invoke();
    }

    public void FadeToSceneWithLoadingScreen(int sceneIndex)
    {
        if (isFading) return;
        LoadingScreenManager.TargetSceneIndex = sceneIndex;
        FadeToScene(1);
    }

    private IEnumerator FadeToSceneCoroutine(int sceneIndex)
    {
        isFading = true;

        // Fade in (transparent -> noir)
        yield return StartCoroutine(Fade(0f, 1f));

        SceneManager.LoadScene(sceneIndex);

        // Attendre que la scene soit chargee
        yield return null;
        yield return null;

        // Fade out (noir -> transparent)
        yield return StartCoroutine(Fade(1f, 0f));

        isFading = false;
    }

    private IEnumerator Fade(float from, float to)
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            fadeImage.color = new Color(0f, 0f, 0f, alpha);
            yield return null;
        }
        fadeImage.color = new Color(0f, 0f, 0f, to);
    }
}