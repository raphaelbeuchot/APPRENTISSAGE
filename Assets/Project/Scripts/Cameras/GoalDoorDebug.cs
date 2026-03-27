using UnityEngine;

public class GoalDoorDebug : MonoBehaviour
{
    public Renderer goalDoorRenderer;
    public Material debugMaterial;

    public void TriggerDebug()
    {
        if (goalDoorRenderer != null && debugMaterial != null)
            goalDoorRenderer.material = debugMaterial;
        Debug.Log("[DEBUG] TriggerDebug appele a " + Time.realtimeSinceStartup);
    }
}