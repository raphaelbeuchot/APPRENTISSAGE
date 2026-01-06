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

        // Calculer distance parcourue ce frame
        float splineLength = splineContainer.Spline.GetLength();
        float distanceThisFrame = settings.moveSpeed * Time.deltaTime;
        float progressIncrement = distanceThisFrame / splineLength;

        // Appliquer direction
        if (settings.reverseDirection)
            progressIncrement = -progressIncrement;

        // Avancer sur la spline
        currentProgress += progressIncrement;

        // Boucler automatiquement
        if (currentProgress > 1f)
            currentProgress -= 1f;
        else if (currentProgress < 0f)
            currentProgress += 1f;

        // Mettre a jour position
        UpdatePosition();
    }

    private void UpdatePosition()
    {
        // Evaluer position sur spline
        Vector3 position = splineContainer.EvaluatePosition(currentProgress);
        transform.position = position;

        // Optionnel : orienter l'obstacle dans la direction du mouvement
        // (desactive pour l'instant, on garde rotation fixe)
    }

    void OnDestroy()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }
}