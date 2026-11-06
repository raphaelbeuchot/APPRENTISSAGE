using UnityEngine;

// Rotation continue de decor (gear, helice, disque...) autour d'un axe au choix.
// Pas une plateforme : ne transporte ni joueur ni ennemi (voir RotatingPlatform pour ca).
// Play() / Stop() lancables depuis un UnityEvent (EnemyPoolTrigger, pupitre...).
public class Spinner : MonoBehaviour
{
    public enum Axis { X, Y, Z, Custom }

    [Header("Rotation")]
    [SerializeField] private Axis axis = Axis.Z;
    [Tooltip("Axe libre, utilise si Axis = Custom")]
    [SerializeField] private Vector3 customAxis = Vector3.forward;
    [Tooltip("Axe dans le repere de l'objet (Self) ou du monde (World)")]
    [SerializeField] private Space space = Space.Self;
    [Tooltip("Degres par seconde (negatif = sens inverse)")]
    [SerializeField] private float speed = 90f;

    [Header("Lancement")]
    [SerializeField] private bool playOnStart = true;

    private bool isSpinning;

    private void Start()
    {
        isSpinning = playOnStart;
    }

    private void Update()
    {
        if (!isSpinning) return;
        transform.Rotate(GetAxis(), speed * Time.deltaTime, space);
    }

    public void Play()
    {
        isSpinning = true;
    }

    public void Stop()
    {
        isSpinning = false;
    }

    private Vector3 GetAxis()
    {
        switch (axis)
        {
            case Axis.X: return Vector3.right;
            case Axis.Y: return Vector3.up;
            case Axis.Z: return Vector3.forward;
            default: return customAxis.sqrMagnitude > 0.0001f ? customAxis.normalized : Vector3.forward;
        }
    }
}
