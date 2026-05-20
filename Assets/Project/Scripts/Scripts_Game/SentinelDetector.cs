using System.Collections.Generic;
using UnityEngine;
using Pathfinding;

public class SentinelDetector : MonoBehaviour
{
    public class TargetTrackingData
    {
        public bool wasInLOS;
        public float lostLOSTime;
        public float reacquiredTime;
        public bool canShoot;
        public bool scheduledDuringWindup = false;

        public bool isBeingShot = false;
        public float lastShotTime = -999f;
        public float shootScheduledTime = -1f;

        public int consecutiveLOSScans = 0;

        public bool isHeadshot = false;
        public Vector3 lastKnownPosition;

        public bool wasPlayerCrouched = false;

        public bool crouchStateChangeInProgress = false;
        public float crouchStateChangeScheduledTime = -1f;

        public bool hasBeenTrackedBefore = false;

        public Vector3 lastCheckPosition;
        public float lastCheckTime;
    }

    public bool playerAlarmTriggered = false;
    public HashSet<GameObject> alreadyShot = new HashSet<GameObject>();
    public Dictionary<GameObject, TargetTrackingData> trackedTargets = new Dictionary<GameObject, TargetTrackingData>();

    private List<float> scheduledShotTimes = new List<float>();
    private const float SHOT_SPACING_WINDOW = 0.05f;
    private float lastActualShotTime = -999f;
    private float detectionTimer = 0f;

    private GameManager gm;
    private SentinelShooter shooter;

    public void Initialize(GameManager gameManager, SentinelShooter sentinelShooter)
    {
        gm = gameManager;
        shooter = sentinelShooter;
    }

    public void Tick()
    {
        detectionTimer += Time.deltaTime;
        if (detectionTimer >= gm.sentinelSettings.redlightScanInterval)
        {
            detectionTimer = 0f;
            CheckForMovingTargetsWithRaycast();
        }
    }

    float GetSafeShootTime(float baseRandomDelay)
    {
        scheduledShotTimes.RemoveAll(t => t < Time.time);

        float proposedTime = Time.time + gm.sentinelSettings.shootDelay + baseRandomDelay;

        while (scheduledShotTimes.Exists(t => Mathf.Abs(t - proposedTime) < SHOT_SPACING_WINDOW))
        {
            proposedTime += SHOT_SPACING_WINDOW;
        }

        scheduledShotTimes.Add(proposedTime);

        return proposedTime - Time.time - gm.sentinelSettings.shootDelay;
    }

    // ============================================
    // SYSTEME DE DETECTION AVEC RAYCASTS
    // ============================================

    void CheckForMovingTargetsWithRaycast()
    {
        float movementThreshold = gm.sentinelSettings.movementThreshold * (ModifierApplier.Instance != null ? ModifierApplier.Instance.sentinelMovementThresholdMultiplier : 1f);
        Vector3 eyePosition = (gm.sentinelEye != null) ? gm.sentinelEye.position : transform.position;
        Vector3 sentinelPos = eyePosition + gm.sentinelSettings.raycastOffset;

        Collider[] targets = Physics.OverlapSphere(sentinelPos, gm.sentinelSettings.detectionRadius, gm.sentinelSettings.targetLayers);

        foreach (var kvp in trackedTargets)
        {
            EnemyAI_AStar ai = kvp.Key != null ? kvp.Key.GetComponent<EnemyAI_AStar>() : null;
            if (ai != null)
                ai.isDetectedBySentinel = false;
        }

        foreach (Collider col in targets)
        {
            EnemyAI_AStar sequenceGuard = col.GetComponent<EnemyAI_AStar>();
            if (sequenceGuard != null && (sequenceGuard.isKnockedDownByEpervier || sequenceGuard.isInStandupPhase))
                continue;
            if (!trackedTargets.ContainsKey(col.gameObject))
                trackedTargets[col.gameObject] = new TargetTrackingData();

            TargetTrackingData trackData = trackedTargets[col.gameObject];

            if (col.gameObject == gm.player.gameObject && !trackData.hasBeenTrackedBefore)
            {
                trackData.wasPlayerCrouched = gm.player.IsCrouching();
            }

            EnemyHealth enemyHealth = col.GetComponent<EnemyHealth>();
            if (enemyHealth != null && enemyHealth.IsRecovering()) continue;

            if (col.gameObject == gm.player.gameObject && (gm.player.IsSweeping() || gm.player.IsGroggy() || gm.player.IsGroggyStunned()))
                continue;

            EnemyAI_AStar stunnedCheck = col.GetComponent<EnemyAI_AStar>();
            if (stunnedCheck != null && stunnedCheck.isStunnedBySentinel) continue;

            GrabAttack grabSystem = col.GetComponent<GrabAttack>();
            bool isInBourrade = grabSystem != null && grabSystem.isInBourradeDuration;
            bool isFakeGrabber = grabSystem != null && grabSystem.isFakeGrabbing;

            HitAttack hitAttack = col.GetComponent<HitAttack>();
            bool isHitterWindingUp = hitAttack != null && hitAttack.isInWindup;
            bool isHitterAttacking = hitAttack != null && hitAttack.IsAttacking();

            Vector3 targetPos = SentinelTargetGeometry.GetTargetCenter(col);
            Vector3 direction = (targetPos - sentinelPos).normalized;
            float distance = Vector3.Distance(sentinelPos, targetPos);

            RaycastHit hit;
            bool hasLOS = true;
            bool isHeadshot = false;
            Vector3 finalTargetPos = targetPos;

            if (Physics.Raycast(sentinelPos, direction, out hit, distance, gm.sentinelSettings.obstacleLayers)
    && hit.collider.gameObject != col.gameObject)
            {
                Vector3 headPos = SentinelTargetGeometry.GetHeadPosition(col);
                Vector3 directionToHead = (headPos - sentinelPos).normalized;
                float distanceToHead = Vector3.Distance(sentinelPos, headPos);

                RaycastHit headHit;
                if (Physics.Raycast(sentinelPos, directionToHead, out headHit, distanceToHead, gm.sentinelSettings.obstacleLayers)
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
            if (col.gameObject == gm.player.gameObject)
            {
                isPlayerCrouched = gm.player.IsCrouching();
            }

            bool hasCrouchStateChanged = false;
            if (col.gameObject == gm.player.gameObject && trackData.hasBeenTrackedBefore)
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
                    if (col.gameObject == gm.player.gameObject)
                    {
                        playerAlarmTriggered = true;
                    }
                }
            }

            if (trackData.crouchStateChangeInProgress && !hasLOS)
            {
                shooter.ResolveCoverOrRicochet(sentinelPos, trackData.lastKnownPosition, trackData.isHeadshot);
                trackData.crouchStateChangeInProgress = false;
                trackData.crouchStateChangeScheduledTime = -1f;
                trackData.consecutiveLOSScans = 0;
                trackData.wasInLOS = false;
                trackData.wasPlayerCrouched = isPlayerCrouched;
                alreadyShot.Remove(col.gameObject);
                if (col.gameObject == gm.player.gameObject) playerAlarmTriggered = false;
                if (gm.playerDetectionFeedback != null) gm.playerDetectionFeedback.OnNoLongerDetected();
                continue;
            }

            if (trackData.crouchStateChangeInProgress && Time.time >= trackData.crouchStateChangeScheduledTime && hasLOS)
            {
                PlayerHealth humanHealth = col.GetComponent<PlayerHealth>();

                if (enemyHealth != null && !enemyHealth.IsDead())
                {
                    shooter.ShootEnemy(col.gameObject, enemyHealth, "CROUCH CHANGE", sentinelPos, finalTargetPos, trackData.isHeadshot);
                }
                else if (humanHealth != null && !humanHealth.IsDead())
                {
                    shooter.ShootPlayer(col.gameObject, humanHealth, "CROUCH CHANGE", sentinelPos, finalTargetPos, trackData.isHeadshot);
                }

                trackData.crouchStateChangeInProgress = false;
                trackData.crouchStateChangeScheduledTime = -1f;
                trackData.lastShotTime = Time.time;
                trackData.wasPlayerCrouched = isPlayerCrouched;

                if (gm.playerDetectionFeedback != null)
                    gm.playerDetectionFeedback.OnNoLongerDetected();

                continue;
            }

            if (trackData.isBeingShot && !hasLOS)
            {
                shooter.ResolveCoverOrRicochet(sentinelPos, trackData.lastKnownPosition, trackData.isHeadshot);
                trackData.isBeingShot = false;
                trackData.shootScheduledTime = -1f;
                trackData.consecutiveLOSScans = 0;
                trackData.wasInLOS = false;
                alreadyShot.Remove(col.gameObject);
                if (col.gameObject == gm.player.gameObject) playerAlarmTriggered = false;
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

            if (col.gameObject == gm.player.gameObject && rb != null)
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

                if (gm.player.GetCurrentPlatform() != null && !(gm.player.GetCurrentPlatform() is PlatformTrainCar))
                    isMoving = true;

                if (PlayerInputManager.Instance.BroomLowActive
                    && PlayerInputManager.Instance.MoveInput.magnitude < 0.1f
                    && gm.player.isInContactWithEnemy)
                    isMoving = false;
            }
            else if (rb != null)
            {
                EnemyAI_AStar zombieAI = col.GetComponent<EnemyAI_AStar>();
                bool isInPitMode = zombieAI != null && zombieAI.isInPitMode;
                bool isOnIslandPlatform = zombieAI != null && zombieAI.isOnIslandPlatform;

                if (isInPitMode)
                {
                    float effectiveThreshold = movementThreshold;
                    EnemyPitInteractable enemyPit = col.GetComponent<EnemyPitInteractable>();
                    if (enemyPit != null && enemyPit.isInShallowWater)
                        effectiveThreshold *= enemyPit.waterSlowdownMultiplier;
                    isMoving = rb.linearVelocity.magnitude > effectiveThreshold;
                }
                else if (isOnIslandPlatform)
                {
                    isMoving = zombieAI.targetHuman != null;
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

            bool playerImmune = (col.gameObject == gm.player.gameObject
                             && gm.player.grabState == PlayerPhysicsMovement.GrabState.Grabbed
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
                    shooter.ShootEnemy(col.gameObject, enemyHealth, "MOUVEMENT", sentinelPos, finalTargetPos, trackData.isHeadshot);
                    lastActualShotTime = Time.time;
                }
                else if (humanHealth != null && !humanHealth.IsDead())
                {
                    shooter.ShootPlayer(col.gameObject, humanHealth, "MOUVEMENT", sentinelPos, finalTargetPos, trackData.isHeadshot);
                    lastActualShotTime = Time.time;
                }

                trackData.isBeingShot = false;
                trackData.shootScheduledTime = -1f;
                trackData.lastShotTime = Time.time;
                trackData.scheduledDuringWindup = false;
                continue;
            }

            if (shouldBeShot && hasLOS && !trackData.isBeingShot && Time.time - trackData.lastShotTime >= gm.sentinelSettings.shootCooldown)
            {
                bool isOffensiveAction = isAttacking || isBroomAttacking || isInBourrade || isFakeGrabber || isHitterWindingUp || isHitterAttacking || isThrowing;
                bool needsExposureDelay = !trackData.wasInLOS && !isOffensiveAction;

                if (needsExposureDelay)
                {
                    trackData.consecutiveLOSScans++;

                    if (trackData.consecutiveLOSScans >= gm.sentinelSettings.minimumExposureScans)
                    {
                        trackData.isBeingShot = true;
                        trackData.scheduledDuringWindup = isWindingUp || isHitterWindingUp;

                        float randomOffset = GetSafeShootTime(Random.Range(0.1f, 0.4f));
                        trackData.shootScheduledTime = Time.time + gm.sentinelSettings.shootDelay + randomOffset;
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
                            if (col.gameObject == gm.player.gameObject && !playerAlarmTriggered)
                                playerAlarmTriggered = true;
                        }
                    }
                }
                else
                {
                    trackData.isBeingShot = true;
                    trackData.scheduledDuringWindup = isWindingUp || isHitterWindingUp;

                    float randomOffset = GetSafeShootTime(Random.Range(0.1f, 0.4f));
                    trackData.shootScheduledTime = Time.time + gm.sentinelSettings.shootDelay + randomOffset;
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
                        if (col.gameObject == gm.player.gameObject && !playerAlarmTriggered)
                            playerAlarmTriggered = true;
                    }
                }
            }
            else
            {
                trackData.consecutiveLOSScans = 0;
            }

            if (col.gameObject == gm.player.gameObject)
            {
                bool isInDanger = (shouldBeShot && hasLOS) || trackData.crouchStateChangeInProgress;

                if (gm.stunBySentinel)
                    isInDanger = false;

                if (isInDanger)
                {
                    if (!trackData.isBeingShot && !trackData.crouchStateChangeInProgress && Time.time - trackData.lastShotTime >= gm.sentinelSettings.shootCooldown)
                    {
                        trackData.isBeingShot = true;
                        float randomOffset = GetSafeShootTime(Random.Range(0.1f, 0.4f));
                        trackData.shootScheduledTime = Time.time + gm.sentinelSettings.shootDelay + randomOffset;
                        trackData.lastShotTime = Time.time;
                        playerAlarmTriggered = true;
                        alreadyShot.Add(col.gameObject);
                    }

                    if (gm.playerDetectionFeedback != null && !gm.playerDetectionFeedback.isCurrentlyDetected)
                        gm.playerDetectionFeedback.OnDetected();
                }
                else
                {
                    if (gm.playerDetectionFeedback != null && gm.playerDetectionFeedback.isCurrentlyDetected)
                        gm.playerDetectionFeedback.OnNoLongerDetected();
                }
            }

            trackData.wasPlayerCrouched = isPlayerCrouched;
            trackData.wasInLOS = hasLOS;
            trackData.hasBeenTrackedBefore = true;
        }
    }

    // ============================================
    // API PUBLIQUE
    // ============================================

    public void RemoveFromAlreadyShot(GameObject target)
    {
        alreadyShot.Remove(target);
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
            kvp.Value.lastCheckPosition = Vector3.zero;
            kvp.Value.lastCheckTime = 0f;
        }

        if (gm.playerDetectionFeedback != null)
            gm.playerDetectionFeedback.OnNoLongerDetected();
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
        td.shootScheduledTime = Time.time + gm.sentinelSettings.shootDelay + randomOffset;
        td.lastShotTime = Time.time;
    }

    public void NotifyStandUpDetected(GameObject enemy)
    {
        EnemyDetectionFeedback enemyFeedback = enemy.GetComponent<EnemyDetectionFeedback>();
        if (enemyFeedback != null) enemyFeedback.OnDetected();
        alreadyShot.Add(enemy);
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

    public bool IsPlayerInSentinelLOS()
    {
        if (gm.player == null) return false;
        if (!trackedTargets.ContainsKey(gm.player.gameObject)) return false;
        return trackedTargets[gm.player.gameObject].canShoot;
    }
}
