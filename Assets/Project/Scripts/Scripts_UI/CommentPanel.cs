using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

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

    private Coroutine currentCoroutine;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        gameObject.SetActive(false);
    }

    public static void Show(string message)
    {
        if (Instance == null)
        {
            Debug.LogWarning("[CommentPanel] Aucune instance en scene !");
            return;
        }

        Instance.DisplayMessage(message);
    }

    private void DisplayMessage(string message)
    {
        if (currentCoroutine != null)
            StopCoroutine(currentCoroutine);

        commentText.text = message;
        gameObject.SetActive(true);
        currentCoroutine = StartCoroutine(AnimateCoroutine());
    }

    private IEnumerator AnimateCoroutine()
    {
        // Pop : 0 -> popScale
        float elapsed = 0f;
        transform.localScale = Vector3.zero;

        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / popDuration;
            float scale = Mathf.Lerp(0f, popScale, t);
            transform.localScale = Vector3.one * scale;
            yield return null;
        }

        // Pop retour : popScale -> 1
        elapsed = 0f;
        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / popDuration;
            float scale = Mathf.Lerp(popScale, 1f, t);
            transform.localScale = Vector3.one * scale;
            yield return null;
        }

        transform.localScale = Vector3.one;

        // Stay
        yield return new WaitForSeconds(stayDuration);

        // Shrink : 1 -> shrinkScale puis disparait
        elapsed = 0f;
        while (elapsed < shrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / shrinkDuration;
            float scale = Mathf.Lerp(1f, shrinkScale, t);
            transform.localScale = Vector3.one * scale;
            yield return null;
        }

        gameObject.SetActive(false);
        currentCoroutine = null;
    }
}