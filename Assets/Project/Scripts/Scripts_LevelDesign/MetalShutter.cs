using UnityEngine;

public class MetalShutter : MonoBehaviour
{
    public float openHeight = 5f;
    public float openSpeed = 2f;
    public float destroyDelay = 0.5f; // Temps avant destruction apres montee

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

            // Si arrive en haut
            if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
            {
                Debug.Log("[MetalShutter] Montee terminee, destruction dans " + destroyDelay + "s");
                Destroy(gameObject, destroyDelay);
                isOpening = false; // Arreter l'update
            }
        }
    }

    public void Open()
    {
        Debug.Log("Rideau s'ouvre!");
        isOpening = true;
    }
}