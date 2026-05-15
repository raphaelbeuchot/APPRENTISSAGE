using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class TomatoUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text countText;
    [SerializeField] private Image tomatoIcon;

    [Header("Refill Animation")]
    [SerializeField] private AudioClip tomatoPickupSound;
    [SerializeField] private float delayBetweenTomatoes = 0.12f;
    [SerializeField] private float punchScale = 1.4f;
    [SerializeField] private float punchDuration = 0.08f;

    private TomatoThrowSystem throwSystem;
    private AudioSource audioSource;
    private bool hasBeenFilled = false;
    private Coroutine refillCoroutine;
    private Vector3 iconBaseScale;

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            throwSystem = playerObj.GetComponent<TomatoThrowSystem>();
            if (throwSystem != null)
            {
                throwSystem.OnTomatoCountChanged += UpdateUI;
                throwSystem.OnRefill += OnRefill;
            }
        }
        else
        {
            Debug.LogWarning("[TomatoUI] Player introuvable");
        }

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f;
        audioSource.playOnAwake = false;

        if (tomatoIcon != null)
            iconBaseScale = tomatoIcon.transform.localScale;

        UpdateUI(0, 0);
    }

    private void UpdateUI(int current, int max)
    {
        if (current > 0) hasBeenFilled = true;
        gameObject.SetActive(hasBeenFilled);

        // Pas de mise a jour du texte si une animation de refill tourne
        if (refillCoroutine == null && countText != null)
            countText.text = "x" + current.ToString();
    }

    private void OnRefill(int added)
    {
        if (!hasBeenFilled) hasBeenFilled = true;
        gameObject.SetActive(true);

        if (refillCoroutine != null)
            StopCoroutine(refillCoroutine);

        int current = throwSystem.GetTomatoCount() - added;
        int target = throwSystem.GetTomatoCount();
        refillCoroutine = StartCoroutine(RefillCoroutine(current, target));
    }

    private IEnumerator RefillCoroutine(int from, int to)
    {
        int displayed = from;

        while (displayed < to)
        {
            displayed++;

            if (countText != null)
                countText.text = "x" + displayed.ToString();

            if (tomatoPickupSound != null)
                audioSource.PlayOneShot(tomatoPickupSound);

            if (tomatoIcon != null)
            {
                StopCoroutine("PunchCoroutine");
                StartCoroutine(PunchCoroutine());
            }

            yield return new WaitForSeconds(delayBetweenTomatoes);
        }

        refillCoroutine = null;
    }

    private IEnumerator PunchCoroutine()
    {
        float elapsed = 0f;
        Vector3 bigScale = iconBaseScale * punchScale;

        while (elapsed < punchDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / punchDuration);
            tomatoIcon.transform.localScale = Vector3.Lerp(iconBaseScale, bigScale, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < punchDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / punchDuration);
            tomatoIcon.transform.localScale = Vector3.Lerp(bigScale, iconBaseScale, t);
            yield return null;
        }

        tomatoIcon.transform.localScale = iconBaseScale;
    }

    private void OnDestroy()
    {
        if (throwSystem != null)
        {
            throwSystem.OnTomatoCountChanged -= UpdateUI;
            throwSystem.OnRefill -= OnRefill;
        }
    }
}