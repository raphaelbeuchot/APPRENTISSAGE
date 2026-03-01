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
        Vector3 eyePosition = (sentinelEye != null) ? sentinelEye.position : transform.position;
        Vector3 sentinelPos = eyePosition + sentinelSettings.raycastOffset;

        Collider[] targets = Physics.OverlapSphere(sentinelPos, sentinelSettings.detectionRadius, sentinelSettings.targetLayers);

        // Reinitialiser la detection
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

            // NOUVEAU : Initialisation etat crouch si nouvelle entree
            if (col.gameObject == player.gameObject && !trackData.hasBeenTrackedBefore)
            {
                trackData.wasPlayerCrouched = player.IsCrouching();
            }

            EnemyHealth enemyHealth = col.GetComponent<EnemyHealth>();
            if (enemyHealth != null && enemyHealth.IsRecovering()) continue;

            GrabAttack grabSystem = col.GetComponent<GrabAttack>();
            bool isInBourrade = grabSystem != null && grabSystem.isInBourradeDuration;
            bool isFakeGrabber = grabSystem != null && grabSystem.isFakeGrabbing;

            Vector3 targetPos = GetTargetCenter(col);
            Vector3 direction = (targetPos - sentinelPos).normalized;
            float distance = Vector3.Distance(sentinelPos, targetPos);

            // SYSTEME DE LOS AVEC HEADSHOT
            RaycastHit hit;
            bool hasLOS = true;
            bool isHeadshot = false;
            Vector3 finalTargetPos = targetPos;

            // Raycast 1 : vers centre
            if (Physics.Raycast(sentinelPos, direction, out hit, distance, sentinelSettings.obstacleLayers)
                && hit.collider.gameObject != col.gameObject)
            {
                // Centre cache, verifier si la tete depasse
                Vector3 headPos = GetHeadPosition(col);
                Vector3 directionToHead = (headPos - sentinelPos).normalized;
                float distanceToHead = Vector3.Distance(sentinelPos, headPos);

                RaycastHit headHit;
                if (Physics.Raycast(sentinelPos, directionToHead, out headHit, distanceToHead, sentinelSettings.obstacleLayers)
                    && headHit.collider.gameObject != col.gameObject)
                {
                    // Tete aussi cachee : vraiment safe
                    hasLOS = false;
                    Debug.DrawLine(sentinelPos, hit.point, Color.red, 0.2f);
                    Debug.DrawLine(sentinelPos, headHit.point, Color.red, 0.1f);
                }
                else
                {
                    // TETE VISIBLE = HEADSHOT !
                    hasLOS = true;
                    isHeadshot = true;
                    finalTargetPos = headPos;
                    Debug.DrawLine(sentinelPos, headPos, Color.yellow, 0.2f);
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

            // Check etat crouch du player
            bool isPlayerCrouched = false;
            if (col.gameObject == player.gameObject)
            {
                isPlayerCrouched = player.IsCrouching();
            }


            // Detection changement d'etat crouch (seulement si deja tracke avant)
            bool hasCrouchStateChanged = false;
            if (col.gameObject == player.gameObject && trackData.hasBeenTrackedBefore)
            {
                hasCrouchStateChanged = (trackData.wasPlayerCrouched != isPlayerCrouched);
            }

            // LANCEMENT TIMER CROUCH si changement detecte (peu importe visibilite)
            if (hasCrouchStateChanged && !trackData.crouchStateChangeInProgress && !trackData.isBeingShot)
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

            // PENDANT LE TIMER CROUCH : Si perd LOS de la tete = RICOCHET ou ZOMBIE INTERCEPTE
            if (trackData.crouchStateChangeInProgress && !hasLOS)
            {
                Vector3 lastSeenPos = trackData.lastKnownPosition;
                Vector3 dirToLastSeen = (lastSeenPos - sentinelPos).normalized;

                RaycastHit obstacleHit;
                if (Physics.Raycast(sentinelPos, dirToLastSeen, out obstacleHit, 100f, sentinelSettings.obstacleLayers))
                {
                    // NOUVEAU : Verifier si l'obstacle est un zombie
                    int enemyLayer = LayerMask.NameToLayer("Enemy");
                    int zombieLayer = LayerMask.NameToLayer("Zombie");

                    if (obstacleHit.collider.gameObject.layer == enemyLayer || obstacleHit.collider.gameObject.layer == zombieLayer)
                    {
                        // C'est un zombie qui cache le player - le zombie prend le tir
                        EnemyHealth coverEnemyHealth = obstacleHit.collider.GetComponent<EnemyHealth>();
                        if (coverEnemyHealth != null && !coverEnemyHealth.IsDead())
                        {
                            ShootEnemy(obstacleHit.collider.gameObject, coverEnemyHealth, "BOUCLIER CROUCH", sentinelPos, obstacleHit.point, trackData.isHeadshot);

                            // RESET immediat
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

                    

                    // Son ricochet
                    if (audioSource != null && sentinelSettings.ricochetSound != null)
                        audioSource.PlayOneShot(sentinelSettings.ricochetSound);

                    // VFX au point d'impact
                    if (sentinelSettings.ricochetVFX != null)
                    {
                        GameObject vfx = Instantiate(sentinelSettings.ricochetVFX, obstacleHit.point, Quaternion.LookRotation(obstacleHit.normal));
                        Destroy(vfx, sentinelSettings.ricochetVFXDuration);
                    }

                    // Laser vers obstacle
                    StartCoroutine(ShowShootLaser(sentinelPos, obstacleHit.point, sentinelSettings.shootLaserFadeDuration));
                }

                // RESET immediat
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

            // FIN TIMER CROUCH : Re-check visibilite et tirer
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

                // RESET apres tir
                trackData.crouchStateChangeInProgress = false;
                trackData.crouchStateChangeScheduledTime = -1f;
                trackData.lastShotTime = Time.time;
                trackData.wasPlayerCrouched = isPlayerCrouched;

                // Enlever le blanc apres le tir (AJOUTER CETTE LIGNE)
                if (playerDetectionFeedback != null)
                    playerDetectionFeedback.OnNoLongerDetected();

                continue;
            }




            // PENDANT LE DELAI : Si cache : RICOCHET IMMEDIAT ou ZOMBIE INTERCEPTE
            if (trackData.isBeingShot && !hasLOS)
            {
                // Raycast vers DERNIERE POSITION CONNUE (ou la tete etait)
                Vector3 lastSeenPos = trackData.lastKnownPosition;
                Vector3 dirToLastSeen = (lastSeenPos - sentinelPos).normalized;

                RaycastHit obstacleHit;
                if (Physics.Raycast(sentinelPos, dirToLastSeen, out obstacleHit, 100f, sentinelSettings.obstacleLayers))
                {
                    // NOUVEAU : Verifier si l'obstacle est un zombie
                    int enemyLayer = LayerMask.NameToLayer("Enemy");
                    int zombieLayer = LayerMask.NameToLayer("Zombie");

                    if (obstacleHit.collider.gameObject.layer == enemyLayer || obstacleHit.collider.gameObject.layer == zombieLayer)
                    {
                        // C'est un zombie qui cache la cible - le zombie prend le tir
                        EnemyHealth coverEnemyHealth = obstacleHit.collider.GetComponent<EnemyHealth>();
                        if (coverEnemyHealth != null && !coverEnemyHealth.IsDead())
                        {
                            ShootEnemy(obstacleHit.collider.gameObject, coverEnemyHealth, "BOUCLIER DELAI", sentinelPos, obstacleHit.point, trackData.isHeadshot);

                            // RESET
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

                    

                    // Son ricochet
                    if (audioSource != null && sentinelSettings.ricochetSound != null)
                        audioSource.PlayOneShot(sentinelSettings.ricochetSound);

                    // VFX au point d'impact
                    if (sentinelSettings.ricochetVFX != null)
                    {
                        GameObject vfx = Instantiate(sentinelSettings.ricochetVFX, obstacleHit.point, Quaternion.LookRotation(obstacleHit.normal));
                        Destroy(vfx, sentinelSettings.ricochetVFXDuration);
                    }

                    // Laser vers obstacle
                    StartCoroutine(ShowShootLaser(sentinelPos, obstacleHit.point, sentinelSettings.shootLaserFadeDuration));
                }

                // RESET immediat
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

            if (isWindingUp && hasLOS && !trackData.isBeingShot
                && Time.time - trackData.lastShotTime >= sentinelSettings.shootCooldown
                && !alreadyShot.Contains(col.gameObject))
            {
                if (enemyHealth != null && !enemyHealth.IsDead())
                {
                    ShootEnemy(col.gameObject, enemyHealth, "WINDUP", sentinelPos, finalTargetPos, trackData.isHeadshot);
                    trackData.lastShotTime = Time.time;
                    lastActualShotTime = Time.time;
                    alreadyShot.Add(col.gameObject);
                    trackData.wasInLOS = hasLOS;
                    trackData.hasBeenTrackedBefore = true;
                }
                continue;
            }
            EnemyAI_AStar ai = col.GetComponent<EnemyAI_AStar>();
            if (ai != null && hasLOS)
                ai.isDetectedBySentinel = true;


            //Punition si meleeattack, spray
            Rigidbody rb = col.GetComponent<Rigidbody>();
            MeleeAttackSystem meleeSystem = col.GetComponent<MeleeAttackSystem>();
            bool isAttacking = meleeSystem != null && meleeSystem.IsAttacking();

            // Check broom attack aussi
            BroomAttackSystem broomSystem = col.GetComponent<BroomAttackSystem>();
            bool isBroomAttacking = broomSystem != null && broomSystem.IsAttacking();

            // Check climb
            TestClimbDetection climbSystem = col.GetComponent<TestClimbDetection>();
            bool isClimbing = climbSystem != null && climbSystem.IsClimbing();

            // Check sortie de pit
            PlayerPitInteractable pitInteractable = col.GetComponent<PlayerPitInteractable>();
            bool isClimbingOutOfPit = pitInteractable != null && pitInteractable.IsClimbingOut();

            bool isMoving = false;

            // CAS PLAYER : World space uniquement
            if (col.gameObject == player.gameObject && rb != null)
            {
                // Initialiser position si premier scan
                if (trackData.lastCheckTime == 0f)
                {
                    trackData.lastCheckPosition = col.transform.position;
                    trackData.lastCheckTime = Time.time;
                }

                // Calculer deplacement depuis dernier scan
                float timeSinceLastCheck = Time.time - trackData.lastCheckTime;
                if (timeSinceLastCheck > 0.01f)
                {
                    float distanceMoved = Vector3.Distance(col.transform.position, trackData.lastCheckPosition);
                    float worldSpaceVelocity = distanceMoved / timeSinceLastCheck;

                    if (worldSpaceVelocity > sentinelSettings.movementThreshold)
                    {
                        isMoving = true;
                        Debug.Log($"[WORLD MOVEMENT] Player velocity: {worldSpaceVelocity:F3} m/s");
                    }

                    trackData.lastCheckPosition = col.transform.position;
                    trackData.lastCheckTime = Time.time;
                }
            }
            // CAS ZOMBIES : Logique existante + world space en complement
            else if (rb != null)
            {
                // Detection classique d'abord
                EnemyAI_AStar zombieAI = col.GetComponent<EnemyAI_AStar>();
                bool isInPitMode = zombieAI != null && zombieAI.isInPitMode;

                if (isInPitMode)
                {
                    float effectiveThreshold = sentinelSettings.movementThreshold;
                    EnemyPitInteractable enemyPit = col.GetComponent<EnemyPitInteractable>();
                    if (enemyPit != null && enemyPit.isInShallowWater)
                    {
                        effectiveThreshold *= enemyPit.waterSlowdownMultiplier;
                    }
                    isMoving = rb.linearVelocity.magnitude > effectiveThreshold;
                }
                else
                {
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
                        float effectiveThreshold = sentinelSettings.movementThreshold;
                        isMoving = rb.linearVelocity.magnitude > effectiveThreshold;
                    }
                }

                // AJOUT : World space detection EN COMPLEMENT (pour plateformes)
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

                        float effectiveThreshold = sentinelSettings.movementThreshold;
                        EnemyPitInteractable enemyPit = col.GetComponent<EnemyPitInteractable>();
                        if (enemyPit != null && enemyPit.isInShallowWater)
                        {
                            effectiveThreshold *= enemyPit.waterSlowdownMultiplier;
                        }

                        if (worldSpaceVelocity > effectiveThreshold)
                        {
                            isMoving = true;
                        }

                        trackData.lastCheckPosition = col.transform.position;
                        trackData.lastCheckTime = Time.time;
                    }
                }
            }
            bool playerImmune = (col.gameObject == player.gameObject
                             && player.grabState == PlayerPhysicsMovement.GrabState.Grabbed
                             && !isMoving);
            bool shouldBeShot = (isMoving || isAttacking || isBroomAttacking || isInBourrade || isFakeGrabber || isClimbing || isClimbingOutOfPit) && !playerImmune;
            EnemyPitInteractable pitInt = col.GetComponent<EnemyPitInteractable>();
            if (pitInt != null && pitInt.isInShallowWater)
            {
            }

            // FIN DU DELAI : Si toujours visible : TIR REUSSI
            if (trackData.isBeingShot && Time.time >= trackData.shootScheduledTime && hasLOS)
            {
                // NOUVEAU : Verifier qu'on n'a pas tire trop recemment dans ce scan
                if (Time.time - lastActualShotTime < SHOT_SPACING_WINDOW)
                {
                    // Trop proche, reporter au prochain scan
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

                // RESET apres tir reussi
                trackData.isBeingShot = false;
                trackData.shootScheduledTime = -1f;
                trackData.lastShotTime = Time.time;

                continue;
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
                        float randomOffset = GetSafeShootTime(Random.Range(0.1f, 0.4f));
                        trackData.shootScheduledTime = Time.time + sentinelSettings.shootDelay + randomOffset;
                        trackData.lastShotTime = Time.time;

                        PlayerHealth humanHealth = col.GetComponent<PlayerHealth>();

                        if (enemyHealth != null && !enemyHealth.IsDead())
                        {
                            alreadyShot.Add(col.gameObject);
                        }
                        if (enemyHealth != null)
                        {
                            EnemyDetectionFeedback enemyFeedback = col.GetComponent<EnemyDetectionFeedback>();
                            if (enemyFeedback != null) enemyFeedback.OnDetected();
                        }
                        else if (humanHealth != null && !humanHealth.IsDead())
                        {
                            alreadyShot.Add(col.gameObject);
                            if (col.gameObject == player.gameObject && !playerAlarmTriggered)
                            {
                                playerAlarmTriggered = true;
                            }
                        }
                    }
                }
                else
                {
                    trackData.isBeingShot = true;
                    float randomOffset = GetSafeShootTime(Random.Range(0.1f, 0.4f));
                    trackData.shootScheduledTime = Time.time + sentinelSettings.shootDelay + randomOffset;
                    trackData.lastShotTime = Time.time;

                    PlayerHealth humanHealth = col.GetComponent<PlayerHealth>();

                    if (enemyHealth != null && !enemyHealth.IsDead())
                    {
                        alreadyShot.Add(col.gameObject);
                    }
                    if (enemyHealth != null)
                    {
                        EnemyDetectionFeedback enemyFeedback = col.GetComponent<EnemyDetectionFeedback>();
                        if (enemyFeedback != null) enemyFeedback.OnDetected();
                    }
                    else if (humanHealth != null && !humanHealth.IsDead())
                    {
                        alreadyShot.Add(col.gameObject);
                        if (col.gameObject == player.gameObject && !playerAlarmTriggered)
                        {
                            playerAlarmTriggered = true;
                        }
                    }
                }
            }
            else
            {
                trackData.consecutiveLOSScans = 0;
            }

            // FEEDBACK VISUEL BLANC : Simple - visible ET (bouge OU changement crouch en cours) = blanc
            if (col.gameObject == player.gameObject)
            {
                bool isInDanger = (shouldBeShot && hasLOS) || trackData.crouchStateChangeInProgress;

                // IMPORTANT : Pas de blanc si stunne par sentinelle
                if (stunBySentinel)
                    isInDanger = false;

                if (isInDanger)
                {
                    // Player visible + bouge OU changement crouch = blanc
                    if (playerDetectionFeedback != null && !playerDetectionFeedback.isCurrentlyDetected)
                        playerDetectionFeedback.OnDetected();
                }
                else
                {
                    // Player cache OU immobile = pas blanc
                    if (playerDetectionFeedback != null && playerDetectionFeedback.isCurrentlyDetected)
                        playerDetectionFeedback.OnNoLongerDetected();
                }
            }

            // NOUVEAU : Update etat crouch pour prochain scan (A LA FIN)
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
            StartCoroutine(StunSpecificZombie(ai));
        }

        SentinelTarget sentinelTarget = enemy.GetComponent<SentinelTarget>();
        if (sentinelTarget != null) sentinelTarget.FlashWhite();

        StartCoroutine(ShowShootLaser(sentinelPos, currentTargetPos, sentinelSettings.shootLaserFadeDuration));

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

        // CHECK si le zombie existe encore
        if (ai == null) yield break;

        ai.isStunnedBySentinel = false;
        alreadyShot.Remove(ai.gameObject);
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

        StartCoroutine(ShowShootLaser(sentinelPos, currentTargetPos, sentinelSettings.shootLaserFadeDuration));

        if (playerDetectionFeedback != null)
            playerDetectionFeedback.OnShotBySentinel();

        // Enlever le blanc au moment du tir
        if (playerDetectionFeedback != null)
            playerDetectionFeedback.OnNoLongerDetected();

        StartCoroutine(PlayerStunBySentinel());

        if (isHeadshot)
        {
            humanHealth.TakeSentinelShot(sentinelPos);
        }
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
            StartCoroutine(ShowShootLaser(sentinelPos, currentPos, sentinelSettings.shootLaserFadeDuration));


            if (audioSource != null && sentinelSettings.shootSound != null)
                audioSource.PlayOneShot(sentinelSettings.shootSound);

            SentinelTarget sentinelTarget = playerObject.GetComponent<SentinelTarget>();
            if (sentinelTarget != null) sentinelTarget.FlashWhite();

            StartCoroutine(ShowShootLaser(sentinelPos, targetPos, sentinelSettings.shootLaserFadeDuration));

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
            // Exclure BrightEyes
            BrightEyesController brightEyes = enemy.GetComponent<BrightEyesController>();
            if (brightEyes != null)
            {
                continue; // Skip BrightEyes
            }

            totalEnemies++;
        }

    }

    public void OnEnemyKilled()
    {
        enemiesKilled++;
    }

    // Getters pour VictoryUI
    public int GetTotalEnemies() => totalEnemies;
    public int GetEnemiesKilled() => enemiesKilled;
}