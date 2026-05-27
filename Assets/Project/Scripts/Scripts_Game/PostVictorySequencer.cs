using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;
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

    [Tooltip("TargetGroupProxy a desactiver des le debut de la sequence (evite les erreurs Infinity quand le sentinel est expulse)")]
    [SerializeField] private TargetGroupProxy targetGroupProxy;

    [Header("5c — Decor Exit")]
    [Tooltip("Sequenceur d'expulsion du decor")]
    [SerializeField] private DecorExitSequencer decorExit;

    [Header("5d — EndCurtain")]
    [Tooltip("Le rideau de fin a lever une fois le decor vide")]
    [SerializeField] private EndCurtainRise endCurtain;

    [Header("5d+ — Apres rideau leve")]
    [Tooltip("GOs dont le Renderer est desactive apres le lever du rideau (ground, murs, lumieres...)")]
    [SerializeField] private GameObject[] objectsToHide;

    [Tooltip("Son joue quand le volume postvictory est desactive (optionnel)")]
    [InspectorName("Allumage lumiere transition room")]
    [SerializeField] private AudioClip lightsOffSound;

    [Header("5d+ — Camera TransitionRoom")]
    [Tooltip("CinemachineCamera a activer une fois le rideau leve")]
    [SerializeField] private CinemachineCamera cm_transitionRoom;
    [SerializeField] private int cm_transitionRoomPriority = 20;

    [Header("6a — Porte TransitionRoom")]
    [Tooltip("La porte a activer une fois le rideau leve")]
    [SerializeField] private TransitionRoomDoor transitionRoomDoor;

    [Header("5b — Lights Out")]
    [Tooltip("Le Global Volume de la scene (profil avec ColorAdjustments)")]
    [SerializeField] private Volume globalVolume;

    [Tooltip("Intervalle en secondes entre chaque flash")]
    [SerializeField] private float lightsOutInterval = 1f;

    [Tooltip("3 couleurs successives vers la teinte finale (la 3e = etat definitif)")]
    [SerializeField] private Color[] lightsOutColors = new Color[]
    {
        new Color(0.807f, 0.876f, 1f),   // flash 1 — (206, 223, 255)
        new Color(0.613f, 0.751f, 1f),   // flash 2 — (156, 191, 255)
        new Color(0.420f, 0.627f, 1f),   // flash 3 — (107, 160, 255) final
    };

    [Tooltip("Son joue a chaque palier (doit avoir la meme longueur que lightsOutColors, ou laisser vide)")]
    [SerializeField] private AudioClip[] lightsOutSounds;

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
        // Desactiver le TargetGroupProxy immediatement -- evite les erreurs Infinity quand le sentinel est expulse
        if (targetGroupProxy != null)
            targetGroupProxy.enabled = false;

        // --- 5a : GoalDoor disparait + reprise controle joueur ---
        yield return StartCoroutine(Step5a_GoalDoorDisappears());

        // --- 5b : lights out (placeholder lerp — sera remplace par flashs) ---
        yield return StartCoroutine(Step5b_LightsOut());

        // --- 5c + 5d en parallele : vidage decor + lever du rideau ---
        yield return StartCoroutine(Step5c5d_DecorExitAndCurtainRise());

        // --- 5d+ : Camera TransitionRoom ---
        if (cm_transitionRoom != null)
            cm_transitionRoom.Priority = cm_transitionRoomPriority;

        // --- 5d+ : Volume off + objets du niveau caches ---
        DisableLevelLights();

        // --- 6a : TransitionRoomDoor activee ---
        if (transitionRoomDoor != null)
            transitionRoomDoor.Enable();

        Debug.Log("[PostVictory] Sequence complete.");
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
    // ETAPE 5c + 5d — DECOR EXIT + CURTAIN RISE (parallele)
    // ============================================

    private IEnumerator Step5c5d_DecorExitAndCurtainRise()
    {
        // Flags initiaux : true si le composant est absent (pas besoin d'attendre)
        bool decorDone  = decorExit  == null;
        bool curtainDone = endCurtain == null;

        if (decorExit != null)
        {
            decorExit.OnDecorExitComplete += () => decorDone = true;
            decorExit.StartExit();
        }
        else Debug.LogWarning("[PostVictory] Pas de DecorExitSequencer assigne — etape 5c skippee.");

        if (endCurtain != null)
        {
            endCurtain.OnRiseComplete += () => curtainDone = true;
            endCurtain.Rise();
        }
        else Debug.LogWarning("[PostVictory] Pas d'EndCurtain assigne — etape 5d skippee.");

        yield return new WaitUntil(() => decorDone && curtainDone);
    }

    // ============================================
    // ETAPE 5d+ — LUMIERES NIVEAU ETEINTES
    // ============================================

    private void DisableLevelLights()
    {
        // Volume post-processing off
        if (globalVolume != null)
        {
            globalVolume.gameObject.SetActive(false);
            if (audioSource != null && lightsOffSound != null)
                audioSource.PlayOneShot(lightsOffSound);
            Debug.Log("[PostVictory] Global Volume desactive.");
        }

        // Renderers des GOs du niveau a cacher
        if (objectsToHide != null)
        {
            int count = 0;
            foreach (GameObject go in objectsToHide)
            {
                if (go == null) continue;
                Renderer rend = go.GetComponent<Renderer>();
                if (rend != null) { rend.enabled = false; count++; }
            }
            Debug.Log($"[PostVictory] {count} renderers desactives.");
        }
    }

    // ============================================
    // ETAPE 5b — LIGHTS OUT (flashs saccades)
    // ============================================

    private IEnumerator Step5b_LightsOut()
    {
        if (globalVolume == null)
        {
            Debug.LogWarning("[PostVictory] Pas de Global Volume assigne — lights out skippee.");
            yield break;
        }

        if (!globalVolume.profile.TryGet<ColorAdjustments>(out var colorAdj))
        {
            Debug.LogWarning("[PostVictory] Pas de ColorAdjustments dans le profil — lights out skippee.");
            yield break;
        }

        // Active le volume au premier flash
        globalVolume.weight = 1f;

        for (int i = 0; i < lightsOutColors.Length; i++)
        {
            colorAdj.colorFilter.Override(lightsOutColors[i]);

            if (audioSource != null && lightsOutSounds != null && i < lightsOutSounds.Length && lightsOutSounds[i] != null)
                audioSource.PlayOneShot(lightsOutSounds[i]);

            Debug.Log($"[PostVictory] Flash {i + 1}/{lightsOutColors.Length} — {lightsOutColors[i]}");
            yield return new WaitForSeconds(lightsOutInterval);
        }

        Debug.Log("[PostVictory] Lights out OK.");
    }
}
