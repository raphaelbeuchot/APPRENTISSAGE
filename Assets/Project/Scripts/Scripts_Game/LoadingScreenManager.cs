using UnityEngine;
using TMPro;
using UnityEngine.UI;

using UnityEngine.SceneManagement;
using System.Collections;

public class LoadingScreenManager : MonoBehaviour
{
    public static int TargetSceneIndex = -1;

    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private Image progressBar;

    void Start()
    {
        if (TargetSceneIndex < 0)
        {
            Debug.LogError("[LoadingScreen] Aucun index de scene cible defini !");
            return;
        }

        StartCoroutine(LoadTargetScene());
    }

    IEnumerator LoadTargetScene()
    {
        yield return null;

        AsyncOperation op = SceneManager.LoadSceneAsync(TargetSceneIndex);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            if (progressBar != null)
                progressBar.fillAmount = op.progress;

            if (loadingText != null)
                loadingText.text = "Loading... " + Mathf.RoundToInt(op.progress * 100f) + "%";

            yield return null;
        }

        if (progressBar != null)
            progressBar.fillAmount = 1f;

        if (loadingText != null)
            loadingText.text = "Loading... 100%";

        // Petit delai pour que le joueur voie le 100%
        yield return new WaitForSeconds(0.3f);

        op.allowSceneActivation = true;
    }
}