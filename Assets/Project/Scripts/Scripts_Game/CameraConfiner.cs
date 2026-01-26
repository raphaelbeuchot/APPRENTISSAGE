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
    public void SetBoundingVolume(Collider newVolume)
    {
        boundingVolume = newVolume;
        Debug.Log("[CameraConfiner] Bounding volume mis a jour");
    }
}