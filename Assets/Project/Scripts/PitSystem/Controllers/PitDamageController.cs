using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AI;

[RequireComponent(typeof(BoxCollider))]
public class PitDamageController : MonoBehaviour
{
    [Header("References")]
    public PitZone pitZone;

    [Header("Detection Settings")]
    public LayerMask fallableLayerMask = -1;

    [Header("Zone Heights")]
    public float bottomZoneHeight = 0.5f;

    [Header("Debug")]
    public bool showDebugGizmos = true;

    private BoxCollider triggerCollider;
    private Dictionary<GameObject, PitEntityData> entitiesInPit = new Dictionary<GameObject, PitEntityData>();

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
        if (pitZone == null)
        {
            Debug.LogError("pitZone is NULL!");
            return;
        }

        if (pitZone.gridData == null)
        {
            Debug.LogError("pitZone.gridData is NULL!");
            return;
        }

        if (pitZone.zoneID == -1)
        {
            Debug.LogError("pitZone has invalid zoneID!");
            return;
        }

        Dictionary<Vector2Int, float> ownedCells = pitZone.gridData.GetCellsForZone(pitZone.zoneID);

        if (ownedCells.Count == 0)
        {
            Debug.LogError("No owned cells in this zone!");
            return;
        }

        // Supprime les anciens colliders
        BoxCollider[] oldColliders = GetComponents<BoxCollider>();
        foreach (BoxCollider col in oldColliders)
        {
            DestroyImmediate(col);
        }

        float cellSize = pitZone.gridData.gridCellSize;
        float maxDepth = pitZone.GetMaxDepth();
        float sizeY = Mathf.Abs(maxDepth);

        // Cree un BoxCollider par cellule
        foreach (var kvp in ownedCells)
        {
            Vector2Int cellPos = kvp.Key;
            Vector3 cellWorldPos = pitZone.gridData.CellToWorld(cellPos);

            BoxCollider cellCollider = gameObject.AddComponent<BoxCollider>();
            cellCollider.isTrigger = true;

            // Position relative au DamageController
            Vector3 localPos = cellWorldPos + new Vector3(cellSize * 0.5f, maxDepth * 0.5f, cellSize * 0.5f);
            cellCollider.center = transform.InverseTransformPoint(localPos);
            cellCollider.size = new Vector3(cellSize, sizeY, cellSize);
        }

        // Met a jour les hauteurs
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

        // Position le DamageController au centre du pit pour simplifier
        Vector2Int min = new Vector2Int(int.MaxValue, int.MaxValue);
        Vector2Int max = new Vector2Int(int.MinValue, int.MinValue);

        foreach (var kvp in ownedCells)
        {
            if (kvp.Key.x < min.x) min.x = kvp.Key.x;
            if (kvp.Key.y < min.y) min.y = kvp.Key.y;
            if (kvp.Key.x > max.x) max.x = kvp.Key.x;
            if (kvp.Key.y > max.y) max.y = kvp.Key.y;
        }

        float centerWorldX = ((min.x + max.x) * 0.5f * cellSize) + (cellSize * 0.5f);
        float centerWorldZ = ((min.y + max.y) * 0.5f * cellSize) + (cellSize * 0.5f);

        transform.localPosition = new Vector3(centerWorldX, 0, centerWorldZ);

        SetupNavMeshObstacle();

        Debug.Log("DamageController: Created " + ownedCells.Count + " BoxColliders for pit shape");
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

        // Calcule la taille englobante pour le NavMeshObstacle
        Dictionary<Vector2Int, float> ownedCells = pitZone.gridData.GetCellsForZone(pitZone.zoneID);

        Vector2Int min = new Vector2Int(int.MaxValue, int.MaxValue);
        Vector2Int max = new Vector2Int(int.MinValue, int.MinValue);

        foreach (var kvp in ownedCells)
        {
            if (kvp.Key.x < min.x) min.x = kvp.Key.x;
            if (kvp.Key.y < min.y) min.y = kvp.Key.y;
            if (kvp.Key.x > max.x) max.x = kvp.Key.x;
            if (kvp.Key.y > max.y) max.y = kvp.Key.y;
        }

        float cellSize = pitZone.gridData.gridCellSize;
        float sizeX = (max.x - min.x + 1) * cellSize;
        float sizeZ = (max.y - min.y + 1) * cellSize;
        float maxDepth = pitZone.GetMaxDepth();
        float sizeY = Mathf.Abs(maxDepth);

        obstacle.size = new Vector3(sizeX, sizeY, sizeZ);
        obstacle.center = new Vector3(0, maxDepth * 0.5f, 0);
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
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;

        BoxCollider[] colliders = GetComponents<BoxCollider>();

        foreach (BoxCollider col in colliders)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(col.center, col.size);
        }

        if (pitZone != null && pitZone.fillType != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.matrix = Matrix4x4.identity;
            Vector3 center = transform.position;
            center.y = fillSurfaceHeight;

            // Approximation pour l'affichage
            NavMeshObstacle obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle != null)
            {
                Gizmos.DrawWireCube(center, new Vector3(obstacle.size.x, 0.1f, obstacle.size.z));
            }
        }
    }
}