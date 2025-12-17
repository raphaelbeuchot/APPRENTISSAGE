using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine.Samples;
using UnityEngine;
using Pathfinding;

public class GameManager : MonoBehaviour
{
    [Header("Cycle Manager")]
    public SentinelCycleManager sentinelCycleManager;
    [Header("Sentinel Settings")]
    public SentinelSettings sentinelSettings;

    [Header("References")]
    public PlayerPhysicsMovement player;
    public SentinelSettings sentinel;
    public PlayerHealth playerHealth;

    public Renderer sentinelLightRenderer;
    public Material greenMaterial;
    public Material redMaterial;
    public Material yellowMaterial;

    [Header("Sentinel Eye")]
    public Transform sentinelEye;

    [Header("Audio")]
    private AudioSource audioSource;

    [Header("Start System")]
    public bool waitForStart = true;
    public bool stunBySentinel = false;
    public bool zombieStunBySentinel = false;

    
    private bool playerAlarmTriggered = false;
    private HashSet<GameObject> alreadyShot = new HashSet<GameObject>();
    private float detectionTimer = 0f;

    // Système de raycasts et LOS
    private Dictionary<GameObject, TargetTrackingData> trackedTargets = new Dictionary<GameObject, TargetTrackingData>();

    // Systeme anti-tirs simultanes
    private List<float> scheduledShotTimes = new List<float>();
    private const float SHOT_SPACING_WINDOW = 0.07f;

    private class TargetTrackingData
    {
        public bool wasInLOS;
        public float lostLOSTime;
        public float reacquiredTime;
        public bool canShoot;

        // Variables pour securiser les tirs
        public bool isBeingShot = false;
        public float lastShotTime = -999f;

        // Compteur de scans consecutifs avec LOS
        public int consecutiveLOSScans = 0;

        // NOUVEAU : Headshot system
        public bool isHeadshot = false;
        public Vector3 lastKnownPosition;
        public bool wasInHeadshotMode = false;

        // NOUVEAU : Stand-up grace period
        public bool isInStandUpGracePeriod = false;

        public bool wasPlayerCrouched = false;


    }

    // ============================================
    // HELPER METHODS - TARGET POSITIONING
    // ============================================

    Vector3 GetTargetCenter(Collider col)
    {
        // Utilise le centre du bounds du collider (s'adapte automatiquement au crouch)
        return col.bounds.center;
    }

    Vector3 GetTargetCenter(GameObject obj)
    {
        Collider col = obj.GetComponent<Collider>();
        if (col != null)
            return col.bounds.center;

        // Fallback si pas de collider
        return obj.transform.position + Vector3.up * 1f;
    }

    Vector3 GetHeadPosition(Collider col)
    {
        // Tête = 90% de la hauteur totale du collider
        float headHeight = col.bounds.min.y + (col.bounds.size.y * 0.9f);
        return new Vector3(col.bounds.center.x, headHeight, col.bounds.center.z);
    }

    Vector3 GetHeadPosition(GameObject obj)
    {
        Collider col = obj.GetComponent<Collider>();
        if (col != null)
            return GetHeadPosition(col);

        // Fallback
        return obj.transform.position + Vector3.up * 1.8f;
    }

    float GetSafeShootTime(float baseRandomDelay)
    {
        // Nettoyer les tirs passes
        scheduledShotTimes.RemoveAll(t => t < Time.time);

        float proposedTime = Time.time + sentinelSettings.shootDelay + baseRandomDelay;

        // Checker si un tir existe deja proche de ce timing
        while (scheduledShotTimes.Exists(t => Mathf.Abs(t - proposedTime) < SHOT_SPACING_WINDOW))
        {
            proposedTime += SHOT_SPACING_WINDOW;
        }

        scheduledShotTimes.Add(proposedTime);

        // Retourner le delai final ajuste
        return proposedTime - Time.time - sentinelSettings.shootDelay;
    }

    // ============================================
    // UNITY LIFECYCLE
    // ============================================

    void Start()
    {
        if (sentinelSettings == null)
        {
            Debug.LogError("GameManager: SentinelSettings non assigné!");
            return;
        }

        if (player == null)
            Debug.LogError("GameManager: PlayerPhysicsMovement non assigné!");

        if (playerHealth == null)
        {
            playerHealth = player != null ? player.GetComponent<PlayerHealth>() : null;
            if (playerHealth == null)
                Debug.LogError("GameManager: PlayerHealth non trouvé sur le joueur!");
        }

        if (sentinelCycleManager == null)
        {
            Debug.LogError("GameManager: SentinelCycleManager non assigné!");
        }

        audioSource = GetComponent<AudioSource>();
        Time.timeScale = 1f;
    }

    void Update()
    {
        if (sentinelCycleManager == null || !sentinelCycleManager.IsGameStarted() || sentinelSettings == null)
            return;

        // Détection uniquement en RedLight
        if (sentinelCycleManager.IsInRedLight())
        {
            detectionTimer += Time.deltaTime;
            if (detectionTimer >= sentinelSettings.redlightScanInterval)
            {
                detectionTimer = 0f;
                CheckForMovingTargetsWithRaycast();
            }
        }
    }

    // ============================================
    // SYSTÈME DE DÉTECTION AVEC RAYCASTS
    // ============================================

    void CheckForMovingTargetsWithRaycast()
    {
        Vector3 eyePosition = (sentinelEye != null) ? sentinelEye.position : transform.position;
        Vector3 sentinelPos = eyePosition + sentinelSettings.raycastOffset;

        Collider[] targets = Physics.OverlapSphere(sentinelPos, sentinelSettings.detectionRadius, sentinelSettings.targetLayers);

        // Réinitialiser la détection
        foreach (var kvp in trackedTargets)
        {
            EnemyAI_AStar ai = kvp.Key != null ? kvp.Key.GetComponent<EnemyAI_AStar>() : null;
            if (ai != null)
                ai.isDetectedBySentinel = false;
        }

        foreach (Collider col in targets)
        {
            if (!trackedTargets.ContainsKey(col.gameObject))
                trackedTargets[col.gameObject] = new TargetTrackingData();

            TargetTrackingData trackData = trackedTargets[col.gameObject];

            EnemyHealth enemyHealth = col.GetComponent<EnemyHealth>();
            if (enemyHealth != null && enemyHealth.IsRecovering()) continue;

            GrabAttack grabSystem = col.GetComponent<GrabAttack>();
            bool isInBourrade = grabSystem != null && grabSystem.isInBourradeDuration;
            bool isFakeGrabber = grabSystem != null && grabSystem.isFakeGrabbing;

            Vector3 targetPos = GetTargetCenter(col);
            Vector3 direction = (targetPos - sentinelPos).normalized;
            float distance = Vector3.Distance(sentinelPos, targetPos);

            // NOUVEAU SYSTÈME DE LOS AVEC HEADSHOT
            RaycastHit hit;
            bool hasLOS = true;
            bool isHeadshot = false;
            Vector3 finalTargetPos = targetPos;

            // Raycast 1 : vers centre
            if (Physics.Raycast(sentinelPos, direction, out hit, distance, sentinelSettings.obstacleLayers))
            {
                // Centre caché, vérifier si la tête dépasse
                Vector3 headPos = GetHeadPosition(col);
                Vector3 directionToHead = (headPos - sentinelPos).normalized;
                float distanceToHead = Vector3.Distance(sentinelPos, headPos);

                RaycastHit headHit;
                if (Physics.Raycast(sentinelPos, directionToHead, out headHit, distanceToHead, sentinelSettings.obstacleLayers))
                {
                    // Tête aussi cachée : vraiment safe
                    hasLOS = false;
                    Debug.DrawLine(sentinelPos, hit.point, Color.red, 0.2f);
                    Debug.DrawLine(sentinelPos, headHit.point, Color.red, 0.1f);
                }
                else
                {
                    // TÊTE VISIBLE = HEADSHOT !
                    hasLOS = true;
                    isHeadshot = true;
                    finalTargetPos = headPos;
                    Debug.DrawLine(sentinelPos, headPos, Color.yellow, 0.2f);
                    Debug.Log($"[HEADSHOT OPPORTUNITY] {col.name} - tête visible à {headPos.y}m");
                }
            }
            else
            {
                // Centre visible : tir normal
                Debug.DrawLine(sentinelPos, targetPos, Color.green, 0.2f);
            }

            trackData.canShoot = hasLOS;
            trackData.isHeadshot = isHeadshot;
            trackData.lastKnownPosition = finalTargetPos;

            // Check état crouch du player
            bool isPlayerCrouched = false;
            if (col.gameObject == player.gameObject)
            {
                isPlayerCrouched = player.IsCrouching();
            }

            // NOUVEAU : Detection STAND UP depuis CROUCH caché
            if (hasLOS && !trackData.wasInLOS && trackData.wasPlayerCrouched && !isPlayerCrouched && !trackData.isBeingShot && !trackData.isInStandUpGracePeriod)
            {
                // Le joueur vient de se lever depuis une position crouch cachée
                Debug.Log($"[STAND UP FROM CROUCH] {col.name} - grace period starts!");

                trackData.isInStandUpGracePeriod = true;
                trackData.isBeingShot = true;

                PlayerHealth humanHealth = col.GetComponent<PlayerHealth>();

                if (enemyHealth != null && !enemyHealth.IsDead())
                {
                    alreadyShot.Add(col.gameObject);
                    StartCoroutine(ShootTargetAfterStandUpGrace(col.gameObject, enemyHealth, null, sentinelPos, finalTargetPos, trackData, isHeadshot));
                }
                else if (humanHealth != null && !humanHealth.IsDead())
                {
                    alreadyShot.Add(col.gameObject);
                    if (col.gameObject == player.gameObject)
                    {
                        playerAlarmTriggered = true;
                        StartCoroutine(ShootTargetAfterStandUpGrace(col.gameObject, null, humanHealth, sentinelPos, finalTargetPos, trackData, isHeadshot));
                    }
                }

                // Update état pour prochain scan
                trackData.wasPlayerCrouched = isPlayerCrouched;

                continue;
            }

            // Update état crouch pour prochain scan (si pas en stand up grace)
            trackData.wasPlayerCrouched = isPlayerCrouched;

            // Grace period avec RICOCHET
                if (!hasLOS && trackData.wasInHeadshotMode && !trackData.isBeingShot)
            {
                // Le joueur était en headshot mode et est devenu invisible (a crouch)
                //  Tir immédiat qui va automatiquement ricochet
                Debug.Log($"[GRACE PERIOD RICOCHET] {col.name} crouched from headshot!");

                PlayerHealth humanHealth = col.GetComponent<PlayerHealth>();

                if (enemyHealth != null && !enemyHealth.IsDead())
                {
                    alreadyShot.Add(col.gameObject);
                    string reason = "CROUCH ESCAPE";
                    // Utiliser la coroutine existante avec délai 0.1s
                    StartCoroutine(ShootEnemyWithDelay(col.gameObject, enemyHealth, reason, sentinelPos, GetTargetCenter(col), trackData, 0.1f));
                }
                else if (humanHealth != null && !humanHealth.IsDead())
                {
                    alreadyShot.Add(col.gameObject);
                    string reason = "CROUCH ESCAPE";
                    if (col.gameObject == player.gameObject)
                    {
                        playerAlarmTriggered = true;
                        // Utiliser la coroutine existante
                        StartCoroutine(ShootPlayerWithAlarm(col.gameObject, humanHealth, reason, sentinelPos, GetTargetCenter(col), trackData));
                    }
                }

                // Reset tracking
                trackData.consecutiveLOSScans = 0;
                trackData.wasInLOS = false;
                trackData.wasInHeadshotMode = false;

                continue;
            }

            // Update tracking state pour prochain scan
            trackData.wasInLOS = hasLOS;
            trackData.wasInHeadshotMode = isHeadshot;  // NOUVEAU

            EnemyAI_AStar ai = col.GetComponent<EnemyAI_AStar>();
            if (ai != null && hasLOS)
                ai.isDetectedBySentinel = true;

            bool playerImmune = (col.gameObject == player.gameObject && player.grabState == PlayerPhysicsMovement.GrabState.Grabbed);

            //Punition si meleeattack, spray
            Rigidbody rb = col.GetComponent<Rigidbody>();
            MeleeAttackSystem meleeSystem = col.GetComponent<MeleeAttackSystem>();
            bool isAttacking = meleeSystem != null && meleeSystem.IsAttacking();

            // AJOUT : Check broom attack aussi
            BroomAttackSystem broomSystem = col.GetComponent<BroomAttackSystem>();
            bool isBroomAttacking = broomSystem != null && broomSystem.IsAttacking();

            // Check climb
            TestClimbDetection climbSystem = col.GetComponent<TestClimbDetection>();
            bool isClimbing = climbSystem != null && climbSystem.IsClimbing();

            // NOUVEAU : Check sortie de pit
            PlayerPitInteractable pitInteractable = col.GetComponent<PlayerPitInteractable>();
            bool isClimbingOutOfPit = pitInteractable != null && pitInteractable.IsClimbingOut();

            bool isMoving = false;
            if (rb != null)
            {
                // NOUVEAU : Check pit mode en premier
                EnemyAI_AStar zombieAI = col.GetComponent<EnemyAI_AStar>();
                bool isInPitMode = zombieAI != null && zombieAI.isInPitMode;

                if (isInPitMode)
                {
                    // En pit mode : utiliser directement rb.linearVelocity
                    float effectiveThreshold = sentinelSettings.movementThreshold;

                    EnemyPitInteractable enemyPit = col.GetComponent<EnemyPitInteractable>();
                    if (enemyPit != null && enemyPit.isInShallowWater)
                    {
                        effectiveThreshold *= enemyPit.waterSlowdownMultiplier;
                    }

                    isMoving = rb.linearVelocity.magnitude > effectiveThreshold;
                    Debug.Log($"[PIT MODE] {col.name} velocity={rb.linearVelocity.magnitude}, threshold={effectiveThreshold}, moving={isMoving}");
                }
                else
                {
                    // Mode normal : essayer A path
                    Pathfinding.AIPath aiPath = col.GetComponent<Pathfinding.AIPath>();
                    if (aiPath != null && aiPath.enabled && aiPath.canMove)
                    {
                        float effectiveThreshold = sentinelSettings.movementThreshold;

                        EnemyPitInteractable enemyPit = col.GetComponent<EnemyPitInteractable>();
                        if (enemyPit != null && enemyPit.isInShallowWater)
                        {
                            effectiveThreshold *= enemyPit.waterSlowdownMultiplier;
                        }

                        isMoving = aiPath.velocity.magnitude > effectiveThreshold;
                    }
                    
                        
                        else
                        {
                            // Dernier fallback : Rigidbody
                            float effectiveThreshold = sentinelSettings.movementThreshold;
                            isMoving = rb.linearVelocity.magnitude > effectiveThreshold;
                        }
                    
                }
            }

            bool shouldBeShot = (isMoving || isAttacking || isBroomAttacking || isInBourrade || isFakeGrabber || isClimbing || isClimbingOutOfPit) && !playerImmune;
            EnemyPitInteractable pitInt = col.GetComponent<EnemyPitInteractable>();
            if (pitInt != null && pitInt.isInShallowWater)
            {
                Debug.Log($"[WATER] {col.name} - isMoving: {isMoving}, hasLOS: {hasLOS}, shouldShoot: {shouldBeShot}");
            }

            // TIR SUR LA CIBLE SI ELLE EST EN MOUVEMENT ET VISIBLE
            if (shouldBeShot && hasLOS && !trackData.isBeingShot && Time.time - trackData.lastShotTime >= sentinelSettings.shootCooldown)
            {
                bool needsExposureDelay = !trackData.wasInLOS;

                if (needsExposureDelay)
                {
                    trackData.consecutiveLOSScans++;

                    if (trackData.consecutiveLOSScans >= sentinelSettings.minimumExposureScans)
                    {
                        trackData.isBeingShot = true;
                        trackData.lastShotTime = Time.time;

                        PlayerHealth humanHealth = col.GetComponent<PlayerHealth>();

                        if (enemyHealth != null && !enemyHealth.IsDead())
                        {
                            alreadyShot.Add(col.gameObject);
                            string reason = isAttacking ? "ATTAQUE" : (isInBourrade ? "BOURRADE" : "MOUVEMENT");
                            float randomOffset = GetSafeShootTime(Random.Range(0.1f, 0.4f));
                            StartCoroutine(ShootEnemyWithDelay(col.gameObject, enemyHealth, reason, sentinelPos, finalTargetPos, trackData, randomOffset));
                        }
                        else if (humanHealth != null && !humanHealth.IsDead())
                        {
                            alreadyShot.Add(col.gameObject);
                            string reason = isAttacking ? "ATTAQUE" : "MOUVEMENT";
                            if (col.gameObject == player.gameObject && !playerAlarmTriggered)
                            {
                                playerAlarmTriggered = true;
                                StartCoroutine(ShootPlayerWithAlarm(col.gameObject, humanHealth, reason, sentinelPos, finalTargetPos, trackData));
                            }
                        }
                    }
                }
                else
                {
                    trackData.isBeingShot = true;
                    trackData.lastShotTime = Time.time;

                    PlayerHealth humanHealth = col.GetComponent<PlayerHealth>();

                    if (enemyHealth != null && !enemyHealth.IsDead())
                    {
                        alreadyShot.Add(col.gameObject);
                        string reason = isAttacking ? "ATTAQUE" : (isInBourrade ? "BOURRADE" : "MOUVEMENT");
                        float randomOffset = GetSafeShootTime(Random.Range(0.1f, 0.4f));
                        StartCoroutine(ShootEnemyWithDelay(col.gameObject, enemyHealth, reason, sentinelPos, finalTargetPos, trackData, randomOffset));
                    }
                    else if (humanHealth != null && !humanHealth.IsDead())
                    {
                        alreadyShot.Add(col.gameObject);
                        string reason = isAttacking ? "ATTAQUE" : "MOUVEMENT";
                        if (col.gameObject == player.gameObject && !playerAlarmTriggered)
                        {
                            playerAlarmTriggered = true;
                            StartCoroutine(ShootPlayerWithAlarm(col.gameObject, humanHealth, reason, sentinelPos, finalTargetPos, trackData));
                        }
                    }
                }
            }
            else
            {
                trackData.consecutiveLOSScans = 0;
            }
        }
    }

    // ============================================
    // LASER TEMPORAIRE
    // ============================================

    IEnumerator ShowShootLaser(Vector3 from, Vector3 to, float duration)
    {
        if (!sentinelSettings.showLasers) yield break;

        GameObject laserObj = new GameObject("ShootLaser_Temp");
        laserObj.transform.SetParent(transform);

        LineRenderer lr = laserObj.AddComponent<LineRenderer>();
        lr.startWidth = sentinelSettings.laserWidth;
        lr.endWidth = sentinelSettings.laserWidth;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = sentinelSettings.laserColor;
        lr.endColor = sentinelSettings.laserColor;
        lr.positionCount = 2;
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - (elapsed / duration);
            Color col = sentinelSettings.laserColor;
            col.a = alpha;
            lr.startColor = col;
            lr.endColor = col;
            yield return null;
        }

        Destroy(laserObj);
    }

    // ============================================
    // COROUTINES DE TIR SÉCURISÉES
    // ============================================

    IEnumerator ShootEnemyWithDelay(GameObject enemy, EnemyHealth enemyHealth, string reason, Vector3 sentinelPos, Vector3 targetPos, TargetTrackingData trackData, float extraDelay = 0f)
    {
        yield return new WaitForSeconds(sentinelSettings.shootDelay + extraDelay);

        if (enemyHealth != null && !enemyHealth.IsDead())
        {
            // LIGNE MODIFIEE : Viser la tête si headshot, sinon le centre
            Vector3 currentEnemyPos = trackData.isHeadshot ? GetHeadPosition(enemy) : GetTargetCenter(enemy);

            Vector3 eyePosition = (sentinelEye != null) ? sentinelEye.position : transform.position;
            Vector3 actualSentinelPos = eyePosition + sentinelSettings.raycastOffset;
            Vector3 direction = (currentEnemyPos - actualSentinelPos).normalized;
            float distance = Vector3.Distance(actualSentinelPos, currentEnemyPos);

            RaycastHit hit;
            bool hasLOS = true;

            if (Physics.Raycast(actualSentinelPos, direction, out hit, distance, sentinelSettings.obstacleLayers))
            {
                hasLOS = false;

                Debug.Log($"RICOCHET! {enemy.name} s'est cache, tir touche {hit.collider.name}");

                if (audioSource != null && sentinelSettings.ricochetSound != null)
                    audioSource.PlayOneShot(sentinelSettings.ricochetSound);

                if (sentinelSettings.ricochetVFX != null)
                {
                    GameObject vfx = Instantiate(sentinelSettings.ricochetVFX, hit.point, Quaternion.LookRotation(hit.normal));
                    Destroy(vfx, sentinelSettings.ricochetVFXDuration);
                }

                StartCoroutine(ShowShootLaser(actualSentinelPos, hit.point, sentinelSettings.shootLaserFadeDuration));

                trackData.isBeingShot = false;
                trackData.lastShotTime = Time.time;
                alreadyShot.Remove(enemy);

                yield break;
            }

            ShootEnemy(enemy, enemyHealth, reason, actualSentinelPos, currentEnemyPos, trackData.isHeadshot);
            trackData.isBeingShot = false;
            trackData.lastShotTime = Time.time;
        }
    }
    IEnumerator ShootPlayerWithAlarm(GameObject playerObject, PlayerHealth humanHealth, string reason, Vector3 sentinelPos, Vector3 targetPos, TargetTrackingData trackData)
    {
        yield return new WaitForSeconds(sentinelSettings.shootDelay);

        if (humanHealth != null && !humanHealth.IsDead())
        {
            // LIGNE MODIFIEE : Viser la tête si headshot, sinon le centre
            Vector3 currentPlayerPos = trackData.isHeadshot ? GetHeadPosition(playerObject) : GetTargetCenter(playerObject);

            Vector3 eyePosition = (sentinelEye != null) ? sentinelEye.position : transform.position;
            Vector3 actualSentinelPos = eyePosition + sentinelSettings.raycastOffset;
            Vector3 direction = (currentPlayerPos - actualSentinelPos).normalized;
            float distance = Vector3.Distance(actualSentinelPos, currentPlayerPos);

            RaycastHit hit;
            bool hasLOS = true;

            if (Physics.Raycast(actualSentinelPos, direction, out hit, distance, sentinelSettings.obstacleLayers))
            {
                hasLOS = false;

                Debug.Log($"RICOCHET! Player s'est cache, tir touche {hit.collider.name}");

                if (audioSource != null && sentinelSettings.ricochetSound != null)
                    audioSource.PlayOneShot(sentinelSettings.ricochetSound);

                if (sentinelSettings.ricochetVFX != null)
                {
                    GameObject vfx = Instantiate(sentinelSettings.ricochetVFX, hit.point, Quaternion.LookRotation(hit.normal));
                    Destroy(vfx, sentinelSettings.ricochetVFXDuration);
                }

                StartCoroutine(ShowShootLaser(actualSentinelPos, hit.point, sentinelSettings.shootLaserFadeDuration));

                trackData.isBeingShot = false;
                trackData.lastShotTime = Time.time;
                playerAlarmTriggered = false;
                alreadyShot.Remove(playerObject);

                yield break;
            }

            ShootPlayer(playerObject, humanHealth, reason, actualSentinelPos, currentPlayerPos, trackData.isHeadshot);
            trackData.isBeingShot = false;
            trackData.lastShotTime = Time.time;
        }
    }

    void ShootEnemy(GameObject enemy, EnemyHealth enemyHealth, string reason, Vector3 sentinelPos, Vector3 targetPos, bool isHeadshot = false)
    {
        string headshotTag = isHeadshot ? " [HEADSHOT]" : "";
        Debug.Log("BANG! " + enemy.name + " (" + reason + ")" + headshotTag);

        if (audioSource != null && sentinelSettings.shootSound != null)
            audioSource.PlayOneShot(sentinelSettings.shootSound);

        Vector3 currentTargetPos = GetTargetCenter(enemy);

        EnemyAI_AStar ai = enemy.GetComponent<EnemyAI_AStar>();
        if (ai != null)
        {
            StartCoroutine(StunSpecificZombie(ai));
        }

        SentinelTarget sentinelTarget = enemy.GetComponent<SentinelTarget>();
        if (sentinelTarget != null) sentinelTarget.FlashWhite();

        StartCoroutine(ShowShootLaser(sentinelPos, currentTargetPos, sentinelSettings.shootLaserFadeDuration));

        enemyHealth.TakeSentinelShot(isHeadshot);

        if (enemyHealth.IsDead())
            Debug.Log(enemy.name + " MORT!");
    }

    IEnumerator StunSpecificZombie(EnemyAI_AStar ai)
    {
        ai.isStunnedBySentinel = true;
        yield return new WaitForSeconds(sentinel.stunZombieDuration);
        ai.isStunnedBySentinel = false;

        alreadyShot.Remove(ai.gameObject);
    }

    void ShootPlayer(GameObject human, PlayerHealth humanHealth, string reason, Vector3 sentinelPos, Vector3 targetPos, bool isHeadshot = false)
    {
        string headshotTag = isHeadshot ? " [HEADSHOT]" : "";
        Debug.Log("BANG! " + human.name + " (" + reason + ")" + headshotTag);

        if (audioSource != null && sentinelSettings.shootSound != null)
            audioSource.PlayOneShot(sentinelSettings.shootSound);

        Vector3 currentTargetPos = GetTargetCenter(human);

        SentinelTarget sentinelTarget = human.GetComponent<SentinelTarget>();
        if (sentinelTarget != null) sentinelTarget.FlashWhite();

        StartCoroutine(ShowShootLaser(sentinelPos, currentTargetPos, sentinelSettings.shootLaserFadeDuration));

        StartCoroutine(PlayerStunBySentinel());

        // Headshot sur player = double degats ou instakill selon ton choix
        if (isHeadshot)
        {
            humanHealth.TakeSentinelShot(); // Tu peux doubler les degats ici si tu veux
        }
        humanHealth.TakeSentinelShot();
    }

    IEnumerator PlayerStunBySentinel()
    {
        stunBySentinel = true;
        yield return new WaitForSeconds(sentinel.stunDuration);
        stunBySentinel = false;

        if (player != null)
        {
            alreadyShot.Remove(player.gameObject);
            playerAlarmTriggered = false;
            Debug.Log("Player recovery complete - can be shot again if moves");
        }
    }

    public IEnumerator ShootPlayerAtEndOfRecoil(GameObject playerObject, PlayerHealth humanHealth, Vector3 sentinelPos, Vector3 targetPos)
    {
        if (humanHealth != null && !humanHealth.IsDead())
        {
            Vector3 currentPos = GetTargetCenter(playerObject);
            StartCoroutine(ShowShootLaser(sentinelPos, currentPos, sentinelSettings.shootLaserFadeDuration));

            Debug.Log("BANG! Player shot at end of recoil");

            if (audioSource != null && sentinelSettings.shootSound != null)
                audioSource.PlayOneShot(sentinelSettings.shootSound);

            SentinelTarget sentinelTarget = playerObject.GetComponent<SentinelTarget>();
            if (sentinelTarget != null) sentinelTarget.FlashWhite();

            StartCoroutine(ShowShootLaser(sentinelPos, targetPos, sentinelSettings.shootLaserFadeDuration));

            StartCoroutine(PlayerStunBySentinel());
            humanHealth.TakeSentinelShot();
        }

        yield return null;
    }

    // ============================================
    // CYCLES DE JEU
    // ============================================

    
    

    

    public void RemoveFromAlreadyShot(GameObject target)
    {
        alreadyShot.Remove(target);
    }

    public void StartGameCycle()
    {
        if (sentinelCycleManager != null)
        {
            sentinelCycleManager.StartGameCycle();
        }
    }

    public bool IsInRedLight()
    {
        return sentinelCycleManager != null && sentinelCycleManager.IsInRedLight();
    }
    IEnumerator ShootTargetAfterStandUpGrace(GameObject target, EnemyHealth enemyHealth, PlayerHealth playerHealth, Vector3 sentinelPos, Vector3 targetPos, TargetTrackingData trackData, bool wasHeadshot)
    {
        // Attendre la grace period
        yield return new WaitForSeconds(sentinelSettings.shootDelay);

        // Revérifier le LOS au moment du tir
        Vector3 eyePosition = (sentinelEye != null) ? sentinelEye.position : transform.position;
        Vector3 actualSentinelPos = eyePosition + sentinelSettings.raycastOffset;

        Vector3 currentTargetPos = wasHeadshot ? GetHeadPosition(target) : GetTargetCenter(target);
        Vector3 direction = (currentTargetPos - actualSentinelPos).normalized;
        float distance = Vector3.Distance(actualSentinelPos, currentTargetPos);

        RaycastHit hit;
        bool stillHasLOS = !Physics.Raycast(actualSentinelPos, direction, out hit, distance, sentinelSettings.obstacleLayers);

        if (!stillHasLOS)
        {
            // RICOCHET : le joueur s'est rebaissé à temps !
            Debug.Log($"[STAND UP GRACE SUCCESS] {target.name} crouched back in time! RICOCHET on {hit.collider.name}");

            if (audioSource != null && sentinelSettings.ricochetSound != null)
                audioSource.PlayOneShot(sentinelSettings.ricochetSound);

            if (sentinelSettings.ricochetVFX != null)
            {
                GameObject vfx = Instantiate(sentinelSettings.ricochetVFX, hit.point, Quaternion.LookRotation(hit.normal));
                Destroy(vfx, sentinelSettings.ricochetVFXDuration);
            }

            StartCoroutine(ShowShootLaser(actualSentinelPos, hit.point, sentinelSettings.shootLaserFadeDuration));

            trackData.isBeingShot = false;
            trackData.isInStandUpGracePeriod = false;
            trackData.lastShotTime = Time.time;
            alreadyShot.Remove(target);
            if (target == player.gameObject) playerAlarmTriggered = false;

            yield break;
        }

        // TIR REUSSI : le joueur n'a pas crouché assez vite
        Debug.Log($"[STAND UP GRACE FAILED] {target.name} too slow! {(wasHeadshot ? "HEADSHOT" : "HIT")}");

        if (enemyHealth != null && !enemyHealth.IsDead())
        {
            ShootEnemy(target, enemyHealth, "STAND UP TOO SLOW", actualSentinelPos, currentTargetPos, wasHeadshot);
        }
        else if (playerHealth != null && !playerHealth.IsDead())
        {
            ShootPlayer(target, playerHealth, "STAND UP TOO SLOW", actualSentinelPos, currentTargetPos, wasHeadshot);
        }

        trackData.isBeingShot = false;
        trackData.isInStandUpGracePeriod = false;
        trackData.lastShotTime = Time.time;
    }

    public void ResetAllTracking()
    {
        foreach (var kvp in trackedTargets)
        {
            kvp.Value.wasInLOS = false;
            kvp.Value.wasPlayerCrouched = false;
            kvp.Value.wasInHeadshotMode = false;
            kvp.Value.consecutiveLOSScans = 0;
        }
        Debug.Log("[TRACKING RESET] All tracking data cleared for new cycle");
    }
}