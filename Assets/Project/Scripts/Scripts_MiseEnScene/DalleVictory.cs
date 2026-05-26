using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// Dalle de la TransitionRoom qui descend apres la victoire,
/// emportant le joueur avec elle.
///
/// Setup :
///   - Assigner endPoint (Transform en bas du puits)
///   - Optionnel : AudioSource + sonDescente
/// </summary>
public class DalleVictory : MonoBehaviour
{
    [Header("Deplacement")]
    [Tooltip("Point d'arrivee de la dalle (en bas du puits)")]
    [SerializeField] private Transform endPoint;

    [Tooltip("Duree de la descente en secondes")]
    [SerializeField] private float descDuration = 2.5f;

    [SerializeField] private AnimationCurve descCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Son")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sonDescente;

    /// <summary>Fire quand la dalle a atteint le bas et que le joueur est departente.</summary>
    public event Action OnDescentComplete;

    private Vector3 startPos;

    private void Awake()
    {
        startPos = transform.position;
    }

    // ============================================
    // API PUBLIQUE
    // ============================================

    /// <summary>
    /// Lance la descente. Le joueur est parenté a la dalle le temps du trajet.
    /// Son Rigidbody est passe en kinematic pendant la descente pour eviter les conflits physiques.
    /// </summary>
    public void StartDescent(Transform playerToCarry)
    {
        if (endPoint == null)
        {
            Debug.LogWarning("[DalleVictory] Pas d'endPoint assigne — descente annulee.");
            OnDescentComplete?.Invoke();
            return;
        }

        StartCoroutine(DescentCoroutine(playerToCarry));
    }

    // ============================================
    // COROUTINE
    // ============================================

    private IEnumerator DescentCoroutine(Transform playerToCarry)
    {
        Rigidbody playerRb = null;
        bool wasKinematic = false;

        if (playerToCarry != null)
        {
            playerRb = playerToCarry.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                wasKinematic = playerRb.isKinematic;
                playerRb.isKinematic = true;
            }

            // worldPositionStays = true par defaut : la position world est conservee
            playerToCarry.SetParent(transform);
        }

        if (audioSource != null && sonDescente != null)
            audioSource.PlayOneShot(sonDescente);

        float elapsed = 0f;
        while (elapsed < descDuration)
        {
            elapsed += Time.deltaTime;
            float t = descCurve.Evaluate(Mathf.Clamp01(elapsed / descDuration));
            transform.position = Vector3.Lerp(startPos, endPoint.position, t);
            yield return null;
        }

        transform.position = endPoint.position;

        // Deparenter et restaurer la physique
        if (playerToCarry != null)
        {
            playerToCarry.SetParent(null);
            if (playerRb != null)
                playerRb.isKinematic = wasKinematic;
        }

        Debug.Log("[DalleVictory] Descente terminee.");
        OnDescentComplete?.Invoke();
    }
}
