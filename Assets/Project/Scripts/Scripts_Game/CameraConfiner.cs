using UnityEngine;

public class CameraConfiner : MonoBehaviour
{
    [SerializeField] private Collider boundingVolume;

    private void LateUpdate()
    {
        if (boundingVolume != null)
        {
            transform.position = boundingVolume.ClosestPoint(transform.position);
        }
    }
}