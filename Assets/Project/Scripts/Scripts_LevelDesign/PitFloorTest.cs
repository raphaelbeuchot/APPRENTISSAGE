using UnityEngine;
using Pathfinding;

public class PitFloorTest : MonoBehaviour
{
    [Header("Configuration")]
    public float pitDepth = 1f;
    public float floorThickness = 0.2f;
    public int penaltyAmount = 10000;

    [Header("Test")]
    public bool applyOnStart = true;

    void Start()
    {
        if (applyOnStart)
        {
            ApplyFloorWalkability();
        }
    }

    [ContextMenu("Apply Floor Walkability")]
    public void ApplyFloorWalkability()
    {
        Bounds floorBounds = new Bounds(
            transform.position + Vector3.down * pitDepth,
            new Vector3(transform.localScale.x, floorThickness, transform.localScale.z)
        );

        GraphUpdateObject guo = new GraphUpdateObject(floorBounds);

        guo.modifyWalkability = true;
        guo.setWalkability = true;

        guo.addPenalty = penaltyAmount;

        AstarPath.active.UpdateGraphs(guo);

        Debug.Log("Pit floor made walkable with penalty " + penaltyAmount);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector3 floorCenter = transform.position + Vector3.down * pitDepth;
        Gizmos.DrawWireCube(floorCenter, new Vector3(transform.localScale.x, floorThickness, transform.localScale.z));
    }
}