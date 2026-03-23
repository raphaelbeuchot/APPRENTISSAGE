using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SentinelLaserManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private Transform sentinelEye;
    [SerializeField] private SentinelSettings sentinelSettings;
    [SerializeField] private PlayerPhysicsMovement player;

    [Header("Laser Permanent - Player")]
    [SerializeField] private Material laserPlayerMaterialNormal;
    [SerializeField] private Material laserPlayerMaterialDetected;
    [SerializeField] private float laserPlayerWidthNormal = 0.02f;
    [SerializeField] private float laserPlayerWidthDetected = 0.05f;

    [Header("Laser Permanent - Ennemis")]
    [SerializeField] private Material laserEnemyMaterialNormal;
    [SerializeField] private Material laserEnemyMaterialDetected;
    [SerializeField] private float laserEnemyWidthNormal = 0.02f;
    [SerializeField] private float laserEnemyWidthDetected = 0.04f;

    [Header("Animation Tir")]
    [SerializeField] private GameObject shotProjectilePrefab;
    [SerializeField] private float shotProjectileSpeed = 120f;

    private bool isRedLight = false;

    private Dictionary<GameObject, LineRenderer> laserLines = new Dictionary<GameObject, LineRenderer>();

    // ============================================
    // LIFECYCLE
    // ============================================

    void OnEnable()
    {
        SentinelCycleManager.OnCycleChanged += OnCycleChanged;
    }

    void OnDisable()
    {
        SentinelCycleManager.OnCycleChanged -= OnCycleChanged;
    }

    void OnCycleChanged(SentinelCycleManager.GameState newState)
    {
        isRedLight = (newState == SentinelCycleManager.GameState.RedLight);

        if (!isRedLight)
        {
            HideAllLasers();
        }
    }

    void Update()
    {
        if (!isRedLight) return;

        Vector3 eyePosition = (sentinelEye != null) ? sentinelEye.position : transform.position;
        Vector3 sentinelPos = eyePosition + sentinelSettings.raycastOffset;

        Collider[] targets = Physics.OverlapSphere(sentinelPos, sentinelSettings.detectionRadius, sentinelSettings.targetLayers);

        HashSet<GameObject> seenThisFrame = new HashSet<GameObject>();

        foreach (Collider col in targets)
        {
            GameObject target = col.gameObject;

            EnemyHealth enemyHealth = target.GetComponent<EnemyHealth>();
            EnemyAI_AStar ai = target.GetComponent<EnemyAI_AStar>();

            if (enemyHealth != null && (enemyHealth.IsDead() || enemyHealth.IsRecovering() || (ai != null && ai.isStunnedBySentinel)))
            {
                HideLaser(target);
                continue;
            }

            seenThisFrame.Add(target);

            Vector3 targetCenter = GetTargetCenter(col);
            Vector3 direction = (targetCenter - sentinelPos).normalized;
            float distance = Vector3.Distance(sentinelPos, targetCenter);

            bool hasLOS = true;
            Vector3 finalTargetPos = targetCenter;

            RaycastHit hit;
            if (Physics.Raycast(sentinelPos, direction, out hit, distance, sentinelSettings.obstacleLayers)
                && hit.collider.gameObject != target)
            {
                Vector3 headPos = GetHeadPosition(col);
                Vector3 dirToHead = (headPos - sentinelPos).normalized;
                float distToHead = Vector3.Distance(sentinelPos, headPos);

                RaycastHit headHit;
                if (Physics.Raycast(sentinelPos, dirToHead, out headHit, distToHead, sentinelSettings.obstacleLayers)
                    && headHit.collider.gameObject != target)
                {
                    hasLOS = false;
                }
                else
                {
                    hasLOS = true;
                    finalTargetPos = headPos;
                }
            }

            if (!hasLOS)
            {
                HideLaser(target);
                continue;
            }

            bool isPlayer = (target == player.gameObject);
            bool isDetected = false;

            if (isPlayer)
            {
                isDetected = gameManager.playerDetectionFeedback != null
                          && gameManager.playerDetectionFeedback.isCurrentlyDetected;
            }
            else
            {
                EnemyDetectionFeedback enemyFeedback = target.GetComponent<EnemyDetectionFeedback>();
                isDetected = enemyFeedback != null && enemyFeedback.isCurrentlyDetected;
            }

            LineRenderer lr = GetOrCreateLaser(target, isPlayer);

            lr.SetPosition(0, sentinelPos);
            lr.SetPosition(1, finalTargetPos);

            if (isPlayer)
            {
                lr.material = isDetected ? laserPlayerMaterialDetected : laserPlayerMaterialNormal;
                float w = isDetected ? laserPlayerWidthDetected : laserPlayerWidthNormal;
                lr.startWidth = w;
                lr.endWidth = w;
            }
            else
            {
                lr.material = isDetected ? laserEnemyMaterialDetected : laserEnemyMaterialNormal;
                float w = isDetected ? laserEnemyWidthDetected : laserEnemyWidthNormal;
                lr.startWidth = w;
                lr.endWidth = w;
            }

            lr.enabled = true;
        }

        List<GameObject> toHide = new List<GameObject>();
        foreach (var kvp in laserLines)
        {
            if (!seenThisFrame.Contains(kvp.Key))
                toHide.Add(kvp.Key);
        }
        foreach (var t in toHide)
        {
            HideLaser(t);
        }
    }

    // ============================================
    // ANIMATION TIR - OPTION B
    // ============================================

    public void TriggerShotAnimation(Vector3 from, Vector3 to)
    {
        StartCoroutine(ShotProjectileCoroutine(from, to));
    }

    private IEnumerator ShotProjectileCoroutine(Vector3 from, Vector3 to)
    {
        if (shotProjectilePrefab == null)
        {
            yield break;
        }

        Quaternion rotation = Quaternion.LookRotation((to - from).normalized);
        GameObject proj = Instantiate(shotProjectilePrefab, from, rotation);

        float totalDistance = Vector3.Distance(from, to);
        float elapsed = 0f;
        float duration = totalDistance / shotProjectileSpeed;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            proj.transform.position = Vector3.Lerp(from, to, t);
            yield return null;
        }

        proj.transform.position = to;
        Destroy(proj);
    }

    // ============================================
    // HELPERS POOL
    // ============================================

    private LineRenderer GetOrCreateLaser(GameObject target, bool isPlayer)
    {
        if (laserLines.ContainsKey(target))
            return laserLines[target];

        GameObject laserGO = new GameObject("SentinelLaser_" + target.name);
        laserGO.transform.SetParent(transform);

        LineRenderer lr = laserGO.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        Material mat = isPlayer ? laserPlayerMaterialNormal : laserEnemyMaterialNormal;
        if (mat != null)
            lr.material = mat;
        else
            lr.material = new Material(Shader.Find("Sprites/Default"));

        laserLines[target] = lr;
        return lr;
    }

    private void HideLaser(GameObject target)
    {
        if (laserLines.ContainsKey(target))
            laserLines[target].enabled = false;
    }

    private void HideAllLasers()
    {
        foreach (var kvp in laserLines)
        {
            if (kvp.Value != null)
                kvp.Value.enabled = false;
        }
    }

    // ============================================
    // HELPERS POSITION
    // ============================================

    private Vector3 GetTargetCenter(Collider col)
    {
        return col.bounds.center;
    }

    private Vector3 GetHeadPosition(Collider col)
    {
        float headHeight = col.bounds.min.y + (col.bounds.size.y * 0.9f);
        return new Vector3(col.bounds.center.x, headHeight, col.bounds.center.z);
    }
}