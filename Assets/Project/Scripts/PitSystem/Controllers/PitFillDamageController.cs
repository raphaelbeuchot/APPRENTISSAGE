using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(PitFill))]
public class PitFillDamageController : MonoBehaviour
{
    [Header("References")]
    public PitFill pitFill;
    public PitZone pitZone;

    [Header("Detection Settings")]
    public LayerMask detectionLayerMask = -1;

    [Header("Debug")]
    public bool showDebugLogs = false;

    private Dictionary<GameObject, FillEntityData> entitiesInFill = new Dictionary<GameObject, FillEntityData>();

    void Awake()
    {
        if (pitFill == null)
        {
            pitFill = GetComponent<PitFill>();
        }

        if (pitZone == null && pitFill != null)
        {
            pitZone = pitFill.pitZone;
        }

        if (pitFill == null || pitZone == null)
        {
            Debug.LogError("PitFillDamageController: Missing PitFill or PitZone reference!");
        }
    }

    void Update()
    {
        CleanupDestroyedEntities();
    }

    private void CleanupDestroyedEntities()
    {
        List<GameObject> keysToRemove = new List<GameObject>();

        foreach (var kvp in entitiesInFill)
        {
            if (kvp.Key == null || kvp.Value.interactable == null)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (GameObject key in keysToRemove)
        {
            entitiesInFill.Remove(key);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & detectionLayerMask) == 0) return;

        IPitInteractable interactable = other.GetComponent<IPitInteractable>();
        if (interactable == null) return;

        GameObject go = other.gameObject;

        if (!entitiesInFill.ContainsKey(go))
        {
            Vector3 entryPos = interactable.GetCharacterCenter();
            FillEntityData data = new FillEntityData(interactable, entryPos);
            entitiesInFill.Add(go, data);

            if (showDebugLogs)
                Debug.Log($"[PitFill] {go.name} entered fill: {pitFill.fillType.contentName}");
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (((1 << other.gameObject.layer) & detectionLayerMask) == 0) return;

        IPitInteractable interactable = other.GetComponent<IPitInteractable>();
        if (interactable == null) return;

        GameObject go = other.gameObject;

        // Si pas tracke, ajoute
        if (!entitiesInFill.ContainsKey(go))
        {
            Vector3 entryPos = interactable.GetCharacterCenter();
            FillEntityData data = new FillEntityData(interactable, entryPos);
            entitiesInFill.Add(go, data);

            if (showDebugLogs)
                Debug.LogWarning($"[PitFill] {go.name} was in fill but not tracked, re-adding");
        }

        FillEntityData entityData = entitiesInFill[go];

        if (entityData.interactable == null)
        {
            entitiesInFill.Remove(go);
            return;
        }

        if (!interactable.CanTakePitDamage())
            return;

        ProcessFillDamage(interactable, entityData);
    }

    void OnTriggerExit(Collider other)
    {
        if (((1 << other.gameObject.layer) & detectionLayerMask) == 0) return;

        IPitInteractable interactable = other.GetComponent<IPitInteractable>();
        if (interactable == null) return;

        GameObject go = other.gameObject;

        if (entitiesInFill.ContainsKey(go))
        {
            entitiesInFill.Remove(go);

            if (showDebugLogs)
                Debug.Log($"[PitFill] {go.name} exited fill");
        }
    }

    private void ProcessFillDamage(IPitInteractable interactable, FillEntityData data)
    {
        if (pitFill.fillType == null) return;

        PitContentType fillType = pitFill.fillType;
        Vector3 centerPos = interactable.GetCharacterCenter();
        float fillSurfaceHeight = pitFill.GetFillSurfaceHeight();

        switch (fillType.category)
        {
            case PitContentType.ContentCategory.Empty:
                // Pas de degats du fill lui-meme (fall damage gere par collision avec floor)
                break;

            case PitContentType.ContentCategory.Water:
                // Water : InstantKill pour zombies, Swim pour player
                ProcessWaterDamage(interactable, centerPos, fillSurfaceHeight, data);
                break;

            case PitContentType.ContentCategory.InstantKill:
                // Lava, Acid : InstantKill pour tous quand center immerge
                if (fillType.killTrigger == PitContentType.KillTrigger.CenterImmersed)
                {
                    if (centerPos.y < fillSurfaceHeight && !data.hasReceivedInstantKill)
                    {
                        ApplyInstantKill(interactable, "InstantKill liquid");
                        data.hasReceivedInstantKill = true;
                    }
                }
                break;

            case PitContentType.ContentCategory.Spikes:
                // Spikes : gere par OnCollisionEnter avec le floor
                break;

            case PitContentType.ContentCategory.DamageZone:
                // DamageOverTime (maggots pool, etc)
                ProcessDamageOverTime(interactable, data, fillType);
                break;
        }
    }

    private void ProcessWaterDamage(IPitInteractable interactable, Vector3 centerPos, float fillSurfaceHeight, FillEntityData data)
    {
        // Check si c'est un zombie (pas le player)
        bool isEnemy = interactable.GetGameObject().CompareTag("Enemy");

        if (isEnemy)
        {
            // Zombies : InstantKill quand center immerge
            if (centerPos.y < fillSurfaceHeight && !data.hasReceivedInstantKill)
            {
                ApplyInstantKill(interactable, "Water (enemy)");
                data.hasReceivedInstantKill = true;
            }
        }
        else
        {
            // Player : Swim mode gere dans PlayerPitInteractable
            // Ici on ne fait rien
        }
    }

    private void ProcessDamageOverTime(IPitInteractable interactable, FillEntityData data, PitContentType fillType)
    {
        data.submersionTime += Time.deltaTime;

        if (data.submersionTime >= fillType.damageDelay)
        {
            if (Time.time >= data.lastDamageTick + fillType.damageInterval)
            {
                float damageThisTick = fillType.damagePerSecond * fillType.damageInterval;
                interactable.TakePitDamage(damageThisTick, PitDamageType.DamageOverTime);
                data.lastDamageTick = Time.time;

                if (showDebugLogs)
                    Debug.Log($"[PitFill] {interactable.GetGameObject().name} took {damageThisTick} DoT damage");
            }
        }
    }

    private void ApplyInstantKill(IPitInteractable interactable, string reason)
    {
        interactable.TakePitDamage(99999f, PitDamageType.InstantKill);

        if (showDebugLogs)
            Debug.Log($"[PitFill] {interactable.GetGameObject().name} instant killed by {reason}");
    }

    // Appele par le floor du pit via OnCollisionEnter
    public void OnEntityHitFloor(IPitInteractable interactable, float fallHeight)
    {
        if (!interactable.CanTakePitDamage()) return;

        if (pitFill.fillType == null) return;

        PitContentType fillType = pitFill.fillType;

        switch (fillType.category)
        {
            case PitContentType.ContentCategory.Empty:
                // Fall damage
                ApplyFallDamage(interactable, fallHeight, fillType);
                break;

            case PitContentType.ContentCategory.Spikes:
                // InstantKill au contact du fond
                ApplyInstantKill(interactable, "Spikes");
                break;

            default:
                // Autres categories : pas de degats au contact du fond
                // (liquides tuent avant d'arriver au fond)
                break;
        }
    }

    private void ApplyFallDamage(IPitInteractable interactable, float fallHeight, PitContentType fillType)
    {
        float immunityThreshold = fillType.fallImmunityThreshold;
        float damageMultiplier = fillType.fallDamageMultiplier;

        if (fallHeight > immunityThreshold)
        {
            float damage = (fallHeight - immunityThreshold) * damageMultiplier;
            interactable.TakePitDamage(damage, PitDamageType.Fall);

            if (showDebugLogs)
                Debug.Log($"[PitFill] {interactable.GetGameObject().name} took {damage} fall damage (fell {fallHeight}m)");
        }
    }
}

public class FillEntityData
{
    public IPitInteractable interactable;
    public Vector3 entryPosition;
    public float entryTime;
    public bool hasReceivedInstantKill;
    public float submersionTime;
    public float lastDamageTick;

    public FillEntityData(IPitInteractable e, Vector3 entryPos)
    {
        interactable = e;
        entryPosition = entryPos;
        entryTime = Time.time;
        hasReceivedInstantKill = false;
        submersionTime = 0f;
        lastDamageTick = 0f;
    }
}