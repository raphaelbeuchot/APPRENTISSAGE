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

    [SerializeField] private float switchCooldown = 0.3f;
    private float lastSwitchTime = 0f;
    private bool switchAxisWasNeutral = true;

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
        if (PlayerInputManager.Instance.LockOnHeld)
        {
            if (!IsLocked)
            {
                LockOntoTarget();
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

            float switchInput = PlayerInputManager.Instance.SwitchTargetInput;
            if (Mathf.Abs(switchInput) < 0.3f)
            {
                switchAxisWasNeutral = true;
            }
            else if (switchAxisWasNeutral && Time.time >= lastSwitchTime + switchCooldown)
            {
                switchAxisWasNeutral = false;
                lastSwitchTime = Time.time;
                SwitchTarget(switchInput);
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

    private void SwitchTarget(float stickX)
    {
        // Rafraichir la liste sans appeler UnlockTarget
        Collider[] hits = Physics.OverlapSphere(transform.position, stats.lockOnRange, enemyLayers);
        availableTargets.Clear();
        foreach (Collider hit in hits)
        {
            if (IsTargetValid(hit.transform))
                availableTargets.Add(hit.transform);
        }

        if (availableTargets.Count <= 1) return;

        // Trier par position X ecran (gauche vers droite)
        availableTargets.Sort((a, b) =>
        {
            float ax = mainCamera.WorldToScreenPoint(a.position).x;
            float bx = mainCamera.WorldToScreenPoint(b.position).x;
            return ax.CompareTo(bx);
        });

        // Trouver l'index de la cible actuelle dans la liste triee
        int idx = availableTargets.IndexOf(currentTarget);
        if (idx < 0) idx = 0;

        // Avancer dans le bon sens
        if (stickX > 0)
            idx = (idx + 1) % availableTargets.Count;
        else
            idx = (idx - 1 + availableTargets.Count) % availableTargets.Count;

        if (availableTargets[idx] == currentTarget) return;

        ClearAllOutlines();
        currentTarget = availableTargets[idx];
        currentTargetIndex = idx;

        EnemyHealthBarUI healthBar = currentTarget.GetComponent<EnemyHealth>()?.healthBarUI;
        if (healthBar == null)
        {
            SwarmController_AStar swarm = currentTarget.GetComponent<SwarmController_AStar>();
            if (swarm != null)
                healthBar = swarm.GetHealthBarUI();
        }

        if (healthBar != null)
        {
            currentTargetHealthBar = healthBar;
            currentTargetHealthBar.SetLockedOutline(true);
        }

        Debug.Log("Switched to: " + currentTarget.name);
    }

    private void LockOntoTarget()
    {
        ClearAllOutlines();

        Transform bestTarget = FindBestTarget();

        if (bestTarget != null)
        {
            currentTarget = bestTarget;

            EnemyHealthBarUI healthBar = currentTarget.GetComponent<EnemyHealth>()?.healthBarUI;
            if (healthBar == null)
            {
                SwarmController_AStar swarm = currentTarget.GetComponent<SwarmController_AStar>();
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

    public void UnlockTarget()
    {
        // 1. Désactiver l'outline de la cible actuelle
        if (currentTargetHealthBar != null)
        {
            currentTargetHealthBar.SetLockedOutline(false);
            currentTargetHealthBar = null;
        }

        // 2. Force clear TOUTES les outlines
        ClearAllOutlines();

        // 3. Clear les références
        currentTarget = null;
        availableTargets.Clear();
        currentTargetIndex = 0;

        Debug.Log("Target unlocked - all outlines cleared");
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

            if (IsTargetValid(target))
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
        EnemyHealthBarUI[] allBars = FindObjectsByType<EnemyHealthBarUI>(FindObjectsSortMode.None);
        Debug.Log("Clearing " + allBars.Length + " health bars outlines");
        foreach (EnemyHealthBarUI bar in allBars)
        {
            if (bar != null)
            {
                bar.SetLockedOutline(false);
            }
        }
    }

    private bool IsTargetValid(Transform target)
    {
        EnemyHealth enemyHealth = target.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
            return enemyHealth.IsAlive();

        SwarmController_AStar swarm = target.GetComponent<SwarmController_AStar>();
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