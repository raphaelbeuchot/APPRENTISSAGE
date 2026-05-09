using UnityEngine;
using System.Collections.Generic;

public class SlipperyZone : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private float slipperyAccel = 4f;
    [SerializeField] private float slipperyBrake = 1.5f;
    [SerializeField] private float slipperyRotation = 10f;

    [Header("Enemies")]
    [Range(0f, 1f)]
    [SerializeField] private float enemyDragMultiplier = 0.5f;

    private Dictionary<EnemyAI_AStar, Rigidbody> enemiesOnZone = new Dictionary<EnemyAI_AStar, Rigidbody>();
    private Dictionary<EnemyAI_AStar, float> originalDrags = new Dictionary<EnemyAI_AStar, float>();

    void OnCollisionEnter(Collision collision)
    {
        PlayerPhysicsMovement player = collision.gameObject.GetComponent<PlayerPhysicsMovement>();
        if (player != null)
        {
            player.SetSlippery(true, slipperyAccel, slipperyBrake, slipperyRotation);
            return;
        }

        EnemyAI_AStar enemy = collision.gameObject.GetComponent<EnemyAI_AStar>();
        if (enemy != null && !enemiesOnZone.ContainsKey(enemy))
        {
            Rigidbody rb = enemy.GetComponent<Rigidbody>();
            if (rb != null)
            {
                enemiesOnZone.Add(enemy, rb);
                originalDrags[enemy] = rb.linearDamping;
                rb.linearDamping = rb.linearDamping * enemyDragMultiplier;
            }
        }
    }

    void OnCollisionExit(Collision collision)
    {
        PlayerPhysicsMovement player = collision.gameObject.GetComponent<PlayerPhysicsMovement>();
        if (player != null)
        {
            player.SetSlippery(false, 0f, 0f, 0f);
            return;
        }

        EnemyAI_AStar enemy = collision.gameObject.GetComponent<EnemyAI_AStar>();
        if (enemy != null)
        {
            if (enemiesOnZone.TryGetValue(enemy, out Rigidbody rb) && rb != null)
                rb.linearDamping = originalDrags.ContainsKey(enemy) ? originalDrags[enemy] : 5f;

            enemiesOnZone.Remove(enemy);
            originalDrags.Remove(enemy);
        }
    }
}