using UnityEngine;
using System.Collections;

// Lance un projectile (prefab avec Rigidbody) en parabole vers un point d'atterrissage. Le projectile ne fait
// rien de special : la physique des Rigidbody opere (il peut bousculer ou faire tomber le joueur).
// Declenchement par frequence, ou quand un joueur entre dans le collider trigger pose sur ce meme objet.
// La parabole est calculee avec la gravite normale : projectile sans amortissement (Linear Damping = 0) et sans
// PhysicsProp (gravite x3 et freinage), sinon il tombe avant le point vise. Calcul repris de BombProjectile.Launch.
public class ParabolicLauncher : MonoBehaviour
{
    public enum TriggerMode { Frequency, PlayerTrigger }

    [Header("Projectile")]
    [SerializeField] private GameObject projectilePrefab;
    [Tooltip("Point de depart du tir (vide = cet objet)")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("Point d'atterrissage vise : un objet a deplacer dans la scene")]
    [SerializeField] private Transform landingPoint;
    [Tooltip("Plus l'angle est grand, plus la parabole est haute et lente")]
    [Range(10f, 80f)]
    [SerializeField] private float launchAngle = 45f;
    [Tooltip("Duree de vie du projectile en secondes (0 = il reste)")]
    [SerializeField] private float projectileLifetime = 8f;

    [Header("Rotation")]
    [Tooltip("Rotation donnee au tir, en degres par seconde (0 = aucune)")]
    [SerializeField] private float spinSpeed = 180f;
    [Tooltip("Coche : axe de rotation au hasard a chaque tir. Decoche : la boule roule vers l'avant, dans le sens du tir")]
    [SerializeField] private bool randomSpinAxis = false;

    [Header("Declenchement")]
    [SerializeField] private TriggerMode mode = TriggerMode.Frequency;
    [Tooltip("Frequence : secondes entre deux tirs")]
    [SerializeField] private float interval = 3f;
    [Tooltip("Frequence : delai avant le premier tir (pour decaler plusieurs lanceurs)")]
    [SerializeField] private float startDelay = 0f;
    [Tooltip("Trigger : delai minimal entre deux tirs")]
    [SerializeField] private float triggerCooldown = 2f;

    [Header("Audio")]
    [SerializeField] private AudioClip launchSound;
    [SerializeField] private float soundVolume = 1f;

    private float lastLaunchTime = -999f;
    private AudioSource audioSource;
    private SentinelCycleManager cycleManager;

    private Vector3 SpawnPosition => spawnPoint != null ? spawnPoint.position : transform.position;

    // Tirs seulement pendant le niveau : du demarrage du cycle (pupitre) jusqu'a la victoire.
    // Sans SentinelCycleManager (scene de test), on tire tout le temps.
    private bool LevelRunning => cycleManager == null || cycleManager.IsGameStarted();

    void Start()
    {
        cycleManager = FindFirstObjectByType<SentinelCycleManager>();

        if (launchSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
        }

        if (mode == TriggerMode.Frequency)
            StartCoroutine(FrequencyLoop());
    }

    IEnumerator FrequencyLoop()
    {
        yield return new WaitUntil(() => LevelRunning);
        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        while (true)
        {
            yield return new WaitUntil(() => LevelRunning);
            Launch();
            yield return new WaitForSeconds(Mathf.Max(0.1f, interval));
        }
    }

    // Tout joueur (solo ou multi) qui entre dans le collider trigger de cet objet
    void OnTriggerEnter(Collider other)
    {
        if (mode != TriggerMode.PlayerTrigger || !LevelRunning) return;
        if (other.GetComponentInParent<PlayerPhysicsMovement>() == null) return;
        if (Time.time - lastLaunchTime < triggerCooldown) return;

        Launch();
    }

    public void Launch()
    {
        if (projectilePrefab == null || landingPoint == null)
        {
            Debug.LogWarning("[ParabolicLauncher] " + name + " : projectile ou point d'atterrissage non assigne");
            return;
        }

        lastLaunchTime = Time.time;
        Vector3 start = SpawnPosition;
        GameObject projectile = Instantiate(projectilePrefab, start, Quaternion.identity);

        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogWarning("[ParabolicLauncher] " + projectilePrefab.name + " n'a pas de Rigidbody");
            Destroy(projectile);
            return;
        }
        if (rb.linearDamping > 0f || projectile.GetComponent<PhysicsProp>() != null)
            Debug.LogWarning("[ParabolicLauncher] " + projectilePrefab.name + " a un amortissement ou un PhysicsProp : il tombera avant le point vise");

        rb.isKinematic = false;
        rb.useGravity = true;
        Vector3 velocity = ComputeLaunchVelocity(start, landingPoint.position);
        rb.linearVelocity = velocity;

        if (spinSpeed != 0f)
        {
            Vector3 forward = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 axis = randomSpinAxis || forward.sqrMagnitude < 0.001f
                ? Random.onUnitSphere
                : Vector3.Cross(Vector3.up, forward).normalized;
            float spin = spinSpeed * Mathf.Deg2Rad;
            rb.maxAngularVelocity = Mathf.Max(rb.maxAngularVelocity, Mathf.Abs(spin));
            rb.angularVelocity = axis * spin;
        }

        if (audioSource != null)
            audioSource.PlayOneShot(launchSound, soundVolume);

        if (projectileLifetime > 0f)
            Destroy(projectile, projectileLifetime);
    }

    // Vitesse initiale pour atteindre target depuis start avec l'angle donne (meme formule que BombProjectile)
    private Vector3 ComputeLaunchVelocity(Vector3 start, Vector3 target)
    {
        Vector3 direction = target - start;
        Vector3 horizontalDir = new Vector3(direction.x, 0f, direction.z);
        float horizontal = horizontalDir.magnitude;
        float vertical = direction.y;
        float angle = launchAngle * Mathf.Deg2Rad;
        horizontalDir = horizontal > 0.001f ? horizontalDir / horizontal : transform.forward;

        float v0 = Mathf.Sqrt((Physics.gravity.magnitude * horizontal * horizontal) /
                   (2f * Mathf.Cos(angle) * Mathf.Cos(angle) * (horizontal * Mathf.Tan(angle) - vertical)));

        // Cible trop haute pour cet angle : on tire quand meme, il faudra augmenter l'angle
        if (float.IsNaN(v0) || float.IsInfinity(v0))
            v0 = 10f;

        return horizontalDir * v0 * Mathf.Cos(angle) + Vector3.up * v0 * Mathf.Sin(angle);
    }

    // Parabole dessinee en edition, telle qu'elle sera jouee
    void OnDrawGizmos()
    {
        if (landingPoint == null) return;

        Vector3 start = SpawnPosition;
        Vector3 velocity = ComputeLaunchVelocity(start, landingPoint.position);
        Vector3 gravity = Physics.gravity;

        Gizmos.color = Color.yellow;
        Vector3 previous = start;
        const float step = 0.05f;
        for (float t = step; t < 10f; t += step)
        {
            Vector3 point = start + velocity * t + 0.5f * gravity * t * t;
            Gizmos.DrawLine(previous, point);
            previous = point;
            if (point.y < landingPoint.position.y && velocity.y + gravity.y * t < 0f) break;
        }

        Gizmos.DrawWireSphere(landingPoint.position, 0.3f);
    }
}
