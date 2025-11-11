using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class TargetLockSystem : MonoBehaviour
{
    [Header("References")]
    public PlayerStats stats;
    public Camera mainCamera;

    [Header("Detection Layers")]
    public LayerMask enemyLayers;

    private Transform currentTarget;
    private EnemyHealthBarUI currentTargetHealthBar;

    private List<Transform> availableTargets = new List<Transform>();
    private int currentTargetIndex = 0;

    public bool IsLocked => currentTarget != null;
    public Transform CurrentTarget => currentTarget;

    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    private void Update()
    {
        if (Input.GetKey(KeyCode.Tab))
        {
            if (!IsLocked)
            {
                LockOntoTarget();
            }
            else
            {
                // Switch target avec molette
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (scroll > 0f)
                    SwitchTarget(1);
                else if (scroll < 0f)
                    SwitchTarget(-1);
            }
        }
        else
        {
            if (IsLocked)
                UnlockTarget();
        }

        if (IsLocked)
        {
            if (currentTarget == null || !IsTargetValid(currentTarget))
            {
                UnlockTarget();
            }
        }
    }

    private void RefreshAvailableTargets()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, stats.lockOnRange, enemyLayers);

        availableTargets.Clear();

        foreach (Collider hit in hits)
        {
            Transform target = hit.transform;

            if (IsTargetValid(target) && IsInCameraAngle(target))
            {
                availableTargets.Add(target);
            }
        }

        // Verifier que la cible actuelle est toujours dans la liste
        if (!availableTargets.Contains(currentTarget))
        {
            UnlockTarget();
        }
        else
        {
            currentTargetIndex = availableTargets.IndexOf(currentTarget);
        }
    }

    private void SwitchTarget(int direction)
    {
        if (availableTargets.Count <= 1)
            return;

        // Force clear de toutes les outlines
        ClearAllOutlines();

        // Changer l'index
        currentTargetIndex += direction;

        if (currentTargetIndex >= availableTargets.Count)
            currentTargetIndex = 0;
        else if (currentTargetIndex < 0)
            currentTargetIndex = availableTargets.Count - 1;

        // Lock la nouvelle cible
        currentTarget = availableTargets[currentTargetIndex];

        EnemyHealthBarUI healthBar = currentTarget.GetComponent<EnemyHealth>()?.healthBarUI;
        if (healthBar == null)
        {
            SwarmController swarm = currentTarget.GetComponent<SwarmController>();
            if (swarm != null)
                healthBar = swarm.GetHealthBarUI();
        }

        if (healthBar != null)
        {
            currentTargetHealthBar = healthBar;
            currentTargetHealthBar.SetLockedOutline(true);
        }

        Debug.Log($"Switched to: {currentTarget.name}");
    }

    private void LockOntoTarget()
    {
        // Force clear de toutes les outlines
        ClearAllOutlines();

        Transform bestTarget = FindBestTarget();

        if (bestTarget != null)
        {
            currentTarget = bestTarget;

            EnemyHealthBarUI healthBar = currentTarget.GetComponent<EnemyHealth>()?.healthBarUI;
            if (healthBar == null)
            {
                SwarmController swarm = currentTarget.GetComponent<SwarmController>();
                if (swarm != null)
                    healthBar = swarm.GetHealthBarUI();
            }

            if (healthBar != null)
            {
                currentTargetHealthBar = healthBar;
                currentTargetHealthBar.SetLockedOutline(true);
            }

            Debug.Log($"Locked onto: {currentTarget.name}");
        }
    }

    private void UnlockTarget()
    {
        // Force clear de toutes les outlines
        ClearAllOutlines();

        if (currentTargetHealthBar != null)
        {
            currentTargetHealthBar.SetLockedOutline(false);
            currentTargetHealthBar = null;
        }

        currentTarget = null;
        availableTargets.Clear();
        currentTargetIndex = 0;

        Debug.Log("Target unlocked");
    }

    private Transform FindBestTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, stats.lockOnRange, enemyLayers);

        if (hits.Length == 0)
            return null;

        availableTargets.Clear();

        foreach (Collider hit in hits)
        {
            Transform target = hit.transform;

            if (IsTargetValid(target) && IsInCameraAngle(target))
            {
                availableTargets.Add(target);
            }
        }

        if (availableTargets.Count == 0)
            return null;

        // Trouver la cible la plus alignee avec le forward du player
        Transform bestTarget = null;
        float bestAlignment = -1f;

        for (int i = 0; i < availableTargets.Count; i++)
        {
            Transform target = availableTargets[i];
            Vector3 directionToTarget = (target.position - transform.position).normalized;
            directionToTarget.y = 0f;
            directionToTarget.Normalize();

            Vector3 playerForward = transform.forward;
            playerForward.y = 0f;
            playerForward.Normalize();

            float alignment = Vector3.Dot(playerForward, directionToTarget);

            if (alignment > bestAlignment)
            {
                bestAlignment = alignment;
                bestTarget = target;
                currentTargetIndex = i;
            }
        }

        return bestTarget;
    }

    private void ClearAllOutlines()
    {
        // Trouver toutes les barres de vie et forcer leur outline a false
        EnemyHealthBarUI[] allBars = FindObjectsByType<EnemyHealthBarUI>(FindObjectsSortMode.None);
        foreach (EnemyHealthBarUI bar in allBars)
        {
            bar.SetLockedOutline(false);
        }
    }

    private bool IsTargetValid(Transform target)
    {
        EnemyHealth enemyHealth = target.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
            return enemyHealth.IsAlive();

        SwarmController swarm = target.GetComponent<SwarmController>();
        if (swarm != null)
            return swarm.IsAlive();

        return false;
    }

    private bool IsInCameraAngle(Transform target)
    {
        Vector3 directionToTarget = (target.position - transform.position).normalized;
        Vector3 playerForward = transform.forward;

        directionToTarget.y = 0f;
        playerForward.y = 0f;
        playerForward.Normalize();

        float angle = Vector3.Angle(playerForward, directionToTarget);

        return angle <= stats.lockOnAngle / 2f;
    }

    public Vector3 GetTargetDirection()
    {
        if (!IsLocked || currentTarget == null)
            return transform.forward;

        Vector3 direction = (currentTarget.position - transform.position);
        direction.y = 0f;
        return direction.normalized;
    }
}