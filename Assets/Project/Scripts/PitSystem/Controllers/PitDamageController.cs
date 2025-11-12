using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(BoxCollider))]
public class PitDamageController : MonoBehaviour
{
    [Header("References")]
    public PitZone pitZone;

    [Header("Detection Settings")]
    [Tooltip("Layers qui peuvent tomber dans la fosse (Player, Enemies, etc.)")]
    public LayerMask fallableLayerMask = -1; // Tous les layers par défaut

    [Header("Zone Heights")]
    [Tooltip("Distance du fond pour déclencher le fall damage (en mètres)")]
    public float bottomZoneHeight = 0.5f;

    [Header("Debug")]
    public bool showDebugGizmos = true;

    // Components
    private BoxCollider triggerCollider;

    // Tracking des entités
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

    /// <summary>
    /// Met à jour les bounds du trigger en fonction de la grille de la fosse
    /// </summary>
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

        // Calcule les bounds de la fosse
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

        // Calcul du centre en world space directement
        float centerWorldX = ((min.x + max.x) * 0.5f * cellSize) + (cellSize * 0.5f);
        float centerWorldZ = ((min.y + max.y) * 0.5f * cellSize) + (cellSize * 0.5f);

        Debug.Log("Center position - X: " + centerWorldX + ", Z: " + centerWorldZ);

        // Position locale par rapport au PitZone parent
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
        triggerCollider.isTrigger = true;  // FIX AJOUTE ICI

        // Cache les hauteurs pour les calculs
        bottomHeight = maxDepth + bottomZoneHeight;

        if (pitZone.fillType != null && pitZone.contentInstance != null)
        {
            // Calcule la hauteur de la surface du fill
            float fillHeightAbsolute = Mathf.Abs(maxDepth * pitZone.fillHeightPercent);
            fillSurfaceHeight = maxDepth + fillHeightAbsolute;
        }
        else
        {
            fillSurfaceHeight = maxDepth;
        }

        Debug.Log("=== BOUNDS UPDATED - Size: " + sizeX.ToString("F1") + "x" + sizeY.ToString("F1") + "x" + sizeZ.ToString("F1") + ", FillSurface: " + fillSurfaceHeight.ToString("F2") + ", Bottom: " + bottomHeight.ToString("F2") + " ===");
    }

    void OnTriggerEnter(Collider other)
    {
        // Check layer mask
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

        // CHECK 1: Contact avec le fill content ?
        if (pitZone.fillType != null && centerY < fillSurfaceHeight)
        {
            ApplyFillDamage(interactable, data);
        }

        // CHECK 2: Contact avec le fond ?
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

            Debug.Log($"[PitDamage] {go.name} exited pit");
        }
    }

    /// <summary>
    /// Applique les dégâts du fill content (InstantKill, DPS, etc.)
    /// </summary>
    private void ApplyFillDamage(IPitInteractable interactable, PitEntityData data)
    {
        if (pitZone.fillType == null) return;
        if (!interactable.CanTakePitDamage()) return;

        PitContentType fillType = pitZone.fillType;

        switch (fillType.category)
        {
            case PitContentType.ContentCategory.Empty:
                // Pas de dégâts
                break;

            case PitContentType.ContentCategory.InstantKill:
                // Mort instantanée au contact
                interactable.TakePitDamage(99999f, PitDamageType.InstantKill);
                Debug.Log($"[PitDamage] {interactable.GetGameObject().name} hit InstantKill content: {fillType.contentName}");
                break;

            case PitContentType.ContentCategory.Liquid:
                // Pas de dégâts, juste slow (géré dans les implémentations)
                break;

            case PitContentType.ContentCategory.DamageZone:
                // Dégâts progressifs
                data.submersionTime += Time.deltaTime;

                if (data.submersionTime >= fillType.damageDelay)
                {
                    if (Time.time >= data.lastDamageTick + fillType.damageInterval)
                    {
                        float damageThisTick = fillType.damagePerSecond * fillType.damageInterval;
                        interactable.TakePitDamage(damageThisTick, PitDamageType.DamageOverTime);
                        data.lastDamageTick = Time.time;

                        Debug.Log($"[PitDamage] {interactable.GetGameObject().name} took {damageThisTick:F1} DoT from {fillType.contentName}");
                    }
                }
                break;
        }
    }

    /// <summary>
    /// Applique les dégâts de chute au contact du fond
    /// </summary>
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

            Debug.Log($"[PitDamage] {interactable.GetGameObject().name} took {damage:F1} fall damage (fell {fallHeight:F1}m)");
        }
        else
        {
            Debug.Log($"[PitDamage] {interactable.GetGameObject().name} fell {fallHeight:F1}m (below {immunityThreshold}m threshold, no damage)");
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;
        if (triggerCollider == null) return;

        // Trigger bounds (rouge transparent)
        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(triggerCollider.center, triggerCollider.size);

        // Fill surface height (bleu)
        if (pitZone != null && pitZone.fillType != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.matrix = Matrix4x4.identity;
            Vector3 center = transform.position;
            center.y = fillSurfaceHeight;
            Gizmos.DrawWireCube(center, new Vector3(triggerCollider.size.x, 0.1f, triggerCollider.size.z));
        }

        // Bottom zone (jaune)
        Gizmos.color = Color.yellow;
        Gizmos.matrix = Matrix4x4.identity;
        Vector3 bottomCenter = transform.position;
        bottomCenter.y = bottomHeight;
        Gizmos.DrawWireCube(bottomCenter, new Vector3(triggerCollider.size.x, 0.1f, triggerCollider.size.z));
    }
}