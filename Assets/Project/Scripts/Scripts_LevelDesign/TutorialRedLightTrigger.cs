using UnityEngine;

public class TutorialRedLightTrigger : MonoBehaviour
{
    public static bool hasSeenRedLightTutorial = false;

    [SerializeField] private SentinelCycleManager sentinelCycleManager;
    private bool canTrigger = false;

    private void Start()
    {
        Invoke(nameof(EnableTrigger), 0.5f);
    }

    private void EnableTrigger()
    {
        canTrigger = true;
        Debug.LogError("[TriggerRedCycle] Trigger ENABLE apres delai");
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.LogError($"[TriggerRedCycle] OnTriggerEnter avec {other.name} - canTrigger: {canTrigger}");

        if (!canTrigger) return;

        if (other.CompareTag("Player"))
        {
            Debug.LogError("[TriggerRedCycle] DECLENCHEMENT du premier RedLight !");

            hasSeenRedLightTutorial = true;

            if (sentinelCycleManager != null)
            {
                sentinelCycleManager.TriggerFirstRedLight();
            }
        }
    }
}