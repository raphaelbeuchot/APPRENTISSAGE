using System.Collections.Generic;
using UnityEngine;

public class TileBridgeManager : MonoBehaviour
{
    [Header("Bridges")]
    public List<PlatformBridge> bridges = new List<PlatformBridge>();

    [Header("References")]
    public SentinelCycleManager sentinelCycleManager;

    private void Start()
    {
        if (sentinelCycleManager == null)
            sentinelCycleManager = FindObjectOfType<SentinelCycleManager>();

        if (sentinelCycleManager == null)
            Debug.LogWarning("[TileBridgeManager] SentinelCycleManager non trouve.");
    }

    public void OnRedLight()
    {
        foreach (PlatformBridge bridge in bridges)
        {
            if (bridge != null)
                bridge.TriggerFall();
        }
    }

    public void OnRelease()
    {
        foreach (PlatformBridge bridge in bridges)
        {
            if (bridge != null)
                bridge.TriggerRise();
        }
    }
}