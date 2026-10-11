using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

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
    [Tooltip("Cocher pour skipper le demantelage du decor (lumieres et rideau restent actifs)")]
    [SerializeField] private bool skipDecorExit = false;
    [HideInInspector] [SerializeField] private DecorExitSequencer decorExit;

    // Tout objet tague Sentinel monte (avec ses enfants), puis est desactive. Un objet tague sous un autre
    // objet tague monte deja avec lui (pas de double montee). Auto au Awake.
    private readonly List<Transform> risingObjects = new List<Transform>();
    [Tooltip("Duree de la montee des objets tagues Sentinel (si skipDecorExit)")]
    [SerializeField] private float sentinelRiseDuration = 2f;
    [Tooltip("Distance de montee en Y (si skipDecorExit)")]
    [SerializeField] private float sentinelRiseDistance = 12f;
    [Tooltip("Securite : delai maximum d'attente des montees (rideau, objets tagues) avant de poursuivre la sequence")]
    [SerializeField] private float maxRiseWait = 10f;

    [Header("Fin du VictoryScale")]
    [Tooltip("GOs desactives (SetActive false) des que le fond de l'ecran de victoire couvre la scene, juste apres le VictoryScale")]
    [SerializeField] private GameObject[] objectsToHideOnVictory;

    [Header("5d — EndCurtain")]
    [HideInInspector] [SerializeField] private EndCurtainRise endCurtain;

    // X world de la GoalDoor, capturé avant sa destruction pour DecorExit
    private float _goalDoorWorldX;

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

        // Tous les objets tagues Sentinel (actifs) ; on ne garde que les plus hauts dans la hierarchie
        risingObjects.Clear();
        foreach (GameObject go in GameObject.FindGameObjectsWithTag("Sentinel"))
            risingObjects.Add(go.transform);
        risingObjects.RemoveAll(t => risingObjects.Any(other => other != t && t.IsChildOf(other)));

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

    /// <summary>Appele par VictoryUI quand le fond couvre la scene (fin du VictoryScale).</summary>
    public void OnVictoryScreenCovered()
    {
        // Retour en vue normale si le joueur etait en vue basse (invisible : sous le fond)
        foreach (CameraPanningExtension panning in FindObjectsOfType<CameraPanningExtension>())
            panning.ForceNormalView();

        if (objectsToHideOnVictory == null) return;

        foreach (GameObject go in objectsToHideOnVictory)
        {
            if (go != null)
                go.SetActive(false);
        }
        Debug.Log($"[PostVictory] {objectsToHideOnVictory.Length} objet(s) desactive(s) a la fin du VictoryScale.");
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
        // Capturer le X avant destruction (reference pour DecorExit)
        _goalDoorWorldX = goalDoor != null ? goalDoor.transform.position.x : 0f;

        // Detruire la GoalDoor
        if (goalDoor != null)
        {
            if (audioSource != null && goalDoorDisappearSound != null)
                audioSource.PlayOneShot(goalDoorDisappearSound);

            Destroy(goalDoor);
            Debug.Log($"[PostVictory] GoalDoor detruite (pivotX capture : {_goalDoorWorldX}).");
        }
        else
        {
            Debug.LogWarning("[PostVictory] GoalDoor non assignee — etape 5a skippee.");
        }

        // Re-afficher le mesh joueur (cache depuis VictoryScale)
        if (player != null)
        {
            PlayerVictoryScale pvs = player.GetComponentInChildren<PlayerVictoryScale>();
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
        // Flags initiaux : true si le composant est absent ou l'etape skippee (pas besoin d'attendre)
        bool decorDone    = decorExit   == null || skipDecorExit;
        bool curtainDone  = endCurtain  == null;

        if (!skipDecorExit && decorExit != null)
        {
            decorExit.OnDecorExitComplete += () => decorDone = true;
            decorExit.StartExit(_goalDoorWorldX);
        }
        else
        {
            if (skipDecorExit) Debug.Log("[PostVictory] Decor exit skippee (skipDecorExit = true).");
            else Debug.LogWarning("[PostVictory] Pas de DecorExitSequencer assigne — etape 5c skippee.");
        }

        // Tout objet tague Sentinel monte (avec ses enfants) puis est desactive
        int risingPending = 0;
        if (skipDecorExit)
        {
            if (risingObjects.Count == 0)
                Debug.LogWarning("[PostVictory] skipDecorExit=true mais aucun objet tague Sentinel — rien ne monte.");
            foreach (Transform rising in risingObjects)
            {
                risingPending++;
                StartCoroutine(RiseAndHide(rising, () => risingPending--));
            }
        }

        // Rustine (2026-10-09) : les cercles concentriques hors des objets tagues montent aussi.
        // A retirer quand les niveaux concernes auront leurs cercles tagues (colonne Cercles du LD Catalog).
        if (skipDecorExit)
        {
            foreach (ConcentricCirclesSpawner circles in FindObjectsOfType<ConcentricCirclesSpawner>())
            {
                if (risingObjects.Any(r => circles.transform.IsChildOf(r))) continue;
                risingPending++;
                StartCoroutine(RiseAndHide(circles.transform, () => risingPending--));
            }
        }

        if (endCurtain != null)
        {
            endCurtain.OnRiseComplete += () => curtainDone = true;
            endCurtain.Rise();
        }
        else Debug.LogWarning("[PostVictory] Pas d'EndCurtain assigne — etape 5d skippee.");

        // Securite : ne jamais rester bloque. Rideau desactive pendant sa montee (ex. range sous un objet tague)
        // ou delai depasse -> on continue avec un avertissement.
        float startTime = Time.time;
        yield return new WaitUntil(() =>
            (decorDone && curtainDone && risingPending == 0)
            || (!curtainDone && endCurtain != null && !endCurtain.gameObject.activeInHierarchy)
            || Time.time - startTime > maxRiseWait);

        if (!curtainDone && endCurtain != null && !endCurtain.gameObject.activeInHierarchy)
            Debug.LogWarning("[PostVictory] L'EndCurtain a ete desactive avant la fin de sa montee (range sous un objet tague Sentinel ?) — sequence poursuivie.");
        else if (!(decorDone && curtainDone && risingPending == 0))
            Debug.LogWarning($"[PostVictory] Montees non terminees apres {maxRiseWait} s (decor {decorDone}, rideau {curtainDone}, objets en cours {risingPending}) — sequence poursuivie.");
    }

    // Montee des objets tagues Sentinel (et de la rustine des cercles) : distance/duree communes, puis desactivation.
    private IEnumerator RiseAndHide(Transform target, Action onDone)
    {
        Vector3 startPos = target.position;
        Vector3 endPos   = startPos + Vector3.up * sentinelRiseDistance;
        float elapsed    = 0f;

        while (elapsed < sentinelRiseDuration && target != null)
        {
            elapsed += Time.deltaTime;
            target.position = Vector3.Lerp(startPos, endPos, Mathf.Clamp01(elapsed / sentinelRiseDuration));
            yield return null;
        }

        if (target != null)
            target.gameObject.SetActive(false);

        onDone?.Invoke();
        Debug.Log($"[PostVictory] {(target != null ? target.name : "?")} monte et cache.");
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
