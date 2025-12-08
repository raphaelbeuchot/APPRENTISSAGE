using UnityEngine;

public class ClimbableObject : MonoBehaviour
{
    public ClimbType climbType;

    [Header("Face Restrictions")]
    [Tooltip("Si true, interdit le climb depuis la face courte (moins de 0.4m)")]
    public bool onlyClimbFromLongSide = false;

    public string climbTypeName
    {
        get { return climbType != null ? climbType.climbName : "None"; }
    }
}