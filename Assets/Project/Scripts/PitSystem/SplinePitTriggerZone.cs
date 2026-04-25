using UnityEngine;

public class SplinePitTriggerZone : MonoBehaviour
{
    private SplinePitZone pitZone;

    private void Start()
    {
        pitZone = GetComponentInParent<SplinePitZone>();
    }

    private void OnTriggerEnter(Collider other)
    {
        EnemyAI_AStar enemy = other.GetComponent<EnemyAI_AStar>();
        if (enemy != null)
        {
            enemy.EnablePitMode();
            return;
        }

        PlayerPitInteractable player = other.GetComponent<PlayerPitInteractable>();
        if (player != null && pitZone != null)
            player.OnEnterSplinePit(pitZone);
    }

    private void OnTriggerExit(Collider other)
    {
        EnemyAI_AStar enemy = other.GetComponent<EnemyAI_AStar>();
        if (enemy != null)
        {
            enemy.DisablePitMode();
            return;
        }

        PlayerPitInteractable player = other.GetComponent<PlayerPitInteractable>();
        if (player != null && !player.IsClimbingOut())
            player.OnExitSplinePit();
    }
}