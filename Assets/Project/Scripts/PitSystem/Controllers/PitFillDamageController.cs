using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Pathfinding;

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

    private AudioSource audioSource2D;

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
        audioSource2D = gameObject.AddComponent<AudioSource>();
        audioSource2D.spatialBlend = 0f;
        audioSource2D.playOnAwake = false;
    }

    /*void Update()
    {
        CleanupDestroyedEntities();
    }
    */

    private void CleanupDestroyedEntities()
    {
        List<GameObject> keysToRemove = new List<GameObject>();

        foreach (var kvp in entitiesInFill)
        {
            // NE PAS RETIRER si en train de tomber (isFalling = true)
            if (kvp.Value.isFalling)
                continue;

            if (kvp.Key == null || kvp.Value.interactable == null)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (GameObject key in keysToRemove)
        {
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

            if (other.gameObject.name.Contains("Hips"))
            {
                CorpsePitHandler corpse = other.GetComponentInParent<CorpsePitHandler>();
                if (corpse != null && pitFill.fillType != null)
                {
                    corpse.OnEnterPit(pitFill.fillType.category);
                }
            }
            return;
        }

        Debug.Log(string.Format("[PitFill DEBUG] IPitInteractable found on {0}, processing...", other.gameObject.name));

        GameObject go = other.gameObject;

        if (!entitiesInFill.ContainsKey(go))
        {
            Vector3 entryPos = interactable.GetCharacterCenter();
            FillEntityData data = new FillEntityData(interactable, entryPos);
            entitiesInFill.Add(go, data);
            Debug.Log($"[PitFill] ADDED {go.name} to dict. Dict size: {entitiesInFill.Count}");
            Debug.Log($"[PitFill] GameObject InstanceID: {go.GetInstanceID()}");

            

            if (showDebugLogs)
                Debug.Log(string.Format("[PitFill] {0} entered fill: {1}", go.name, pitFill.fillType.contentName));

            // Traiter immediatement l'entree
            ProcessFillEntry(interactable, data);

            // NOUVEAU : Notifier les ennemis qu'ils entrent dans un pit Empty
            EnemyPitInteractable enemyPit = interactable as EnemyPitInteractable;
            if (enemyPit != null)
            {
                if (pitFill.fillType.category == PitContentType.ContentCategory.Empty ||
                    pitFill.fillType.category == PitContentType.ContentCategory.Water)
                {
                    enemyPit.OnEnterPit(pitZone);
                    Debug.Log(string.Format("[PitFill DEBUG] Called OnEnterPit for Enemy ({0})", pitFill.fillType.category));
                }
            }

            // Notifier le Player qu'il entre dans le pit (Water OU Empty)
            PlayerPitInteractable playerPit = interactable as PlayerPitInteractable;
            if (playerPit != null)
            {
                if (pitFill.fillType.category == PitContentType.ContentCategory.Water ||
                    pitFill.fillType.category == PitContentType.ContentCategory.Empty)
                {
                    playerPit.OnEnterPit(pitZone);
                    Debug.Log($"[PitFill DEBUG] Called OnEnterPit for Player ({pitFill.fillType.category})");
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
            FillEntityData data = entitiesInFill[go];

            // NE PAS RETIRER si l'entite est en train de tomber
            if (data.isFalling)
            {
                // NOUVEAU : Exception SEULEMENT pour le player en train de climb out
                PlayerPitInteractable playerPit = interactable as PlayerPitInteractable;
                if (playerPit != null && playerPit.IsClimbingOut())
                {
                    entitiesInFill.Remove(go);
                    if (showDebugLogs)
                        Debug.Log($"[PitFill] {go.name} climbing out - removed from dict");
                    return;
                }

                if (showDebugLogs)
                    Debug.Log($"[PitFill] {go.name} exited trigger but is falling - keeping in dict");
                return;
            }

            // NE PAS RETIRER si mort progressive en cours (Water, Lava, etc.)
            if (activeDeathCoroutines.ContainsKey(go))
            {
                if (showDebugLogs)
                    Debug.Log($"[PitFill] {go.name} exited trigger but death coroutine active - keeping in dict");
                return;
            }

            // Sinon, retirer normalement
            entitiesInFill.Remove(go);

            if (showDebugLogs)
                Debug.Log($"[PitFill] {go.name} exited fill trigger - removed from dict");
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

                // NOUVEAU : Si pit profond (>6m), ignorer distance check pour healthbar
                EnemyPitInteractable enemyPitInt = interactable as EnemyPitInteractable;
                if (enemyPitInt != null && data.fallHeight > 6f)
                {
                    enemyPitInt.shouldIgnoreHealthbarDistance = true;
                    Debug.Log(string.Format("[PitFill] {0} in deep pit ({1}m), healthbar will ignore distance", go.name, data.fallHeight));
                }

                // NOUVEAU : Reduire air resistance pour chute realiste
                Rigidbody rbFall = go.GetComponent<Rigidbody>();
                if (rbFall != null)
                {
                    // Sauvegarder damping original
                    data.originalLinearDamping = rbFall.linearDamping;

                    // Quasi-supprimer l'air resistance
                    rbFall.linearDamping = 0.1f;

                    Debug.Log(string.Format("[PitFill] {0} damping reduced: {1:F1} -> 0.1", go.name, data.originalLinearDamping));
                }

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
                EnemyPitInteractable enemyPitLava = interactable as EnemyPitInteractable;
                if (enemyPitLava != null)
                {
                    EnemyHealth ehLava = go.GetComponent<EnemyHealth>();
                    if (ehLava != null && ehLava.stats.lavaSplashSound != null)
                        audioSource2D.PlayOneShot(ehLava.stats.lavaSplashSound);
                }
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

        // LOG AJOUTÉ
        Debug.Log($"[PitFill COROUTINE] Starting for {go.name}: duration={duration}s, ticks={ticksNeeded}, dmg/tick={damagePercentPerTick}%");

        if (showDebugLogs)
            Debug.Log(string.Format("[PitFill] {0} progressive death: {1:F1}% every {2}s for {3}s",
                go.name, damagePercentPerTick, damageTickRate, duration));

        while (elapsed < duration)
        {
            // LOG AJOUTÉ
            Debug.Log($"[PitFill COROUTINE] {go.name} tick: elapsed={elapsed:F2}s / {duration}s");

            // Verifier si l'entite existe toujours
            if (go == null || data.interactable == null)
            {
                Debug.Log("[PitFill COROUTINE] Entity destroyed, stopping"); // MODIFIÉ
                yield break;
            }

            // Verifier si l'entite peut encore prendre des degats
            if (!data.interactable.CanTakePitDamage())
            {
                Debug.Log($"[PitFill COROUTINE] {go.name} already dead, stopping"); // MODIFIÉ
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
                Debug.Log($"[PitFill COROUTINE] {go.name} died after damage"); // AJOUTÉ
                CheckAndDestroyIfDead(data, fillType);
                yield break;
            }

            // Attendre le prochain tick
            yield return new WaitForSeconds(damageTickRate);
            elapsed += damageTickRate;
        }

        // LOG AJOUTÉ
        Debug.Log($"[PitFill COROUTINE] {go.name} finished loop, force killing");

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

        // LOG AJOUTÉ
        Debug.Log($"[PitFill COROUTINE] {go.name} coroutine completed");
    }
    public void ForceRemoveEntity(GameObject go)
    {
        if (entitiesInFill.ContainsKey(go))
        {
            if (activeDeathCoroutines.ContainsKey(go))
            {
                if (activeDeathCoroutines[go] != null)
                {
                    StopCoroutine(activeDeathCoroutines[go]);
                }
                activeDeathCoroutines.Remove(go);
            }

            entitiesInFill.Remove(go);

            if (showDebugLogs)
                Debug.Log($"[PitFill] Force removed {go.name} from dict");
        }
    }
    private void CheckAndDestroyIfDead(FillEntityData data, PitContentType fillType)
    {
        if (data.interactable == null) return;

        if (!data.interactable.CanTakePitDamage())
        {
            GameObject go = data.interactable.GetGameObject();
            if (go != null)
            {
                // Si c'est un ennemi, on declenche le clean up
                EnemyHealth eh = go.GetComponent<EnemyHealth>();
                if (eh != null && fillType != null)
                {
                    CorpsePitHandler handler = go.GetComponent<CorpsePitHandler>();
                    if (handler == null)
                        handler = go.AddComponent<CorpsePitHandler>();
                    handler.sourceEnemy = eh;
                    handler.OnEnterPit(fillType.category);
                }

                if (fillType.category == PitContentType.ContentCategory.InstantKill ||
                    fillType.category == PitContentType.ContentCategory.Water ||
                    fillType.category == PitContentType.ContentCategory.Empty)
                {
                    float delay = fillType.destroyDelay;
                    if (showDebugLogs)
                        Debug.Log(string.Format("[PitFill] {0} will be destroyed in {1}s", go.name, delay));
                    if (eh == null || eh.destroyOnDeath)
                        Destroy(go, delay);
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
        Debug.Log($"[PitFill] Looking for {go.name}. GameObject InstanceID: {go.GetInstanceID()}");
        Debug.Log($"[PitFill] Dict contains {entitiesInFill.Count} entries");

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

        float pitDepth = data.fallHeight;
        Debug.Log(string.Format("[PitFill DEBUG] Using stored fallHeight={0:F2}m", pitDepth));

        float immunityThreshold = 3f;
        float damageMultiplier = 10f;

        if (pitFill.fillType != null)
        {
            immunityThreshold = pitFill.fillType.fallImmunityThreshold;
            damageMultiplier = pitFill.fillType.fallDamageMultiplier;
        }

        Debug.Log(string.Format("[PitFill DEBUG] threshold={0}, multiplier={1}", immunityThreshold, damageMultiplier));

        // Pour empty pit avec trigger à -0.3m, on ajuste le seuil
        float effectiveThreshold = immunityThreshold - 0.3f; // 3m - 0.3m = 2.7m

        if (pitDepth > immunityThreshold)
        {
            float totalDamagePercent = pitDepth * damageMultiplier;
            GameObject go = interactable.GetGameObject();

            // Vérifier si les dégâts vont tuer (pour ragdoll)
            EnemyHealth enemyHealth = go.GetComponent<EnemyHealth>();
            bool willDie = false;

            if (enemyHealth != null)
            {
                float currentHealthPercent = (enemyHealth.GetCurrentHealth() / enemyHealth.GetMaxHealth()) * 100f;
                willDie = totalDamagePercent >= currentHealthPercent;
                Debug.Log($"[PitFill] {go.name} - Current HP: {currentHealthPercent:F1}%, Damage: {totalDamagePercent:F1}%, WillDie: {willDie}");
            }

            // Activer le ragdoll SEULEMENT si ça va le tuer
            if (willDie && enemyHealth != null)
            {
                enemyHealth.deathByPit = true;
                IDeathEffect[] deathEffects = go.GetComponents<IDeathEffect>();
                if (deathEffects != null && deathEffects.Length > 0)
                {
                    DeathContext impactContext = new DeathContext
                    {
                        deathType = DeathContext.DeathType.Fall,
                        impactDirection = Vector3.down,
                        impactForce = pitDepth * 10f
                    };

                    foreach (IDeathEffect effect in deathEffects)
                    {
                        effect.OnDeath(go.transform.position, impactContext);
                    }

                    Debug.Log($"[PitFill] Ragdoll activated on lethal impact for {go.name}");
                }
            }

                       
            // APPLIQUER LES DÉGÂTS INSTANTANÉMENT
            interactable.TakePitDamage(totalDamagePercent, PitDamageType.Fall);
            Debug.Log($"[PitFill] {go.name} took {totalDamagePercent:F1}% fall damage instantly");

            // APPLIQUER LES DEGATS INSTANTANEMENT
            interactable.TakePitDamage(totalDamagePercent, PitDamageType.Fall);
            Debug.Log(string.Format("[PitFill] {0} took {1:F1}% fall damage instantly", go.name, totalDamagePercent));

            // NOUVEAU : Delai avant destruction pour voir la healthbar se vider
            if (!data.interactable.CanTakePitDamage())
            {
                // Zombie mort : lancer coroutine de destruction avec delai
                float displayDelay = 2f; // Parametrable si tu veux
                StartCoroutine(DelayedDestroyCoroutine(go, data, displayDelay));
            }
            else
            {
                // Zombie survit : destruction normale
                CheckAndDestroyIfDead(data, pitFill.fillType);
            }

            CheckAndDestroyIfDead(data, pitFill.fillType);
        }
    }
    private IEnumerator DelayedDestroyCoroutine(GameObject go, FillEntityData data, float delay)
    {
        Debug.Log(string.Format("[PitFill] {0} will be destroyed in {1}s (Empty pit fall death)", go.name, delay));

        // SAUVEGARDER les references AVANT le delai (au cas ou le GameObject est detruit)
        EnemyHealth enemyHealth = go.GetComponent<EnemyHealth>();
        EnemyHealthBarUI healthBarUI = enemyHealth != null ? enemyHealth.healthBarUI : null;
        Transform enemyTransform = go.transform;

        yield return new WaitForSeconds(delay);

        // Unregister la healthbar (meme si le GameObject est null maintenant)
        if (healthBarUI != null)
        {
            EnemyHealthBarManager manager = FindObjectOfType<EnemyHealthBarManager>();
            if (manager != null)
            {
                manager.UnregisterEnemy(enemyTransform);
                Debug.Log(string.Format("[PitFill] Unregistered healthbar after delay"));
            }
        }

        if (go != null)
        {
            Debug.Log(string.Format("[PitFill] Destroying {0} after fall death delay", go.name));
            EnemyHealth eh = go.GetComponent<EnemyHealth>();
            if (eh == null || eh.destroyOnDeath)
                Destroy(go);
        }

        // Cleanup dict
        if (entitiesInFill.ContainsKey(go))
        {
            entitiesInFill.Remove(go);
        }
    }
    private IEnumerator ProgressiveFallDamageCoroutine(GameObject go, FillEntityData data, float totalDamagePercent)
    {
        // Durée de la mort progressive (ajustable)
        float duration = 1.0f; // 1 seconde pour voir la barre descendre
        float elapsed = 0f;
        float ticksNeeded = duration / damageTickRate;
        float damagePercentPerTick = totalDamagePercent / ticksNeeded;

        if (showDebugLogs)
            Debug.Log(string.Format("[PitFill] {0} progressive fall damage: {1:F1}% every {2}s for {3}s",
                go.name, damagePercentPerTick, damageTickRate, duration));

        while (elapsed < duration)
        {
            if (go == null || data.interactable == null)
            {
                yield break;
            }

            if (!data.interactable.CanTakePitDamage())
            {
                CheckAndDestroyIfDead(data, pitFill.fillType);
                yield break;
            }

            // Appliquer les dégâts progressifs
            data.interactable.TakePitDamage(damagePercentPerTick, PitDamageType.Fall);

            if (!data.interactable.CanTakePitDamage())
            {
                CheckAndDestroyIfDead(data, pitFill.fillType);
                yield break;
            }

            yield return new WaitForSeconds(damageTickRate);
            elapsed += damageTickRate;
        }

        /*// Force kill si toujours vivant
        if (go != null && data.interactable != null && data.interactable.CanTakePitDamage())
        {
            data.interactable.TakePitDamage(100f, PitDamageType.Fall);
            CheckAndDestroyIfDead(data, pitFill.fillType);
        }

        if (activeDeathCoroutines.ContainsKey(go))
        {
            activeDeathCoroutines.Remove(go);
        }*/
    }

    public class FillEntityData
    {
        public IPitInteractable interactable;
        public Vector3 entryPosition;
        public float entryTime;
        public bool isFalling;
        public float fallHeight;
        public float originalLinearDamping; // NOUVEAU

        public FillEntityData(IPitInteractable e, Vector3 entryPos)
        {
            interactable = e;
            entryPosition = entryPos;
            entryTime = Time.time;
            isFalling = false;
            fallHeight = 0f;
            originalLinearDamping = 0f; // NOUVEAU
        }
    }
}