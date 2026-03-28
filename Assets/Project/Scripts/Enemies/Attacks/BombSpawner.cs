using UnityEngine;
using System.Collections;

public class BombSpawner : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float detectionRadius = 5f;
    [SerializeField] private LayerMask detectionLayer;

    [Header("Bomb")]
    [SerializeField] private GameObject bombPrefab;
    [SerializeField] private float spawnCooldown = 3f;

    private GameObject currentBomb;
    private bool isWaitingForRespawn = false;

    void Start()
    {
        SpawnBomb();
    }

    void Update()
    {
        if (currentBomb == null || isWaitingForRespawn) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, detectionLayer);

        if (hits.Length == 0) return;

        Transform targetTransform = hits[0].transform;

        BombProjectile bomb = currentBomb.GetComponent<BombProjectile>();
        if (bomb != null)
        {
            bomb.Launch(targetTransform, OnBombExploded);
            currentBomb = null;
        }
    }

    void OnBombExploded()
    {
        StartCoroutine(RespawnAfterCooldown());
    }

    IEnumerator RespawnAfterCooldown()
    {
        isWaitingForRespawn = true;
        yield return new WaitForSeconds(spawnCooldown);
        SpawnBomb();
        isWaitingForRespawn = false;
    }

    void SpawnBomb()
    {
        if (bombPrefab == null) return;
        Vector3 spawnPos = transform.position + Vector3.up * 1.5f;
        currentBomb = Instantiate(bombPrefab, spawnPos, Quaternion.identity);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}