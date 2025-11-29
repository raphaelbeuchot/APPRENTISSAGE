using UnityEngine;

[CreateAssetMenu(fileName = "New ClimbType", menuName = "1-2-3 Soleil/Climb Type")]
public class ClimbType : ScriptableObject
{
    [Header("Identification")]
    public string climbName;
    [Header("Detection")]
    public float minHeight = 0f;
    public float maxHeight = 1f;
    public float obstacleDepth = 0.3f;

    [Header("Movement")]
    public float moveDistanceForward = 1f;
    public float moveHeightUp = 1f; // Non utilise (hauteur calculee dynamiquement)

    [Header("Animation Phases")]
    public float phase1Duration = 0.8f; // Montee verticale
    public float phase2Duration = 0.4f; // Avancee horizontale

    [Header("Vulnerability")]
    public bool vulnerableToSentinel = true;
}