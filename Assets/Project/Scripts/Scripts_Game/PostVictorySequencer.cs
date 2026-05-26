using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;

/// <summary>
/// Orchestre la sequence post-victoire une fois le wipe termine.
/// Setup : ajouter sur le meme GO que LevelManager.
/// </summary>
public class PostVictorySequencer : MonoBehaviour
{
    public static PostVictorySequencer Instance { get; private set; }

    // ============================================
    // INSPECTOR
    // ============================================

    [Header("5a — GoalDoor")]
    [Tooltip("Son joue quand la GoalDoor disparait (optionnel)")]
    [SerializeField] private AudioClip goalDoorDisappearSound;

    [Tooltip("AudioSource utilisee pour les sons de la sequence (optionnel — si null, PlayOneShot ne jouera pas)")]
    [SerializeField] private AudioSource audioSource;

    [Header("5b — Lights Out (placeholder : lerp continu, sera remplace par flashs)")]
    [Tooltip("Le Global Volume de la scene (profil post-process post-victoire)")]
    [SerializeField] private Volume globalVolume;

    [Tooltip("Valeur cible du weight (1 = profil plein)")]
    [SerializeField] private float dimTargetWeight = 1f;

    [Tooltip("Duree du fondu en secondes")]
    [SerializeField] private float dimDuration = 1.5f;

    // ============================================
    // UNITY LIFECYCLE
    // ============================================

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    // ============================================
    // API PUBLIQUE
    // ============================================

    /// <summary>Point d'entree : appele par LevelManager quand le wipe est termine.</summary>
    public void StartPostVictorySequence()
    {
        Debug.Log("[PostVictory] Sequence demarre.");
        StartCoroutine(SequenceCoroutine());
    }

    // ============================================
    // SEQUENCE
    // ============================================

    private IEnumerator SequenceCoroutine()
    {
        // --- 5a : GoalDoor disparait + reprise controle joueur ---
        yield return StartCoroutine(Step5a_GoalDoorDisappears());

        // --- 5b : lights out (placeholder lerp — sera remplace par flashs) ---
        yield return StartCoroutine(Step5b_LightsOut());

        // --- 5c (TODO) : vidage decor ---
        // --- 5d (TODO) : EndCurtain se leve ---
        // --- 6a (TODO) : TransitionRoomDoor activee ---
        Debug.Log("[PostVictory] 5a+5b OK. Suite a implementer.");
    }

    // ============================================
    // ETAPE 5a — GOALDOOR DISPARAIT
    // ============================================

    private IEnumerator Step5a_GoalDoorDisappears()
    {
        LevelManager lm = GetComponent<LevelManager>();

        // Detruire la GoalDoor
        GameObject doorGO = null;
        if (lm != null && lm.goalDoor != null)
            doorGO = lm.goalDoor.gameObject;
        else if (lm != null && lm.goalDoorNew != null)
            doorGO = lm.goalDoorNew.gameObject;

        if (doorGO != null)
        {
            if (audioSource != null && goalDoorDisappearSound != null)
                audioSource.PlayOneShot(goalDoorDisappearSound);

            Destroy(doorGO);
            Debug.Log("[PostVictory] GoalDoor detruite.");
        }
        else
        {
            Debug.LogWarning("[PostVictory] GoalDoor introuvable — etape 5a skippee.");
        }

        // Re-afficher le mesh joueur (cache depuis VictoryScale)
        if (lm != null && lm.player != null)
        {
            PlayerVictoryScale pvs = lm.player.GetComponent<PlayerVictoryScale>();
            if (pvs != null) pvs.ShowPlayerMesh();

            // Reprise du controle immédiate
            lm.player.enabled = true;
            Debug.Log("[PostVictory] Joueur visible + controle rendu.");
        }

        yield break;
    }

    // ============================================
    // ETAPE 5b — LIGHTS OUT (placeholder)
    // ============================================

    private IEnumerator Step5b_LightsOut()
    {
        if (globalVolume == null)
        {
            Debug.LogWarning("[PostVictory] Pas de Global Volume assigne — lights out skippee.");
            yield break;
        }

        float startWeight = globalVolume.weight;
        float elapsed = 0f;

        while (elapsed < dimDuration)
        {
            elapsed += Time.deltaTime;
            globalVolume.weight = Mathf.Lerp(startWeight, dimTargetWeight, elapsed / dimDuration);
            yield return null;
        }

        globalVolume.weight = dimTargetWeight;
        Debug.Log("[PostVictory] Lights out OK.");
    }
}
