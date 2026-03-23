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

    // Multi-pages
    private Sprite[] currentPages;
    private int currentPageIndex;
    private System.Action onAllPagesClosed;

    void Start()
    {
        if (resetTipsOnStart)
            ResetAllTips();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        isVisible = false;
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
    }

    void OnApplicationQuit()
    {
        Time.timeScale = 1f;
    }

    private void ResetAllTips()
    {
        Debug.Log("[TutorialPromptUI] Reset de tous les tips tutorial");
        foreach (string tipID in tipIDsToReset)
        {
            if (PlayerPrefs.HasKey(tipID))
                PlayerPrefs.DeleteKey(tipID);
        }
        PlayerPrefs.Save();
    }

    // --- API existante, inchangee ---
    public void Show(Sprite tutorialSprite)
    {
        Show(new Sprite[] { tutorialSprite }, null);
    }

    // --- Nouvelle API multi-pages ---
    public void Show(Sprite[] pages, System.Action onComplete = null)
    {
        if (isVisible) return;
        if (pages == null || pages.Length == 0) return;

        currentPages = pages;
        currentPageIndex = 0;
        onAllPagesClosed = onComplete;

        ApplyCurrentPage();

        isVisible = true;
        Time.timeScale = 0f;

        StartCoroutine(FadeCoroutine(1f, () =>
        {
            if (waitCoroutine != null)
                StopCoroutine(waitCoroutine);
            waitCoroutine = StartCoroutine(WaitForAnyInput());
        }));
    }

    private void ApplyCurrentPage()
    {
        if (tutorialImage != null && currentPages[currentPageIndex] != null)
            tutorialImage.sprite = currentPages[currentPageIndex];
    }

    public void Hide()
    {
        if (!isVisible) return;

        if (waitCoroutine != null)
        {
            StopCoroutine(waitCoroutine);
            waitCoroutine = null;
        }

        StartCoroutine(FadeCoroutine(0f, () =>
        {
            Time.timeScale = 1f;
            isVisible = false;
            onAllPagesClosed?.Invoke();
            onAllPagesClosed = null;
            currentPages = null;
        }));
    }

    private IEnumerator WaitForAnyInput()
    {
        yield return null;

        while (true)
        {
            bool inputDetected = false;

            if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
                inputDetected = true;

            if (!inputDetected && PlayerInputManager.Instance != null)
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
                    inputDetected = true;
                }
            }

            if (inputDetected)
            {
                // Page suivante ou fermeture
                if (currentPages != null && currentPageIndex < currentPages.Length - 1)
                {
                    currentPageIndex++;
                    ApplyCurrentPage();
                    // Petit delai pour eviter de skipper deux pages d'un coup
                    yield return new WaitForSecondsRealtime(0.2f);
                }
                else
                {
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