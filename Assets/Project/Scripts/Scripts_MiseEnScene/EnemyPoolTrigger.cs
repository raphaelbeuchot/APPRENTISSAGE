using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

// Declenche des evenements quand tous les ennemis d'un pool (liste dans l'Inspector) sont morts.
// Ex : ouvrir une porte, jouer une Timeline, faire tourner une salle.
// Chaque evenement a son propre delai et peut attendre la fin de l'alerte.
public class EnemyPoolTrigger : MonoBehaviour
{
    [System.Serializable]
    public class PoolEvent
    {
        [Tooltip("Delai entre la mort du dernier ennemi et cet evenement")]
        public float delay = 0f;
        [Tooltip("Si l'evenement tombe en Alert ou Red Light, il attend le retour en Green Light / Release")]
        public bool waitEndOfAlert = false;
        public UnityEvent onFire;
    }

    [Header("Pool")]
    [Tooltip("Les ennemis a tuer (glisser les GO qui portent EnemyHealth)")]
    [SerializeField] private List<EnemyHealth> enemies = new List<EnemyHealth>();

    [Header("Evenements")]
    [SerializeField] private List<PoolEvent> events = new List<PoolEvent>();

    private readonly HashSet<EnemyHealth> deadEnemies = new HashSet<EnemyHealth>();
    private bool hasFired = false;
    private SentinelCycleManager cycleManager;

    private void Start()
    {
        cycleManager = FindAnyObjectByType<SentinelCycleManager>();

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
        foreach (PoolEvent poolEvent in events)
            StartCoroutine(FireEvent(poolEvent));
    }

    private IEnumerator FireEvent(PoolEvent poolEvent)
    {
        if (poolEvent.delay > 0f)
            yield return new WaitForSeconds(poolEvent.delay);

        if (poolEvent.waitEndOfAlert)
        {
            while (IsAlertOrRedLight())
                yield return null;
        }

        poolEvent.onFire?.Invoke();
    }

    private bool IsAlertOrRedLight()
    {
        if (cycleManager == null) return false;
        SentinelCycleManager.GameState state = cycleManager.currentState;
        return state == SentinelCycleManager.GameState.Alert || state == SentinelCycleManager.GameState.RedLight;
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
