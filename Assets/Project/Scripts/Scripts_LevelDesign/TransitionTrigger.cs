using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TransitionTrigger : MonoBehaviour
{
    [Header("Zone References")]
    [SerializeField] private GameObject zone2;

    [Header("Camera References")]
    [SerializeField] private GameObject zone1Camera;
    [SerializeField] private GameObject zone2Camera;
    [SerializeField] private CameraConfiner mainCameraConfiner;
    [SerializeField] private Collider zone2Confiner;

    [Header("Fade")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private float waitTimeInBlack = 1f;

    [Header("Debug")]
    [SerializeField] private bool hasTriggered = false;

    private void Start()
    {
        if (fadeCanvasGroup != null)
            fadeCanvasGroup.alpha = 0f;

        if (zone2 != null)
        {
            zone2.SetActive(false);
            Debug.Log("[TransitionTrigger] Zone 2 forcee en desactive au Start");
        }

        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null)
            Debug.LogError("[TransitionTrigger] PAS DE BOXCOLLIDER SUR CE GAMEOBJECT !");
        else if (!col.isTrigger)
            Debug.LogError("[TransitionTrigger] BoxCollider.isTrigger = FALSE !");
        else
            Debug.Log("[TransitionTrigger] BoxCollider OK, isTrigger = true");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;

        if (other.CompareTag("Player"))
        {
            Debug.Log("[TransitionTrigger] TAG PLAYER DETECTE ! Activation Zone 2...");
            hasTriggered = true;
            StartCoroutine(FadeTransitionSequence());
        }
    }

    private IEnumerator FadeTransitionSequence()
    {
        // 1. Fade to black
        yield return StartCoroutine(Fade(0f, 1f));

        // 2. Pendant le noir : tout changer
        yield return new WaitForSeconds(waitTimeInBlack);

        if (zone2 != null)
            zone2.SetActive(true);

        if (zone1Camera != null)
            zone1Camera.SetActive(false);

        if (zone2Camera != null)
            zone2Camera.SetActive(true);

        if (mainCameraConfiner != null && zone2Confiner != null)
            mainCameraConfiner.SetBoundingVolume(zone2Confiner);

        // 3. Fade from black
        yield return StartCoroutine(Fade(1f, 0f));

        Debug.Log("[TransitionTrigger] Transition complete !");
    }

    private IEnumerator Fade(float from, float to)
    {
        if (fadeCanvasGroup == null) yield break;

        fadeCanvasGroup.blocksRaycasts = true;
        fadeCanvasGroup.alpha = from;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            yield return null;
        }

        fadeCanvasGroup.alpha = to;

        if (to == 0f)
            fadeCanvasGroup.blocksRaycasts = false;
    }
}