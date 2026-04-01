using UnityEngine;
using System.Collections;

public class BombSpawner : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float detectionRadius = 5f;
    [SerializeField] private LayerMask detectionLayer;

    [Header("Charge")]
    [SerializeField] private float chargeDuration = 1.5f;

    [Header("Bomb")]
    [SerializeField] private GameObject bombPrefab;
    [SerializeField] private float spawnHeightOffset = 1.5f;

    private GameObject currentBomb;
    private BombProjectile currentBombProjectile;
    private bool isCharging = false;

    void Start()
    {
        SpawnBomb();
        StartCoroutine(DetectionLoop());
    }

    IEnumerator DetectionLoop()
    {
        while (true)
        {
            if (currentBomb == null || isCharging)
            {
                yield return null;
                continue;
            }

            Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, detectionLayer);
            if (hits.Length > 0)
            {
                StartCoroutine(ChargeAndLaunch(hits[0].transform));
            }

            yield return null;
        }
    }

    IEnumerator ChargeAndLaunch(Transform player)
    {
        isCharging = true;
        float elapsed = 0f;

        while (elapsed < chargeDuration)
        {
            if (currentBomb == null)
            {
                isCharging = false;
                yield break;
            }

            bool playerStillInRange = false;
            Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, detectionLayer);
            foreach (var hit in hits)
            {
                if (hit.transform == player)
                {
                    playerStillInRange = true;
                    break;
                }
            }

            if (!playerStillInRange)
            {
                float currentT = elapsed / chargeDuration;
                Vector3 currentEndPos = Vector3.Lerp(currentBombProjectile.transform.position, player.position + Vector3.up, currentT);
                currentBombProjectile.StartRetractLine(currentEndPos, 0.75f);
                isCharging = false;
                yield break;
            }

            float t = elapsed / chargeDuration;
            currentBombProjectile.UpdateChargeLine(player.position + Vector3.up, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (currentBomb != null)
        {
            currentBombProjectile.HideChargeLine();
            currentBombProjectile.Launch(player, OnBombExploded);
            currentBomb = null;
        }

        isCharging = false;
    }

    void OnBombExploded()
    {
        SpawnBomb();
    }

    void SpawnBomb()
    {
        if (bombPrefab == null) return;
        Vector3 spawnPos = transform.position + Vector3.up * spawnHeightOffset;
        currentBomb = Instantiate(bombPrefab, spawnPos, Quaternion.identity);
        currentBombProjectile = currentBomb.GetComponent<BombProjectile>();
        if (currentBombProjectile != null)
            currentBombProjectile.SetSpawner(transform, detectionRadius);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}