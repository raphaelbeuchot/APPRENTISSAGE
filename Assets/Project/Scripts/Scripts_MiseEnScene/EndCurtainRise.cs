using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// Leve l'EndCurtain de sa propre hauteur.
/// Appeler Rise() depuis PostVictorySequencer une fois le decor vide.
/// </summary>
public class EndCurtainRise : MonoBehaviour
{
    [Header("Mouvement")]
    [Tooltip("Duree de la montee en secondes")]
    [SerializeField] private float riseDuration = 1.2f;

    [SerializeField] private AnimationCurve riseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Son")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip riseSound;

    /// <summary>Fire quand le rideau a atteint sa position haute.</summary>
    public event Action OnRiseComplete;

    // ============================================
    // API PUBLIQUE
    // ============================================

    public void Rise()
    {
        StartCoroutine(RiseCoroutine());
    }

    // ============================================
    // COROUTINE
    // ============================================

    private IEnumerator RiseCoroutine()
    {
        // Hauteur = bounds du Renderer (ou Collider en fallback)
        float height = GetHeight();
        if (height <= 0f)
        {
            Debug.LogWarning("[EndCurtainRise] Hauteur introuvable — rideau monte de 1 unite par defaut.");
            height = 1f;
        }

        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + Vector3.up * height;

        if (audioSource != null && riseSound != null)
            audioSource.PlayOneShot(riseSound);

        Debug.Log($"[EndCurtainRise] Montee de {height:F2}u ({startPos} -> {endPos})");

        float elapsed = 0f;
        while (elapsed < riseDuration)
        {
            elapsed += Time.deltaTime;
            float t = riseCurve.Evaluate(Mathf.Clamp01(elapsed / riseDuration));
            transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        transform.position = endPos;
        Debug.Log("[EndCurtainRise] Rideau leve.");
        OnRiseComplete?.Invoke();
    }

    // ============================================
    // HELPERS
    // ============================================

    private float GetHeight()
    {
        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend != null) return rend.bounds.size.y;

        Collider col = GetComponentInChildren<Collider>();
        if (col != null) return col.bounds.size.y;

        return 0f;
    }
}
