using UnityEngine;

/// <summary>
/// Rotation de la sentinelle du boss de Rush Hour IV (sentinelle + ses cercles, separes du GiratoireReverse).
/// Simple decor qui tourne : ce n'est pas une plateforme mobile (pas d'IMovingPlatform), rien ne monte dessus.
/// Stop : bloque d'un coup (a brancher sur onPhaseComplete). Resume : repart a pleine vitesse
/// (a brancher sur BossPhaseManager.onCycleResumed).
/// Astuce : une camera en enfant de cet objet garde toujours le meme cadrage par rapport aux cercles.
/// </summary>
public class BossSentinelRotator : MonoBehaviour
{
    [Header("Rotation")]
    [Tooltip("Vitesse (degres par seconde). Negatif = sens inverse.")]
    [SerializeField] private float speed = 20f;
    [Tooltip("Axe de rotation.")]
    [SerializeField] private Vector3 axis = Vector3.up;
    [Tooltip("Coche : axe dans le repere de l'objet. Decoche : axe du monde.")]
    [SerializeField] private bool localAxis = false;
    [Tooltip("Tourne des le lancement de la scene.")]
    [SerializeField] private bool rotateOnStart = true;

    [Header("Debug")]
    [SerializeField] private bool isRotating = false;

    public bool IsRotating => isRotating;

    private void Start()
    {
        isRotating = rotateOnStart;
    }

    private void Update()
    {
        if (!isRotating || axis == Vector3.zero) return;
        transform.Rotate(axis.normalized, speed * Time.deltaTime, localAxis ? Space.Self : Space.World);
    }

    [ContextMenu("Arreter")]
    public void Stop()
    {
        if (!isRotating) return;
        isRotating = false;
        Debug.Log("[BOSS SENTINEL] Rotation arretee");
    }

    [ContextMenu("Reprendre")]
    public void Resume()
    {
        if (isRotating) return;
        isRotating = true;
        Debug.Log("[BOSS SENTINEL] Rotation reprise");
    }

    /// <summary>Vitesse de rotation actuelle (radians par seconde, axe monde). Zero a l'arret.</summary>
    public Vector3 AngularVelocity
    {
        get
        {
            if (!isRotating || axis == Vector3.zero) return Vector3.zero;
            Vector3 worldAxis = localAxis ? transform.TransformDirection(axis.normalized) : axis.normalized;
            return worldAxis * (speed * Mathf.Deg2Rad);
        }
    }

    /// <summary>Vitesse (m/s) d'un point qui tourne avec la sentinelle. Sert aux ampoules qui s'echappent d'un cercle.</summary>
    public Vector3 GetVelocityAtPoint(Vector3 worldPoint)
    {
        return Vector3.Cross(AngularVelocity, worldPoint - transform.position);
    }
}
