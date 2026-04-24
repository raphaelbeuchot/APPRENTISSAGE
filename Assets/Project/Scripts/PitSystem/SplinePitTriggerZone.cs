using UnityEngine;

public class SplinePitTriggerZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        EnemyAI_AStar enemy = other.GetComponent<EnemyAI_AStar>();
        if (enemy != null)
            enemy.EnablePitMode();
    }

    private void OnTriggerExit(Collider other)
    {
        EnemyAI_AStar enemy = other.GetComponent<EnemyAI_AStar>();
        if (enemy != null)
            enemy.DisablePitMode();
    }
}