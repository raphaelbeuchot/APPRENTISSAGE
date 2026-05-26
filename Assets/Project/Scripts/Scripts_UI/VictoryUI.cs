using UnityEngine;
using TMPro;
using Unity.Cinemachine;
using System;
using System.Collections;

public class VictoryUI : MonoBehaviour
{
    [Header("Containers")]
    [Tooltip("Parent qui slide pendant le wipe (contient le panel textes)")]
    [SerializeField] private RectTransform slideContainer;
    [Tooltip("Panel textes — slide depuis la droite pendant le Show")]
    [SerializeField] private RectTransform victoryPanel;
    [Tooltip("CanvasGroup du panel textes")]
    [SerializeField] private CanvasGroup victoryCanvasGroup;

    [Header("Background 3D")]
    [Tooltip("Material unlit solide pour le fond (ex: Unlit/Color). ZTest=Always et ZWrite=Off seront forces en code.")]
    [SerializeField] private Material bgQuadMaterial;
    [Tooltip("Distance devant la camera pour le quad de fond (doit etre < distance camera-ghosts)")]
    [SerializeField] private float bgQuadDistance = 5f;

    [Header("Textes")]
    [SerializeField] private TextMeshProUGUI levelCompleteText;
    [SerializeField] private TextMeshProUGUI attemptsText;
    [SerializeField] private string levelCompleteMessage = "LEVEL COMPLETE";
    [SerializeField] private string attemptsPrefix = "Essais : ";

    [Header("Timings Show")]
    [Tooltip("Duree du slide du texte et des ghosts")]
    [SerializeField] private float slideDuration = 0.55f;
    [Tooltip("Duree du pop-in de Essais")]
    [SerializeField] private float essaisPopDuration = 0.25f;
    [SerializeField] private AnimationCurve slideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Ghost Slide")]
    [Tooltip("Distance en unites monde vers laquelle les ghosts glissent (gauche = negatif)")]
    [SerializeField] private float ghostSlideDistance = 80f;

    [Header("Wipe")]
    [SerializeField] private float wipeDuration = 0.6f;
    [SerializeField] private AnimationCurve wipeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Camera Victory")]
    [Tooltip("CinemachineCamera a activer pendant le wipe (a creer dans la scene)")]
    [SerializeField] private CinemachineCamera cm_victory;
    [Tooltip("Priorite donnee a CM_Victory au moment du wipe")]
    [SerializeField] private int cm_victoryPriority = 20;

    /// <summary>Fire quand le wipe est termine et que le niveau est revele.</summary>
    public event Action OnWipeComplete;

    private bool isActive = false;
    private bool isTransitioning = false;
    private Canvas parentCanvas;
    private GameObject bgQuad;

    // ============================================
    // UNITY LIFECYCLE
    // ============================================

    void Start()
    {
        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null)
            parentCanvas = FindObjectOfType<Canvas>();

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
    // SHOW — appele par LevelManager apres VictoryScale
    // ============================================

    public void Show(PlayerHealth playerHealth = null)
    {
        isActive = false;
        isTransitioning = false;

        // Reset container canvas
        if (slideContainer != null)
            slideContainer.anchoredPosition = Vector2.zero;

        // Panel part hors-ecran a droite
        if (victoryPanel != null)
            victoryPanel.anchoredPosition = new Vector2(CanvasWidth(), 0f);

        if (victoryCanvasGroup != null)
        {
            victoryCanvasGroup.alpha = 1f;
            victoryCanvasGroup.interactable = false;
            victoryCanvasGroup.blocksRaycasts = false;
        }

        // Peupler les textes
        if (levelCompleteText != null)
            levelCompleteText.text = levelCompleteMessage;

        if (attemptsText != null)
        {
            int attempts = LevelStatsTracker.Instance != null
                ? LevelStatsTracker.Instance.AttemptCount : 1;
            attemptsText.text = attemptsPrefix + attempts;
        }

        // "Essais" commence a scale 0 (pop-in plus tard)
        if (attemptsText != null)
            attemptsText.transform.localScale = Vector3.zero;

        // Background 3D : spawn instantane, sous les ghosts (renderQueue 2990 < ghosts 3000+)
        SpawnBackgroundQuad();

        StartCoroutine(ShowCoroutine());
    }

    private IEnumerator ShowCoroutine()
    {
        // 1. Stop sentinel blinking + reset visuel -> GreenLight
        SentinelCycleManager cycle = FindObjectOfType<SentinelCycleManager>();
        if (cycle != null) cycle.StopCycleForVictory();

        // 2. Collecte des ghosts pour slide
        VictoryGhostBillboard[] ghosts = FindObjectsByType<VictoryGhostBillboard>(FindObjectsSortMode.None);
        Vector3[] ghostStarts = new Vector3[ghosts.Length];
        Vector3 slideDir = Camera.main != null ? -Camera.main.transform.right : Vector3.left;
        for (int i = 0; i < ghosts.Length; i++)
            ghostStarts[i] = ghosts[i].transform.position;

        // 3. Slide panel (droite->centre) + ghosts (gauche) simultanement
        //    Background quad 3D est deja en place, ghosts (renderQueue 3000+) s'affichent devant.
        float canvasW = CanvasWidth();
        Vector2 panelStart = new Vector2(canvasW, 0f);
        Vector2 panelEnd = Vector2.zero;

        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = slideCurve.Evaluate(Mathf.Clamp01(elapsed / slideDuration));

            if (victoryPanel != null)
                victoryPanel.anchoredPosition = Vector2.Lerp(panelStart, panelEnd, t);

            for (int i = 0; i < ghosts.Length; i++)
                if (ghosts[i] != null)
                    ghosts[i].transform.position = ghostStarts[i] + slideDir * (ghostSlideDistance * t);

            yield return null;
        }
        if (victoryPanel != null) victoryPanel.anchoredPosition = panelEnd;

        // 4. Detruire les ghosts (hors-ecran)
        foreach (var ghost in ghosts)
            if (ghost != null) Destroy(ghost.gameObject);

        // 5. Pop-in "Essais"
        if (attemptsText != null)
            yield return StartCoroutine(PopIn(attemptsText.transform));

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
    // WIPE — declenche par input
    // ============================================

    private IEnumerator WipeCoroutine()
    {
        Time.timeScale = 1f;

        // Activer CM_Victory
        if (cm_victory != null)
            cm_victory.Priority = cm_victoryPriority;

        // Positions de depart pour le slide
        Vector2 canvasStart = slideContainer != null ? slideContainer.anchoredPosition : Vector2.zero;
        Vector2 canvasEnd = new Vector2(canvasStart.x + CanvasWidth(), canvasStart.y);

        Vector3 bgStart = bgQuad != null ? bgQuad.transform.position : Vector3.zero;
        Vector3 bgSlide = Camera.main != null
            ? Camera.main.transform.right * BgQuadWorldWidth()
            : Vector3.right * 10f;

        // Slide canvas + quad ensemble vers la droite
        float elapsed = 0f;
        while (elapsed < wipeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = wipeCurve.Evaluate(Mathf.Clamp01(elapsed / wipeDuration));

            if (slideContainer != null)
                slideContainer.anchoredPosition = Vector2.Lerp(canvasStart, canvasEnd, t);

            if (bgQuad != null)
                bgQuad.transform.position = bgStart + bgSlide * t;

            yield return null;
        }

        if (slideContainer != null) slideContainer.anchoredPosition = canvasEnd;

        Hide();
        Debug.Log("[VictoryUI] Wipe termine — niveau revele.");
        OnWipeComplete?.Invoke();
    }

    // ============================================
    // HIDE
    // ============================================

    public void Hide()
    {
        isActive = false;

        if (victoryCanvasGroup != null)
        {
            victoryCanvasGroup.alpha = 0f;
            victoryCanvasGroup.interactable = false;
            victoryCanvasGroup.blocksRaycasts = false;
        }

        // Reset victoryPanel au centre pour le prochain Show
        if (victoryPanel != null)
            victoryPanel.anchoredPosition = Vector2.zero;

        // Detruire le quad de fond
        if (bgQuad != null)
        {
            Destroy(bgQuad);
            bgQuad = null;
        }
    }

    // ============================================
    // BACKGROUND QUAD 3D
    // ============================================

    private void SpawnBackgroundQuad()
    {
        // Detruire l'ancien si existant
        if (bgQuad != null) { Destroy(bgQuad); bgQuad = null; }
        if (bgQuadMaterial == null || Camera.main == null) return;

        Camera cam = Camera.main;

        // Taille pour couvrir exactement l'ecran a bgQuadDistance
        float h = 2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * bgQuadDistance;
        float w = h * cam.aspect;

        bgQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bgQuad.name = "VictoryBgQuad";

        // Pas de physique, pas d'ombres
        Destroy(bgQuad.GetComponent<Collider>());
        MeshRenderer mr = bgQuad.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        // Position et orientation (face camera)
        bgQuad.transform.position = cam.transform.position + cam.transform.forward * bgQuadDistance;
        bgQuad.transform.rotation = cam.transform.rotation;
        bgQuad.transform.localScale = new Vector3(w, h, 1f);

        // Material : instance pour ne pas modifier l'original
        // renderQueue 2990 < ghosts (3000+) -> les ghosts s'affichent devant
        // ZTest=Always : couvre la scene de jeu sans etre occulte par elle
        // ZWrite=Off  : ne bloque pas le depth test des ghosts
        Material mat = new Material(bgQuadMaterial);
        mat.renderQueue = 2990;
        mat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        mat.SetFloat("_ZWrite", 0f);
        mr.material = mat;
    }

    /// <summary>Largeur monde du quad de fond (pour le wipe).</summary>
    private float BgQuadWorldWidth()
    {
        if (Camera.main == null) return 10f;
        Camera cam = Camera.main;
        return 2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * bgQuadDistance * cam.aspect;
    }

    // ============================================
    // HELPERS
    // ============================================

    private IEnumerator PopIn(Transform target)
    {
        if (target == null) yield break;
        float elapsed = 0f;
        while (elapsed < essaisPopDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / essaisPopDuration));
            target.localScale = Vector3.one * t;
            yield return null;
        }
        target.localScale = Vector3.one;
    }

    private float CanvasWidth()
    {
        if (parentCanvas != null)
            return parentCanvas.GetComponent<RectTransform>().rect.width;
        return Screen.width;
    }
}
