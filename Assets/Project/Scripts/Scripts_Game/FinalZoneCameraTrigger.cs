using UnityEngine;
using Unity.Cinemachine;
using System.Collections;

public class FinalZoneCameraTrigger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CinemachineTargetGroup targetGroup;
    [SerializeField] private CinemachineCamera virtualCamera;
    [SerializeField] private Transform newTargetToAdd;

    [Header("Framing Settings")]
    [SerializeField] private float targetFramingSize = 0.8f;
    [SerializeField] private float transitionDuration = 2f;

    [Header("Target Settings")]
    [SerializeField] private float newTargetWeight = 1f;
    [SerializeField] private float newTargetRadius = 1f;

    private CinemachineGroupFraming groupFramingExtension;
    private float originalFramingSize;
    private Coroutine activeTransition;
    private bool isInZone = false;

    private void Start()
    {
        if (virtualCamera != null)
        {
            groupFramingExtension = virtualCamera.GetComponent<CinemachineGroupFraming>();

            if (groupFramingExtension != null)
            {
                originalFramingSize = groupFramingExtension.FramingSize;
            }
            else
            {
                Debug.LogWarning("CinemachineGroupFraming extension non trouvee sur la camera virtuelle!");
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isInZone)
        {
            isInZone = true;

            // Arreter toute transition en cours
            if (activeTransition != null)
            {
                StopCoroutine(activeTransition);
            }

            activeTransition = StartCoroutine(TransitionToFinalZone());
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && isInZone)
        {
            isInZone = false;

            // Arreter toute transition en cours
            if (activeTransition != null)
            {
                StopCoroutine(activeTransition);
            }

            activeTransition = StartCoroutine(TransitionBackToNormal());
        }
    }

    private IEnumerator TransitionToFinalZone()
    {
        // Ajouter le nouveau target
        if (targetGroup != null && newTargetToAdd != null)
        {
            targetGroup.AddMember(newTargetToAdd, newTargetWeight, newTargetRadius);
        }

        // Transition smooth du framing
        if (groupFramingExtension != null)
        {
            float startSize = groupFramingExtension.FramingSize;
            float elapsed = 0f;

            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / transitionDuration;

                // Utiliser SmoothStep pour une transition plus smooth
                t = Mathf.SmoothStep(0f, 1f, t);

                groupFramingExtension.FramingSize = Mathf.Lerp(startSize, targetFramingSize, t);

                yield return null;
            }

            groupFramingExtension.FramingSize = targetFramingSize;
        }

        activeTransition = null;
    }

    private IEnumerator TransitionBackToNormal()
    {
        // Retirer le target ajoute
        if (targetGroup != null && newTargetToAdd != null)
        {
            targetGroup.RemoveMember(newTargetToAdd);
        }

        // Transition smooth de retour
        if (groupFramingExtension != null)
        {
            float startSize = groupFramingExtension.FramingSize;
            float elapsed = 0f;

            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / transitionDuration;

                // Utiliser SmoothStep pour une transition plus smooth
                t = Mathf.SmoothStep(0f, 1f, t);

                groupFramingExtension.FramingSize = Mathf.Lerp(startSize, originalFramingSize, t);

                yield return null;
            }

            groupFramingExtension.FramingSize = originalFramingSize;
        }

        activeTransition = null;
    }
}