using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneFadeOut : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeDuration = 1f;

    public void FadeToScene(int sceneIndex)
    {
        StartCoroutine(FadeOutRoutine(sceneIndex));
    }

    public void FadeToScene(string sceneName)
    {
        int index = SceneUtility.GetBuildIndexByScenePath(sceneName);
        StartCoroutine(FadeOutRoutine(index));
    }

    IEnumerator FadeOutRoutine(int targetSceneIndex)
    {
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }

        LoadingScreenManager.TargetSceneIndex = targetSceneIndex;
        SceneManager.LoadScene("LoadingScreen");
    }
}