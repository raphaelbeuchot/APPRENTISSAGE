using UnityEngine;
using UnityEngine.Splines;

public class RoamingObstacle : MonoBehaviour, IMovingPlatform
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private bool reverseDirection = false;
    [SerializeField] private bool usePingPong = true;

    [Header("Audio")]
    [SerializeField] private RoamingObstacleSettings settings;

    [Header("Spline")]
    [SerializeField] private SplineContainer splineContainer;

    [Header("Instance Settings")]
    [SerializeField]
    [Range(0f, 1f)]
    private float startingProgress = 0f;

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
        if (splineContainer == null)
        {
            Debug.LogError("[RoamingObstacle] SplineContainer non assigne !");
            enabled = false;
            return;
        }

        // Initialiser la position de depart
        currentProgress = startingProgress;
        lastPosition = transform.position;

        // Setup audio seulement si settings assigne
        if (settings != null)
        {
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
        }

        // Position initiale sur la spline
        UpdatePosition();

        // Initialiser lastPosition
        lastPosition = transform.position;

        Debug.Log("[RoamingObstacle] Obstacle demarre sur spline");
    }

    void Update()
    {
        if (splineContainer == null) return;

        float splineLength = splineContainer.Spline.GetLength();
        float distanceThisFrame = moveSpeed * Time.deltaTime;
        float progressIncrement = distanceThisFrame / splineLength;

        // Appliquer direction
        if (reverseDirection)
            progressIncrement = -progressIncrement;

        // Appliquer direction ping-pong
        progressIncrement *= currentDirection;

        // Avancer
        currentProgress += progressIncrement;

        // Gestion boucle ou ping-pong
        if (usePingPong)
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
        // Verifier si c'est un autre RoamingObstacle OU un objet sur layer "Obstacle"
        RoamingObstacle otherObstacle = other.GetComponent<RoamingObstacle>();
        bool isObstacleLayer = other.gameObject.layer == LayerMask.NameToLayer("Obstacle");

        if (otherObstacle != null || isObstacleLayer)
        {
            // Verifier cooldown pour eviter inversions multiples
            if (Time.time - lastCollisionTime > collisionCooldown)
            {
                // Inverser direction
                currentDirection *= -1f;
                lastCollisionTime = Time.time;

                string collisionType = otherObstacle != null ? "autre RoamingObstacle" : "obstacle";
                Debug.Log($"[RoamingObstacle] Obstacle collision avec {collisionType} detectee - inversion direction");
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