using UnityEngine;

// Pince decorative a deux machoires : claque en chasse, se ferme en grab, repos sinon.
// Lit seulement l'etat de l'ennemi (EnemyAI_AStar, GrabAttack), ne modifie rien.
// Chaque machoire est enfant d'un pivot vide place a la charniere ; le script tourne les pivots.
// Angle 0 = pose des pivots dans le prefab. Angle positif = machoires qui s'ecartent.
public class EnemyClaw : MonoBehaviour
{
    [Header("Machoires")]
    public Transform leftPivot;
    public Transform rightPivot;
    [Tooltip("Axe de rotation, dans le repere local des pivots")]
    public Vector3 axis = Vector3.up;
    [Tooltip("Cocher si les machoires se croisent au lieu de s'ecarter")]
    public bool invert = false;

    [Header("Angles (degres)")]
    public float restAngle = 10f;
    public float openAngle = 35f;
    public float closedAngle = 0f;

    [Header("Chasse")]
    [Tooltip("Battements par seconde (ouvert -> ferme -> ouvert)")]
    [Min(0f)] public float chaseSpeed = 2f;

    [Header("Windup du grab")]
    public bool openWideOnWindup = true;
    public float windupAngle = 50f;

    [Header("Lissage")]
    [Tooltip("Plus haut = suit plus vite l'angle vise")]
    [Min(0.1f)] public float smoothing = 15f;

    [Header("Debug (lecture seule)")]
    [Unused] public string mode = "";

    EnemyAI_AStar ai;
    GrabAttack grab;
    Quaternion leftBase, rightBase;
    float currentAngle;
    float phase;

    void Awake()
    {
        ai = GetComponentInParent<EnemyAI_AStar>();
        if (leftPivot) leftBase = leftPivot.localRotation;
        if (rightPivot) rightBase = rightPivot.localRotation;
        currentAngle = restAngle;
    }

    void Update()
    {
        // GrabAttack est ajoute au lancement par EnemyAI_AStar : on le cherche tant qu'il manque
        if (grab == null && ai != null) grab = ai.GetComponent<GrabAttack>();

        float target = restAngle;

        if (grab != null && (grab.isGrabbing || grab.isFakeGrabbing))
        {
            target = closedAngle;
            mode = "Grab";
        }
        else if (openWideOnWindup && grab != null && grab.isInWindup)
        {
            target = windupAngle;
            mode = "Windup";
        }
        else if (ai != null && (ai.currentState == EnemyAI_AStar.State.Chasing
                             || ai.currentState == EnemyAI_AStar.State.Attacking))
        {
            phase += Time.deltaTime * chaseSpeed * Mathf.PI * 2f;
            if (phase > Mathf.PI * 2f) phase -= Mathf.PI * 2f;
            float t = 0.5f - 0.5f * Mathf.Cos(phase); // 0 = ferme, 1 = ouvert
            target = Mathf.Lerp(closedAngle, openAngle, t);
            mode = "Chasse";
        }
        else
        {
            phase = 0f;
            mode = "Repos";
        }
        if (grab == null) mode += " (GrabAttack introuvable)";

        currentAngle = Mathf.Lerp(currentAngle, target, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
        Apply(currentAngle);
    }

    void Apply(float angle)
    {
        float a = invert ? -angle : angle;
        Vector3 ax = axis.sqrMagnitude > 0f ? axis.normalized : Vector3.up;
        if (leftPivot) leftPivot.localRotation = leftBase * Quaternion.AngleAxis(a, ax);
        if (rightPivot) rightPivot.localRotation = rightBase * Quaternion.AngleAxis(-a, ax);
    }
}
