using System.Collections;
using UnityEngine;

public class SentinelHitEffect : MonoBehaviour
{
    private Transform arms;
    private Coroutine effectCoroutine;
    private Vector3 originalLocalPos;
    [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 0.6f, 0f);

    void Start()
    {
        foreach (Transform t in GetComponentsInChildren<Transform>())
        {
            if (t.name == "T-PoseNEW")
            {
                arms = t;
                break;
            }
        }

        if (arms == null)
        {
            Debug.LogWarning("[SentinelHitEffect] T-PoseNEW non trouve sur " + gameObject.name);
            return;
        }

        originalLocalPos = arms.localPosition;
    }

    public void TriggerEffect()
    {
        if (arms == null) return;
        if (effectCoroutine != null)
            StopCoroutine(effectCoroutine);
        effectCoroutine = StartCoroutine(ScaleCoroutine());
    }

    private IEnumerator ScaleCoroutine()
    {
        float elapsed = 0f;
        float halfDuration = 0.25f;
        Vector3 normalScale = Vector3.one;
        Vector3 bigScale = Vector3.one * 2f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            arms.localScale = Vector3.Lerp(normalScale, bigScale, t);
            arms.localPosition = originalLocalPos + pivotOffset * (1f - arms.localScale.x);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            arms.localScale = Vector3.Lerp(bigScale, normalScale, t);
            arms.localPosition = originalLocalPos + pivotOffset * (1f - arms.localScale.x);
            yield return null;
        }

        arms.localScale = normalScale;
        arms.localPosition = originalLocalPos;
        effectCoroutine = null;
    }
}