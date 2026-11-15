using System.Collections;
using UnityEngine;

/// <summary>
/// Rideau qui s'ouvre en faisant coulisser un ou deux panneaux (couloir de fin du boss).
/// Ne s'ouvre que par Open() : en general depuis un CurtainTrigger pose a distance,
/// ou depuis un evenement (UnityEvent, Timeline...).
///
/// Modes (directions = axes locaux de ce GameObject, le rideau peut etre tourne) :
/// - Lateral deux panneaux : Left Panel en -X, Right Panel en +X ;
/// - Lateral un panneau : Single Panel en -X ou +X (Single Direction) ;
/// - Par le haut : Single Panel en +Y ;
/// - Haut / bas : Top Panel en +Y, Bottom Panel en -Y.
///
/// Le rideau reste ouvert, rien n'est detruit. Mettre les colliders sur les panneaux.
/// </summary>
public class SlidingCurtain : MonoBehaviour
{
    public enum OpeningMode { LateralTwoPanels, LateralOnePanel, Lift, SplitTopBottom }
    public enum Side { Left, Right }

    [Header("Ouverture")]
    [SerializeField] private OpeningMode mode = OpeningMode.LateralTwoPanels;
    [Tooltip("Attente entre l'ordre d'ouverture (Open) et le debut du mouvement (s). Sert a decaler les rideaux " +
             "d'une cascade lancee par un seul trigger.")]
    [Min(0f)] [SerializeField] private float delay = 0f;
    [Tooltip("Temps d'ouverture (s).")]
    [SerializeField] private float duration = 1f;
    [Tooltip("Distance parcourue par chaque panneau (m).")]
    [SerializeField] private float distance = 3f;

    [Header("Lateral deux panneaux")]
    [SerializeField] private Transform leftPanel;
    [SerializeField] private Transform rightPanel;

    [Header("Lateral un panneau / Par le haut")]
    [SerializeField] private Transform singlePanel;
    [Tooltip("Lateral un panneau seulement : cote vers lequel le panneau part.")]
    [SerializeField] private Side singleDirection = Side.Left;

    [Header("Haut / bas")]
    [SerializeField] private Transform topPanel;
    [SerializeField] private Transform bottomPanel;

    private bool opened = false;

    public bool IsOpened => opened;

    /// <summary>Ouvre le rideau (une seule fois).</summary>
    public void Open()
    {
        if (opened) return;
        opened = true;
        StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        Vector3 right = transform.right;
        Vector3 up = transform.up;

        Transform a = null, b = null;
        Vector3 dirA = Vector3.zero, dirB = Vector3.zero;

        switch (mode)
        {
            case OpeningMode.LateralTwoPanels:
                a = leftPanel; dirA = -right;
                b = rightPanel; dirB = right;
                break;
            case OpeningMode.LateralOnePanel:
                a = singlePanel; dirA = singleDirection == Side.Left ? -right : right;
                break;
            case OpeningMode.Lift:
                a = singlePanel; dirA = up;
                break;
            case OpeningMode.SplitTopBottom:
                a = topPanel; dirA = up;
                b = bottomPanel; dirB = -up;
                break;
        }

        if (a == null && b == null)
        {
            Debug.LogWarning("[SlidingCurtain] Aucun panneau assigne pour le mode " + mode + " : " + name, this);
            yield break;
        }

        Vector3 startA = a != null ? a.position : Vector3.zero;
        Vector3 startB = b != null ? b.position : Vector3.zero;
        Vector3 endA = startA + dirA * distance;
        Vector3 endB = startB + dirB * distance;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            if (a != null) a.position = Vector3.Lerp(startA, endA, t);
            if (b != null) b.position = Vector3.Lerp(startB, endB, t);
            yield return null;
        }

        if (a != null) a.position = endA;
        if (b != null) b.position = endB;
    }
}
