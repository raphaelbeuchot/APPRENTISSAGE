using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Cage d'entre-phases du boss de Rush Hour IV, une par pupitre (champ Cage du BossPupitre) :
/// celle du dernier pupitre active d'une phase emporte le joueur pendant que la scene se reinstalle.
/// Pilotee par la BossTransition.
///
/// Montage : la cage (racine, posee au sol) > Bloc (monte et descend avec le joueur) > Barreaux
/// (caches sous le sol au repos). Les hauteurs se comptent depuis la position de repos de chaque piece,
/// vers le haut de son parent.
///
/// RaiseWithPlayer : prend le joueur (Grab), barreaux qui sortent, bloc qui monte -> onRaised.
/// LowerAndRelease : bloc qui redescend, barreaux qui rentrent, joueur relache (Release) -> onLowered.
///
/// PlacePlayer : pose le joueur sur le point d'accroche, pendant le plan sentinelle (option Center Player).
/// Grab : joueur fige (isFrozen), Rigidbody cinematique, FallTracker coupe, bascule de vue bloquee.
///        Le joueur suit le point d'accroche en gardant son ecart du moment (nul s'il a ete pose).
/// Release : tout remis. FallTracker repart de zero (son OnEnable reprend la position du moment).
/// </summary>
public class BossCage : MonoBehaviour
{
    [Header("Pieces")]
    [Tooltip("Partie qui monte et descend avec le joueur (contient les barreaux). Vide = la cage elle-meme.")]
    [SerializeField] private Transform block;
    [Tooltip("Barreaux, caches sous le sol au repos. Enfant du bloc conseille.")]
    [SerializeField] private Transform bars;
    [Tooltip("Point d'accroche (au niveau du sol de la cage). Vide = le bloc.")]
    [SerializeField] private Transform holdPoint;
    [Tooltip("Coche : le joueur est pose sur le point d'accroche pendant le plan sentinelle (teleportation cachee " +
             "par le changement de plan). Decoche : il garde sa place et suit la cage a son ecart.")]
    [SerializeField] private bool centerPlayer = true;

    [Header("Barreaux")]
    [SerializeField] private float barsHeight = 1.5f;
    [Tooltip("Sortie des barreaux (s).")]
    [Min(0.01f)] [SerializeField] private float barsRaiseDuration = 0.15f;
    [Tooltip("Rentree des barreaux, cage posee (s).")]
    [Min(0.01f)] [SerializeField] private float barsOpenDuration = 0.15f;

    [Header("Bloc")]
    [SerializeField] private float blockHeight = 3.5f;
    [Min(0.01f)] [SerializeField] private float blockRaiseDuration = 1.5f;
    [Min(0.01f)] [SerializeField] private float blockLowerDuration = 1.5f;

    [Header("Effets")]
    [Tooltip("Bloc arrive en haut, joueur dedans.")]
    [SerializeField] private UnityEvent onRaised;
    [Tooltip("Cage posee, barreaux rentres, joueur relache.")]
    [SerializeField] private UnityEvent onLowered;

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
    private Vector3 holdOffset; // position du joueur dans le repere du point d'accroche, prise au Grab

    private Vector3 blockRest;
    private Vector3 barsRest;
    private Coroutine moveCoroutine;

    private Transform Block => block != null ? block : transform;
    private Transform Anchor => holdPoint != null ? holdPoint : Block;

    private void Awake()
    {
        blockRest = Block.localPosition;
        if (bars != null) barsRest = bars.localPosition;
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

    // ============================================
    // SEQUENCES
    // ============================================

    /// <summary>Prend le joueur, sort les barreaux, monte le bloc. onDone (et onRaised) a l'arrivee en haut.</summary>
    public void RaiseWithPlayer(System.Action onDone = null)
    {
        StartMove(RaiseCoroutine(onDone));
    }

    /// <summary>Redescend le bloc, rentre les barreaux, relache le joueur. onDone (et onLowered) a la fin.</summary>
    public void LowerAndRelease(System.Action onDone = null)
    {
        StartMove(LowerCoroutine(onDone));
    }

    [ContextMenu("Monter avec le joueur")]
    private void RaiseFromMenu() => RaiseWithPlayer();

    [ContextMenu("Redescendre et relacher")]
    private void LowerFromMenu() => LowerAndRelease();

    private void StartMove(IEnumerator routine)
    {
        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        moveCoroutine = StartCoroutine(routine);
    }

    private IEnumerator RaiseCoroutine(System.Action onDone)
    {
        Grab();
        if (bars != null)
            yield return Move(bars, barsRest + Vector3.up * barsHeight, barsRaiseDuration);
        yield return Move(Block, blockRest + Vector3.up * blockHeight, blockRaiseDuration);

        moveCoroutine = null;
        Debug.Log("[BOSS CAGE] En haut : " + name);
        onRaised?.Invoke();
        onDone?.Invoke();
    }

    private IEnumerator LowerCoroutine(System.Action onDone)
    {
        yield return Move(Block, blockRest, blockLowerDuration);
        if (bars != null)
            yield return Move(bars, barsRest, barsOpenDuration);
        Release();

        moveCoroutine = null;
        Debug.Log("[BOSS CAGE] Posee : " + name);
        onLowered?.Invoke();
        onDone?.Invoke();
    }

    private static IEnumerator Move(Transform target, Vector3 toLocal, float duration)
    {
        Vector3 fromLocal = target.localPosition;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            target.localPosition = Vector3.Lerp(fromLocal, toLocal, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        target.localPosition = toLocal;
    }

    // ============================================
    // JOUEUR
    // ============================================

    /// <summary>
    /// Pose le joueur sur le point d'accroche (si "Center Player" est coche). Appele par la BossTransition
    /// pendant le plan sentinelle, pour que la teleportation ne se voie pas.
    /// </summary>
    public void PlacePlayer()
    {
        if (!centerPlayer || player == null) return;
        Vector3 p = Anchor.position;
        player.position = p;
        if (rb != null)
        {
            rb.position = p;
            if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
        }
        Debug.Log("[BOSS CAGE] Joueur pose dans la cage : " + name);
    }

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

        holdOffset = Anchor.InverseTransformPoint(player.position);
        Debug.Log("[BOSS CAGE] Joueur pris : " + name);
    }

    [ContextMenu("Relacher le joueur (Release)")]
    public void Release()
    {
        if (!isHolding) return;
        isHolding = false;

        FollowAnchor();

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

        Debug.Log("[BOSS CAGE] Joueur relache : " + name);
    }

    private void LateUpdate()
    {
        if (isHolding) FollowAnchor();
    }

    private void FollowAnchor()
    {
        if (player == null) return;
        Vector3 p = Anchor.TransformPoint(holdOffset);
        player.position = p;
        if (rb != null) rb.position = p;
    }
}
