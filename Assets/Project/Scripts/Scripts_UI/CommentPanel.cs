using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

// Style d'apparition d'un texte du CommentPanel.
// Pop : grossit puis rebondit, retrecit en sortant (nettoyage, moments de jeu dynamiques).
// Fade : fondu entrant / sortant, echelle fixe (invites : Press X, balai, pupitre...).
public enum CommentStyle
{
    Pop,
    Fade
}

public class CommentPanel : MonoBehaviour
{
    public static CommentPanel Instance;

    [Header("References")]
    [SerializeField] private TextMeshProUGUI commentText;

    [Header("Timings")]
    [SerializeField] private float popDuration = 0.15f;
    [SerializeField] private float stayDuration = 3f;
    [SerializeField] private float shrinkDuration = 0.2f;

    [Header("Scale")]
    [SerializeField] private float popScale = 1.75f;
    [SerializeField] private float shrinkScale = 0.2f;

    [Header("Fondu (style Fade)")]
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private float fadeOutDuration = 0.3f;

    private Coroutine currentCoroutine;
    private CanvasGroup canvasGroup;
    private CommentStyle currentStyle = CommentStyle.Pop;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        // Ajoute par le code : rien a changer dans les prefabs UI / UI_Multi
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        gameObject.SetActive(false);
    }

    public static void Show(string message, CommentStyle style = CommentStyle.Pop)
    {
        if (Instance == null)
        {
            Debug.LogWarning("[CommentPanel] Aucune instance en scene !");
            return;
        }

        Instance.DisplayMessage(message, style, false);
    }

    public static void ShowPersistent(string message, CommentStyle style = CommentStyle.Pop)
    {
        if (Instance == null) return;
        Instance.DisplayMessage(message, style, true);
    }

    // Sortie dans le style du texte affiche (retrecit apres un pop, fondu apres un fondu)
    public static void Hide()
    {
        if (Instance == null) return;
        Instance.HideMessage();
    }

    private void DisplayMessage(string message, CommentStyle style, bool persistent)
    {
        if (currentCoroutine != null)
            StopCoroutine(currentCoroutine);

        currentStyle = style;
        commentText.text = Loc.Text(message);
        gameObject.SetActive(true);
        currentCoroutine = StartCoroutine(ShowCoroutine(style, persistent));
    }

    private void HideMessage()
    {
        // Panneau deja cache : rien a faire (une coroutine ne demarre pas sur un objet inactif)
        if (!gameObject.activeInHierarchy) return;

        if (currentCoroutine != null)
            StopCoroutine(currentCoroutine);
        currentCoroutine = StartCoroutine(currentStyle == CommentStyle.Fade ? FadeOutCoroutine() : ShrinkCoroutine());
    }

    private IEnumerator ShowCoroutine(CommentStyle style, bool persistent)
    {
        if (style == CommentStyle.Fade)
            yield return FadeIn();
        else
            yield return PopIn();

        if (persistent)
        {
            currentCoroutine = null;
            yield break;
        }

        yield return new WaitForSeconds(stayDuration);

        yield return style == CommentStyle.Fade ? FadeOutCoroutine() : ShrinkCoroutine();
    }

    // Pop : 0 -> popScale -> 1
    private IEnumerator PopIn()
    {
        canvasGroup.alpha = 1f;
        float elapsed = 0f;
        transform.localScale = Vector3.zero;
        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.one * Mathf.Lerp(0f, popScale, elapsed / popDuration);
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.one * Mathf.Lerp(popScale, 1f, elapsed / popDuration);
            yield return null;
        }
        transform.localScale = Vector3.one;
    }

    private IEnumerator FadeIn()
    {
        transform.localScale = Vector3.one;
        float elapsed = 0f;
        canvasGroup.alpha = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;
    }

    // Retrecit 1 -> shrinkScale puis disparait
    private IEnumerator ShrinkCoroutine()
    {
        float elapsed = 0f;
        while (elapsed < shrinkDuration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.one * Mathf.Lerp(1f, shrinkScale, elapsed / shrinkDuration);
            yield return null;
        }
        gameObject.SetActive(false);
        currentCoroutine = null;
    }

    // Fondu depuis l'opacite actuelle (un Hide pendant le fondu entrant repart de la ou il en est)
    private IEnumerator FadeOutCoroutine()
    {
        float start = canvasGroup.alpha;
        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(start, 0f, elapsed / fadeOutDuration);
            yield return null;
        }
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
        currentCoroutine = null;
    }
}
