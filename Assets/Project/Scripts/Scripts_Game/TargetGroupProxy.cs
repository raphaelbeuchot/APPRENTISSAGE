using UnityEngine;

public class TargetGroupProxy : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Transform referenceGO;
    [SerializeField] private float yOffsetMultiplier = 2f;
    [SerializeField] private float maxZTravelForYOffset = 5f;
    [SerializeField] private float zOffsetMultiplier = 2f;
    [SerializeField] private float maxZTravelForZOffset = 5f;
    [SerializeField] private CameraPanningExtension cameraPanning;

    private float yOffset = 0f;
    private float zOffset = 0f;
    private float lastPlayerZ;

    void Start()
    {
        if (player != null)
            lastPlayerZ = player.position.z;
    }

    void Update()
    {
        if (player == null) return;

        float playerZDelta = player.position.z - lastPlayerZ;
        lastPlayerZ = player.position.z;

        bool isLowView = cameraPanning != null && cameraPanning.isLowView;

        // Offset Y (desactive en vue basse)
        if (!isLowView)
        {
            if (playerZDelta < 0f)
            {
                yOffset += -playerZDelta;
                yOffset = Mathf.Min(yOffset, maxZTravelForYOffset);
            }
            else if (playerZDelta > 0f)
            {
                yOffset -= playerZDelta;
                yOffset = Mathf.Max(0f, yOffset);
            }
        }

        // Offset Z (toujours actif)
        if (playerZDelta < 0f)
        {
            zOffset += -playerZDelta;
            zOffset = Mathf.Min(zOffset, maxZTravelForZOffset);
        }
        else if (playerZDelta > 0f)
        {
            zOffset -= playerZDelta;
            zOffset = Mathf.Max(0f, zOffset);
        }

        float baseY = referenceGO != null ? referenceGO.position.y : transform.position.y;
        float baseZ = referenceGO != null ? referenceGO.position.z : transform.position.z;

        transform.position = new Vector3(
            player.position.x,
            baseY + yOffset * yOffsetMultiplier,
            baseZ - zOffset * zOffsetMultiplier
        );
    }
}