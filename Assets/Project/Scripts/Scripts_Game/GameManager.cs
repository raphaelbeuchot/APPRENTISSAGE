using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public enum GameState { GreenLight, Alert, RedLight, Release }

    [Header("Sentinel Settings")]
    public SentinelSettings sentinelSettings; // Reference au ScriptableObject

    [Header("References")]
    public PlayerPhysicsMovement player;
    public PlayerHealth playerHealth;
    public Renderer sentinelLightRenderer;
    public Material greenMaterial;
    public Material redMaterial;
    public Material yellowMaterial; // Pour Alert

    [Header("Audio")]
    private AudioSource audioSource;

    [Header("Start System")]
    public bool waitForStart = true;

    // State
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
        {
            Debug.LogError("GameManager: PlayerPhysicsMovement non assigne!");
        }

        if (playerHealth == null)
        {
            playerHealth = player != null ? player.GetComponent<PlayerHealth>() : null;
            if (playerHealth == null)
            {
                Debug.LogError("GameManager: PlayerHealth non trouve sur le joueur!");
            }
        }

        audioSource = GetComponent<AudioSource>();
        Time.timeScale = 1f;

        if (waitForStart)
        {
            gameStarted = false;
        }
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

        // STATE: ALERT (attendre la fin du son)
        if (currentState == GameState.Alert)
        {
            bool audioFinished = (audioSource != null && !audioSource.isPlaying) ||
                                (audioSource == null || sentinelSettings.alertSound == null);

            if (audioFinished || cycleTimer >= sentinelSettings.alertDuration)
            {
                StartNewCycle(GameState.RedLight);
                return;
            }
            return;
        }

        // STATE: REDLIGHT (detection et tir)
        if (currentState == GameState.RedLight)
        {
            detectionTimer += Time.deltaTime;

            if (detectionTimer >= sentinelSettings.redlightScanInterval)
            {
                detectionTimer = 0f;
                CheckForMovingTargets();
            }
        }

        // STATE: RELEASE (transition vers GreenLight)
        if (currentState == GameState.Release)
        {
            if (cycleTimer >= sentinelSettings.releaseDuration)
            {
                StartNewCycle(GameState.GreenLight);
                return;
            }
            return;
        }

        // Changement de cycle
        if (cycleTimer >= targetDuration)
        {
            if (currentState == GameState.GreenLight)
            {
                StartNewCycle(GameState.Alert);
                return;
            }
            else if (currentState == GameState.RedLight)
            {
                StartNewCycle(GameState.Release);
                return;
            }
        }
    }

    // ===============================================
    // DETECTION ET TIR
    // ===============================================

    void CheckForMovingTargets()
    {
        Collider[] targets = Physics.OverlapSphere(
            transform.position,
            sentinelSettings.detectionRadius,
            sentinelSettings.targetLayers
        );

        foreach (Collider col in targets)
        {
            if (alreadyShot.Contains(col.gameObject)) continue;

            // Ignorer les zombies en recuperation apres un tir
            ZombieHealth zombieHealth = col.GetComponent<ZombieHealth>();
            if (zombieHealth != null && zombieHealth.IsRecovering()) continue;

            // Verifier si le zombie est en train de grab
            ZombieGrabSystem grabSystem = col.GetComponent<ZombieGrabSystem>();
            bool isGrabbing = grabSystem != null && grabSystem.IsGrabbing();

            // Verifier si l'entite est en knockback (grace period)
            bool isInKnockbackGrace = false;
            if (grabSystem != null)
            {
                isInKnockbackGrace = grabSystem.IsInKnockbackGracePeriod();
            }
            // Verifier aussi pour le joueur
            if (col.gameObject == player.gameObject && player != null)
            {
                PlayerHealth humanHealth = col.GetComponent<PlayerHealth>();
                if (humanHealth != null)
                {
                    isInKnockbackGrace = humanHealth.IsInKnockbackGracePeriod();
                }
            }

            // Si en grace period de knockback, ignorer la detection
            if (isInKnockbackGrace) continue;

            Rigidbody rb = col.GetComponent<Rigidbody>();
            MeleeAttackSystem meleeSystem = col.GetComponent<MeleeAttackSystem>();

            if (rb != null)
            {
                Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
                bool isAttacking = meleeSystem != null && meleeSystem.IsAttacking();

                bool isMoving = horizontalVelocity.magnitude > sentinelSettings.movementThreshold;

                // Conditions de tir
                bool shouldBeShot = isMoving || isAttacking || isGrabbing;

                if (shouldBeShot)
                {
                    PlayerHealth humanHealth = col.GetComponent<PlayerHealth>();

                    // ZOMBIE
                    if (zombieHealth != null && !zombieHealth.IsDead())
                    {
                        alreadyShot.Add(col.gameObject);

                        string reason = "MOUVEMENT";
                        if (isGrabbing) reason = "GRAB ACTIF";
                        else if (isAttacking) reason = "ATTAQUE";

                        // Si le zombie est en train de grab, le tirer immediatement libere le joueur
                        if (isGrabbing && sentinelSettings.shootGrabbingZombiesInRedlight)
                        {
                            grabSystem.ForceRelease();
                            Debug.Log($"Zombie {col.gameObject.name} tire pendant un grab - liberation du joueur!");
                        }

                        StartCoroutine(ShootZombieWithDelay(col.gameObject, zombieHealth, reason));
                    }
                    // JOUEUR
                    else if (humanHealth != null && !humanHealth.IsDead())
                    {
                        alreadyShot.Add(col.gameObject);

                        string reason = "MOUVEMENT";
                        if (isAttacking) reason = "ATTAQUE";

                        if (col.gameObject == player.gameObject)
                        {
                            if (!playerAlarmTriggered)
                            {
                                playerAlarmTriggered = true;
                                StartCoroutine(ShootPlayerWithAlarm(col.gameObject, humanHealth, reason));
                            }
                        }
                    }
                }
            }
        }
    }

    IEnumerator ShootPlayerWithAlarm(GameObject playerObject, PlayerHealth humanHealth, string reason)
    {
        if (audioSource != null && sentinelSettings.shootSound != null)
        {
            audioSource.PlayOneShot(sentinelSettings.shootSound);
        }

        Debug.Log("ALARME! Joueur detecte! (" + reason + ")");

        // Optionnel : Cutscene / Slow-mo
        if (sentinelSettings.enableDetectionCutscene)
        {
            Time.timeScale = sentinelSettings.slowMoTimeScale;
            yield return new WaitForSecondsRealtime(sentinelSettings.slowMoDuration);
            Time.timeScale = 1f;
        }

        yield return new WaitForSeconds(sentinelSettings.shootDelay);

        if (humanHealth != null && !humanHealth.IsDead())
        {
            ShootPlayer(playerObject, humanHealth, reason);

            if (humanHealth.IsDead())
            {
                Debug.Log("GAME OVER!");
                // Tu peux gerer le game over ici
            }
        }
    }

    IEnumerator ShootZombieWithDelay(GameObject zombie, ZombieHealth zombieHealth, string reason)
    {
        float delay = sentinelSettings.shootDelay;
        yield return new WaitForSeconds(delay);

        while (Time.time - lastShotTime < 0.15f) // minTimeBetweenShots
        {
            yield return new WaitForSeconds(0.05f);
        }

        if (zombieHealth != null && !zombieHealth.IsDead())
        {
            ShootZombie(zombie, zombieHealth, reason);
            lastShotTime = Time.time;
        }
    }

    void ShootZombie(GameObject zombie, ZombieHealth zombieHealth, string reason)
    {
        Debug.Log("BANG! " + zombie.name + " (" + reason + ")");

        if (audioSource != null && sentinelSettings.shootSound != null)
        {
            audioSource.PlayOneShot(sentinelSettings.shootSound);
        }

        bool isHeadshot = UnityEngine.Random.value < 0.1f; // Tu peux mettre ca dans settings
        zombieHealth.TakeSentinelShot(isHeadshot);

        if (zombieHealth.IsDead())
        {
            Debug.Log(zombie.name + " MORT!");
        }
    }

    void ShootPlayer(GameObject human, PlayerHealth humanHealth, string reason)
    {
        Debug.Log("BANG! " + human.name + " (" + reason + ")");

        if (audioSource != null && sentinelSettings.shootSound != null)
        {
            audioSource.PlayOneShot(sentinelSettings.shootSound);
        }

        humanHealth.TakeSentinelShot();

        if (humanHealth.IsDead())
        {
            Debug.Log(human.name + " MORT!");
        }
    }

    // ===============================================
    // GESTION DES CYCLES
    // ===============================================

    void StartNewCycle(GameState newState)
    {
        SetState(newState);
        cycleTimer = 0f;

        if (newState == GameState.GreenLight)
        {
            if (player != null)
            {
                player.enabled = true;
            }
            targetDuration = sentinelSettings.GetRandomGreenlightDuration();
            playerAlarmTriggered = false;
            alreadyShot.Clear();
            detectionTimer = 0f;
        }
        else if (newState == GameState.Alert)
        {
            targetDuration = sentinelSettings.alertDuration;
            if (audioSource != null && sentinelSettings.alertSound != null)
            {
                audioSource.PlayOneShot(sentinelSettings.alertSound);
            }
        }
        else if (newState == GameState.RedLight)
        {
            targetDuration = sentinelSettings.redlightDuration;
            alreadyShot.Clear();
            playerAlarmTriggered = false;

            if (audioSource != null && sentinelSettings.redlightSound != null)
            {
                audioSource.PlayOneShot(sentinelSettings.redlightSound);
            }
        }
        else if (newState == GameState.Release)
        {
            targetDuration = sentinelSettings.releaseDuration;

            if (audioSource != null && sentinelSettings.releaseSound != null)
            {
                audioSource.PlayOneShot(sentinelSettings.releaseSound);
            }
        }
    }

    void SetState(GameState newState)
    {
        currentState = newState;

        if (player == null) return;

        player.isRedLight = (newState == GameState.RedLight);

        // Changement de couleur des lumieres
        if (sentinelLightRenderer != null)
        {
            if (newState == GameState.GreenLight)
            {
                sentinelLightRenderer.material = greenMaterial;
            }
            else if (newState == GameState.Alert)
            {
                sentinelLightRenderer.material = yellowMaterial != null ? yellowMaterial : redMaterial;
            }
            else if (newState == GameState.RedLight)
            {
                sentinelLightRenderer.material = redMaterial;
            }
            else if (newState == GameState.Release)
            {
                sentinelLightRenderer.material = greenMaterial;
            }
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