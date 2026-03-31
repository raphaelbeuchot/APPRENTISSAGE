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
    [SerializeField] private float offsetYdeBase = -10f ;
    private float positiveZTravel = 0f;
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

        if (playerZDelta > 0f)
        {
            if (zOffset > 0f)
            {
                zOffset -= playerZDelta;
                zOffset = Mathf.Max(0f, zOffset);
            }
            else
            {
                positiveZTravel += playerZDelta;
                positiveZTravel = Mathf.Min(positiveZTravel, maxZTravelForZOffset);
            }
        }
        else if (playerZDelta < 0f)
        {
            if (positiveZTravel > 0f)
            {
                positiveZTravel += playerZDelta;
                positiveZTravel = Mathf.Max(0f, positiveZTravel);
            }
            else
            {
                zOffset += -playerZDelta;
                zOffset = Mathf.Min(zOffset, maxZTravelForZOffset);
            }
        }

        float baseY = referenceMainSentinel != null ? referenceMainSentinel.position.y : transform.position.y;
        float baseZ = referenceMainSentinel != null ? referenceMainSentinel.position.z : transform.position.z;

        float proxyZ = baseZ + positiveZTravel - zOffset * zOffsetMultiplier;

        transform.position = new Vector3(
            player.position.x,
            baseY + player.position.y + offsetYdeBase,
            proxyZ+baseZ
        );
    }
    public void ResetOffsets()
    {
        yOffset = 0f;
        zOffset = 0f;
        positiveZTravel = 0f;
        if (player != null)
            lastPlayerZ = player.position.z;
    }
}