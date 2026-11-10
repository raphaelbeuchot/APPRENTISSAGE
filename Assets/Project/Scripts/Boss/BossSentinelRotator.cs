using System.Collections;
using UnityEngine;

/// <summary>
/// Rotation de la sentinelle du boss de Rush Hour IV (sentinelle + ses cercles, separes du GiratoireReverse).
/// Simple decor qui tourne : ce n'est pas une plateforme mobile (pas d'IMovingPlatform), rien ne monte dessus.
/// ReturnToZero : revient a sa rotation de depart en continuant dans le meme sens, en ralentissant, en
/// "Return Duration" (a brancher sur onPhaseComplete : meme cadrage quel que soit le dernier pupitre).
/// Stop : bloque d'un coup. Resume : repart a pleine vitesse (a brancher sur BossPhaseManager.onCycleResumed).
/// Une camera en enfant de cet objet tourne avec la sentinelle : la camera du plan sentinelle doit etre
/// hors de cet objet (fixe), sinon elle suivrait le retour a zero.
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

    [Header("Retour a zero")]
    [Tooltip("Duree maximale du retour a la rotation de depart (s). Part de la vitesse actuelle, ralentit jusqu'a l'arret, " +
             "sans jamais repartir en arriere ; plus court si la sentinelle est presque arrivee. " +
             "A garder sous la duree du fondu vers la camera sentinelle.")]
    [Min(0.01f)] [SerializeField] private float returnDuration = 1f;

    [Header("Debug")]
    [SerializeField] private bool isRotating = false;
    [Tooltip("Angle parcouru depuis la rotation de depart (degres, 0 a 360, dans le sens de rotation).")]
    [SerializeField] private float angle = 0f;

    public bool IsRotating => isRotating;

    private Quaternion startLocalRotation;
    private Coroutine returnCoroutine;

    private void Start()
    {
        startLocalRotation = transform.localRotation;
        isRotating = rotateOnStart;
    }

    private void Update()
    {
        if (!isRotating) return;
        RotateBy(speed * Time.deltaTime);
    }

    // Tourne de "degrees" autour de l'axe et tient le compte de l'angle depuis le depart
    private void RotateBy(float degrees)
    {
        if (axis == Vector3.zero) return;
        transform.Rotate(axis.normalized, degrees, localAxis ? Space.Self : Space.World);
        angle = Mathf.Repeat(angle + degrees, 360f);
    }

    [ContextMenu("Revenir a zero")]
    public void ReturnToZero()
    {
        if (returnCoroutine != null) StopCoroutine(returnCoroutine);
        returnCoroutine = StartCoroutine(ReturnCoroutine());
    }

    private IEnumerator ReturnCoroutine()
    {
        // Vitesse de depart : celle de la rotation si elle tournait, sinon zero (depart en douceur)
        float v0 = isRotating ? Mathf.Abs(speed) : 0f;
        isRotating = false;

        // Angle restant jusqu'au depart, dans le sens de rotation
        float remaining = speed >= 0f ? 360f - angle : angle;
        if (remaining >= 360f - 0.01f || remaining <= 0.01f) remaining = 0f;

        if (remaining > 0f)
        {
            // Courbe d'Hermite : part a v0, arrive a l'arret pile sur 0. Elle ne recule jamais tant que
            // v0 * T <= 3 * remaining : si la sentinelle est presque arrivee, on raccourcit la duree
            float duration = returnDuration;
            if (v0 > 0f) duration = Mathf.Min(duration, 3f * remaining / v0);

            float sign = speed >= 0f ? 1f : -1f;
            float done = 0f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float s = Mathf.Clamp01(elapsed / duration);
                float target = v0 * duration * (s - 2f * s * s + s * s * s) + remaining * (3f * s * s - 2f * s * s * s);
                RotateBy(sign * (target - done));
                done = target;
                yield return null;
            }
        }

        transform.localRotation = startLocalRotation;
        angle = 0f;
        returnCoroutine = null;
        Debug.Log("[BOSS SENTINEL] Revenue a zero");
    }

    [ContextMenu("Arreter")]
    public void Stop()
    {
        if (returnCoroutine != null) { StopCoroutine(returnCoroutine); returnCoroutine = null; }
        if (!isRotating) return;
        isRotating = false;
        Debug.Log("[BOSS SENTINEL] Rotation arretee");
    }

    [ContextMenu("Reprendre")]
    public void Resume()
    {
        if (returnCoroutine != null) { StopCoroutine(returnCoroutine); returnCoroutine = null; }
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
