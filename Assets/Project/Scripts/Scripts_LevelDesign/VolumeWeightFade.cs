using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

/// <summary>
/// Fait varier l'influence (weight) d'un Volume a la demande, en fondu.
/// Pose sur le GameObject du Volume. Branchement sans code : evenement (UnityEvent, Event d'un
/// CinemachineTriggerAction...) -> VolumeWeightFade.FadeIn (vers 1) ou FadeOut (vers 0).
/// Fondu d'un volume a l'autre : le volume normal reste allume dessous ; celui-ci, en Priority plus haute,
/// monte (FadeIn) puis redescend (FadeOut). Delai et duree propres a chaque sens.
/// Ex. boss Rush Hour IV : ENDVOLUME, FadeIn sur onRetract, FadeOut sur onDecorEvacuated (Delay du rideau).
/// </summary>
[RequireComponent(typeof(Volume))]
public class VolumeWeightFade : MonoBehaviour
{
    [Header("Fade In (vers 1)")]
    [Tooltip("Attente entre l'appel FadeIn et le debut du fondu (s).")]
    [Min(0f)] [SerializeField] private float fadeInDelay = 0f;
    [Tooltip("Duree du fondu vers 1 (s). 0 = d'un coup.")]
    [Min(0f)] [SerializeField] private float fadeInDuration = 1.5f;

    [Header("Fade Out (vers 0)")]
    [Tooltip("Attente entre l'appel FadeOut et le debut du fondu (s). Ex. Delay d'un SlidingCurtain.")]
    [FormerlySerializedAs("delay")]
    [Min(0f)] [SerializeField] private float fadeOutDelay = 0f;
    [Tooltip("Duree du fondu vers 0 (s). 0 = d'un coup.")]
    [FormerlySerializedAs("fadeDuration")]
    [Min(0f)] [SerializeField] private float fadeOutDuration = 1.5f;
    [Tooltip("Desactive le GameObject du Volume une fois a zero (comme la victoire normale).")]
    [SerializeField] private bool disableAtZero = true;

    private Volume volume;
    private Coroutine running;

    private void Awake()
    {
        volume = GetComponent<Volume>();
    }

    [ContextMenu("Fade In")]
    public void FadeIn()
    {
        // Volume desactive : il repart de zero, sinon il reapparaitrait d'un coup avec son ancien weight
        if (!gameObject.activeSelf)
        {
            if (volume == null) volume = GetComponent<Volume>();
            volume.weight = 0f;
            gameObject.SetActive(true);
        }
        StartFade(1f, fadeInDelay, fadeInDuration);
    }

    [ContextMenu("Fade Out")]
    public void FadeOut()
    {
        StartFade(0f, fadeOutDelay, fadeOutDuration);
    }

    private void StartFade(float target, float delay, float duration)
    {
        if (volume == null) volume = GetComponent<Volume>();
        if (!isActiveAndEnabled) return;
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(FadeRoutine(target, delay, duration));
    }

    private IEnumerator FadeRoutine(float target, float delay, float duration)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        float start = volume.weight;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            volume.weight = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }
        volume.weight = target;
        running = null;

        if (target <= 0f && disableAtZero) gameObject.SetActive(false);
    }
}
