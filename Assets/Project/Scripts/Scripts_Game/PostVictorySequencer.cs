using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;

/// <summary>
/// Orchestre la sequence post-victoire une fois le wipe termine :
///   1. Lights dim via Global Volume (ambiance bleutee)
///   2. [Etape 5] Sol qui s'enfonce -> TransitionRoom
///
/// Setup : ajouter sur le meme GO que LevelManager.
/// Assigner le Global Volume de la scene dans l'Inspector.
/// </summary>
public class PostVictorySequencer : MonoBehaviour
{
    public static PostVictorySequencer Instance { get; private set; }

    [Header("Lights Dim")]
    [Tooltip("Le Global Volume de la scene (celui qui contient le profil post-process)")]
    [SerializeField] private Volume globalVolume;

    [Tooltip("Valeur cible du weight du volume apres dim (0 = normal, 1 = profil plein)")]
    [SerializeField] private float dimTargetWeight = 1f;

    [Tooltip("Duree du fondu des lumieres en secondes")]
    [SerializeField] private float dimDuration = 1.5f;

    private float dimStartWeight = 0f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    /// <summary>
    /// Point d'entree : appele par LevelManager quand le wipe est termine.
    /// </summary>
    public void StartPostVictorySequence()
    {
        Debug.Log("[PostVictory] Sequence demarre.");
        StartCoroutine(SequenceCoroutine());
    }

    private IEnumerator SequenceCoroutine()
    {
        // --- Etape 4 : lights dim ---
        yield return StartCoroutine(DimLights());

        // --- Etape 5 (TODO) : sol qui s'enfonce + TransitionRoom ---
        // Joueur freeze ici en attendant l'implementation de TransitionRoom
        Debug.Log("[PostVictory] Lights dim OK. En attente de TransitionRoom (Etape 5).");
    }

    private IEnumerator DimLights()
    {
        if (globalVolume == null)
        {
            Debug.LogWarning("[PostVictory] Pas de Global Volume assigne — lights dim skippee.");
            yield break;
        }

        dimStartWeight = globalVolume.weight;
        float elapsed = 0f;

        Debug.Log($"[PostVictory] Dim lights : {dimStartWeight:F2} -> {dimTargetWeight:F2} sur {dimDuration}s");

        while (elapsed < dimDuration)
        {
            elapsed += Time.deltaTime;
            globalVolume.weight = Mathf.Lerp(dimStartWeight, dimTargetWeight, elapsed / dimDuration);
            yield return null;
        }

        globalVolume.weight = dimTargetWeight;
        Debug.Log("[PostVictory] Lights dim termine.");
    }
}
