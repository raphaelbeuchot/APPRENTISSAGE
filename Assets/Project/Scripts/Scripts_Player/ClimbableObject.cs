using UnityEngine;

public class ClimbableObject : MonoBehaviour
{
    public ClimbType climbType;

    // Pour debug (optionnel)
    public string climbTypeName
    {
        get { return climbType != null ? climbType.climbName : "None"; }
    }
}