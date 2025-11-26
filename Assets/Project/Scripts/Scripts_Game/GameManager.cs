using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine.Samples;
using UnityEngine;
using UnityEngine.AI;
using Pathfinding;

public class GameManager : MonoBehaviour
{
    public enum GameState { GreenLight, Alert, RedLight, Release }

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

    public GameState currentState = GameState.GreenLight;
    private float cycleTimer;
    private float targetDuration;
    private bool gameStarted = false;
    private bool playerAlarmTriggered = false;
    private HashSet<GameObject> alreadyShot = new HashSet<GameObject>();
    private float detectionTimer = 0f;

    // Système de raycasts et LOS
    private Dictionary<GameObject, TargetTrackingData> trackedTargets = new Dictionary<GameObject, TargetTrackingData>();

    private class TargetTrackingData
    {
        public bool wasInLOS;
        public float lostLOSTime;
        public float reacquiredTime;
        public bool canShoot;

        // Nouvelles variables pour sécuriser les tirs
        public bool isBeingShot = false;
        public float lastShotTime = -999f;
    }

    void Start()
    {
        if (sentinelSettings == null)
        {
            Debug.LogError("GameManager: SentinelSettings non assigne!");
            return;
        }

        if (player == null)
            Debug.LogError("GameManager: PlayerPhysicsMovement non assigne!");

        if (playerHealth == null)
        {
            playerHealth = player != null ? player.GetComponent<PlayerHealth>() : null;
            if (playerHealth == null)
                Debug.LogError("GameManager: PlayerHealth non trouve sur le joueur!");
        }

        audioSource = GetComponent<AudioSource>();
        Time.timeScale = 1f;

        if (waitForStart)
            gameStarted = false;
        else
        {
            gameStarted = true;
            StartNewCycle(GameState.GreenLight);
        }
    }

    void Update()
    {
        if (!gameStarted || sentinelSettings == null) return;

        cycleTimer += Time.deltaTime;

        if (currentState == GameState.Alert)
        {
            bool audioFinished = (audioSource != null && !audioSource.isPlaying) || (audioSource == null || sentinelSettings.alertSound == null);
            if (audioFinished || cycleTimer >= sentinelSettings.alertDuration)
            {
                StartNewCycle(GameState.RedLight);
                return;
            }
            return;
        }

        if (currentState == GameState.RedLight)
        {
            detectionTimer += Time.deltaTime;
            if (detectionTimer >= sentinelSettings.redlightScanInterval)
            {
                detectionTimer = 0f;
                CheckForMovingTargetsWithRaycast();
            }
        }

        if (currentState == GameState.Release)
        {
            if (cycleTimer >= sentinelSettings.releaseDuration)
            {
                StartNewCycle(GameState.GreenLight);
                return;
            }
            return;
        }

        if (cycleTimer >= targetDuration)
        {
            if (currentState == GameState.GreenLight)
                StartNewCycle(GameState.Alert);
            else if (currentState == GameState.RedLight)
                StartNewCycle(GameState.Release);
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
            EnemyAI ai = kvp.Key != null ? kvp.Key.GetComponent<EnemyAI>() : null;
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

            Vector3 targetPos = col.transform.position + Vector3.up * 1f;
            Vector3 direction = (targetPos - sentinelPos).normalized;
            float distance = Vector3.Distance(sentinelPos, targetPos);

            // NOUVEAU SYSTÈME DE LOS (ligne de vue)
            RaycastHit hit;
            bool hasLOS = true;

            if (Physics.Raycast(sentinelPos, direction, out hit, distance, sentinelSettings.obstacleLayers))
            {
                // Si le raycast touche un obstacle avant la cible : vision bloquée
                hasLOS = false;
                Debug.DrawLine(sentinelPos, hit.point, Color.red, 0.2f);
            }
            else
            {
                Debug.DrawLine(sentinelPos, targetPos, Color.green, 0.2f);
            }

            trackData.canShoot = hasLOS;
            trackData.wasInLOS = hasLOS;

            EnemyAI ai = col.GetComponent<EnemyAI>();
            if (ai != null && hasLOS)
                ai.isDetectedBySentinel = true;

            bool playerImmune = (col.gameObject == player.gameObject && player.grabState == PlayerPhysicsMovement.GrabState.Grabbed);

            Rigidbody rb = col.GetComponent<Rigidbody>();
            MeleeAttackSystem meleeSystem = col.GetComponent<MeleeAttackSystem>();
            bool isAttacking = meleeSystem != null && meleeSystem.IsAttacking();

            bool isMoving = false;
            if (rb != null)
            {
                // ESSAYER A* EN PREMIER
                Pathfinding.AIPath aiPath = col.GetComponent<Pathfinding.AIPath>();
                if (aiPath != null && aiPath.canMove)
                {
                    float effectiveThreshold = sentinelSettings.movementThreshold;

                    // Si dans shallow water, reduire le threshold proportionnellement
                    EnemyPitInteractable enemyPit = col.GetComponent<EnemyPitInteractable>();
                    if (enemyPit != null && enemyPit.isInShallowWater)
                    {
                        effectiveThreshold *= enemyPit.waterSlowdownMultiplier;
                    }

                    // AIPath : utiliser velocity (Vector3)
                    isMoving = aiPath.velocity.magnitude > effectiveThreshold;
                }
                // SINON ESSAYER NAVMESH (ancien systeme)
                else
                {
                    NavMeshAgent agent = col.GetComponent<NavMeshAgent>();
                    if (agent != null && agent.isOnNavMesh)
                    {
                        float effectiveThreshold = sentinelSettings.movementThreshold;

                        EnemyPitInteractable enemyPit = col.GetComponent<EnemyPitInteractable>();
                        if (enemyPit != null && enemyPit.isInShallowWater)
                        {
                            effectiveThreshold *= enemyPit.waterSlowdownMultiplier;
                        }

                        isMoving = agent.velocity.magnitude > effectiveThreshold;
                    }
                    // FALLBACK : Rigidbody velocity
                    else
                    {
                        isMoving = rb.linearVelocity.magnitude > sentinelSettings.movementThreshold;
                    }
                }
            }

            bool shouldBeShot = (isMoving || isAttacking || isInBourrade || isFakeGrabber) && !playerImmune;

            // AJOUTEZ LE LOG ICI
            EnemyPitInteractable pitInt = col.GetComponent<EnemyPitInteractable>();
            if (pitInt != null && pitInt.isInShallowWater)
            {
                Debug.Log($"[WATER] {col.name} - isMoving: {isMoving}, hasLOS: {hasLOS}, shouldShoot: {shouldBeShot}");
            }

            // TIR SUR LA CIBLE SI ELLE EST EN MOUVEMENT ET VISIBLE
            if (shouldBeShot && hasLOS && !trackData.isBeingShot && Time.time - trackData.lastShotTime >= sentinelSettings.shootCooldown)
            {
                trackData.isBeingShot = true;
                trackData.lastShotTime = Time.time;

                PlayerHealth humanHealth = col.GetComponent<PlayerHealth>();

                if (enemyHealth != null && !enemyHealth.IsDead())
                {
                    alreadyShot.Add(col.gameObject);
                    string reason = isAttacking ? "ATTAQUE" : (isInBourrade ? "BOURRADE" : "MOUVEMENT");
                    float randomOffset = Random.Range(0.1f, 0.4f);
                    StartCoroutine(ShootEnemyWithDelay(col.gameObject, enemyHealth, reason, sentinelPos, targetPos, trackData, randomOffset));
                }
                else if (humanHealth != null && !humanHealth.IsDead())
                {
                    alreadyShot.Add(col.gameObject);
                    string reason = isAttacking ? "ATTAQUE" : "MOUVEMENT";
                    if (col.gameObject == player.gameObject && !playerAlarmTriggered)
                    {
                        playerAlarmTriggered = true;
                        StartCoroutine(ShootPlayerWithAlarm(col.gameObject, humanHealth, reason, sentinelPos, targetPos, trackData));
                    }
                }
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
            ShootEnemy(enemy, enemyHealth, reason, sentinelPos, targetPos);
            trackData.isBeingShot = false;
            trackData.lastShotTime = Time.time;
        }
    }


    IEnumerator ShootPlayerWithAlarm(GameObject playerObject, PlayerHealth humanHealth, string reason, Vector3 sentinelPos, Vector3 targetPos, TargetTrackingData trackData)
    {
        yield return new WaitForSeconds(sentinelSettings.shootDelay);

        if (humanHealth != null && !humanHealth.IsDead())
        {
            ShootPlayer(playerObject, humanHealth, reason, sentinelPos, targetPos);
            trackData.isBeingShot = false;
            trackData.lastShotTime = Time.time;


        }
    }

    void ShootEnemy(GameObject enemy, EnemyHealth enemyHealth, string reason, Vector3 sentinelPos, Vector3 targetPos)
    {
        Debug.Log("BANG! " + enemy.name + " (" + reason + ")");
        
        
        if (audioSource != null && sentinelSettings.shootSound != null)
            audioSource.PlayOneShot(sentinelSettings.shootSound);

        Vector3 currentTargetPos = enemy.transform.position + Vector3.up * 1f;

        EnemyAI ai = enemy.GetComponent<EnemyAI>();
        if (ai != null)
        {
            StartCoroutine(StunSpecificZombie(ai));
        }

        SentinelTarget sentinelTarget = enemy.GetComponent<SentinelTarget>();
        if (sentinelTarget != null) sentinelTarget.FlashWhite();

        StartCoroutine(ShowShootLaser(sentinelPos, currentTargetPos, sentinelSettings.shootLaserFadeDuration));

        // AJOUTER CES LIGNES :
        bool isHeadshot = sentinelSettings.headshotInstakill &&
                  Random.value < sentinelSettings.headshotChance;

        enemyHealth.TakeSentinelShot(isHeadshot);  

        if (enemyHealth.IsDead())
            Debug.Log(enemy.name + " MORT!");

    }


    IEnumerator StunSpecificZombie(EnemyAI ai)
    {
        ai.isStunnedBySentinel = true;
        yield return new WaitForSeconds(sentinel.stunZombieDuration);
        ai.isStunnedBySentinel = false;

        alreadyShot.Remove(ai.gameObject);
    }

    

    void ShootPlayer(GameObject human, PlayerHealth humanHealth, string reason, Vector3 sentinelPos, Vector3 oldTargetPos)
    {
        Debug.Log("BANG! " + human.name + " (" + reason + ")");
        if (audioSource != null && sentinelSettings.shootSound != null)
            audioSource.PlayOneShot(sentinelSettings.shootSound);

        // On reprend la position actuelle du joueur (corrige le décalage)
        Vector3 currentTargetPos = human.transform.position + Vector3.up * 1f;

        SentinelTarget sentinelTarget = human.GetComponent<SentinelTarget>();
        if (sentinelTarget != null) sentinelTarget.FlashWhite();

        // Le laser tire là où le joueur est vraiment, pas là où il a été vu
        StartCoroutine(ShowShootLaser(sentinelPos, currentTargetPos, sentinelSettings.shootLaserFadeDuration));

        StartCoroutine(PlayerStunBySentinel());
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
            Vector3 currentPos = playerObject.transform.position + Vector3.up * 1f;
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

    void StartNewCycle(GameState newState)
    {
        SetState(newState);
        cycleTimer = 0f;

        if (newState == GameState.GreenLight)
        {
            if (player != null) player.enabled = true;
            targetDuration = sentinelSettings.GetRandomGreenlightDuration();
            playerAlarmTriggered = false;
            alreadyShot.Clear();
            detectionTimer = 0f;
            trackedTargets.Clear();
            HideAllCircles();
        }
        else if (newState == GameState.Alert)
        {
            targetDuration = sentinelSettings.alertDuration;
            if (audioSource != null && sentinelSettings.alertSound != null)
                audioSource.PlayOneShot(sentinelSettings.alertSound);
        }
        else if (newState == GameState.RedLight)
        {
            targetDuration = sentinelSettings.GetRandomRedlightDuration();
            alreadyShot.Clear();
            playerAlarmTriggered = false;
            if (audioSource != null && sentinelSettings.redlightSound != null)
                audioSource.PlayOneShot(sentinelSettings.redlightSound);
        }
        else if (newState == GameState.Release)
        {
            targetDuration = sentinelSettings.releaseDuration;
            if (audioSource != null && sentinelSettings.releaseSound != null)
                audioSource.PlayOneShot(sentinelSettings.releaseSound);
            FadeOutAllCircles();
        }
    }

    void HideAllCircles()
    {
        foreach (SentinelTarget target in FindObjectsOfType<SentinelTarget>())
            target.HideCircle();
    }

    void FadeOutAllCircles()
    {
        foreach (SentinelTarget target in FindObjectsOfType<SentinelTarget>())
            target.FadeOutCircle(sentinelSettings.laserFadeOutDuration);
    }

    void SetState(GameState newState)
    {
        currentState = newState;
        if (player != null)
            player.isRedLight = (newState == GameState.RedLight);

        if (sentinelLightRenderer != null)
        {
            if (newState == GameState.GreenLight)
                sentinelLightRenderer.material = greenMaterial;
            else if (newState == GameState.Alert)
                sentinelLightRenderer.material = yellowMaterial != null ? yellowMaterial : redMaterial;
            else if (newState == GameState.RedLight)
                sentinelLightRenderer.material = redMaterial;
            else if (newState == GameState.Release)
                sentinelLightRenderer.material = greenMaterial;
        }
    }
    public void RemoveFromAlreadyShot(GameObject target)
    {
        alreadyShot.Remove(target);
    }
    public void StartGameCycle()
    {
        if (gameStarted) return;
        gameStarted = true;
        StartNewCycle(GameState.GreenLight);
    }

    public bool IsInRedLight()
    {
        return currentState == GameState.RedLight;
    }
}
