using UnityEngine;

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
    [SerializeField] private FadeManager fadeManager;
    [SerializeField] private float waitTimeInBlack = 1f;

    [Header("Debug")]
    [SerializeField] private bool hasTriggered = false;

    private void Start()
    {
        // Force la desactivation de Zone2 au demarrage
        if (zone2 != null)
        {
            zone2.SetActive(false);
            Debug.Log("[TransitionTrigger] Zone 2 forcee en desactive au Start");
        }

        // Verifications au demarrage
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null)
        {
            Debug.LogError("[TransitionTrigger] PAS DE BOXCOLLIDER SUR CE GAMEOBJECT !");
        }
        else if (!col.isTrigger)
        {
            Debug.LogError("[TransitionTrigger] BoxCollider.isTrigger = FALSE ! Il doit etre a TRUE !");
        }
        else
        {
            Debug.Log("[TransitionTrigger] BoxCollider OK, isTrigger = true");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[TransitionTrigger] ========== TRIGGER TOUCHE ==========");
        Debug.Log($"[TransitionTrigger] GameObject: {other.gameObject.name}");
        Debug.Log($"[TransitionTrigger] Tag: {other.tag}");
        Debug.Log($"[TransitionTrigger] Layer: {LayerMask.LayerToName(other.gameObject.layer)}");

        if (hasTriggered)
        {
            Debug.Log("[TransitionTrigger] Deja triggere, ignore");
            return;
        }

        if (other.CompareTag("Player"))
        {
            Debug.Log("[TransitionTrigger] TAG PLAYER DETECTE ! Activation Zone 2...");
            ActivateZone2();
        }
        else
        {
            Debug.LogWarning($"[TransitionTrigger] Tag incorrect : '{other.tag}' au lieu de 'Player'");
        }
    }

    private void ActivateZone2()
    {
        hasTriggered = true;
        Debug.Log("[TransitionTrigger] ========== ACTIVATION ZONE 2 ==========");

        StartCoroutine(FadeTransitionSequence());
    }

    private System.Collections.IEnumerator FadeTransitionSequence()
    {
        // 1. Fade to black
        if (fadeManager != null)
        {
            Debug.Log("[TransitionTrigger] Fade to black...");
            yield return StartCoroutine(fadeManager.FadeToBlack());
        }

        // 2. Pendant le noir : tout changer
        yield return new WaitForSeconds(waitTimeInBlack);

        // Activer Zone 2
        if (zone2 != null)
        {
            zone2.SetActive(true);
            Debug.Log("[TransitionTrigger] Zone 2 activee");
        }

        // Switch cameras
        if (zone1Camera != null)
        {
            zone1Camera.SetActive(false);
            Debug.Log("[TransitionTrigger] Camera Zone 1 desactivee");
        }

        if (zone2Camera != null)
        {
            zone2Camera.SetActive(true);
            Debug.Log("[TransitionTrigger] Camera Zone 2 activee");
        }

        // Update confiner
        if (mainCameraConfiner != null && zone2Confiner != null)
        {
            mainCameraConfiner.SetBoundingVolume(zone2Confiner);
            Debug.Log("[TransitionTrigger] Confiner Main Camera mis a jour vers Zone 2");
        }

        // 3. Fade from black
        if (fadeManager != null)
        {
            Debug.Log("[TransitionTrigger] Fade from black...");
            yield return StartCoroutine(fadeManager.FadeFromBlack());
        }

        Debug.Log("[TransitionTrigger] Transition complete !");
    }
}