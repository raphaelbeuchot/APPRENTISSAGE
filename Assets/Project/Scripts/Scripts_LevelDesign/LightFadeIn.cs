using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Allume des lumieres en fondu : FadeIn() active les GOs de la liste, puis fait monter l'intensite de chaque
/// Light (enfants compris) de 0 jusqu'a la valeur reglee dans son Inspector, en "Duration".
/// Les GOs peuvent etre decoches au lancement. Branchement : UnityEvent, DelayedEvent, Timeline...
/// Ex. boss Rush Hour IV : SpotDelay -> FadeIn (REDLIGHTS + Spot Light), en meme temps que ENDVOLUME.FadeOut.
/// </summary>
public class LightFadeIn : MonoBehaviour
{
    [Tooltip("GOs a allumer (actives par FadeIn), toutes leurs lumieres enfants comprises")]
    [SerializeField] private GameObject[] targets;
    [Tooltip("Duree de la montee de 0 a l'intensite reglee (s). 0 = d'un coup")]
    [Min(0f)] [SerializeField] private float duration = 1f;

    private readonly List<Light> lights = new List<Light>();
    private readonly List<float> intensities = new List<float>();
    private Coroutine running;

    [ContextMenu("Fade In")]
    public void FadeIn()
    {
        if (!isActiveAndEnabled) return;

        // Intensites cibles lues une seule fois (au premier appel), avant toute mise a zero
        if (lights.Count == 0)
        {
            foreach (GameObject target in targets)
            {
                if (target == null) continue;
                foreach (Light l in target.GetComponentsInChildren<Light>(true))
                {
                    if (lights.Contains(l)) continue;
                    lights.Add(l);
                    intensities.Add(l.intensity);
                }
            }
        }

        for (int i = 0; i < lights.Count; i++)
            if (lights[i] != null) lights[i].intensity = 0f;
        foreach (GameObject target in targets)
            if (target != null) target.SetActive(true);

        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Fade());
    }

    private IEnumerator Fade()
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            for (int i = 0; i < lights.Count; i++)
                if (lights[i] != null) lights[i].intensity = intensities[i] * p;
            yield return null;
        }
        for (int i = 0; i < lights.Count; i++)
            if (lights[i] != null) lights[i].intensity = intensities[i];
        running = null;
    }
}
