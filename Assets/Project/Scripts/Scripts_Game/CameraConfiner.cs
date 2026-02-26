using UnityEngine;
public class CameraConfiner : MonoBehaviour
{
    [SerializeField] private Collider boundingVolume;
    [SerializeField] private Transform player;
    [SerializeField] private float cameraZMarginFromPlayer = 2f;

    private void Start()
    {
        if (player == null)
        {
            GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null)
                player = playerGO.transform;
        }
    }
    private void LateUpdate()
    {
        if (boundingVolume != null)
        {
            transform.position = boundingVolume.ClosestPoint(transform.position);
        }

        if (player != null)
        {
            Vector3 camPos = transform.position;
            float maxZ = player.position.z - cameraZMarginFromPlayer;
            if (camPos.z > maxZ)
            {
                transform.position = new Vector3(camPos.x, camPos.y, maxZ);
            }
        }
    }

    public void SetBoundingVolume(Collider newVolume)
    {
        boundingVolume = newVolume;
        Debug.Log("[CameraConfiner] Bounding volume mis a jour");
    }
}