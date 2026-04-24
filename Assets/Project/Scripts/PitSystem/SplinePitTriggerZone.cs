using UnityEngine;

public class SplinePitTriggerZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        EnemyAI_AStar enemy = other.GetComponent<EnemyAI_AStar>();
        if (enemy != null)
        {
            enemy.EnablePitMode();
            Debug.Log("[SplinePit] " + other.name + " entered pit - PitMode ON");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        EnemyAI_AStar enemy = other.GetComponent<EnemyAI_AStar>();
        if (enemy != null)
        {
            enemy.DisablePitMode();
            Debug.Log("[SplinePit] " + other.name + " exited pit - PitMode OFF");
        }
    }
}