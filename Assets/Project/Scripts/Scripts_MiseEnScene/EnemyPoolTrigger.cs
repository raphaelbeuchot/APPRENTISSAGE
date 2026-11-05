using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

// Declenche des evenements quand tous les ennemis d'un pool (liste dans l'Inspector) sont morts.
// Ex : ouvrir une porte, jouer une Timeline, activer une zone.
public class EnemyPoolTrigger : MonoBehaviour
{
    [Header("Pool")]
    [Tooltip("Les ennemis a tuer (glisser les GO qui portent EnemyHealth)")]
    [SerializeField] private List<EnemyHealth> enemies = new List<EnemyHealth>();

    [Header("Evenements")]
    [Tooltip("Delai entre la mort du dernier ennemi et les evenements")]
    [SerializeField] private float delay = 0f;
    [SerializeField] private UnityEvent onAllDead;

    private readonly HashSet<EnemyHealth> deadEnemies = new HashSet<EnemyHealth>();
    private bool hasFired = false;

    private void Start()
    {
        if (enemies.Count == 0)
        {
            Debug.LogWarning("[EnemyPoolTrigger] Pool vide sur " + gameObject.name);
            return;
        }

        foreach (EnemyHealth enemy in enemies)
        {
            if (enemy == null) continue;

            if (enemy.IsDead())
                deadEnemies.Add(enemy);
            else
                enemy.OnDeath += () => HandleEnemyDeath(enemy);
        }

        CheckAllDead();
    }

    private void HandleEnemyDeath(EnemyHealth enemy)
    {
        deadEnemies.Add(enemy);
        CheckAllDead();
    }

    private void CheckAllDead()
    {
        if (hasFired) return;

        foreach (EnemyHealth enemy in enemies)
        {
            if (enemy != null && !deadEnemies.Contains(enemy))
                return;
        }

        hasFired = true;
        if (delay > 0f)
            Invoke(nameof(Fire), delay);
        else
            Fire();
    }

    private void Fire()
    {
        onAllDead?.Invoke();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        foreach (EnemyHealth enemy in enemies)
        {
            if (enemy != null)
                Gizmos.DrawLine(transform.position, enemy.transform.position);
        }
    }
}
