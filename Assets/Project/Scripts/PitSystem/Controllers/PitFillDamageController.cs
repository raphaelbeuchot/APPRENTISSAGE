using UnityEngine;
using System.Collections;
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
    [Tooltip("Seuil pour pit shallow vs deep (base sur fill height en metres)")]
    public float shallowPitThreshold = 1f;

    [Header("Progressive Death Settings")]
    [Tooltip("Intervalle entre chaque tick de degats progressifs (en secondes)")]
    public float damageTickRate = 0.1f;

    [Header("Debug")]
    public bool showDebugLogs = false;

    private Dictionary<GameObject, FillEntityData> entitiesInFill = new Dictionary<GameObject, FillEntityData>();
    private Dictionary<GameObject, Coroutine> activeDeathCoroutines = new Dictionary<GameObject, Coroutine>();

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
            // Arreter la coroutine si elle existe
            if (activeDeathCoroutines.ContainsKey(key))
            {
                if (activeDeathCoroutines[key] != null)
                {
                    StopCoroutine(activeDeathCoroutines[key]);
                }
                activeDeathCoroutines.Remove(key);
            }

            entitiesInFill.Remove(key);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log(string.Format("[PitFill DEBUG] OnTriggerEnter detected: {0}, layer: {1}",
            other.gameObject.name, LayerMask.LayerToName(other.gameObject.layer)));

        if (((1 << other.gameObject.layer) & detectionLayerMask) == 0)
        {
            Debug.Log("[PitFill DEBUG] Layer mask blocked!");
            return;
        }

        Debug.Log("[PitFill DEBUG] Layer mask passed, looking for IPitInteractable...");

        IPitInteractable interactable = other.GetComponent<IPitInteractable>();
        if (interactable == null)
        {
            Debug.Log(string.Format("[PitFill DEBUG] No IPitInteractable found on {0}!", other.gameObject.name));
            return;
        }

        Debug.Log(string.Format("[PitFill DEBUG] IPitInteractable found on {0}, processing...", other.gameObject.name));

        GameObject go = other.gameObject;

        if (!entitiesInFill.ContainsKey(go))
        {
            Vector3 entryPos = interactable.GetCharacterCenter();
            FillEntityData data = new FillEntityData(interactable, entryPos);
            entitiesInFill.Add(go, data);

            if (showDebugLogs)
                Debug.Log(string.Format("[PitFill] {0} entered fill: {1}", go.name, pitFill.fillType.contentName));

            // Traiter immediatement l'entree
            ProcessFillEntry(interactable, data);

            // NOUVEAU : Notifier le Player s'il entre dans l'eau
            if (pitFill.fillType.category == PitContentType.ContentCategory.Water)
            {
                PlayerPitInteractable playerPit = interactable as PlayerPitInteractable;
                if (playerPit != null)
                {
                    playerPit.OnEnterPit(pitZone);
                    Debug.Log("[PitFill DEBUG] Called OnEnterPit for Player");
                }
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (((1 << other.gameObject.layer) & detectionLayerMask) == 0) return;

        IPitInteractable interactable = other.GetComponent<IPitInteractable>();
        if (interactable == null) return;

        GameObject go = other.gameObject;

        if (entitiesInFill.ContainsKey(go))
        {
            if (showDebugLogs)
                Debug.Log(string.Format("[PitFill] {0} exited fill trigger", go.name));
        }
    }

    private void ProcessFillEntry(IPitInteractable interactable, FillEntityData data)
    {
        Debug.Log(string.Format("[PitFill DEBUG] ProcessFillEntry called for {0}", interactable.GetGameObject().name));

        if (pitFill.fillType == null)
        {
            Debug.Log("[PitFill DEBUG] pitFill.fillType is NULL!");
            return;
        }

        Debug.Log("[PitFill DEBUG] fillType exists, checking CanTakePitDamage...");

        if (!interactable.CanTakePitDamage())
        {
            Debug.Log("[PitFill DEBUG] Cannot take pit damage!");
            return;
        }

        Debug.Log("[PitFill DEBUG] Can take damage, getting fill height...");

        PitContentType fillType = pitFill.fillType;
        float fillHeight = pitFill.GetFillHeightMeters();
        bool isShallow = fillHeight <= shallowPitThreshold;

        GameObject go = interactable.GetGameObject();

        Debug.Log(string.Format("[PitFill DEBUG] fillHeight={0:F2}, shallowThreshold={1:F2}, isShallow={2}",
            fillHeight, shallowPitThreshold, isShallow));

        if (showDebugLogs)
        {
            Debug.Log(string.Format("[PitFill] ProcessFillEntry: {0}, category={1}, fillHeight={2:F2}m, isShallow={3}",
                go.name, fillType.category, fillHeight, isShallow));
        }

        Debug.Log(string.Format("[PitFill DEBUG] About to check enemy type..."));

        // Verifier si c'est un ennemi
        bool isEnemy = interactable is EnemyPitInteractable;

        Debug.Log(string.Format("[PitFill DEBUG] isEnemy={0}, category={1}", isEnemy, fillType.category));

        switch (fillType.category)
        {
            case PitContentType.ContentCategory.Empty:
                Debug.Log("[PitFill DEBUG] Category is Empty, marking as falling");

                // Marquer comme en chute et stocker la hauteur de chute
                data.isFalling = true;
                data.fallHeight = Mathf.Abs(pitZone.GetMaxDepth());

                Debug.Log(string.Format("[PitFill DEBUG] Stored fallHeight={0:F2}m", data.fallHeight));
                break;

            case PitContentType.ContentCategory.Water:
                Debug.Log(string.Format("[PitFill DEBUG] Category is Water, isEnemy={0}, isShallow={1}", isEnemy, isShallow));
                if (isEnemy)
                {
                    if (isShallow)
                    {
                        Debug.Log("[PitFill DEBUG] Shallow water for enemy - marking flag");

                        // Marquer le flag pour que EnemyAI ralentisse
                        EnemyPitInteractable enemyPit = interactable as EnemyPitInteractable;
                        if (enemyPit != null)
                        {
                            enemyPit.isInShallowWater = true;
                            Debug.Log(string.Format("[PitFill DEBUG] Flag set for {0}", go.name));
                        }

                        if (showDebugLogs)
                            Debug.Log(string.Format("[PitFill] {0} in shallow water - will be slowed by AI", go.name));
                    }
                    else
                    {
                        Debug.Log("[PitFill DEBUG] Deep water for enemy - starting death coroutine");
                        // Deep water: mort progressive
                        StartProgressiveDeathCoroutine(go, data, fillType);
                    }
                }
                // Pour le player: swim mode (TODO plus tard)
                break;

            case PitContentType.ContentCategory.InstantKill:
                Debug.Log("[PitFill DEBUG] Category is InstantKill - starting death coroutine");
                // Lava, Acid: mort progressive immediate
                StartProgressiveDeathCoroutine(go, data, fillType);
                break;

            case PitContentType.ContentCategory.Spikes:
                Debug.Log("[PitFill DEBUG] Category is Spikes");
                // Gere par collision avec le floor
                break;

            case PitContentType.ContentCategory.DamageZone:
                Debug.Log("[PitFill DEBUG] Category is DamageZone");
                // TODO: Degats over time si necessaire
                break;
        }

        Debug.Log("[PitFill DEBUG] ProcessFillEntry completed");
    }

    private void StartProgressiveDeathCoroutine(GameObject go, FillEntityData data, PitContentType fillType)
    {
        // Verifier si une coroutine existe deja
        if (activeDeathCoroutines.ContainsKey(go))
        {
            if (showDebugLogs)
                Debug.LogWarning(string.Format("[PitFill] {0} already has active death coroutine!", go.name));
            return;
        }

        // Lancer la coroutine
        Coroutine deathCoroutine = StartCoroutine(ProgressiveDeathCoroutine(go, data, fillType));
        activeDeathCoroutines[go] = deathCoroutine;

        if (showDebugLogs)
            Debug.Log(string.Format("[PitFill] {0} started progressive death coroutine ({1}s)", go.name, fillType.deathDuration));
    }

    private IEnumerator ProgressiveDeathCoroutine(GameObject go, FillEntityData data, PitContentType fillType)
    {
        float duration = fillType.deathDuration;
        float elapsed = 0f;
        float ticksNeeded = duration / damageTickRate;
        float damagePercentPerTick = 100f / ticksNeeded;

        if (showDebugLogs)
            Debug.Log(string.Format("[PitFill] {0} progressive death: {1:F1}% every {2}s for {3}s",
                go.name, damagePercentPerTick, damageTickRate, duration));

        while (elapsed < duration)
        {
            // Verifier si l'entite existe toujours
            if (go == null || data.interactable == null)
            {
                if (showDebugLogs)
                    Debug.Log("[PitFill] Entity destroyed, stopping coroutine");
                yield break;
            }

            // Verifier si l'entite peut encore prendre des degats
            if (!data.interactable.CanTakePitDamage())
            {
                if (showDebugLogs)
                    Debug.Log(string.Format("[PitFill] {0} already dead, stopping coroutine", go.name));
                CheckAndDestroyIfDead(data, fillType);
                yield break;
            }

            // Appliquer les degats
            data.interactable.TakePitDamage(damagePercentPerTick, PitDamageType.InstantKill);

            if (showDebugLogs)
                Debug.Log(string.Format("[PitFill] {0} took {1:F1}% damage (elapsed: {2:F2}s)",
                    go.name, damagePercentPerTick, elapsed));

            // Verifier si mort
            if (!data.interactable.CanTakePitDamage())
            {
                CheckAndDestroyIfDead(data, fillType);
                yield break;
            }

            // Attendre le prochain tick
            yield return new WaitForSeconds(damageTickRate);
            elapsed += damageTickRate;
        }

        // Fin de la duree : tuer si toujours vivant
        if (go != null && data.interactable != null && data.interactable.CanTakePitDamage())
        {
            data.interactable.TakePitDamage(100f, PitDamageType.InstantKill);

            if (showDebugLogs)
                Debug.Log(string.Format("[PitFill] {0} force killed after {1}s", go.name, duration));

            CheckAndDestroyIfDead(data, fillType);
        }

        // Nettoyer la reference a la coroutine
        if (activeDeathCoroutines.ContainsKey(go))
        {
            activeDeathCoroutines.Remove(go);
        }
    }

    private void CheckAndDestroyIfDead(FillEntityData data, PitContentType fillType)
    {
        if (data.interactable == null) return;

        // Verifier si l'entite est morte
        if (!data.interactable.CanTakePitDamage())
        {
            // Si c'est un liquide destructif (Lava, Acid), detruire le GameObject
            if (fillType.category == PitContentType.ContentCategory.InstantKill)
            {
                GameObject go = data.interactable.GetGameObject();

                if (go != null)
                {
                    if (showDebugLogs)
                        Debug.Log(string.Format("[PitFill] {0} will be destroyed by {1}", go.name, fillType.contentName));

                    Destroy(go, 0.5f); // Delai pour que la mort se termine proprement
                }
            }
        }
    }

    public void OnEntityHitFloor(IPitInteractable interactable, float fallHeight)
    {
        Debug.Log(string.Format("[PitFill DEBUG] OnEntityHitFloor called for {0}", interactable.GetGameObject().name));

        if (!interactable.CanTakePitDamage())
        {
            Debug.Log("[PitFill DEBUG] Cannot take pit damage, returning");
            return;
        }

        GameObject go = interactable.GetGameObject();

        Debug.Log(string.Format("[PitFill DEBUG] Checking if {0} is in entitiesInFill...", go.name));

        // Recuperer les donnees de l'entite si elle etait trackee
        if (entitiesInFill.ContainsKey(go))
        {
            Debug.Log(string.Format("[PitFill DEBUG] {0} FOUND in entitiesInFill!", go.name));

            FillEntityData data = entitiesInFill[go];

            Debug.Log(string.Format("[PitFill DEBUG] isFalling={0}, fallHeight={1:F2}", data.isFalling, data.fallHeight));

            // Si l'entite etait en chute (pit Empty) et a traverse le trigger
            if (data.isFalling)
            {
                Debug.Log(string.Format("[PitFill DEBUG] Calling ApplyFallDamage for {0} with fallHeight={1:F2}m", go.name, data.fallHeight));
                ApplyFallDamage(interactable, data);
                data.isFalling = false;
            }
        }
        else
        {
            Debug.Log(string.Format("[PitFill DEBUG] {0} NOT FOUND in entitiesInFill!", go.name));
        }

        // Gerer les cas speciaux (Spikes, etc.)
        if (pitFill.fillType != null)
        {
            switch (pitFill.fillType.category)
            {
                case PitContentType.ContentCategory.Spikes:
                    interactable.TakePitDamage(99999f, PitDamageType.InstantKill);
                    if (showDebugLogs)
                        Debug.Log(string.Format("[PitFill] {0} instant killed by Spikes", go.name));
                    break;
            }
        }
    }

    private void ApplyFallDamage(IPitInteractable interactable, FillEntityData data)
    {
        Debug.Log(string.Format("[PitFill DEBUG] ApplyFallDamage called for {0}", interactable.GetGameObject().name));

        // Utiliser la hauteur stockee dans les donnees
        float pitDepth = data.fallHeight;

        Debug.Log(string.Format("[PitFill DEBUG] Using stored fallHeight={0:F2}m", pitDepth));

        // Parametres depuis le SO ou valeurs par defaut
        float immunityThreshold = 3f;
        float damageMultiplier = 10f;

        if (pitFill.fillType != null)
        {
            immunityThreshold = pitFill.fillType.fallImmunityThreshold;
            damageMultiplier = pitFill.fillType.fallDamageMultiplier;
        }

        Debug.Log(string.Format("[PitFill DEBUG] threshold={0}, multiplier={1}", immunityThreshold, damageMultiplier));

        // Appliquer les degats si au-dessus du seuil
        if (pitDepth > immunityThreshold)
        {
            float damage = pitDepth * damageMultiplier;

            Debug.Log(string.Format("[PitFill DEBUG] Applying {0} fall damage to {1}", damage, interactable.GetGameObject().name));

            interactable.TakePitDamage(damage, PitDamageType.Fall);

            if (showDebugLogs)
                Debug.Log(string.Format("[PitFill] {0} took {1} fall damage (pit depth: {2}m, threshold: {3}m)",
                    interactable.GetGameObject().name, damage, pitDepth, immunityThreshold));
        }
        else
        {
            Debug.Log(string.Format("[PitFill DEBUG] No damage - pit too shallow ({0}m <= {1}m)", pitDepth, immunityThreshold));

            if (showDebugLogs)
                Debug.Log(string.Format("[PitFill] {0} hit floor but pit too shallow ({1}m <= {2}m)",
                    interactable.GetGameObject().name, pitDepth, immunityThreshold));
        }
    }
}

public class FillEntityData
{
    public IPitInteractable interactable;
    public Vector3 entryPosition;
    public float entryTime;

    // Pour Empty pits
    public bool isFalling;
    public float fallHeight;

    public FillEntityData(IPitInteractable e, Vector3 entryPos)
    {
        interactable = e;
        entryPosition = entryPos;
        entryTime = Time.time;
        isFalling = false;
        fallHeight = 0f;
    }
}