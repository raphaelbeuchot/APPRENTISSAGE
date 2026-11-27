using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Declenche une liste d'actions apres un delai. Trigger() lance l'attente (depuis un UnityEvent,
/// un TriggerAction, une Timeline...), puis onFire part.
/// Ex. boss Rush Hour IV : onRetract -> Trigger, 3,5 s (1,5 de descente du volume + 2 dans le noir)
/// -> MaterialSwapper.Swap, ENDVOLUME.FadeOut, spot allume.
/// </summary>
public class DelayedEvent : MonoBehaviour
{
    [Tooltip("Attente entre l'appel Trigger et le declenchement (s)")]
    [Min(0f)] [SerializeField] private float delay = 1f;
    [Tooltip("Un nouvel appel pendant l'attente la relance de zero ; sinon il est ignore")]
    [SerializeField] private bool restartIfTriggeredAgain = false;
    [SerializeField] private UnityEvent onFire;

    private Coroutine running;

    [ContextMenu("Trigger")]
    public void Trigger()
    {
        if (!isActiveAndEnabled) return;
        if (running != null)
        {
            if (!restartIfTriggeredAgain) return;
            StopCoroutine(running);
        }
        running = StartCoroutine(Wait());
    }

    public void Cancel()
    {
        if (running != null) StopCoroutine(running);
        running = null;
    }

    private IEnumerator Wait()
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        running = null;
        onFire?.Invoke();
    }
}
