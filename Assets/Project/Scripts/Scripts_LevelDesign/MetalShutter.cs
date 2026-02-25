using UnityEngine;

public class MetalShutter : MonoBehaviour
{
    [Header("Opening Settings")]
    public float openHeight = 5f;
    public float openSpeed = 2f;
    public float destroyDelay = 0.5f;

    private Vector3 startPosition;
    private Vector3 targetPosition;
    private bool isOpening = false;

    void Start()
    {
        startPosition = transform.position;
        targetPosition = startPosition + Vector3.up * openHeight;
    }

    void Update()
    {
        if (isOpening)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                openSpeed * Time.deltaTime
            );

            // Si arrivé en haut
            if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
            {
                
                isOpening = false;
            }
        }
    }

    // NOUVELLE méthode appelée depuis PupitreInteraction
    public void StartOpening()
    {
        if (!isOpening)
        {
            isOpening = true;
            PlatformTrainManager train = FindFirstObjectByType<PlatformTrainManager>();
            if (train != null)
                train.StartTrain();
            Destroy(gameObject, destroyDelay);
        }
    }

    // Ancienne méthode - garde pour compatibilité si utilisée ailleurs
    public void Open()
    {
        StartOpening();
    }
}