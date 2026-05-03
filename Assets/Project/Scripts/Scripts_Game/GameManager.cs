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
    public PlayerDetectionFeedback playerDetectionFeedback;
    
    [Header("Laser Manager")]
    [SerializeField] private SentinelLaserManager laserManager;


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

    [Header("Enemy Tracking")]
    public int totalEnemies = 0;
    public int enemiesKilled = 0;


    private bool playerAlarmTriggered = false;
    private HashSet<GameObject> alreadyShot = new HashSet<GameObject>();
    private float detectionTimer = 0f;

    // Systeme de raycasts et LOS
    private Dictionary<GameObject, TargetTrackingData> trackedTargets = new Dictionary<GameObject, TargetTrackingData>();

    // Systeme anti-tirs simultanes
    private List<float> scheduledShotTimes = new List<float>();
    private const float SHOT_SPACING_WINDOW = 0.05f;
    private float lastActualShotTime = -999f;

    private class TargetTrackingData
    {
        public bool wasInLOS;
        public float lostLOSTime;
        public float reacquiredTime;
        public bool canShoot;
        public bool scheduledDuringWindup = false;


        // Variables pour securiser les tirs
        public bool isBeingShot = false;
        public float lastShotTime = -999f;
        public float shootScheduledTime = -1f;

        // Compteur de scans consecutifs avec LOS
        public int consecutiveLOSScans = 0;

        // Headshot system
        public bool isHeadshot = false;
        public Vector3 lastKnownPosition;

        public bool wasPlayerCrouched = false;

        // NOUVEAU : Detection changement crouch
        public bool crouchStateChangeInProgress = false;
        public float crouchStateChangeScheduledTime = -1f;

        // NOUVEAU : Flag premier scan
        public bool hasBeenTrackedBefore = false;

        // NOUVEAU : Detection mouvement sur plateforme
        public Vector3 lastCheckPosition;
        public float lastCheckTime;
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
        // Tete = 90% de la hauteur totale du collider
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
            return;
        }

         if (playerHealth == null)
        {
            playerHealth = player != null ? player.GetComponent<PlayerHealth>() : null;
            
        }

       

        audioSource = GetComponent<AudioSource>();
        Time.timeScale = 1f;

        // Compter les ennemis au demarrage (SAUF BrightEyes)
        CountEnemiesAtStart();
    }

    void Update()
    {
        if (sentinelCycleManager == null || !sentinelCycleManager.IsGameStarted() || sentinelSettings == null)
            return;

        // Detection uniquement en RedLight
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
    // SYSTEME DE DETECTION AVEC RAYCASTS
    // ============================================

    void CheckForMovingTargetsWithRaycast()
    {
        float movementThreshold = sentinelSettings.movementThreshold * (ModifierApplier.Instance != null ? ModifierApplier.Instance.sentinelMovementThresholdMultiplier : 1f);
        Vector3 eyePosition = (sentinelEye != null) ? sentinelEye.position : transform.position;
        Vector3 sentinelPos = eyePosition + sentinelSettings.raycastOffset;

        Collider[] targets = Physics.OverlapSphere(sentinelPos, sentinelSettings.detectionRadius, sentinelSettings.targetLayers);

        foreach (var kvp in trackedTargets)
        {
            EnemyAI_AStar ai = kvp.Key != null ? kvp.Key.GetComponent<EnemyAI_AStar>() : null;
            if (ai != null)
                ai.isDetectedBySentinel = false;
        }

        foreach (Collider col in targets)
        {
            // Exclure les ennemis en cours de sequence sweep/standup
            EnemyAI_AStar sequenceGuard = col.GetComponent<EnemyAI_AStar>();
            if (sequenceGuard != null && (sequenceGuard.isKnockedDownByEpervier || sequenceGuard.isInStandupPhase))
                continue;
            if (!trackedTargets.ContainsKey(col.gameObject))
                trackedTargets[col.gameObject] = new TargetTrackingData();

            TargetTrackingData trackData = trackedTargets[col.gameObject];

            if (col.gameObject == player.gameObject && !trackData.hasBeenTrackedBefore)
            {
                trackData.wasPlayerCrouched = player.IsCrouching();
            }

            EnemyHealth enemyHealth = col.GetComponent<EnemyHealth>();
            if (enemyHealth != null && enemyHealth.IsRecovering()) continue;

            if (col.gameObject == player.gameObject && (player.IsSweeping() || player.IsGroggy() || player.IsGroggyStunned()))
                continue;

            EnemyAI_AStar stunnedCheck = col.GetComponent<EnemyAI_AStar>();
            if (stunnedCheck != null && stunnedCheck.isStunnedBySentinel) continue;

            GrabAttack grabSystem = col.GetComponent<GrabAttack>();
            bool isInBourrade = grabSystem != null && grabSystem.isInBourradeDuration;
            bool isFakeGrabber = grabSystem != null && grabSystem.isFakeGrabbing;

            HitAttack hitAttack = col.GetComponent<HitAttack>();
            bool isHitterWindingUp = hitAttack != null && hitAttack.isInWindup;
            bool isHitterAttacking = hitAttack != null && hitAttack.IsAttacking();

            Vector3 targetPos = GetTargetCenter(col);
            Vector3 direction = (targetPos - sentinelPos).normalized;
            float distance = Vector3.Distance(sentinelPos, targetPos);

            RaycastHit hit;
            bool hasLOS = true;
            bool isHeadshot = false;
            Vector3 finalTargetPos = targetPos;

            if (Physics.Raycast(sentinelPos, direction, out hit, distance, sentinelSettings.obstacleLayers)
    && hit.collider.gameObject != col.gameObject)
            {
                Vector3 headPos = GetHeadPosition(col);
                Vector3 directionToHead = (headPos - sentinelPos).normalized;
                float distanceToHead = Vector3.Distance(sentinelPos, headPos);

                RaycastHit headHit;
                if (Physics.Raycast(sentinelPos, directionToHead, out headHit, distanceToHead, sentinelSettings.obstacleLayers)
                    && headHit.collider.gameObject != col.gameObject)
                {
                    hasLOS = false;
                }
                else
                {
                    hasLOS = true;
                    isHeadshot = true;
                    finalTargetPos = headPos;
                }
            }

            trackData.canShoot = hasLOS;
            trackData.isHeadshot = isHeadshot;
            trackData.lastKnownPosition = finalTargetPos;

            bool isPlayerCrouched = false;
            if (col.gameObject == player.gameObject)
            {
                isPlayerCrouched = player.IsCrouching();
            }

            bool hasCrouchStateChanged = false;
            if (col.gameObject == player.gameObject && trackData.hasBeenTrackedBefore)
            {
                hasCrouchStateChanged = (trackData.wasPlayerCrouched != isPlayerCrouched);
            }

            if (hasCrouchStateChanged && !trackData.crouchStateChangeInProgress && !trackData.isBeingShot && trackData.wasInLOS)
            {
                Debug.Log($"[CROUCH STATE CHANGE] {col.name} - Timer 0.3s lance!");

                trackData.crouchStateChangeInProgress = true;
                trackData.crouchStateChangeScheduledTime = Time.time + 0.3f;

                PlayerHealth humanHealth = col.GetComponent<PlayerHealth>();

                if (enemyHealth != null && !enemyHealth.IsDead())
                {
                    alreadyShot.Add(col.gameObject);
                }
                else if (humanHealth != null && !humanHealth.IsDead())
                {
                    alreadyShot.Add(col.gameObject);
                    if (col.gameObject == player.gameObject)
                    {
                        playerAlarmTriggered = true;
                    }
                }
            }

            if (trackData.crouchStateChangeInProgress && !hasLOS)
            {
                Vector3 lastSeenPos = trackData.lastKnownPosition;
                Vector3 dirToLastSeen = (lastSeenPos - sentinelPos).normalized;

                RaycastHit obstacleHit;
                if (Physics.Raycast(sentinelPos, dirToLastSeen, out obstacleHit, 100f, sentinelSettings.obstacleLayers))
                {
                    int enemyLayer = LayerMask.NameToLayer("Enemy");
                    int zombieLayer = LayerMask.NameToLayer("Zombie");

                    if (obstacleHit.collider.gameObject.layer == enemyLayer || obstacleHit.collider.gameObject.layer == zombieLayer)
                    {
                        EnemyHealth coverEnemyHealth = obstacleHit.collider.GetComponent<EnemyHealth>();
                        if (coverEnemyHealth != null && !coverEnemyHealth.IsDead())
                        {
                            ShootEnemy(obstacleHit.collider.gameObject, coverEnemyHealth, "BOUCLIER CROUCH", sentinelPos, obstacleHit.point, trackData.isHeadshot);

                            trackData.crouchStateChangeInProgress = false;
                            trackData.crouchStateChangeScheduledTime = -1f;
                            trackData.consecutiveLOSScans = 0;
                            trackData.wasInLOS = false;
                            trackData.wasPlayerCrouched = isPlayerCrouched;
                            alreadyShot.Remove(col.gameObject);
                            if (col.gameObject == player.gameObject)
                                playerAlarmTriggered = false;
                            if (playerDetectionFeedback != null)
                                playerDetectionFeedback.OnNoLongerDetected();

                            continue;
                        }
                    }

                    if (audioSource != null && sentinelSettings.ricochetSound != null)
                        audioSource.PlayOneShot(sentinelSettings.ricochetSound);

                    if (sentinelSettings.ricochetVFX != null)
                    {
                        GameObject vfx = Instantiate(sentinelSettings.ricochetVFX, obstacleHit.point, Quaternion.LookRotation(obstacleHit.normal));
                        Destroy(vfx, sentinelSettings.ricochetVFXDuration);
                    }

                    //StartCoroutine(ShowShootLaser(sentinelPos, obstacleHit.point, sentinelSettings.shootLaserFadeDuration));
                }

                trackData.crouchStateChangeInProgress = false;
                trackData.crouchStateChangeScheduledTime = -1f;
                trackData.consecutiveLOSScans = 0;
                trackData.wasInLOS = false;
                trackData.wasPlayerCrouched = isPlayerCrouched;
                alreadyShot.Remove(col.gameObject);
                if (col.gameObject == player.gameObject)
                    playerAlarmTriggered = false;

                continue;
            }

            if (trackData.crouchStateChangeInProgress && Time.time >= trackData.crouchStateChangeScheduledTime && hasLOS)
            {
                PlayerHealth humanHealth = col.GetComponent<PlayerHealth>();

                if (enemyHealth != null && !enemyHealth.IsDead())
                {
                    ShootEnemy(col.gameObject, enemyHealth, "CROUCH CHANGE", sentinelPos, finalTargetPos, trackData.isHeadshot);
                }
                else if (humanHealth != null && !humanHealth.IsDead())
                {
                    ShootPlayer(col.gameObject, humanHealth, "CROUCH CHANGE", sentinelPos, finalTargetPos, trackData.isHeadshot);
                }

                trackData.crouchStateChangeInProgress = false;
                trackData.crouchStateChangeScheduledTime = -1f;
                trackData.lastShotTime = Time.time;
                trackData.wasPlayerCrouched = isPlayerCrouched;

                if (playerDetectionFeedback != null)
                    playerDetectionFeedback.OnNoLongerDetected();

                continue;
            }

            if (trackData.isBeingShot && !hasLOS)
            {
                Vector3 lastSeenPos = trackData.lastKnownPosition;
                Vector3 dirToLastSeen = (lastSeenPos - sentinelPos).normalized;

                RaycastHit obstacleHit;
                if (Physics.Raycast(sentinelPos, dirToLastSeen, out obstacleHit, 100f, sentinelSettings.obstacleLayers))
                {
                    int enemyLayer = LayerMask.NameToLayer("Enemy");
                    int zombieLayer = LayerMask.NameToLayer("Zombie");

                    if (obstacleHit.collider.gameObject.layer == enemyLayer || obstacleHit.collider.gameObject.layer == zombieLayer)
                    {
                        EnemyHealth coverEnemyHealth = obstacleHit.collider.GetComponent<EnemyHealth>();
                        if (coverEnemyHealth != null && !coverEnemyHealth.IsDead())
                        {
                            ShootEnemy(obstacleHit.collider.gameObject, coverEnemyHealth, "BOUCLIER DELAI", sentinelPos, obstacleHit.point, trackData.isHeadshot);

                            trackData.isBeingShot = false;
                            trackData.shootScheduledTime = -1f;
                            trackData.consecutiveLOSScans = 0;
                            trackData.wasInLOS = false;
                            alreadyShot.Remove(col.gameObject);
                            if (col.gameObject == player.gameObject)
                                playerAlarmTriggered = false;

                            continue;
                        }
                    }

                    if (audioSource != null && sentinelSettings.ricochetSound != null)
                        audioSource.PlayOneShot(sentinelSettings.ricochetSound);

                    if (sentinelSettings.ricochetVFX != null)
                    {
                        GameObject vfx = Instantiate(sentinelSettings.ricochetVFX, obstacleHit.point, Quaternion.LookRotation(obstacleHit.normal));
                        Destroy(vfx, sentinelSettings.ricochetVFXDuration);
                    }

                    //StartCoroutine(ShowShootLaser(sentinelPos, obstacleHit.point, sentinelSettings.shootLaserFadeDuration));
                }

                trackData.isBeingShot = false;
                trackData.shootScheduledTime = -1f;
                trackData.consecutiveLOSScans = 0;
                trackData.wasInLOS = false;
                alreadyShot.Remove(col.gameObject);
                if (col.gameObject == player.gameObject)
                    playerAlarmTriggered = false;

                continue;
            }

            bool isWindingUp = grabSystem != null && grabSystem.isInWindup;
            EnemyAI_AStar ai = col.GetComponent<EnemyAI_AStar>();
            if (ai != null && hasLOS)
                ai.isDetectedBySentinel = true;

            Rigidbody rb = col.GetComponent<Rigidbody>();
            MeleeAttackSystem meleeSystem = col.GetComponent<MeleeAttackSystem>();
            bool isAttacking = meleeSystem != null && meleeSystem.IsAttacking();

            BroomAttackSystem broomSystem = col.GetComponent<BroomAttackSystem>();
            bool isBroomAttacking = broomSystem != null && broomSystem.IsAttacking();

            TomatoThrowSystem tomatoThrow = col.GetComponent<TomatoThrowSystem>();
            bool isThrowing = tomatoThrow != null && tomatoThrow.IsThrowingTomato();

            TestClimbDetection climbSystem = col.GetComponent<TestClimbDetection>();
            bool isClimbing = climbSystem != null && climbSystem.IsClimbing();

            PlayerPitInteractable pitInteractable = col.GetComponent<PlayerPitInteractable>();
            bool isClimbingOutOfPit = pitInteractable != null && pitInteractable.IsClimbingOut();

            bool isMoving = false;

            if (col.gameObject == player.gameObject && rb != null)
            {
                if (trackData.lastCheckTime == 0f)
                {
                    trackData.lastCheckPosition = col.transform.position;
                    trackData.lastCheckTime = Time.time;
                }

                float timeSinceLastCheck = Time.time - trackData.lastCheckTime;
                if (timeSinceLastCheck > 0.01f)
                {
                    float distanceMoved = Vector3.Distance(col.transform.position, trackData.lastCheckPosition);
                    float worldSpaceVelocity = distanceMoved / timeSinceLastCheck;

                    if (worldSpaceVelocity > movementThreshold)
                    {
                        isMoving = true;
                    }

                    trackData.lastCheckPosition = col.transform.position;
                    trackData.lastCheckTime = Time.time;
                }

                if (PlayerInputManager.Instance.BroomLowActive
                    && PlayerInputManager.Instance.MoveInput.magnitude < 0.1f
                    && player.isInContactWithEnemy)
                    isMoving = false;
            }
            else if (rb != null)
            {
                EnemyAI_AStar zombieAI = col.GetComponent<EnemyAI_AStar>();
                bool isInPitMode = zombieAI != null && zombieAI.isInPitMode;

                if (isInPitMode)
                {
                    float effectiveThreshold = movementThreshold;
                    EnemyPitInteractable enemyPit = col.GetComponent<EnemyPitInteractable>();
                    if (enemyPit != null && enemyPit.isInShallowWater)
                        effectiveThreshold *= enemyPit.waterSlowdownMultiplier;
                    isMoving = rb.linearVelocity.magnitude > effectiveThreshold;
                }
                else
                {
                    Pathfinding.AIPath aiPath = col.GetComponent<Pathfinding.AIPath>();
                    if (aiPath != null && aiPath.enabled && aiPath.canMove)
                    {
                        float effectiveThreshold = movementThreshold;
                        EnemyPitInteractable enemyPit = col.GetComponent<EnemyPitInteractable>();
                        if (enemyPit != null && enemyPit.isInShallowWater)
                            effectiveThreshold *= enemyPit.waterSlowdownMultiplier;
                        isMoving = aiPath.velocity.magnitude > effectiveThreshold;
                    }
                    else
                    {
                        float effectiveThreshold = movementThreshold;
                        isMoving = rb.linearVelocity.magnitude > effectiveThreshold;
                    }
                }

                if (!isMoving)
                {
                    if (trackData.lastCheckTime == 0f)
                    {
                        trackData.lastCheckPosition = col.transform.position;
                        trackData.lastCheckTime = Time.time;
                    }

                    float timeSinceLastCheck = Time.time - trackData.lastCheckTime;
                    if (timeSinceLastCheck > 0.01f)
                    {
                        float distanceMoved = Vector3.Distance(col.transform.position, trackData.lastCheckPosition);
                        float worldSpaceVelocity = distanceMoved / timeSinceLastCheck;

                        float effectiveThreshold = movementThreshold;
                        EnemyPitInteractable enemyPit = col.GetComponent<EnemyPitInteractable>();
                        if (enemyPit != null && enemyPit.isInShallowWater)
                            effectiveThreshold *= enemyPit.waterSlowdownMultiplier;

                        if (worldSpaceVelocity > effectiveThreshold)
                            isMoving = true;

                        trackData.lastCheckPosition = col.transform.position;
                        trackData.lastCheckTime = Time.time;
                    }
                }
            }

            bool playerImmune = (col.gameObject == player.gameObject
                             && player.grabState == PlayerPhysicsMovement.GrabState.Grabbed
                             && !isMoving);
            if (isWindingUp || isHitterWindingUp || isHitterAttacking) isMoving = true;

            bool shouldBeShot = (isMoving || isAttacking || isBroomAttacking || isInBourrade || isFakeGrabber || isClimbing || isClimbingOutOfPit || isThrowing || (ai != null && ai.isKnockedDownByEpervier)) && !playerImmune; EnemyPitInteractable pitInt = col.GetComponent<EnemyPitInteractable>();
            if (pitInt != null && pitInt.isInShallowWater)
            {
            }

            bool losCheck = trackData.scheduledDuringWindup ? true : hasLOS;
            if (trackData.isBeingShot && Time.time >= trackData.shootScheduledTime && losCheck)
            {
                if (Time.time - lastActualShotTime < SHOT_SPACING_WINDOW)
                {
                    trackData.shootScheduledTime += SHOT_SPACING_WINDOW;
                    continue;
                }

                PlayerHealth humanHealth = col.GetComponent<PlayerHealth>();

                if (enemyHealth != null && !enemyHealth.IsDead())
                {
                    ShootEnemy(col.gameObject, enemyHealth, "MOUVEMENT", sentinelPos, finalTargetPos, trackData.isHeadshot);
                    lastActualShotTime = Time.time;
                }
                else if (humanHealth != null && !humanHealth.IsDead())
                {
                    ShootPlayer(col.gameObject, humanHealth, "MOUVEMENT", sentinelPos, finalTargetPos, trackData.isHeadshot);
                    lastActualShotTime = Time.time;
                }

                trackData.isBeingShot = false;
                trackData.shootScheduledTime = -1f;
                trackData.lastShotTime = Time.time;
                trackData.scheduledDuringWindup = false;
                continue;
            }

            if (shouldBeShot && hasLOS && !trackData.isBeingShot && Time.time - trackData.lastShotTime >= sentinelSettings.shootCooldown)
            {
                bool isOffensiveAction = isAttacking || isBroomAttacking || isInBourrade || isFakeGrabber || isHitterWindingUp || isHitterAttacking || isThrowing;
                bool needsExposureDelay = !trackData.wasInLOS && !isOffensiveAction;

                if (needsExposureDelay)
                {
                    trackData.consecutiveLOSScans++;

                    if (trackData.consecutiveLOSScans >= sentinelSettings.minimumExposureScans)
                    {
                        trackData.isBeingShot = true;
                        trackData.scheduledDuringWindup = isWindingUp || isHitterWindingUp;

                        float randomOffset = GetSafeShootTime(Random.Range(0.1f, 0.4f));
                        trackData.shootScheduledTime = Time.time + sentinelSettings.shootDelay + randomOffset;
                        trackData.lastShotTime = Time.time;

                        PlayerHealth humanHealth = col.GetComponent<PlayerHealth>();

                        if (enemyHealth != null && !enemyHealth.IsDead())
                            alreadyShot.Add(col.gameObject);

                        if (enemyHealth != null)
                        {
                            EnemyDetectionFeedback enemyFeedback = col.GetComponent<EnemyDetectionFeedback>();
                            if (enemyFeedback != null) enemyFeedback.OnDetected();
                        }
                        else if (humanHealth != null && !humanHealth.IsDead())
                        {
                            alreadyShot.Add(col.gameObject);
                            if (col.gameObject == player.gameObject && !playerAlarmTriggered)
                                playerAlarmTriggered = true;
                        }
                    }
                }
                else
                {
                    trackData.isBeingShot = true;
                    trackData.scheduledDuringWindup = isWindingUp || isHitterWindingUp;

                    float randomOffset = GetSafeShootTime(Random.Range(0.1f, 0.4f));
                    trackData.shootScheduledTime = Time.time + sentinelSettings.shootDelay + randomOffset;
                    trackData.lastShotTime = Time.time;

                    PlayerHealth humanHealth = col.GetComponent<PlayerHealth>();

                    if (enemyHealth != null && !enemyHealth.IsDead())
                        alreadyShot.Add(col.gameObject);

                    if (enemyHealth != null)
                    {
                        EnemyDetectionFeedback enemyFeedback = col.GetComponent<EnemyDetectionFeedback>();
                        if (enemyFeedback != null) enemyFeedback.OnDetected();
                    }
                    else if (humanHealth != null && !humanHealth.IsDead())
                    {
                        alreadyShot.Add(col.gameObject);
                        if (col.gameObject == player.gameObject && !playerAlarmTriggered)
                            playerAlarmTriggered = true;
                    }
                }
            }
            else
            {
                trackData.consecutiveLOSScans = 0;
            }

            if (col.gameObject == player.gameObject)
            {
                bool isInDanger = (shouldBeShot && hasLOS) || trackData.crouchStateChangeInProgress;

                if (stunBySentinel)
                    isInDanger = false;

                if (isInDanger)
                {
                    if (playerDetectionFeedback != null && !playerDetectionFeedback.isCurrentlyDetected)
                        playerDetectionFeedback.OnDetected();
                }
                else
                {
                    if (playerDetectionFeedback != null && playerDetectionFeedback.isCurrentlyDetected)
                        playerDetectionFeedback.OnNoLongerDetected();
                }
            }

            trackData.wasPlayerCrouched = isPlayerCrouched;
            trackData.wasInLOS = hasLOS;
            trackData.hasBeenTrackedBefore = true;
        }
    }

    // ============================================
    // LASER TEMPORAIRE
    // ============================================

    IEnumerator ShowShootLaser(Vector3 from, Vector3 to, float duration)
    {
        // Remplace par SentinelLaserManager.TriggerShotAnimation()
        yield break;
    }


    // ============================================
    // METHODES DE TIR
    // ============================================

    void ShootEnemy(GameObject enemy, EnemyHealth enemyHealth, string reason, Vector3 sentinelPos, Vector3 targetPos, bool isHeadshot = false)
    {
        // PAS de verification d'interception pour les ennemis entre eux
        // Tir normal direct
        string headshotTag = isHeadshot ? " [HEADSHOT]" : "";

        if (audioSource != null && sentinelSettings.shootSound != null)
            audioSource.PlayOneShot(sentinelSettings.shootSound);

        Vector3 currentTargetPos = GetTargetCenter(enemy);

        EnemyAI_AStar ai = enemy.GetComponent<EnemyAI_AStar>();
        if (ai != null)
        {
            if (ai.isKnockedDownByEpervier)
                ai.wasAlreadyShotDuringSweep = true;
            StartCoroutine(StunSpecificZombie(ai));
        }

        SentinelTarget sentinelTarget = enemy.GetComponent<SentinelTarget>();
        if (sentinelTarget != null) sentinelTarget.FlashWhite();

        // StartCoroutine(ShowShootLaser(sentinelPos, currentTargetPos, sentinelSettings.shootLaserFadeDuration));
        if (laserManager != null)
            laserManager.TriggerShotAnimation(sentinelPos, currentTargetPos);

        enemyHealth.TakeSentinelShot(isHeadshot);
        EnemyDetectionFeedback enemyFeedback = enemy.GetComponent<EnemyDetectionFeedback>();
        if (enemyFeedback != null) enemyFeedback.OnShotBySentinel();

        if (enemyHealth.IsDead())
            Debug.Log(enemy.name + " MORT!");
    }

    IEnumerator StunSpecificZombie(EnemyAI_AStar ai)
    {
        ai.isStunnedBySentinel = true;
        yield return new WaitForSeconds(sentinel.stunZombieDuration);

        if (ai == null) yield break;

        ai.isStunnedBySentinel = false;
        alreadyShot.Remove(ai.gameObject);

        // Reset cooldown pour eviter re-tir immediat apres stun
        if (trackedTargets.ContainsKey(ai.gameObject))
        {
            TargetTrackingData td = trackedTargets[ai.gameObject];
            td.lastShotTime = Time.time - sentinelSettings.shootCooldown;
            td.isBeingShot = false;
            td.shootScheduledTime = -1f;
            td.wasInLOS = false;
            td.consecutiveLOSScans = 0;
            td.lastCheckPosition = ai.transform.position;
            td.lastCheckTime = Time.time;
        }

        // Reset lastPathDestination pour forcer recalcul path a la reprise
        ai.lastPathDestination = Vector3.positiveInfinity;
    }

    void ShootPlayer(GameObject human, PlayerHealth humanHealth, string reason, Vector3 sentinelPos, Vector3 targetPos, bool isHeadshot = false)
    {
        // NOUVEAU : Verifier interception par un ennemi
        Vector3 currentTargetPos = GetTargetCenter(human);
        Vector3 direction = (currentTargetPos - sentinelPos).normalized;
        float distance = Vector3.Distance(sentinelPos, currentTargetPos);

        RaycastHit hit;
        if (Physics.Raycast(sentinelPos, direction, out hit, distance, sentinelSettings.obstacleLayers))
        {
            // Si on tape un layer Enemy ou Zombie
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            int zombieLayer = LayerMask.NameToLayer("Zombie");

            if (hit.collider.gameObject.layer == enemyLayer || hit.collider.gameObject.layer == zombieLayer)
            {
                EnemyHealth coverEnemyHealth = hit.collider.GetComponent<EnemyHealth>();
                if (coverEnemyHealth != null && !coverEnemyHealth.IsDead())
                {
                    ShootEnemy(hit.collider.gameObject, coverEnemyHealth, "BOUCLIER HUMAIN", sentinelPos, hit.point, isHeadshot);
                    return;
                }
            }
        }

        // Tir normal
        string headshotTag = isHeadshot ? " [HEADSHOT]" : "";

        if (audioSource != null && sentinelSettings.shootSound != null)
            audioSource.PlayOneShot(sentinelSettings.shootSound);

        SentinelTarget sentinelTarget = human.GetComponent<SentinelTarget>();
        if (sentinelTarget != null) sentinelTarget.FlashWhite();

        // StartCoroutine(ShowShootLaser(sentinelPos, currentTargetPos, sentinelSettings.shootLaserFadeDuration));
        if (laserManager != null)
            laserManager.TriggerShotAnimation(sentinelPos, currentTargetPos);

        if (playerDetectionFeedback != null)
            playerDetectionFeedback.OnShotBySentinel();

        // Enlever le blanc au moment du tir
        if (playerDetectionFeedback != null)
            playerDetectionFeedback.OnNoLongerDetected();

        
        StartCoroutine(PlayerStunBySentinel());

        //if (isHeadshot)
        //{
           // humanHealth.TakeSentinelShot(sentinelPos);
       // }
        humanHealth.TakeSentinelShot(sentinelPos);
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

        }
    }

    public IEnumerator ShootPlayerAtEndOfRecoil(GameObject playerObject, PlayerHealth humanHealth, Vector3 sentinelPos, Vector3 targetPos)
    {
        if (humanHealth != null && !humanHealth.IsDead())
        {
            Vector3 currentPos = GetTargetCenter(playerObject);

            // StartCoroutine(ShowShootLaser(sentinelPos, currentPos, sentinelSettings.shootLaserFadeDuration));
            if (laserManager != null)
                laserManager.TriggerShotAnimation(sentinelPos, currentPos);

            if (audioSource != null && sentinelSettings.shootSound != null)
                audioSource.PlayOneShot(sentinelSettings.shootSound);

            SentinelTarget sentinelTarget = playerObject.GetComponent<SentinelTarget>();
            if (sentinelTarget != null) sentinelTarget.FlashWhite();

            StartCoroutine(PlayerStunBySentinel());
            humanHealth.TakeSentinelShot(sentinelPos);
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

    public void ResetAllTracking()
    {
        foreach (var kvp in trackedTargets)
        {
            kvp.Value.wasInLOS = false;
            kvp.Value.wasPlayerCrouched = false;
            kvp.Value.consecutiveLOSScans = 0;
            kvp.Value.isBeingShot = false;
            kvp.Value.shootScheduledTime = -1f;
            kvp.Value.hasBeenTrackedBefore = false;

            // NOUVEAU : Reset les positions pour eviter faux mouvement au premier scan RedLight
            kvp.Value.lastCheckPosition = Vector3.zero;
            kvp.Value.lastCheckTime = 0f;
        }

        // Reset le blanc au passage en GreenLight
        if (playerDetectionFeedback != null)
            playerDetectionFeedback.OnNoLongerDetected();
    }

    void CountEnemiesAtStart()
    {
        EnemyHealth[] allEnemies = FindObjectsOfType<EnemyHealth>();

        foreach (EnemyHealth enemy in allEnemies)
        {
            

            totalEnemies++;
        }

    }

    public void OnEnemyKilled()
    {
        enemiesKilled++;
    }

    public void ForceScheduleShot(GameObject enemy)
    {
        EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
        if (enemyHealth == null || enemyHealth.IsDead()) return;
        if (!trackedTargets.ContainsKey(enemy))
            trackedTargets[enemy] = new TargetTrackingData();
        TargetTrackingData td = trackedTargets[enemy];
        if (td.isBeingShot) return;
        td.isBeingShot = true;
        td.wasInLOS = true;
        float randomOffset = GetSafeShootTime(Random.Range(0.1f, 0.4f));
        td.shootScheduledTime = Time.time + sentinelSettings.shootDelay + randomOffset;
        td.lastShotTime = Time.time;

    }
    public void NotifyStandUpDetected(GameObject enemy)
    {
        EnemyDetectionFeedback enemyFeedback = enemy.GetComponent<EnemyDetectionFeedback>();
        if (enemyFeedback != null) enemyFeedback.OnDetected();
        alreadyShot.Add(enemy);
    }
    public void ExecutePlayerShotOnGroggy()
    {
        if (player == null || playerHealth == null || playerHealth.IsDead()) return;

        Vector3 eyePosition = (sentinelEye != null) ? sentinelEye.position : transform.position;
        Vector3 sentinelPos = eyePosition + sentinelSettings.raycastOffset;
        Vector3 targetPos = GetTargetCenter(player.gameObject);

        if (audioSource != null && sentinelSettings.shootSound != null)
            audioSource.PlayOneShot(sentinelSettings.shootSound);

        if (laserManager != null)
            laserManager.TriggerShotAnimation(sentinelPos, targetPos);

        if (playerDetectionFeedback != null)
            playerDetectionFeedback.OnShotBySentinelGroggy();

        playerHealth.TakeDamage(sentinelSettings.playerDamage);
    }
    public void ExecuteSequenceShot(GameObject enemy)
    {
        EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
        if (enemyHealth == null || enemyHealth.IsDead()) return;

        Vector3 eyePosition = (sentinelEye != null) ? sentinelEye.position : transform.position;
        Vector3 sentinelPos = eyePosition + sentinelSettings.raycastOffset;
        Vector3 targetPos = GetTargetCenter(enemy);

        if (audioSource != null && sentinelSettings.shootSound != null)
            audioSource.PlayOneShot(sentinelSettings.shootSound);

        if (laserManager != null)
            laserManager.TriggerShotAnimation(sentinelPos, targetPos);

        SentinelTarget sentinelTarget = enemy.GetComponent<SentinelTarget>();
        if (sentinelTarget != null) sentinelTarget.FlashWhite();

        EnemyDetectionFeedback feedback = enemy.GetComponent<EnemyDetectionFeedback>();
        if (feedback != null) feedback.OnShotBySentinel();

        enemyHealth.TakeSentinelShotDuringSequence();
    }

    public void ResetSequenceTarget(GameObject enemy)
    {
        alreadyShot.Remove(enemy);
        if (!trackedTargets.ContainsKey(enemy)) return;

        TargetTrackingData td = trackedTargets[enemy];
        td.isBeingShot = false;
        td.shootScheduledTime = -1f;
        td.wasInLOS = false;
        td.consecutiveLOSScans = 0;
        td.lastShotTime = Time.time;
        td.lastCheckPosition = enemy.transform.position;
        td.lastCheckTime = Time.time;
    }

    // Dans GameManager.cs - ajouter cette methode publique

    public bool IsPlayerMoving()
    {
        if (player == null || sentinelSettings == null) return false;

        float movementThreshold = sentinelSettings.movementThreshold *
            (ModifierApplier.Instance != null ? ModifierApplier.Instance.sentinelMovementThresholdMultiplier : 1f);

        bool isMovingByVelocity = player.GetComponent<Rigidbody>().linearVelocity.magnitude > movementThreshold;

        bool isAttacking = player.GetComponent<MeleeAttackSystem>()?.IsAttacking() ?? false;
        bool isBroomAttacking = player.GetComponent<BroomAttackSystem>()?.IsAttacking() ?? false;
        bool isClimbing = player.GetComponent<TestClimbDetection>()?.IsClimbing() ?? false;
        bool isClimbingOutOfPit = player.GetComponent<PlayerPitInteractable>()?.IsClimbingOut() ?? false;
        GrabAttack grab = player.GetComponent<GrabAttack>();
        bool isInBourrade = grab != null && grab.isInBourradeDuration;

        return isMovingByVelocity || isAttacking || isBroomAttacking || isClimbing || isClimbingOutOfPit || isInBourrade;
    }

    // Getters pour VictoryUI
    public int GetTotalEnemies() => totalEnemies;
    public int GetEnemiesKilled() => enemiesKilled;
}