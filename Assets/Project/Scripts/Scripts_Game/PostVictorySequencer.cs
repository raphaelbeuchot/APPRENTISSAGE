using UnityEngine;
using UnityEngine.Rendering;
using Unity.Cinemachine;
using System.Collections;

/// <summary>
/// Orchestre la sequence post-victoire une fois le wipe termine :
///   4. Lights dim via Global Volume (ambiance post-process)
///   5. Teleport joueur -> TransitionRoom, dalle qui s'enfonce
///   6. Joueur libre -> portes (niveau suivant / restart)
///
/// Setup : ajouter sur le meme GO que LevelManager.
/// </summary>
public class PostVictorySequencer : MonoBehaviour
{
    public static PostVictorySequencer Instance { get; private set; }

    // ============================================
    // INSPECTOR
    // ============================================

    [Header("Etape 4 — Lights Dim")]
    [Tooltip("Le Global Volume de la scene (profil post-process post-victoire)")]
    [SerializeField] private Volume globalVolume;

    [Tooltip("Valeur cible du weight (1 = profil plein)")]
    [SerializeField] private float dimTargetWeight = 1f;

    [Tooltip("Duree du fondu en secondes")]
    [SerializeField] private float dimDuration = 1.5f;

    [Header("Etape 5 — TransitionRoom")]
    [Tooltip("Ou teleporter le joueur au debut de la TransitionRoom (sommet de la dalle)")]
    [SerializeField] private Transform transitionRoomSpawnPoint;

    [Tooltip("La dalle sur laquelle le joueur descend")]
    [SerializeField] private DalleVictory dalle;

    [Tooltip("Camera Cinemachine dediee a la TransitionRoom")]
    [SerializeField] private CinemachineCamera cm_transitionRoom;

    [Tooltip("Priorite de la camera TransitionRoom (doit depasser celle de CM_Victory)")]
    [SerializeField] private int cm_transitionRoomPriority = 25;

    [Tooltip("Temps d'attente (s) pour que le blend camera soit etabli avant de demarrer la dalle")]
    [SerializeField] private float cameraBlendWait = 0.8f;

    [Header("Etape 6 — Portes")]
    [Tooltip("Portes a activer une fois la dalle posee (joueur libre de choisir)")]
    [SerializeField] private TransitionRoomDoor[] doors;

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
        // --- Etape 4 : lights dim ---
        yield return StartCoroutine(DimLights());

        // --- Etape 5 : TransitionRoom ---
        yield return StartCoroutine(TransitionRoomSequence());

        Debug.Log("[PostVictory] Sequence complete.");
    }

    // ============================================
    // ETAPE 4 — DIM LIGHTS
    // ============================================

    private IEnumerator DimLights()
    {
        if (globalVolume == null)
        {
            Debug.LogWarning("[PostVictory] Pas de Global Volume assigne — lights dim skippee.");
            yield break;
        }

        float startWeight = globalVolume.weight;
        float elapsed = 0f;

        Debug.Log($"[PostVictory] Dim lights : {startWeight:F2} -> {dimTargetWeight:F2} sur {dimDuration}s");

        while (elapsed < dimDuration)
        {
            elapsed += Time.deltaTime;
            globalVolume.weight = Mathf.Lerp(startWeight, dimTargetWeight, elapsed / dimDuration);
            yield return null;
        }

        globalVolume.weight = dimTargetWeight;
        Debug.Log("[PostVictory] Lights dim OK.");
    }

    // ============================================
    // ETAPE 5 — TRANSITION ROOM
    // ============================================

    private IEnumerator TransitionRoomSequence()
    {
        // Recuperer le joueur via LevelManager (meme GO)
        LevelManager lm = GetComponent<LevelManager>();
        PlayerPhysicsMovement player = lm != null ? lm.player : FindObjectOfType<PlayerPhysicsMovement>();

        if (player == null)
        {
            Debug.LogWarning("[PostVictory] Player introuvable — TransitionRoom skippee.");
            yield break;
        }

        // --- Teleporter le joueur (avant de lever la camera) ---
        // On utilise le Rigidbody pour eviter les conflits physiques
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (transitionRoomSpawnPoint != null)
        {
            if (rb != null)
                rb.position = transitionRoomSpawnPoint.position;
            else
                player.transform.position = transitionRoomSpawnPoint.position;

            player.transform.rotation = transitionRoomSpawnPoint.rotation;
        }

        // --- Re-afficher le mesh du joueur (cache depuis VictoryScale) ---
        PlayerVictoryScale pvs = player.GetComponent<PlayerVictoryScale>();
        if (pvs == null) pvs = FindObjectOfType<PlayerVictoryScale>();
        if (pvs != null) pvs.ShowPlayerMesh();

        // --- Switcher vers la camera TransitionRoom ---
        if (cm_transitionRoom != null)
            cm_transitionRoom.Priority = cm_transitionRoomPriority;

        // Attendre que le blend soit etabli
        yield return new WaitForSeconds(cameraBlendWait);

        // --- Descente de la dalle ---
        if (dalle != null)
        {
            bool descentDone = false;
            dalle.OnDescentComplete += () => descentDone = true;
            dalle.StartDescent(player.transform);
            yield return new WaitUntil(() => descentDone);
        }
        else
        {
            Debug.LogWarning("[PostVictory] Pas de DalleVictory assignee — descente skippee.");
        }

        // --- Etape 6 : joueur libre + portes activees ---
        player.enabled = true;
        Debug.Log("[PostVictory] Joueur libre — portes activees.");

        if (doors != null)
        {
            foreach (TransitionRoomDoor door in doors)
                if (door != null) door.Enable();
        }
    }
}
