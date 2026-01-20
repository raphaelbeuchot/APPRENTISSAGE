using UnityEngine;
using UnityEngine.Splines;

public class RoamingObstacle : MonoBehaviour, IMovingPlatform
{
    [Header("Settings")]
    [SerializeField] private RoamingObstacleSettings settings;

    [Header("Spline")]
    [SerializeField] private SplineContainer splineContainer;

    [Header("Instance Settings")]
    [SerializeField]
    [Range(0f, 1f)]
    private float startingProgress = 0f;

    [SerializeField]
    [Range(0.1f, 3f)]
    private float speedMultiplier = 1f;

    private float currentProgress = 0f;
    private AudioSource audioSource;
    private float currentDirection = 1f;

    // Pour calculer la velocite
    private Vector3 lastPosition;
    private Vector3 currentVelocity;

    // Gestion collision
    private float lastCollisionTime = -999f;
    private float collisionCooldown = 0.5f;

    void Start()
    {
        if (settings == null)
        {
            Debug.LogError("[RoamingObstacle] Settings non assignes !");
            enabled = false;
            return;
        }

        if (splineContainer == null)
        {
            Debug.LogError("[RoamingObstacle] SplineContainer non assigne !");
            enabled = false;
            return;
        }

        // Initialiser la position de depart
        currentProgress = startingProgress;
        lastPosition = transform.position;

        // Setup audio
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.volume = settings.soundVolume;

        if (settings.movementSound != null)
        {
            audioSource.clip = settings.movementSound;
            audioSource.Play();
        }

        // Position initiale sur la spline
        UpdatePosition();

        // Initialiser lastPosition
        lastPosition = transform.position;

        Debug.Log($"[RoamingObstacle] {settings.obstacleName} demarre sur spline");
    }

    void Update()
    {
        if (splineContainer == null || settings == null) return;

        float splineLength = splineContainer.Spline.GetLength();
        float distanceThisFrame = settings.moveSpeed * speedMultiplier * Time.deltaTime;
        float progressIncrement = distanceThisFrame / splineLength;

        // Appliquer direction
        if (settings.reverseDirection)
            progressIncrement = -progressIncrement;

        // Appliquer direction ping-pong
        progressIncrement *= currentDirection;

        // Avancer
        currentProgress += progressIncrement;

        // Gestion boucle ou ping-pong
        if (settings.usePingPong)
        {
            // Aller-retour
            if (currentProgress >= 1f)
            {
                currentProgress = 1f;
                currentDirection = -1f;
            }
            else if (currentProgress <= 0f)
            {
                currentProgress = 0f;
                currentDirection = 1f;
            }
        }
        else
        {
            // Boucle fermee
            if (currentProgress > 1f)
                currentProgress -= 1f;
            else if (currentProgress < 0f)
                currentProgress += 1f;
        }

        UpdatePosition();

        // Calculer la velocite apres avoir bouge
        CalculateVelocity();
    }

    private void UpdatePosition()
    {
        // Evaluer position sur spline
        Vector3 position = splineContainer.EvaluatePosition(currentProgress);

        // Offset pour que la BASE de l'obstacle touche la spline
        Collider col = GetComponent<Collider>();
        float yOffset = 0f;

        if (col != null)
        {
            yOffset = col.bounds.extents.y;
        }

        position.y += yOffset;
        transform.position = position;
    }

    private void CalculateVelocity()
    {
        currentVelocity = (transform.position - lastPosition) / Time.deltaTime;
        lastPosition = transform.position;
    }

    // IMPLEMENTATION INTERFACE IMovingPlatform
    public Vector3 GetPlatformVelocity()
    {
        return currentVelocity;
    }

    public Transform GetTransform()
    {
        return transform;
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log("TRIGGER DETECTE !"); // TEST

        // Verifier si c'est un autre RoamingObstacle
        RoamingObstacle otherObstacle = other.GetComponent<RoamingObstacle>();

        if (otherObstacle != null)
        {
            // Verifier cooldown pour eviter inversions multiples
            if (Time.time - lastCollisionTime > collisionCooldown)
            {
                // Inverser direction
                currentDirection *= -1f;
                lastCollisionTime = Time.time;

                Debug.Log($"[RoamingObstacle] {settings.obstacleName} croisement detecte - inversion direction");
            }
        }
    }
    void OnDestroy()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }
}