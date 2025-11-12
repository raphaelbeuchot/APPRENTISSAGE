using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AI;

[RequireComponent(typeof(BoxCollider))]
public class PitDamageController : MonoBehaviour
{
    [Header("References")]
    public PitZone pitZone;

    [Header("Detection Settings")]
    [Tooltip("Layers qui peuvent tomber dans la fosse (Player, Enemies, etc.)")]
    public LayerMask fallableLayerMask = -1;

    [Header("Zone Heights")]
    [Tooltip("Distance du fond pour declencher le fall damage (en metres)")]
    public float bottomZoneHeight = 0.5f;

    [Header("Debug")]
    public bool showDebugGizmos = true;

    // Components
    private BoxCollider triggerCollider;

    // Tracking des entites
    private Dictionary<GameObject, PitEntityData> entitiesInPit = new Dictionary<GameObject, PitEntityData>();

    // Cache des hauteurs
    private float fillSurfaceHeight;
    private float bottomHeight;

    void Awake()
    {
        triggerCollider = GetComponent<BoxCollider>();
        triggerCollider.isTrigger = true;

        if (pitZone == null)
        {
            pitZone = GetComponentInParent<PitZone>();
        }

        if (pitZone == null)
        {
            Debug.LogError("PitDamageController: No PitZone found in parent!");
        }
    }

    void Start()
    {
        UpdateTriggerBounds();
    }

    public void UpdateTriggerBounds()
    {
        Debug.Log("=== UPDATE TRIGGER BOUNDS START ===");

        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<BoxCollider>();
            if (triggerCollider == null)
            {
                Debug.LogError("No BoxCollider found! Adding one...");
                triggerCollider = gameObject.AddComponent<BoxCollider>();
                triggerCollider.isTrigger = true;
            }
        }

        if (pitZone == null)
        {
            Debug.LogError("pitZone is NULL!");
            return;
        }
        Debug.Log("pitZone OK: " + pitZone.name);

        if (pitZone.gridData == null)
        {
            Debug.LogError("pitZone.gridData is NULL!");
            return;
        }
        Debug.Log("gridData OK: " + pitZone.gridData.name);

        Dictionary<Vector2Int, float> allCells = pitZone.gridData.GetAllCells();
        Debug.Log("Cell count: " + allCells.Count);

        if (allCells.Count == 0)
        {
            Debug.LogError("No cells in gridData!");
            return;
        }

        Vector2Int min = new Vector2Int(int.MaxValue, int.MaxValue);
        Vector2Int max = new Vector2Int(int.MinValue, int.MinValue);
        float maxDepth = pitZone.GetMaxDepth();

        Debug.Log("maxDepth: " + maxDepth);

        foreach (var kvp in allCells)
        {
            if (kvp.Key.x < min.x) min.x = kvp.Key.x;
            if (kvp.Key.y < min.y) min.y = kvp.Key.y;
            if (kvp.Key.x > max.x) max.x = kvp.Key.x;
            if (kvp.Key.y > max.y) max.y = kvp.Key.y;
        }

        Debug.Log("Grid bounds - min: " + min + ", max: " + max);

        float cellSize = pitZone.gridData.gridCellSize;
        float sizeX = (max.x - min.x + 1) * cellSize;
        float sizeZ = (max.y - min.y + 1) * cellSize;
        float sizeY = Mathf.Abs(maxDepth);

        Debug.Log("Calculated size - X: " + sizeX + ", Y: " + sizeY + ", Z: " + sizeZ);

        float centerWorldX = ((min.x + max.x) * 0.5f * cellSize) + (cellSize * 0.5f);
        float centerWorldZ = ((min.y + max.y) * 0.5f * cellSize) + (cellSize * 0.5f);

        Debug.Log("Center position - X: " + centerWorldX + ", Z: " + centerWorldZ);

        transform.localPosition = new Vector3(
            centerWorldX,
            maxDepth / 2f,
            centerWorldZ
        );

        if (triggerCollider == null)
        {
            Debug.LogError("triggerCollider is NULL!");
            return;
        }

        triggerCollider.size = new Vector3(sizeX, sizeY, sizeZ);
        triggerCollider.center = Vector3.zero;
        triggerCollider.isTrigger = true;

        bottomHeight = maxDepth + bottomZoneHeight;

        if (pitZone.fillType != null && pitZone.contentInstance != null)
        {
            float fillHeightAbsolute = Mathf.Abs(maxDepth * pitZone.fillHeightPercent);
            fillSurfaceHeight = maxDepth + fillHeightAbsolute;
        }
        else
        {
            fillSurfaceHeight = maxDepth;
        }

        Debug.Log("=== BOUNDS UPDATED - Size: " + sizeX.ToString("F1") + "x" + sizeY.ToString("F1") + "x" + sizeZ.ToString("F1") + ", FillSurface: " + fillSurfaceHeight.ToString("F2") + ", Bottom: " + bottomHeight.ToString("F2") + " ===");

        SetupNavMeshObstacle();
    }

    private void SetupNavMeshObstacle()
    {
        NavMeshObstacle obstacle = GetComponent<NavMeshObstacle>();

        if (obstacle == null)
        {
            obstacle = gameObject.AddComponent<NavMeshObstacle>();
        }

        obstacle.carving = true;
        obstacle.shape = NavMeshObstacleShape.Box;

        if (triggerCollider != null)
        {
            obstacle.size = triggerCollider.size;
            obstacle.center = triggerCollider.center;
        }

        Debug.Log("DamageController: NavMeshObstacle configured");
    }

    void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & fallableLayerMask) == 0) return;

        IPitInteractable interactable = other.GetComponent<IPitInteractable>();
        if (interactable == null) return;

        GameObject go = other.gameObject;

        if (!entitiesInPit.ContainsKey(go))
        {
            Vector3 entryPos = interactable.GetCharacterCenter();
            PitEntityData data = new PitEntityData(interactable, entryPos);
            entitiesInPit.Add(go, data);

            interactable.OnEnterPit(pitZone);

            Debug.Log("[PitDamage] " + go.name + " entered pit at Y=" + entryPos.y.ToString("F2"));
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (((1 << other.gameObject.layer) & fallableLayerMask) == 0) return;

        IPitInteractable interactable = other.GetComponent<IPitInteractable>();
        if (interactable == null) return;

        GameObject go = other.gameObject;

        if (!entitiesInPit.ContainsKey(go)) return;

        PitEntityData data = entitiesInPit[go];
        Vector3 centerPos = interactable.GetCharacterCenter();
        float centerY = centerPos.y;

        if (pitZone.fillType != null && centerY < fillSurfaceHeight)
        {
            ApplyFillDamage(interactable, data);
        }

        if (centerY < bottomHeight && !data.hasHitBottom)
        {
            ApplyFallDamage(interactable, data);
            data.hasHitBottom = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (((1 << other.gameObject.layer) & fallableLayerMask) == 0) return;

        IPitInteractable interactable = other.GetComponent<IPitInteractable>();
        if (interactable == null) return;

        GameObject go = other.gameObject;

        if (entitiesInPit.ContainsKey(go))
        {
            interactable.OnExitPit(pitZone);
            entitiesInPit.Remove(go);

            Debug.Log("[PitDamage] " + go.name + " exited pit");
        }
    }

    private void ApplyFillDamage(IPitInteractable interactable, PitEntityData data)
    {
        if (pitZone.fillType == null) return;
        if (!interactable.CanTakePitDamage()) return;

        PitContentType fillType = pitZone.fillType;

        switch (fillType.category)
        {
            case PitContentType.ContentCategory.Empty:
                break;

            case PitContentType.ContentCategory.InstantKill:
                interactable.TakePitDamage(99999f, PitDamageType.InstantKill);
                Debug.Log("[PitDamage] " + interactable.GetGameObject().name + " hit InstantKill content: " + fillType.contentName);
                break;

            case PitContentType.ContentCategory.Liquid:
                break;

            case PitContentType.ContentCategory.DamageZone:
                data.submersionTime += Time.deltaTime;

                if (data.submersionTime >= fillType.damageDelay)
                {
                    if (Time.time >= data.lastDamageTick + fillType.damageInterval)
                    {
                        float damageThisTick = fillType.damagePerSecond * fillType.damageInterval;
                        interactable.TakePitDamage(damageThisTick, PitDamageType.DamageOverTime);
                        data.lastDamageTick = Time.time;

                        Debug.Log("[PitDamage] " + interactable.GetGameObject().name + " took " + damageThisTick.ToString("F1") + " DoT from " + fillType.contentName);
                    }
                }
                break;
        }
    }

    private void ApplyFallDamage(IPitInteractable interactable, PitEntityData data)
    {
        if (!interactable.CanTakePitDamage()) return;

        float fallHeight = data.entryPosition.y - pitZone.GetMaxDepth();

        PitContentType fillType = pitZone.fillType;
        float immunityThreshold = fillType != null ? fillType.fallImmunityThreshold : 3f;
        float damageMultiplier = fillType != null ? fillType.fallDamageMultiplier : 10f;

        if (fallHeight > immunityThreshold)
        {
            float damage = (fallHeight - immunityThreshold) * damageMultiplier;
            interactable.TakePitDamage(damage, PitDamageType.Fall);

            Debug.Log("[PitDamage] " + interactable.GetGameObject().name + " took " + damage.ToString("F1") + " fall damage (fell " + fallHeight.ToString("F1") + "m)");
        }
        else
        {
            Debug.Log("[PitDamage] " + interactable.GetGameObject().name + " fell " + fallHeight.ToString("F1") + "m (below " + immunityThreshold.ToString("F1") + "m threshold, no damage)");
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;
        if (triggerCollider == null) return;

        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(triggerCollider.center, triggerCollider.size);

        if (pitZone != null && pitZone.fillType != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.matrix = Matrix4x4.identity;
            Vector3 center = transform.position;
            center.y = fillSurfaceHeight;
            Gizmos.DrawWireCube(center, new Vector3(triggerCollider.size.x, 0.1f, triggerCollider.size.z));
        }

        Gizmos.color = Color.yellow;
        Gizmos.matrix = Matrix4x4.identity;
        Vector3 bottomCenter = transform.position;
        bottomCenter.y = bottomHeight;
        Gizmos.DrawWireCube(bottomCenter, new Vector3(triggerCollider.size.x, 0.1f, triggerCollider.size.z));
    }
}