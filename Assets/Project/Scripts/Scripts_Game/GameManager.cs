using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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
    private float lastShotTime = 0f;

    // Système de raycasts et LOS
    private Dictionary<GameObject, TargetTrackingData> trackedTargets = new Dictionary<GameObject, TargetTrackingData>();

    private class TargetTrackingData
    {
        public bool wasInLOS;
        public float lostLOSTime;
        public float reacquiredTime;
        public bool canShoot;
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

        foreach (Collider col in targets)
        {
            if (alreadyShot.Contains(col.gameObject)) continue;

            EnemyHealth enemyHealth = col.GetComponent<EnemyHealth>();
            if (enemyHealth != null && enemyHealth.IsRecovering()) continue;

            GrabAttack grabSystem = col.GetComponent<GrabAttack>();
            bool isGrabbing = grabSystem != null && grabSystem.IsGrabbing();

            if (col.gameObject == player.gameObject && player != null)
            {
                PlayerHealth humanHealth = col.GetComponent<PlayerHealth>();
                if (humanHealth != null) { }
                    
            }

            Vector3 targetPos = col.transform.position + Vector3.up * 1f;
            Vector3 direction = (targetPos - sentinelPos).normalized;
            float distance = Vector3.Distance(sentinelPos, targetPos);

            RaycastHit hit;
            bool hasLOS = false;

            if (Physics.Raycast(sentinelPos, direction, out hit, distance, sentinelSettings.obstacleLayers))
            {
                if (hit.collider.gameObject == col.gameObject)
                {
                    hasLOS = true;
                }
                else
                {
                    hasLOS = false;
                }
            }
            else
            {
                hasLOS = true;
            }

            if (!trackedTargets.ContainsKey(col.gameObject))
            {
                trackedTargets[col.gameObject] = new TargetTrackingData();
            }

            TargetTrackingData trackData = trackedTargets[col.gameObject];

            if (hasLOS)
            {
                // Afficher le cercle rouge
                SentinelTarget sentinelTarget = col.GetComponent<SentinelTarget>();
                /*if (sentinelTarget != null && currentState == GameState.RedLight)
                {
                    sentinelTarget.ShowCircle();
                }
                */

                trackData.canShoot = true;
                
                /*if (!trackData.wasInLOS)
                {
                    trackData.reacquiredTime = Time.time;
                    trackData.canShoot = false;
                }
                else
                {
                    if (Time.time - trackData.reacquiredTime >= sentinelSettings.reacquisitionDelay)
                    {
                        trackData.canShoot = true;
                    }
                }*/

                trackData.wasInLOS = true;
            }
            else
            {
                if (trackData.wasInLOS)
                {
                    trackData.lostLOSTime = Time.time;
                }
                trackData.wasInLOS = false;
                trackData.canShoot = false;

                // Cacher le cercle
                SentinelTarget sentinelTarget = col.GetComponent<SentinelTarget>();
                if (sentinelTarget != null)
                {
                    sentinelTarget.HideCircle();
                }
            }

            if (hasLOS && trackData.canShoot)
            {
                Rigidbody rb = col.GetComponent<Rigidbody>();
                MeleeAttackSystem meleeSystem = col.GetComponent<MeleeAttackSystem>();
                if (rb != null)

                {
                    Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
                    Debug.Log($"[SENTINEL CHECK] {col.name} velocity: {horizontalVelocity.magnitude} (threshold: {sentinelSettings.movementThreshold})"); // AJOUTE

                    bool isAttacking = meleeSystem != null && meleeSystem.IsAttacking();
                    bool isMoving = horizontalVelocity.magnitude > sentinelSettings.movementThreshold;
                    
                    // IMMUNITÉ GRAB
                    bool playerImmune = (col.gameObject == player.gameObject && (player.grabState == PlayerPhysicsMovement.GrabState.Grabbed));
                    bool zombieImmune = (grabSystem != null && (grabSystem.isGrabbing || grabSystem.isInBourradeCooldown));

                    bool shouldBeShot = (isMoving || isAttacking) && !playerImmune && !zombieImmune;

                    if (shouldBeShot)
                    {
                        PlayerHealth humanHealth = col.GetComponent<PlayerHealth>();

                        if (enemyHealth != null && !enemyHealth.IsDead())
                        {
                            alreadyShot.Add(col.gameObject);
                            string reason = "MOUVEMENT";
                            if (isGrabbing) reason = "GRAB ACTIF";
                            else if (isAttacking) reason = "ATTAQUE";

                            StartCoroutine(ShootEnemyWithDelay(col.gameObject, enemyHealth, reason, sentinelPos, targetPos));
                        }
                        else if (humanHealth != null && !humanHealth.IsDead())
                        {
                            alreadyShot.Add(col.gameObject);
                            string reason = "MOUVEMENT";
                            if (isAttacking) reason = "ATTAQUE";

                            if (col.gameObject == player.gameObject && !playerAlarmTriggered)
                            {
                                playerAlarmTriggered = true;
                                StartCoroutine(ShootPlayerWithAlarm(col.gameObject, humanHealth, reason, sentinelPos, targetPos));
                            }
                        }
                    }
                }
            }
        }
    }

    // ============================================
    // LASER DE TIR TEMPORAIRE
    // ============================================

    IEnumerator ShowShootLaser(Vector3 from, Vector3 to, float duration)
    {
        if (!sentinelSettings.showLasers) yield break;

        // Créer le laser temporaire
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

        // Fade out
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

        // Détruire
        Destroy(laserObj);
    }

    // ============================================
    // SYSTÈME DE TIR
    // ============================================

    IEnumerator ShootPlayerWithAlarm(GameObject playerObject, PlayerHealth humanHealth, string reason, Vector3 sentinelPos, Vector3 targetPos)
    {
        if (audioSource != null && sentinelSettings.shootSound != null)
            /* audioSource.PlayOneShot(sentinelSettings.shootSound); */

            Debug.Log("ALARME! Joueur detecte! (" + reason + ")");
        yield return new WaitForSeconds(sentinelSettings.shootDelay);

        if (humanHealth != null && !humanHealth.IsDead())
        {
            ShootPlayer(playerObject, humanHealth, reason, sentinelPos, targetPos);
            if (humanHealth.IsDead())
                Debug.Log("GAME OVER!");
        }
    }

    IEnumerator ShootEnemyWithDelay(GameObject enemy, EnemyHealth enemyHealth, string reason, Vector3 sentinelPos, Vector3 targetPos)
    {
        float delay = sentinelSettings.shootDelay;
        yield return new WaitForSeconds(delay);

        while (Time.time - lastShotTime < 0.15f)
            yield return new WaitForSeconds(0.05f);

        if (enemyHealth != null && !enemyHealth.IsDead())
        {
            ShootEnemy(enemy, enemyHealth, reason, sentinelPos, targetPos);
            lastShotTime = Time.time;
        }
    }

    void ShootEnemy(GameObject enemy, EnemyHealth enemyHealth, string reason, Vector3 sentinelPos, Vector3 targetPos)
    {
        Debug.Log("BANG! " + enemy.name + " (" + reason + ")");
        if (audioSource != null && sentinelSettings.shootSound != null)
            audioSource.PlayOneShot(sentinelSettings.shootSound);

        // Flash blanc du cercle
        SentinelTarget sentinelTarget = enemy.GetComponent<SentinelTarget>();
        if (sentinelTarget != null)
        {
            sentinelTarget.FlashWhite();
        }

        // Laser temporaire
        StartCoroutine(ShowShootLaser(sentinelPos, targetPos, sentinelSettings.shootLaserFadeDuration));

        bool isHeadshot = UnityEngine.Random.value < 0.1f;
        enemyHealth.TakeSentinelShot(isHeadshot);

        if (enemyHealth.IsDead())
            Debug.Log(enemy.name + " MORT!");

        StartCoroutine(EnemyStunBySentinel());
    }

    private IEnumerator EnemyStunBySentinel()
    {
        zombieStunBySentinel = true;
        yield return new WaitForSeconds(sentinel.stunZombieDuration);
        zombieStunBySentinel = false;
        // CLEANUP - permettre de retirer les zombies
        alreadyShot.Clear();
    }

    void ShootPlayer(GameObject human, PlayerHealth humanHealth, string reason, Vector3 sentinelPos, Vector3 targetPos)
    {
        Debug.Log("BANG! " + human.name + " (" + reason + ")");
        if (audioSource != null && sentinelSettings.shootSound != null)
            audioSource.PlayOneShot(sentinelSettings.shootSound);

        // Flash blanc du cercle
        SentinelTarget sentinelTarget = human.GetComponent<SentinelTarget>();
        if (sentinelTarget != null)
        {
            sentinelTarget.FlashWhite();
        }

        // Laser temporaire
        StartCoroutine(ShowShootLaser(sentinelPos, targetPos, sentinelSettings.shootLaserFadeDuration));

        StartCoroutine(PlayerStunBySentinel());
        humanHealth.TakeSentinelShot();

        if (humanHealth.IsDead())
            Debug.Log(human.name + " MORT!");
    }

    private IEnumerator PlayerStunBySentinel()
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
        // Pas de délai, tir immédiat car on est déjà à la fin du recoil
        if (humanHealth != null && !humanHealth.IsDead())
        {
            Debug.Log("BANG! Player shot at end of recoil");

            if (audioSource != null && sentinelSettings.shootSound != null)
                audioSource.PlayOneShot(sentinelSettings.shootSound);

            // Flash blanc du cercle
            SentinelTarget sentinelTarget = playerObject.GetComponent<SentinelTarget>();
            if (sentinelTarget != null)
            {
                sentinelTarget.FlashWhite();
            }

            // Laser temporaire
            StartCoroutine(ShowShootLaser(sentinelPos, targetPos, sentinelSettings.shootLaserFadeDuration));

            StartCoroutine(PlayerStunBySentinel());
            humanHealth.TakeSentinelShot();

            if (humanHealth.IsDead())
                Debug.Log(playerObject.name + " MORT!");
        }

        yield return null;
    }
    // ============================================
    // GESTION DES CYCLES
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

            // Cacher tous les cercles
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

            // Fade out des cercles
            FadeOutAllCircles();
        }
    }

    void HideAllCircles()
    {
        SentinelTarget[] allTargets = FindObjectsOfType<SentinelTarget>();
        foreach (SentinelTarget target in allTargets)
        {
            target.HideCircle();
        }
    }

    void FadeOutAllCircles()
    {
        SentinelTarget[] allTargets = FindObjectsOfType<SentinelTarget>();
        foreach (SentinelTarget target in allTargets)
        {
            target.FadeOutCircle(sentinelSettings.laserFadeOutDuration);
        }
    }

    void SetState(GameState newState)
    {
        currentState = newState;
        if (player == null) return;

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