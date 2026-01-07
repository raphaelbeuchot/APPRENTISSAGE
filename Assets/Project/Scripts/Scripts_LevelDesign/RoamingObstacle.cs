using UnityEngine;
using UnityEngine.Splines;

public class RoamingObstacle : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private RoamingObstacleSettings settings;

    [Header("Spline")]
    [SerializeField] private SplineContainer splineContainer;

    private float currentProgress = 0f;
    private AudioSource audioSource;

    private float currentDirection = 1f;

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

        // Setup audio
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f; // 3D sound
        audioSource.volume = settings.soundVolume;

        if (settings.movementSound != null)
        {
            audioSource.clip = settings.movementSound;
            audioSource.Play();
        }

        // Position initiale sur la spline
        UpdatePosition();

        Debug.Log($"[RoamingObstacle] {settings.obstacleName} demarre sur spline");
    }

    void Update()
    {
        if (splineContainer == null || settings == null) return;

        float splineLength = splineContainer.Spline.GetLength();
        float distanceThisFrame = settings.moveSpeed * Time.deltaTime;
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
                currentDirection = -1f; // Inverser
            }
            else if (currentProgress <= 0f)
            {
                currentProgress = 0f;
                currentDirection = 1f; // Inverser
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
    }

    private void UpdatePosition()
    {
        // Evaluer position sur spline
        Vector3 position = splineContainer.EvaluatePosition(currentProgress);

        // Offset pour que la BASE de l'obstacle touche la spline
        // Calcule automatiquement selon la taille du collider
        Collider col = GetComponent<Collider>();
        float yOffset = 0f;

        if (col != null)
        {
            yOffset = col.bounds.extents.y; // Moitie de la hauteur
        }

        position.y += yOffset;
        transform.position = position;
    }

    void OnDestroy()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }
}