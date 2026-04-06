using UnityEngine;
using System.Collections;

public class EnemyCollectibleDropper : MonoBehaviour
{
    [Tooltip("Le collectible enfant de cet ennemi, assigne manuellement dans l'inspector")]
    public GameObject collectible;

    private EnemyHealth enemyHealth;

    void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();
    }

    void Start()
    {
        if (enemyHealth == null)
        {
            Debug.LogWarning("[EnemyCollectibleDropper] Pas de EnemyHealth sur " + gameObject.name);
            return;
        }

        if (collectible == null)
        {
            Debug.LogWarning("[EnemyCollectibleDropper] Pas de collectible assigne sur " + gameObject.name);
            return;
        }

        enemyHealth.OnDeath += HandleEnemyDeath;
    }

    void OnDestroy()
    {
        if (enemyHealth != null)
            enemyHealth.OnDeath -= HandleEnemyDeath;
    }

    private void HandleEnemyDeath()
    {
        if (collectible == null) return;

        collectible.transform.SetParent(null);
        StartCoroutine(DropToGround(collectible.transform));
    }

    private IEnumerator DropToGround(Transform target)
    {
        Vector3 startPos = target.position;
        Vector3 endPos = new Vector3(target.position.x, 0.25f, target.position.z);

        float elapsed = 0f;
        float duration = 0.3f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float tEased = 1f - Mathf.Pow(1f - t, 3f);
            target.position = Vector3.Lerp(startPos, endPos, tEased);
            yield return null;
        }

        target.position = endPos;
    }
}