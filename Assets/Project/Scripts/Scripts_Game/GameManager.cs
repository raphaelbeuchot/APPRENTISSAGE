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
                CheckForMovingTargets();
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

    void CheckForMovingTargets()
    {
        Collider[] targets = Physics.OverlapSphere(transform.position, sentinelSettings.detectionRadius, sentinelSettings.targetLayers);

        foreach (Collider col in targets)
        {
            if (alreadyShot.Contains(col.gameObject)) continue;

            // CORRECTION BUG 1 : Utiliser EnemyHealth au lieu de ZombieHealth
            EnemyHealth enemyHealth = col.GetComponent<EnemyHealth>();
            if (enemyHealth != null && enemyHealth.IsRecovering()) continue;

            GrabAttack grabSystem = col.GetComponent<GrabAttack>();
            bool isGrabbing = grabSystem != null && grabSystem.IsGrabbing();
            bool isInKnockbackGrace = grabSystem != null && grabSystem.IsInBourrade();

            if (col.gameObject == player.gameObject && player != null)
            {
                PlayerHealth humanHealth = col.GetComponent<PlayerHealth>();
                if (humanHealth != null)
                    isInKnockbackGrace = humanHealth.IsInKnockbackGracePeriod();
            }

            Rigidbody rb = col.GetComponent<Rigidbody>();
            MeleeAttackSystem meleeSystem = col.GetComponent<MeleeAttackSystem>();

            if (rb != null)
            {
                Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
                bool isAttacking = meleeSystem != null && meleeSystem.IsAttacking();
                bool isMoving = horizontalVelocity.magnitude > sentinelSettings.movementThreshold;

                bool shouldBeShot = isMoving || isAttacking;

                if (shouldBeShot)
                {
                    PlayerHealth humanHealth = col.GetComponent<PlayerHealth>();

                    // CORRECTION BUG 1 : Utiliser enemyHealth au lieu de zombieHealth
                    if (enemyHealth != null && !enemyHealth.IsDead())
                    {
                        alreadyShot.Add(col.gameObject);
                        string reason = "MOUVEMENT";
                        if (isGrabbing) reason = "GRAB ACTIF";
                        else if (isAttacking) reason = "ATTAQUE";

                        StartCoroutine(ShootEnemyWithDelay(col.gameObject, enemyHealth, reason));
                    }
                    else if (humanHealth != null && !humanHealth.IsDead())
                    {
                        alreadyShot.Add(col.gameObject);
                        string reason = "MOUVEMENT";
                        if (isAttacking) reason = "ATTAQUE";

                        if (col.gameObject == player.gameObject && !playerAlarmTriggered)
                        {
                            playerAlarmTriggered = true;
                            StartCoroutine(ShootPlayerWithAlarm(col.gameObject, humanHealth, reason));
                        }
                    }
                }
            }
        }
    }

    IEnumerator ShootPlayerWithAlarm(GameObject playerObject, PlayerHealth humanHealth, string reason)
    {
        if (audioSource != null && sentinelSettings.shootSound != null)
            /* audioSource.PlayOneShot(sentinelSettings.shootSound); */

            Debug.Log("ALARME! Joueur detecte! (" + reason + ")");
        yield return new WaitForSeconds(sentinelSettings.shootDelay);

        if (humanHealth != null && !humanHealth.IsDead())
        {
            ShootPlayer(playerObject, humanHealth, reason);
            if (humanHealth.IsDead())
                Debug.Log("GAME OVER!");
        }
    }

    // CORRECTION BUG 1 : Renommer et utiliser EnemyHealth
    IEnumerator ShootEnemyWithDelay(GameObject enemy, EnemyHealth enemyHealth, string reason)
    {
        float delay = sentinelSettings.shootDelay;
        yield return new WaitForSeconds(delay);

        while (Time.time - lastShotTime < 0.15f)
            yield return new WaitForSeconds(0.05f);

        if (enemyHealth != null && !enemyHealth.IsDead())
        {
            ShootEnemy(enemy, enemyHealth, reason);
            lastShotTime = Time.time;
        }
    }

    // CORRECTION BUG 1 : Renommer et utiliser EnemyHealth
    void ShootEnemy(GameObject enemy, EnemyHealth enemyHealth, string reason)
    {
        Debug.Log("BANG! " + enemy.name + " (" + reason + ")");
        if (audioSource != null && sentinelSettings.shootSound != null)
            audioSource.PlayOneShot(sentinelSettings.shootSound);

        bool isHeadshot = UnityEngine.Random.value < 0.1f;
        enemyHealth.TakeSentinelShot(isHeadshot);

        if (enemyHealth.IsDead())
            Debug.Log(enemy.name + " MORT!");

        StartCoroutine(EnemyStunBySentinel());
    }

    // CORRECTION : Renommer pour clarté
    private IEnumerator EnemyStunBySentinel()
    {
        zombieStunBySentinel = true;
        yield return new WaitForSeconds(sentinel.stunZombieDuration);
        zombieStunBySentinel = false;
    }

    void ShootPlayer(GameObject human, PlayerHealth humanHealth, string reason)
    {
        Debug.Log("BANG! " + human.name + " (" + reason + ")");
        if (audioSource != null && sentinelSettings.shootSound != null)
            audioSource.PlayOneShot(sentinelSettings.shootSound);

        StartCoroutine(PlayerStunBySentinel());
        humanHealth.TakeSentinelShot();

        if (humanHealth.IsDead())
            Debug.Log(human.name + " MORT!");
    }

    // CORRECTION BUG 2 : Retirer le joueur de alreadyShot après recovery
    private IEnumerator PlayerStunBySentinel()
    {
        stunBySentinel = true;
        yield return new WaitForSeconds(sentinel.stunDuration);
        stunBySentinel = false;

        // BUG FIX : Retirer le joueur de la liste pour qu'il puisse se faire tirer dessus à nouveau
        if (player != null)
        {
            alreadyShot.Remove(player.gameObject);
            playerAlarmTriggered = false;
            Debug.Log("Player recovery complete - can be shot again if moves");
        }
    }

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