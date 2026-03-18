using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class TutorialPromptUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Image backgroundPanel;
    [SerializeField] private Image tutorialImage;
    [SerializeField] private TextMeshProUGUI anyButtonText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Settings")]
    [SerializeField] private float fadeDuration = 0.3f;

    [Header("Reset on Level Start")]
    [SerializeField] private bool resetTipsOnStart = true;
    [SerializeField]
    private string[] tipIDsToReset = new string[]
    {
        "Tuto_Sprint",
        "Tuto_Climb",
        "Tuto_Crouch",
        "Tuto_CameraSafety"
    };

    private bool isVisible = false;
    public bool IsVisible => isVisible;
    private Coroutine waitCoroutine;

    void Start()
    {
        // Reset des tips si demande
        if (resetTipsOnStart)
        {
            ResetAllTips();
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        isVisible = false;
    }
    void OnDestroy()
    {
        // S'assurer que le timeScale est restaure
        Time.timeScale = 1f;
    }

    void OnApplicationQuit()
    {
        // Pareil si on quitte en playmode
        Time.timeScale = 1f;
    }
    private void ResetAllTips()
    {
        Debug.Log("[TutorialPromptUI] Reset de tous les tips tutorial");

        foreach (string tipID in tipIDsToReset)
        {
            if (PlayerPrefs.HasKey(tipID))
            {
                PlayerPrefs.DeleteKey(tipID);
            }
        }

        PlayerPrefs.Save();
    }

    public void Show(Sprite tutorialSprite)
    {
        if (isVisible) return;

        Debug.Log("[TutorialPromptUI] Affichage tutorial");

        if (tutorialImage != null && tutorialSprite != null)
        {
            tutorialImage.sprite = tutorialSprite;
        }

        isVisible = true;

        Time.timeScale = 0f;

        StartCoroutine(FadeCoroutine(1f, () =>
        {
            if (waitCoroutine != null)
                StopCoroutine(waitCoroutine);
            waitCoroutine = StartCoroutine(WaitForAnyInput());
        }));
    }

    public void Hide()
    {
        if (!isVisible) return;

        Debug.Log("[TutorialPromptUI] Masquage tutorial");

        if (waitCoroutine != null)
        {
            StopCoroutine(waitCoroutine);
            waitCoroutine = null;
        }

        StartCoroutine(FadeCoroutine(0f, () =>
        {
            Time.timeScale = 1f;
            isVisible = false;
        }));
    }

    private IEnumerator WaitForAnyInput()
    {
        Debug.Log("[TutorialPromptUI] Attente input joueur...");

        yield return null;

        while (true)
        {
            if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                Debug.Log("[TutorialPromptUI] Input detecte (clavier/souris)");
                Hide();
                yield break;
            }

            if (PlayerInputManager.Instance != null)
            {
                if (PlayerInputManager.Instance.SprintPressed ||
                    PlayerInputManager.Instance.SprayAttackPressed ||
                    PlayerInputManager.Instance.BroomAttackPressed ||
                    PlayerInputManager.Instance.InteractPressed ||
                    PlayerInputManager.Instance.ReloadPressed ||
                    PlayerInputManager.Instance.CrouchPressed ||
                    PlayerInputManager.Instance.ToggleCameraViewPressed ||
                    PlayerInputManager.Instance.CancelPressed ||
                    PlayerInputManager.Instance.PausePressed)
                {
                    Debug.Log("[TutorialPromptUI] Input detecte (manette)");
                    Hide();
                    yield break;
                }
            }

            yield return null;
        }
    }

    private IEnumerator FadeCoroutine(float targetAlpha, System.Action onComplete = null)
    {
        if (canvasGroup == null) yield break;

        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / fadeDuration;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        canvasGroup.blocksRaycasts = (targetAlpha > 0f);

        onComplete?.Invoke();
    }


}