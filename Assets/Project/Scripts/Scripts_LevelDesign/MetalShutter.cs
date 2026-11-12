using UnityEngine;

/// <summary>
/// Rideau metallique de depart, ouvert par le pupitre (PupitreInteraction, MultiLevelStart).
/// Deux modes :
/// - Levee (defaut, comportement historique) : tout le GO monte de "Open Height" ;
/// - Ouverture laterale : "Left Panel" coulisse en -X et "Right Panel" en +X (X local du shutter) de
///   "Slide Distance". Mettre les colliders sur les panneaux, pas sur la racine.
/// Dans les deux cas le shutter entier est detruit "Destroy Delay" apres l'arrivee
/// (EnemyHealthBarManager se sert de sa presence).
/// </summary>
public class MetalShutter : MonoBehaviour
{
    public enum OpeningMode { Lift, SlideApart }

    [Header("Opening Settings")]
    public OpeningMode mode = OpeningMode.Lift;
    public float openSpeed = 2f;
    public float destroyDelay = 0.5f;

    [Header("Levee")]
    public float openHeight = 5f;

    [Header("Ouverture laterale")]
    [Tooltip("Panneau qui coulisse en -X local du shutter.")]
    public Transform leftPanel;
    [Tooltip("Panneau qui coulisse en +X local du shutter.")]
    public Transform rightPanel;
    [Tooltip("Distance parcourue par chaque panneau (m).")]
    public float slideDistance = 3f;

    private Vector3 startPosition;
    private Vector3 targetPosition;
    private Vector3 leftTarget;
    private Vector3 rightTarget;
    private bool isOpening = false;

    void Start()
    {
        startPosition = transform.position;
        targetPosition = startPosition + Vector3.up * openHeight;

        Vector3 side = transform.right; // X local du shutter, en monde
        if (leftPanel != null) leftTarget = leftPanel.position - side * slideDistance;
        if (rightPanel != null) rightTarget = rightPanel.position + side * slideDistance;

        if (mode == OpeningMode.SlideApart && leftPanel == null && rightPanel == null)
            Debug.LogWarning("[MetalShutter] Ouverture laterale sans panneau assigne : " + name, this);
    }

    void Update()
    {
        if (!isOpening) return;

        bool arrived;
        if (mode == OpeningMode.SlideApart)
        {
            // & (pas &&) : les deux panneaux bougent a chaque frame
            arrived = MovePanel(leftPanel, leftTarget) & MovePanel(rightPanel, rightTarget);
        }
        else
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, openSpeed * Time.deltaTime);
            arrived = Vector3.Distance(transform.position, targetPosition) < 0.01f;
        }

        if (arrived)
        {
            isOpening = false;
            Destroy(gameObject, destroyDelay);
        }
    }

    // Renvoie true quand le panneau est arrive (ou absent)
    private bool MovePanel(Transform panel, Vector3 target)
    {
        if (panel == null) return true;
        panel.position = Vector3.MoveTowards(panel.position, target, openSpeed * Time.deltaTime);
        return Vector3.Distance(panel.position, target) < 0.01f;
    }

    // Methode appelee depuis PupitreInteraction
    public void StartOpening()
    {
        if (!isOpening)
            isOpening = true;
    }

    // Ancienne methode - garde pour compatibilite si utilisee ailleurs
    public void Open()
    {
        StartOpening();
    }
}
