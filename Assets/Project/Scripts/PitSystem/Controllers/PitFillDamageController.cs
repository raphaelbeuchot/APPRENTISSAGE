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

    [Header("Pit Depth Settings")]
    [Tooltip("Seuil pour pit shallow vs deep (en mètres)")]
    public float shallowPitThreshold = 1f;

    [Header("Progressive Death Settings")]
    [Tooltip("Intervalle entre chaque tick de dégâts progressifs (en secondes)")]
    public float damageTickRate = 0.1f;

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
        ProcessProgressiveDeaths();
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

    private void ProcessProgressiveDeaths()
    {
        foreach (var kvp in entitiesInFill)
        {
            FillEntityData data = kvp.Value;

            if (data.isDyingProgressively)
            {
                if (Time.time >= data.nextDamageTick)
                {
                    ApplyProgressiveDamage(data);
                    data.nextDamageTick = Time.time + damageTickRate;
                }
            }
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

        float pitDepth = Mathf.Abs(pitZone.GetMaxDepth());
        bool isShallowPit = pitDepth <= shallowPitThreshold;

        // AJOUTE CE LOG
        if (showDebugLogs)
        {
            Debug.Log($"[PitFill] ProcessFillDamage: {interactable.GetGameObject().name}, category={fillType.category}, isShallow={isShallowPit}");
        }

        switch (fillType.category)
        {
            case PitContentType.ContentCategory.Empty:
                // Pas de dégâts du fill lui-même
                break;

            case PitContentType.ContentCategory.Water:
                ProcessWaterDamage(interactable, centerPos, fillSurfaceHeight, data, isShallowPit);
                break;

            case PitContentType.ContentCategory.InstantKill:
                ProcessInstantKillDamage(interactable, centerPos, fillSurfaceHeight, data, isShallowPit);
                break;

            case PitContentType.ContentCategory.Spikes:
                // Géré par OnCollisionEnter avec le floor
                break;

            case PitContentType.ContentCategory.DamageZone:
                ProcessDamageOverTime(interactable, data, fillType);
                break;
        }
    }

    private void ProcessWaterDamage(IPitInteractable interactable, Vector3 centerPos, float fillSurfaceHeight, FillEntityData data, bool isShallowPit)
    {
        bool isEnemy = interactable.GetGameObject().layer == LayerMask.NameToLayer("Zombie");

        if (isEnemy)
        {
            if (isShallowPit)
            {
                // Shallow water: ralentissement, pas de mort
                if (showDebugLogs && !data.hasLoggedShallowWater)
                {
                    Debug.Log($"[PitFill] {interactable.GetGameObject().name} in shallow water - slowed but alive");
                    data.hasLoggedShallowWater = true;
                }
            }
            else
            {
                // AJOUTE CE LOG TEMPORAIRE
                if (showDebugLogs)
                {
                    Debug.Log($"[PitFill DEBUG] centerPos.y={centerPos.y:F2}, fillSurfaceHeight={fillSurfaceHeight:F2}, isBelow={centerPos.y < fillSurfaceHeight}");
                }

                // Deep water: mort progressive
                if (centerPos.y < fillSurfaceHeight && !data.isDyingProgressively)
                {
                    StartProgressiveDeath(data, pitFill.fillType);
                }
            }
        }
    }

    private void ProcessInstantKillDamage(IPitInteractable interactable, Vector3 centerPos, float fillSurfaceHeight, FillEntityData data, bool isShallowPit)
    {
        if (pitFill.fillType.killTrigger == PitContentType.KillTrigger.CenterImmersed)
        {
            if (!data.isDyingProgressively && !data.hasReceivedInstantKill)
            {
                StartProgressiveDeath(data, pitFill.fillType);
            }
        }
    }

    private void StartProgressiveDeath(FillEntityData data, PitContentType fillType)
    {
        data.isDyingProgressively = true;
        data.deathStartTime = Time.time;
        data.deathDuration = fillType.deathDuration;
        data.nextDamageTick = Time.time + damageTickRate;

        // Calcule le damage par tick en pourcentage
        float ticksNeeded = fillType.deathDuration / damageTickRate;
        data.damagePercentPerTick = 100f / ticksNeeded;

        if (showDebugLogs)
            Debug.Log($"[PitFill] {data.interactable.GetGameObject().name} started progressive death ({fillType.deathDuration}s, {data.damagePercentPerTick:F1}% per tick)");
    }

    private void ApplyProgressiveDamage(FillEntityData data)
    {
        if (data.interactable == null || !data.interactable.CanTakePitDamage())
        {
            data.isDyingProgressively = false;
            CheckAndDestroyIfDead(data);
            return;
        }

        // Applique les dégâts en pourcentage
        // On utilise une grande valeur qui sera réduite par le pourcentage
        float damage = data.damagePercentPerTick;

        data.interactable.TakePitDamage(damage, PitDamageType.InstantKill);
        CheckAndDestroyIfDead(data);
        if (showDebugLogs)
            Debug.Log($"[PitFill] {data.interactable.GetGameObject().name} took {damage:F1}% damage (progressive death)");
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

    private void CheckAndDestroyIfDead(FillEntityData data)
    {
        if (data.interactable == null) return;

        // Check si l'entité est morte
        if (!data.interactable.CanTakePitDamage())
        {
            // Check si c'est un liquide destructif (Lava, Acid)
            if (pitFill.fillType.category == PitContentType.ContentCategory.InstantKill)
            {
                GameObject go = data.interactable.GetGameObject();

                if (go != null)
                {
                    if (showDebugLogs)
                        Debug.Log($"[PitFill] {go.name} destroyed by {pitFill.fillType.contentName}");

                    Destroy(go, 0.2f); // Petit délai pour que la mort se termine
                }
            }

            // Arrête la mort progressive
            data.isDyingProgressively = false;
        }
    }

    public void OnEntityHitFloor(IPitInteractable interactable, float fallHeight)
    {
        if (!interactable.CanTakePitDamage()) return;

        if (pitFill.fillType == null) return;

        PitContentType fillType = pitFill.fillType;

        switch (fillType.category)
        {
            case PitContentType.ContentCategory.Empty:
                ApplyFallDamage(interactable, fallHeight, fillType);
                break;

            case PitContentType.ContentCategory.Spikes:
                ApplyInstantKill(interactable, "Spikes");
                break;

            default:
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

    private void ApplyInstantKill(IPitInteractable interactable, string reason)
    {
        interactable.TakePitDamage(99999f, PitDamageType.InstantKill);

        if (showDebugLogs)
            Debug.Log($"[PitFill] {interactable.GetGameObject().name} instant killed by {reason}");
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

    // Progressive death
    public bool isDyingProgressively;
    public float deathStartTime;
    public float deathDuration;
    public float damagePercentPerTick;
    public float nextDamageTick;

    // Debug
    public bool hasLoggedShallowWater;

    public FillEntityData(IPitInteractable e, Vector3 entryPos)
    {
        interactable = e;
        entryPosition = entryPos;
        entryTime = Time.time;
        hasReceivedInstantKill = false;
        submersionTime = 0f;
        lastDamageTick = 0f;
        isDyingProgressively = false;
        hasLoggedShallowWater = false;
    }
}