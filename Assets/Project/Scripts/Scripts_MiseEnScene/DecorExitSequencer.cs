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

    [Header("Exclusions manuelles")]
    [Tooltip("GOs a exclure explicitement (LevelEssentiels, StartRoom, triggers, etc.)")]
    [SerializeField] private Transform[] manualExclusions;

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
        foreach (Transform t in targets)
            Debug.Log($"[DecorExit]   -> {t.name} (tag:{t.tag} layer:{LayerMask.LayerToName(t.gameObject.layer)}) dir:{GetExitDirection(t)}");

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
        HashSet<Transform> checked_ = new HashSet<Transform>();

        foreach (Renderer rend in FindObjectsOfType<Renderer>())
        {
            if (rend is ParticleSystemRenderer) continue;

            Transform root = rend.transform.root;
            if (checked_.Contains(root)) continue;
            checked_.Add(root);

            string reason = null;

            if      (HasGroundAncestor(rend.transform))                      reason = "Ground ancestor";
            else if (root.tag == "Player")                                    reason = "Player";
            else if (root.GetComponentInChildren<EndCurtainRise>() != null)   reason = "EndCurtainRise";
            else if (root.GetComponentInChildren<Camera>() != null)           reason = "Camera";
            else if (root.GetComponentInChildren<Canvas>() != null)           reason = "Canvas";
            else if (root.GetComponent<LevelManager>() != null)              reason = "LevelManager";
            else if (exitPivot != null && root == exitPivot.root)            reason = "ExitPivot";
            else if (IsManuallyExcluded(root))                               reason = "ManualExclusion";

            if (reason != null)
                Debug.Log($"[DecorExit] EXCLU : {root.name} ({reason})");
            else
                roots.Add(root);
        }

        return new List<Transform>(roots);
    }

    private bool IsManuallyExcluded(Transform root)
    {
        if (manualExclusions == null) return false;
        foreach (Transform excl in manualExclusions)
            if (excl != null && excl.root == root) return true;
        return false;
    }

    /// <summary>
    /// Retourne true si le GO ou l'un de ses ancetres est tague "Ground" ou sur le layer Ground.
    /// </summary>
    private bool HasGroundAncestor(Transform t)
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        Transform current = t;
        while (current != null)
        {
            if (current.tag == "Ground") return true;
            if (groundLayer >= 0 && current.gameObject.layer == groundLayer) return true;
            current = current.parent;
        }
        return false;
    }

    // ============================================
    // DIRECTION D'EXPULSION
    // ============================================

    private Vector3 GetExitDirection(Transform target)
    {
        // Cas special : sentinel
        if (target.tag == "Sentinel")
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
