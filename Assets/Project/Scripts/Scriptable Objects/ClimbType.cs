using UnityEngine;

[CreateAssetMenu(fileName = "New ClimbType", menuName = "1-2-3 Soleil/Climb Type")]
public class ClimbType : ScriptableObject
{
    [Header("Identification")]
    public string climbName;
    public bool isVault = true;

    [Header("Detection")]
    public float minHeight = 0f;
    public float maxHeight = 1f;
    public float obstacleDepth = 0.3f;

    [Header("Movement")]
    public float moveDistanceForward = 1f;

    [Header("Animation Phases")]
    public float phase1Duration = 0.8f;
    public float phase2Duration = 0.4f;

    [Header("Vulnerability")]
    public bool vulnerableToSentinel = true;
}