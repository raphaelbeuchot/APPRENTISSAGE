using UnityEngine;

public class SilhouetteCameraSync : MonoBehaviour
{
    private Camera silhouetteCam;

    void Start()
    {
        silhouetteCam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (Camera.main != null && silhouetteCam != null)
        {
            silhouetteCam.projectionMatrix = Camera.main.projectionMatrix;
        }
    }
}