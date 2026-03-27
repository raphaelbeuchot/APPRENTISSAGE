using UnityEngine;
using System.Collections;

public class VictoryOutlineController : MonoBehaviour
{
 

    [SerializeField] private Material victoryOutlineMat;
    [SerializeField] private Camera silhouetteCamera;
    [SerializeField] private float duration = 1.5f;
    [SerializeField] private float maxRadius = 1.5f;

    void Start()
    {
        if (silhouetteCamera != null)
            silhouetteCamera.enabled = false;
    }

    public void TriggerOutline()
    {
        if (silhouetteCamera != null)
            silhouetteCamera.enabled = true;

        StartCoroutine(AnimateOutline());
    }
    

    private IEnumerator AnimateOutline()
    {
        float elapsed = 0f;
        victoryOutlineMat.SetFloat("_Radius", 0f);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            float radius = Mathf.Lerp(0f, maxRadius, t);
            victoryOutlineMat.SetFloat("_Radius", radius);
            yield return null;
        }

        victoryOutlineMat.SetFloat("_Radius", 0f);
    }
}