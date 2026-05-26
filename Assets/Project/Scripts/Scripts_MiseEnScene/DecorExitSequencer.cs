using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Expulse tous les props visibles du decor une fois la victoire obtenue.
///
/// Regles de direction (basees sur DecorExitPivot) :
///   - Tag "Sentinel"          -> -Z
///   - X < pivot.X             -> -X
///   - X > pivot.X             -> +X
///   - |X - pivot.X| <= center threshold ET Y > pivot.Y -> +Y
///
/// Setup :
///   - Placer un GO vide "DecorExitPivot" au centre de la zone de jeu
///   - Tagger les props sols/murs avec le layer Ground ou le tag Ground
///   - Tagger le bloc sentinel avec le tag "Sentinel"
///   - Le GO EndCurtain (avec EndCurtainRise) est exclu automatiquement
/// </summary>
public class DecorExitSequencer : MonoBehaviour
{
    [Header("Reference")]
    [Tooltip("GO vide centre dans la zone de jeu — definit les directions d'expulsion")]
    [SerializeField] private Transform exitPivot;

    [Header("Mouvement")]
    [Tooltip("Duree de la translation pour chaque prop (secondes)")]
    [SerializeField] private float exitDuration = 0.6f;

    [Tooltip("Distance de deplacement avant destruction (unites monde)")]
    [SerializeField] private float exitDistance = 20f;

    [Tooltip("Decalage entre le lancement de chaque prop (secondes)")]
    [SerializeField] private float staggerDelay = 0.05f;

    [Tooltip("Seuil en X pour considerer un GO 'central' et l'envoyer vers le haut")]
    [SerializeField] private float centerThreshold = 1f;

    [SerializeField] private AnimationCurve exitCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    /// <summary>Fire quand tous les props ont quitte le decor.</summary>
    public event Action OnDecorExitComplete;

    // ============================================
    // API PUBLIQUE
    // ============================================

    public void StartExit()
    {
        StartCoroutine(ExitCoroutine());
    }

    // ============================================
    // SEQUENCE
    // ============================================

    private IEnumerator ExitCoroutine()
    {
        List<Transform> targets = CollectTargets();
        Debug.Log($"[DecorExit] {targets.Count} props a expulser.");

        int completed = 0;

        for (int i = 0; i < targets.Count; i++)
        {
            Transform t = targets[i];
            if (t == null) { completed++; continue; }

            Vector3 dir = GetExitDirection(t);
            StartCoroutine(ExitOneTarget(t, dir, () => completed++));

            if (staggerDelay > 0f)
                yield return new WaitForSeconds(staggerDelay);
        }

        // Attendre que tous les props aient fini leur translation
        yield return new WaitUntil(() => completed >= targets.Count);

        Debug.Log("[DecorExit] Tous les props expulses.");
        OnDecorExitComplete?.Invoke();
    }

    private IEnumerator ExitOneTarget(Transform target, Vector3 direction, Action onDone)
    {
        Vector3 startPos = target.position;
        Vector3 endPos = startPos + direction * exitDistance;

        float elapsed = 0f;
        while (elapsed < exitDuration && target != null)
        {
            elapsed += Time.deltaTime;
            float t = exitCurve.Evaluate(Mathf.Clamp01(elapsed / exitDuration));
            target.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        if (target != null)
            Destroy(target.gameObject);

        onDone?.Invoke();
    }

    // ============================================
    // COLLECTE DES CIBLES
    // ============================================

    private List<Transform> CollectTargets()
    {
        HashSet<Transform> roots = new HashSet<Transform>();

        foreach (Renderer rend in FindObjectsOfType<Renderer>())
        {
            // Exclure UI (CanvasRenderer est different, mais par securite)
            if (rend is ParticleSystemRenderer) continue;

            Transform root = rend.transform.root;

            // Exclure Ground (layer ou tag)
            if (rend.gameObject.layer == LayerMask.NameToLayer("Ground")) continue;
            if (rend.CompareTag("Ground")) continue;

            // Exclure le root si layer ou tag Ground
            if (root.gameObject.layer == LayerMask.NameToLayer("Ground")) continue;
            if (root.CompareTag("Ground")) continue;

            // Exclure le player
            if (root.CompareTag("Player")) continue;

            // Exclure l'EndCurtain (il a son propre script de montee)
            if (root.GetComponentInChildren<EndCurtainRise>() != null) continue;

            // Exclure les GOs managers (pas de Renderer direct dessus)
            if (root.GetComponent<LevelManager>() != null) continue;
            if (root.GetComponent<Camera>() != null) continue;

            roots.Add(root);
        }

        return new List<Transform>(roots);
    }

    // ============================================
    // DIRECTION D'EXPULSION
    // ============================================

    private Vector3 GetExitDirection(Transform target)
    {
        // Cas special : sentinel
        if (target.CompareTag("Sentinel"))
            return Vector3.back;

        if (exitPivot == null)
        {
            Debug.LogWarning("[DecorExit] Pas d'exitPivot assigne — direction +X par defaut.");
            return Vector3.right;
        }

        float dx = target.position.x - exitPivot.position.x;

        // Autour du centre et au-dessus du pivot -> monte
        if (Mathf.Abs(dx) <= centerThreshold && target.position.y > exitPivot.position.y)
            return Vector3.up;

        return dx < 0f ? Vector3.left : Vector3.right;
    }
}
