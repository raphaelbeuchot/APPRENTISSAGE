using UnityEngine;

public class MetalShutter : MonoBehaviour
{
    public float openHeight = 5f;
    public float openSpeed = 2f;

    private Vector3 startPosition;
    private bool isOpening = false;

    void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        
        if (isOpening)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                startPosition + Vector3.up * openHeight,
                openSpeed * Time.deltaTime
            );
        }
    }

    public void Open()
    {
        Debug.Log("Rideau s'ouvre!");
        isOpening = true;
    }
}