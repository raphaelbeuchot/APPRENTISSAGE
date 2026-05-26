using UnityEngine;
using TMPro;
using Unity.Cinemachine;
using System;
using System.Collections;

public class VictoryUI : MonoBehaviour
{
    [Header("Containers")]
    [Tooltip("Container des textes — apparait apres le fade du fond")]
    [SerializeField] private RectTransform slideContainer;
    [Tooltip("CanvasGroup du fond colore — fade in sur les ghosts")]
    [SerializeField] private CanvasGroup colorBackground;
    [Tooltip("CanvasGroup du panel textes (interactabilite)")]
    [SerializeField] private CanvasGroup victoryCanvasGroup;

    [Header("Textes")]
    [SerializeField] private TextMeshProUGUI levelCompleteText;
    [SerializeField] private TextMeshProUGUI attemptsText;
    [SerializeField] private string levelCompleteMessage = "LEVEL COMPLETE";
    [SerializeField] private string attemptsPrefix = "Essais : ";

    [Header("Timings")]
    [Tooltip("Duree du fade-in du fond sur les ghosts")]
    [SerializeField] private float bgFadeDuration = 0.4f;
    [Tooltip("Duree du slide des textes (titre depuis gauche, essais depuis droite)")]
    [SerializeField] private float textSlideDuration = 0.35f;
    [SerializeField] private AnimationCurve textSlideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Sons")]
    [Tooltip("Son joue au debut du slide du titre")]
    [SerializeField] private AudioClip titleSlideSound;
    [Tooltip("Son joue au debut du slide de Essais")]
    [SerializeField] private AudioClip essaisSlideSound;
    private AudioSource audioSource;

    [Header("Wipe")]
    [SerializeField] private float wipeDuration = 0.6f;
    [SerializeField] private AnimationCurve wipeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Camera Victory")]
    [SerializeField] private CinemachineCamera cm_victory;
    [SerializeField] private int cm_victoryPriority = 20;

    public event Action OnWipeComplete;

    private bool isActive = false;
    private bool isTransitioning = false;
    private Canvas parentCanvas;

    // ============================================
    // UNITY LIFECYCLE
    // ============================================

    void Start()
    {
        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null)
            parentCanvas = FindObjectOfType<Canvas>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f;

        Hide();
    }

    void Update()
    {
        if (!isActive || isTransitioning) return;

        if (Input.anyKeyDown || Input.GetButtonDown("Submit") || Input.GetButtonDown("Jump") ||
            Input.GetButtonDown("Fire1") || Input.GetButtonDown("Fire2") || Input.GetButtonDown("Fire3"))
        {
            isTransitioning = true;
            StartCoroutine(WipeCoroutine());
        }
    }

    // ============================================
    // SHOW
    // ============================================

    public void Show(PlayerHealth playerHealth = null)
    {
        isActive = false;
        isTransitioning = false;

        // Container invisible au depart
        if (slideContainer != null)
            slideContainer.localScale = Vector3.zero;

        // Fond invisible au depart
        if (colorBackground != null)
            colorBackground.alpha = 0f;

        if (victoryCanvasGroup != null)
        {
            victoryCanvasGroup.alpha = 1f;
            victoryCanvasGroup.interactable = false;
            victoryCanvasGroup.blocksRaycasts = false;
        }

        // Peupler textes
        if (levelCompleteText != null)
            levelCompleteText.text = levelCompleteMessage;

        if (attemptsText != null)
        {
            int attempts = LevelStatsTracker.Instance != null
                ? LevelStatsTracker.Instance.AttemptCount : 1;
            attemptsText.text = attemptsPrefix + attempts;
        }

        StartCoroutine(ShowCoroutine());
    }

    private IEnumerator ShowCoroutine()
    {
        // 1. Stop sentinel
        SentinelCycleManager cycle = FindObjectOfType<SentinelCycleManager>();
        if (cycle != null) cycle.StopCycleForVictory();

        // 2. Fond fade in sur les ghosts
        if (colorBackground != null)
        {
            float e = 0f;
            while (e < bgFadeDuration)
            {
                e += Time.unscaledDeltaTime;
                colorBackground.alpha = Mathf.Clamp01(e / bgFadeDuration);
                yield return null;
            }
            colorBackground.alpha = 1f;
        }

        // 3. Kill ghosts (couverts par le fond)
        VictoryGhostBillboard[] ghosts = FindObjectsByType<VictoryGhostBillboard>(FindObjectsSortMode.None);
        foreach (var ghost in ghosts)
            if (ghost != null) Destroy(ghost.gameObject);

        // 4. Rendre le container visible (scale 1), textes hors-ecran
        if (slideContainer != null)
            slideContainer.localScale = Vector3.one;

        RectTransform titleRT  = levelCompleteText != null ? levelCompleteText.GetComponent<RectTransform>() : null;
        RectTransform essaisRT = attemptsText      != null ? attemptsText.GetComponent<RectTransform>()      : null;

        // Positions normales (definies dans le layout)
        Vector2 titleNormal  = titleRT  != null ? titleRT.anchoredPosition  : Vector2.zero;
        Vector2 essaisNormal = essaisRT != null ? essaisRT.anchoredPosition : Vector2.zero;

        float w = CanvasWidth();

        // Positions de depart : titre depuis gauche, essais depuis droite
        Vector2 titleStart  = new Vector2(titleNormal.x  - w, titleNormal.y);
        Vector2 essaisStart = new Vector2(essaisNormal.x + w, essaisNormal.y);

        if (titleRT  != null) titleRT.anchoredPosition  = titleStart;
        if (essaisRT != null) essaisRT.anchoredPosition = essaisStart;

        // 5a. Titre slide depuis la gauche
        if (titleSlideSound != null) audioSource.PlayOneShot(titleSlideSound);
        float elapsed = 0f;
        while (elapsed < textSlideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = textSlideCurve.Evaluate(Mathf.Clamp01(elapsed / textSlideDuration));
            if (titleRT != null) titleRT.anchoredPosition = Vector2.Lerp(titleStart, titleNormal, t);
            yield return null;
        }
        if (titleRT != null) titleRT.anchoredPosition = titleNormal;

        // 5b. Essais slide depuis la droite (declenche quand titre est arrive)
        if (essaisSlideSound != null) audioSource.PlayOneShot(essaisSlideSound);
        elapsed = 0f;
        while (elapsed < textSlideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = textSlideCurve.Evaluate(Mathf.Clamp01(elapsed / textSlideDuration));
            if (essaisRT != null) essaisRT.anchoredPosition = Vector2.Lerp(essaisStart, essaisNormal, t);
            yield return null;
        }
        if (essaisRT != null) essaisRT.anchoredPosition = essaisNormal;

        // 6. Activer input
        if (victoryCanvasGroup != null)
        {
            victoryCanvasGroup.interactable = true;
            victoryCanvasGroup.blocksRaycasts = true;
        }
        Time.timeScale = 0f;
        isActive = true;
        Debug.Log("[VictoryUI] Pret — appuyer sur un bouton pour continuer");
    }

    // ============================================
    // WIPE
    // ============================================

    private IEnumerator WipeCoroutine()
    {
        Time.timeScale = 1f;

        if (cm_victory != null)
            cm_victory.Priority = cm_victoryPriority;

        Vector2 startPos = slideContainer != null ? slideContainer.anchoredPosition : Vector2.zero;
        Vector2 endPos   = new Vector2(startPos.x + CanvasWidth(), startPos.y);
        float   bgStart  = colorBackground != null ? colorBackground.alpha : 0f;

        float elapsed = 0f;
        while (elapsed < wipeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = wipeCurve.Evaluate(Mathf.Clamp01(elapsed / wipeDuration));

            if (slideContainer != null)
                slideContainer.anchoredPosition = Vector2.Lerp(startPos, endPos, t);

            if (colorBackground != null)
                colorBackground.alpha = Mathf.Lerp(bgStart, 0f, t);

            yield return null;
        }

        Hide();
        Debug.Log("[VictoryUI] Wipe termine.");
        OnWipeComplete?.Invoke();
    }

    // ============================================
    // HIDE
    // ============================================

    public void Hide()
    {
        isActive = false;

        if (colorBackground != null)
            colorBackground.alpha = 0f;

        if (victoryCanvasGroup != null)
        {
            victoryCanvasGroup.interactable = false;
            victoryCanvasGroup.blocksRaycasts = false;
        }

        if (slideContainer != null)
        {
            slideContainer.anchoredPosition = Vector2.zero;
            slideContainer.localScale = Vector3.zero;
        }
    }

    // ============================================
    // HELPERS
    // ============================================

    private float CanvasWidth()
    {
        if (parentCanvas != null)
        {
            float w = parentCanvas.GetComponent<RectTransform>().rect.width;
            if (w > 0f) return w;
        }
        return Screen.width;
    }
}
