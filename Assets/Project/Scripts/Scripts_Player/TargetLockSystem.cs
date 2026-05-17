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
    public LayerMask lockableTargetLayers;

    [SerializeField] private float switchCooldown = 0.3f;
    private float lastSwitchTime = 0f;
    private bool switchAxisWasNeutral = true;

    private Transform currentTarget;
    private EnemyHealthBarUI currentTargetHealthBar;
    private LockableTarget currentLockableTarget;

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

            if (IsLocked && Vector3.Distance(transform.position, currentTarget.position) > stats.lockOnRange)
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

    private List<Transform> GatherAllTargets()
    {
        List<Transform> results = new List<Transform>();

        Collider[] enemyHits = Physics.OverlapSphere(transform.position, stats.lockOnRange, enemyLayers);
        foreach (Collider hit in enemyHits)
        {
            if (IsTargetValid(hit.transform))
                results.Add(hit.transform);
        }

        Collider[] lockableHits = Physics.OverlapSphere(transform.position, stats.lockOnRange, lockableTargetLayers);
        foreach (Collider hit in lockableHits)
        {
            if (IsTargetValid(hit.transform) && !results.Contains(hit.transform))
                results.Add(hit.transform);
        }

        return results;
    }

    private void SwitchTarget(float stickX)
    {
        availableTargets = GatherAllTargets();

        if (availableTargets.Count <= 1) return;

        // Trier par position X ecran (gauche vers droite)
        availableTargets.Sort((a, b) =>
        {
            float ax = mainCamera.WorldToScreenPoint(a.position).x;
            float bx = mainCamera.WorldToScreenPoint(b.position).x;
            return ax.CompareTo(bx);
        });

        int idx = availableTargets.IndexOf(currentTarget);
        if (idx < 0) idx = 0;

        if (stickX > 0)
            idx = (idx + 1) % availableTargets.Count;
        else
            idx = (idx - 1 + availableTargets.Count) % availableTargets.Count;

        if (availableTargets[idx] == currentTarget) return;

        ClearCurrentTargetVisual();
        currentTarget = availableTargets[idx];
        currentTargetIndex = idx;
        ApplyCurrentTargetVisual();

        Debug.Log("Switched to: " + currentTarget.name);
    }

    private void LockOntoTarget()
    {
        ClearCurrentTargetVisual();

        availableTargets = GatherAllTargets();

        if (availableTargets.Count == 0)
            return;

        // Trouver la cible la plus alignee avec le forward du player
        Transform bestTarget = null;
        float bestAlignment = -1f;

        for (int i = 0; i < availableTargets.Count; i++)
        {
            Transform target = availableTargets[i];
            Vector3 dirToTarget = (target.position - transform.position).normalized;
            dirToTarget.y = 0f;
            dirToTarget.Normalize();

            Vector3 playerFwd = transform.forward;
            playerFwd.y = 0f;
            playerFwd.Normalize();

            float alignment = Vector3.Dot(playerFwd, dirToTarget);

            if (alignment > bestAlignment)
            {
                bestAlignment = alignment;
                bestTarget = target;
                currentTargetIndex = i;
            }
        }

        if (bestTarget != null)
        {
            currentTarget = bestTarget;
            ApplyCurrentTargetVisual();
            Debug.Log("Locked onto: " + currentTarget.name);
        }
    }

    public void UnlockTarget()
    {
        ClearCurrentTargetVisual();
        ClearAllEnemyOutlines();

        currentTarget = null;
        availableTargets.Clear();
        currentTargetIndex = 0;

        Debug.Log("Target unlocked");
    }

    // Applique le feedback visuel selon le type de cible lockee
    private void ApplyCurrentTargetVisual()
    {
        if (currentTarget == null) return;

        LockableTarget lockable = currentTarget.GetComponent<LockableTarget>();
        if (lockable != null)
        {
            currentLockableTarget = lockable;
            currentLockableTarget.SetLockedVisual(true);
            return;
        }

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
    }

    // Efface uniquement le feedback de la cible courante
    private void ClearCurrentTargetVisual()
    {
        if (currentTargetHealthBar != null)
        {
            currentTargetHealthBar.SetLockedOutline(false);
            currentTargetHealthBar = null;
        }

        if (currentLockableTarget != null)
        {
            currentLockableTarget.SetLockedVisual(false);
            currentLockableTarget = null;
        }
    }

    // Garde le clear force sur toutes les barres ennemis (securite)
    private void ClearAllEnemyOutlines()
    {
        EnemyHealthBarUI[] allBars = FindObjectsByType<EnemyHealthBarUI>(FindObjectsSortMode.None);
        foreach (EnemyHealthBarUI bar in allBars)
        {
            if (bar != null)
                bar.SetLockedOutline(false);
        }
    }

    private bool IsTargetValid(Transform target)
    {
        LockableTarget lockable = target.GetComponent<LockableTarget>();
        if (lockable != null)
            return lockable.IsValidTarget();

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