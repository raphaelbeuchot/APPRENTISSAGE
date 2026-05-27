using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;
using System.Collections;

/// <summary>
/// Orchestre la sequence post-victoire une fois le wipe termine.
/// Setup : GO dedie VictoryManager (ou sur LevelManager, compatible).
/// Appele via PostVictorySequencer.Instance.StartPostVictorySequence().
/// </summary>
public class PostVictorySequencer : MonoBehaviour
{
    public static PostVictorySequencer Instance { get; private set; }

    // ============================================
    // INSPECTOR
    // ============================================

    [Header("5a — GoalDoor")]
    [Tooltip("GO de la GoalDoor a detruire (GoalDoor ou GoalDoorNew selon le niveau)")]
    [SerializeField] private GameObject goalDoor;

    [HideInInspector] [SerializeField] private PlayerPhysicsMovement player;

    [Tooltip("Son joue quand la GoalDoor disparait (optionnel)")]
    [SerializeField] private AudioClip goalDoorDisappearSound;

    [HideInInspector] [SerializeField] private AudioSource audioSource;

    [HideInInspector] [SerializeField] private TargetGroupProxy targetGroupProxy;

    [Header("5c — Decor Exit")]
    [HideInInspector] [SerializeField] private DecorExitSequencer decorExit;

    [Header("5d — EndCurtain")]
    [HideInInspector] [SerializeField] private EndCurtainRise endCurtain;

    [Header("5d+ — Apres rideau leve")]
    [Tooltip("GOs supplementaires a cacher en plus du layer Ground (murs, lumieres specifiques au niveau...)")]
    [SerializeField] private GameObject[] objectsToHide;

    [Tooltip("Duree du fade-out du volume PostVictory apres le lever du rideau (0 = coupure immediate)")]
    [SerializeField] private float volumeFadeOutDuration = 1.5f;

    [Tooltip("Son joue quand le volume postvictory est desactive (optionnel)")]
    [InspectorName("Allumage lumiere transition room")]
    [SerializeField] private AudioClip lightsOffSound;

    [Header("5d+ — Camera TransitionRoom")]
    [HideInInspector] [SerializeField] private CinemachineCamera cm_transitionRoom;
    [SerializeField] private int cm_transitionRoomPriority = 20;

    [Header("6a — Porte TransitionRoom")]
    [HideInInspector] [SerializeField] private TransitionRoomDoor transitionRoomDoor;

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
        AutoAssign();
    }

    private void AutoAssign()
    {
        if (player == null)
            player = FindObjectOfType<PlayerPhysicsMovement>();

        if (goalDoor == null)
        {
            GoalDoor gd = FindObjectOfType<GoalDoor>();
            if (gd != null) goalDoor = gd.gameObject;
        }

        if (targetGroupProxy == null)
            targetGroupProxy = FindObjectOfType<TargetGroupProxy>();

        if (decorExit == null)
            decorExit = GetComponent<DecorExitSequencer>();

        if (endCurtain == null)
            endCurtain = FindObjectOfType<EndCurtainRise>();

        if (transitionRoomDoor == null)
            transitionRoomDoor = FindObjectOfType<TransitionRoomDoor>();

        if (cm_transitionRoom == null)
        {
            GameObject go = GameObject.FindWithTag("CameraTransitionRoom");
            if (go != null) cm_transitionRoom = go.GetComponent<CinemachineCamera>();
        }

        if (globalVolume == null)
        {
            GameObject go = GameObject.FindWithTag("VolumePostVictory");
            if (go != null) globalVolume = go.GetComponent<Volume>();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }
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
        // Detruire la GoalDoor
        if (goalDoor != null)
        {
            if (audioSource != null && goalDoorDisappearSound != null)
                audioSource.PlayOneShot(goalDoorDisappearSound);

            Destroy(goalDoor);
            Debug.Log("[PostVictory] GoalDoor detruite.");
        }
        else
        {
            Debug.LogWarning("[PostVictory] GoalDoor non assignee — etape 5a skippee.");
        }

        // Re-afficher le mesh joueur (cache depuis VictoryScale)
        if (player != null)
        {
            PlayerVictoryScale pvs = player.GetComponent<PlayerVictoryScale>();
            if (pvs != null) pvs.ShowPlayerMesh();

            // Reprise du controle immediate
            player.enabled = true;
            Debug.Log("[PostVictory] Joueur visible + controle rendu.");
        }
        else
        {
            Debug.LogWarning("[PostVictory] Player non assigne — reprise controle skippee.");
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
            if (audioSource != null && lightsOffSound != null)
                audioSource.PlayOneShot(lightsOffSound);

            if (volumeFadeOutDuration > 0f)
                StartCoroutine(FadeOutVolume(globalVolume, volumeFadeOutDuration));
            else
                globalVolume.gameObject.SetActive(false);

            Debug.Log("[PostVictory] Global Volume : debut fade-out.");
        }

        int count = 0;

        // Auto : tous les renderers sur le layer Ground
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer != -1)
        {
            foreach (Renderer rend in FindObjectsOfType<Renderer>())
            {
                if (rend.gameObject.layer == groundLayer)
                {
                    rend.enabled = false;
                    count++;
                }
            }
        }
        else
        {
            Debug.LogWarning("[PostVictory] Layer 'Ground' introuvable — auto-hide Ground skippee.");
        }

        // Manuel : objectsToHide (extras niveau-specifiques)
        if (objectsToHide != null)
        {
            foreach (GameObject go in objectsToHide)
            {
                if (go == null) continue;
                Renderer rend = go.GetComponent<Renderer>();
                if (rend != null) { rend.enabled = false; count++; }
            }
        }

        Debug.Log($"[PostVictory] {count} renderers desactives (Ground auto + manuel).");
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

    // ============================================
    // UTILITAIRES — VOLUME FADE
    // ============================================

    private IEnumerator FadeOutVolume(Volume volume, float duration)
    {
        float startWeight = volume.weight;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            volume.weight = Mathf.Lerp(startWeight, 0f, elapsed / duration);
            yield return null;
        }

        volume.weight = 0f;
        volume.gameObject.SetActive(false);
        Debug.Log("[PostVictory] Volume fade-out termine.");
    }
}
