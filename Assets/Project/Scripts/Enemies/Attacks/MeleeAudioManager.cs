using UnityEngine;

public static class MeleeAudioManager
{
    public delegate void MeleeHitEvent(Vector3 hitPosition);
    public static event MeleeHitEvent OnMeleeHit;

    public static void TriggerMeleeHit(Vector3 position)
    {
        OnMeleeHit?.Invoke(position);
    }
}