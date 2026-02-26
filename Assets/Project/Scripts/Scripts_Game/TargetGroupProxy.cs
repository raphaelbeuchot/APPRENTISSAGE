using UnityEngine;

public class TargetGroupProxy : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform referenceMainSentinel;
    [SerializeField] private CameraPanningExtension cameraPanning;

    [Header("Y Offset")]
    [SerializeField] private float yOffsetMultiplier = 2f;
    [SerializeField] private float maxZTravelForYOffset = 5f;

    [Header("Z Offset")]
    [SerializeField] private float zOffsetMultiplier = 2f;
    [SerializeField] private float maxZTravelForZOffset = 5f;

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

        float baseY = referenceMainSentinel != null ? referenceMainSentinel.position.y : transform.position.y;
        float baseZ = referenceMainSentinel != null ? referenceMainSentinel.position.z : transform.position.z;

        transform.position = new Vector3(
            player.position.x,
            baseY + yOffset * yOffsetMultiplier,
            baseZ - zOffset * zOffsetMultiplier
        );
    }
}