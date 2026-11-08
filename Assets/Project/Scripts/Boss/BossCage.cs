using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Cage d'entre-phases du boss de Rush Hour IV : tient le joueur sur un point d'accroche qui bouge,
/// le temps que le decor s'installe, puis le relache.
/// Le script ne deplace rien : le trajet de la cage (descente, montee, deplacement, atterrissage)
/// vient d'une animation ou d'une Timeline. Grab / Release s'appellent depuis les effets de phase
/// du BossPhaseManager, un signal de Timeline, ou le menu du composant.
///
/// Grab : joueur fige (isFrozen), Rigidbody cinematique, colle au point d'accroche chaque frame,
///        FallTracker coupe, bascule de vue bloquee, camera de transition 0 prioritaire.
/// Release : tout remis, le joueur repart la ou est la cage. FallTracker repart de zero
///           (son OnEnable reprend la position du moment) : relacher une fois la cage posee.
///
/// Cameras : mises a "transitionPriority" (au-dessus des 20 de CameraPanningExtension) pour prendre
/// la main, remises a 0 pour la rendre a la vue active (normale ou basse).
/// </summary>
public class BossCage : MonoBehaviour
{
    [Header("Accroche")]
    [Tooltip("Point ou le joueur est tenu (enfant de la cage, au niveau du sol de la cage). Vide = la cage elle-meme.")]
    [SerializeField] private Transform holdPoint;

    [Header("Cameras de transition")]
    [Tooltip("Cameras de l'entre-phases. Grab montre la premiere ; ShowCamera(i) pour passer aux suivantes.")]
    [SerializeField] private List<CinemachineCamera> transitionCameras = new List<CinemachineCamera>();
    [Tooltip("Doit rester au-dessus de 20 (priorite de la vue active dans CameraPanningExtension).")]
    [SerializeField] private int transitionPriority = 30;

    [Header("Debug")]
    [SerializeField] private bool isHolding = false;

    public bool IsHolding => isHolding;

    private Transform player;
    private PlayerPhysicsMovement movement;
    private Rigidbody rb;
    private FallTracker fallTracker;
    private CameraPanningExtension cameraPanning;

    private bool wasKinematic;
    private RigidbodyInterpolation previousInterpolation;
    private bool fallTrackerWasEnabled;

    private void Awake()
    {
        // Les cameras de transition ne doivent jamais prendre la main hors de la cage
        foreach (CinemachineCamera cam in transitionCameras)
            if (cam != null) cam.Priority = 0;
    }

    private void Start()
    {
        GameObject go = GameObject.FindGameObjectWithTag("Player");
        if (go == null)
        {
            Debug.LogWarning("[BOSS CAGE] Pas de joueur (tag Player)");
            return;
        }

        player = go.transform;
        movement = go.GetComponentInParent<PlayerPhysicsMovement>();
        rb = movement != null ? movement.GetComponent<Rigidbody>() : go.GetComponentInParent<Rigidbody>();
        fallTracker = go.GetComponentInParent<FallTracker>();
        if (fallTracker == null) fallTracker = go.GetComponentInChildren<FallTracker>();
        if (rb != null) player = rb.transform;

        cameraPanning = FindObjectOfType<CameraPanningExtension>();
    }

    private Transform Anchor => holdPoint != null ? holdPoint : transform;

    [ContextMenu("Prendre le joueur (Grab)")]
    public void Grab()
    {
        if (isHolding || player == null) return;
        isHolding = true;

        if (movement != null) movement.isFrozen = true;

        if (rb != null)
        {
            wasKinematic = rb.isKinematic;
            previousInterpolation = rb.interpolation;
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
            // Position posee a la main en LateUpdate : l'interpolation la contredirait
            rb.interpolation = RigidbodyInterpolation.None;
        }

        if (fallTracker != null)
        {
            fallTrackerWasEnabled = fallTracker.enabled;
            fallTracker.enabled = false;
        }

        if (cameraPanning != null) cameraPanning.LockViewToggle();

        if (transitionCameras.Count > 0) ShowCamera(0);

        SnapToAnchor();
        Debug.Log("[BOSS CAGE] Joueur pris");
    }

    [ContextMenu("Relacher le joueur (Release)")]
    public void Release()
    {
        if (!isHolding) return;
        isHolding = false;

        SnapToAnchor();

        if (rb != null)
        {
            rb.isKinematic = wasKinematic;
            rb.interpolation = previousInterpolation;
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        if (movement != null) movement.isFrozen = false;

        // Reactive apres le placement : OnEnable repart de la position actuelle
        if (fallTracker != null && fallTrackerWasEnabled) fallTracker.enabled = true;

        if (cameraPanning != null) cameraPanning.UnlockViewToggle();

        HideCameras();
        Debug.Log("[BOSS CAGE] Joueur relache");
    }

    /// <summary>Montre la camera de transition d'indice i (les autres repassent a 0).</summary>
    public void ShowCamera(int index)
    {
        for (int i = 0; i < transitionCameras.Count; i++)
            if (transitionCameras[i] != null)
                transitionCameras[i].Priority = (i == index) ? transitionPriority : 0;
    }

    /// <summary>Rend la main a la vue active du joueur.</summary>
    public void HideCameras()
    {
        foreach (CinemachineCamera cam in transitionCameras)
            if (cam != null) cam.Priority = 0;
    }

    private void LateUpdate()
    {
        if (isHolding) SnapToAnchor();
    }

    private void SnapToAnchor()
    {
        if (player == null) return;
        Vector3 p = Anchor.position;
        player.position = p;
        if (rb != null) rb.position = p;
    }
}
