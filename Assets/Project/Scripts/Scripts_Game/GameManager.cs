using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public enum GameState { GreenLight, Alert, RedLight, Blocked }

    [Header("Parametres du Cycle")]
    public float minGreenTime = 5f;
    public float maxGreenTime = 10f;
    public float minRedTime = 2f;
    public float maxRedTime = 4f;

    [Header("References")]
    public PlayerPhysicsMovement player;
    public HumanHealth playerHealth;
    public Renderer sentinelLightRenderer;
    public Material greenMaterial;
    public Material redMaterial;

    [Header("Audio")]
    public AudioClip alertBuzzerClip;
    public AudioClip blockedSoundClip;
    public AudioClip gunshotClip;
    public AudioClip detectionAlarmClip;
    private AudioSource audioSource;

    [Header("Systeme de Tir")]
    public float headshotChance = 0.1f;
    public float detectionCheckInterval = 0.05f;
    public float velocityThreshold = 0.02f;
    public float playerAlarmDuration = 0.3f;
    public float botShootFixedDelay = 0.4f;
    public float botShootRandomDelay = 0.4f;
    public float minTimeBetweenShots = 0.15f;

    [Header("Bot Detection")]
    public LayerMask zombieLayer;
    public float detectionRadius = 50f;

    [Header("Start System")]
    public bool waitForStart = true;

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
        if (player == null)
        {
            Debug.LogError("GameManager: PlayerPhysicsMovement non assigne!");
        }

        if (playerHealth == null)
        {
            playerHealth = player != null ? player.GetComponent<HumanHealth>() : null;
            if (playerHealth == null)
            {
                Debug.LogError("GameManager: HumanHealth non trouve sur le joueur!");
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
        if (!gameStarted) return;

        cycleTimer += Time.deltaTime;

        if (currentState == GameState.Blocked)
        {
            if (audioSource != null && !audioSource.isPlaying)
            {
                StopAndResetCycle(player);
                return;
            }
            return;
        }

        if (currentState == GameState.Alert)
        {
            bool audioFinished = (audioSource != null && !audioSource.isPlaying) || (audioSource == null || alertBuzzerClip == null);

            if (audioFinished)
            {
                StartNewCycle(GameState.RedLight);
                return;
            }
            return;
        }

        if (currentState == GameState.RedLight)
        {
            detectionTimer += Time.deltaTime;

            if (detectionTimer >= detectionCheckInterval)
            {
                detectionTimer = 0f;
                CheckForMovingZombies();
            }
        }

        if (cycleTimer >= targetDuration)
        {
            if (currentState == GameState.GreenLight)
            {
                StartNewCycle(GameState.Alert);
                return;
            }
            else if (currentState == GameState.RedLight)
            {
                StartNewCycle(GameState.GreenLight);
                return;
            }
        }
    }

    void CheckForMovingZombies()
    {
        int combinedLayers = zombieLayer | LayerMask.GetMask("Human");
        Collider[] zombies = Physics.OverlapSphere(transform.position, detectionRadius, combinedLayers);

        foreach (Collider col in zombies)
        {
            if (alreadyShot.Contains(col.gameObject)) continue;

            // Ignorer les zombies en récupération après un tir
            ZombieHealth zombieHealth = col.GetComponent<ZombieHealth>();
            if (zombieHealth != null && zombieHealth.IsRecovering()) continue;

            // NOUVEAU : Vérifier si le zombie est en train de grab
            ZombieGrabSystem grabSystem = col.GetComponent<ZombieGrabSystem>();
            bool isGrabbing = grabSystem != null && grabSystem.IsGrabbing();

            // NOUVEAU : Vérifier si l'entité est en knockback (grace period)
            bool isInKnockbackGrace = false;
            if (grabSystem != null)
            {
                isInKnockbackGrace = grabSystem.IsInKnockbackGracePeriod();
            }
            // Vérifier aussi pour le joueur
            if (col.gameObject == player.gameObject && player != null)
            {
                HumanHealth humanHealth = col.GetComponent<HumanHealth>();
                if (humanHealth != null)
                {
                    isInKnockbackGrace = humanHealth.IsInKnockbackGracePeriod();
                }
            }

            // Si en grace period de knockback, ignorer la détection
            if (isInKnockbackGrace) continue;

            Rigidbody rb = col.GetComponent<Rigidbody>();
            MeleeAttackSystem meleeSystem = col.GetComponent<MeleeAttackSystem>();

            if (rb != null)
            {
                Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
                bool isStunned = meleeSystem != null && meleeSystem.IsStunned();
                bool isAttacking = meleeSystem != null && meleeSystem.IsAttacking();

                bool isMoving = horizontalVelocity.magnitude > velocityThreshold;

                // MODIFIE : Ajouter isGrabbing aux conditions
                bool shouldBeShot = isMoving || isStunned || isAttacking || isGrabbing;

                if (shouldBeShot)
                {
                    HumanHealth humanHealth = col.GetComponent<HumanHealth>();

                    if (zombieHealth != null && !zombieHealth.IsDead())
                    {
                        alreadyShot.Add(col.gameObject);

                        string reason = "MOUVEMENT";
                        if (isGrabbing) reason = "GRAB ACTIF"; // NOUVEAU
                        else if (isStunned) reason = "ETOURDI";
                        else if (isAttacking) reason = "ATTAQUE";

                        // Si le zombie est en train de grab, le tirer immédiatement libère le joueur
                        if (isGrabbing)
                        {
                            grabSystem.ForceRelease(); // Libérer immédiatement
                            Debug.Log($"Zombie {col.gameObject.name} tire pendant un grab - liberation du joueur!");
                        }

                        StartCoroutine(ShootBotWithDelay(col.gameObject, zombieHealth, reason));
                    }
                    else if (humanHealth != null && !humanHealth.IsDead())
                    {
                        alreadyShot.Add(col.gameObject);

                        string reason = "MOUVEMENT";
                        if (isStunned) reason = "ETOURDI";
                        else if (isAttacking) reason = "ATTAQUE";

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

    IEnumerator ShootPlayerWithAlarm(GameObject playerObject, HumanHealth humanHealth, string reason)
    {
        if (audioSource != null && detectionAlarmClip != null)
        {
            audioSource.PlayOneShot(detectionAlarmClip);
        }

        Debug.Log("ALARME! Joueur detecte! (" + reason + ")");

        yield return new WaitForSeconds(playerAlarmDuration);

        if (humanHealth != null && !humanHealth.IsDead())
        {
            ShootHuman(playerObject, humanHealth, reason);

            if (humanHealth.IsDead())
            {
                Debug.Log("GAME OVER!");
                InterruptCycleAndStartBlock(player);
            }
        }
    }

    IEnumerator ShootBotWithDelay(GameObject bot, ZombieHealth zombieHealth, string reason)
    {
        float totalDelay = botShootFixedDelay + Random.Range(0f, botShootRandomDelay);
        yield return new WaitForSeconds(totalDelay);

        while (Time.time - lastShotTime < minTimeBetweenShots)
        {
            yield return new WaitForSeconds(0.05f);
        }

        if (zombieHealth != null && !zombieHealth.IsDead())
        {
            ShootZombie(bot, zombieHealth, reason);
            lastShotTime = Time.time;
        }
    }

    void ShootZombie(GameObject zombie, ZombieHealth zombieHealth, string reason)
    {
        Debug.Log("BANG! " + zombie.name + " (" + reason + ")");

        if (audioSource != null && gunshotClip != null)
        {
            audioSource.PlayOneShot(gunshotClip);
        }

        bool isHeadshot = Random.value < headshotChance;
        zombieHealth.TakeDamage(isHeadshot);

        if (zombieHealth.IsDead())
        {
            Debug.Log(zombie.name + " MORT!");
        }
    }

    void ShootHuman(GameObject human, HumanHealth humanHealth, string reason)
    {
        Debug.Log("BANG! " + human.name + " (" + reason + ")");

        if (audioSource != null && gunshotClip != null)
        {
            audioSource.PlayOneShot(gunshotClip);
        }

        humanHealth.TakeDamage();

        if (humanHealth.IsDead())
        {
            Debug.Log(human.name + " MORT!");
        }
    }

    void InterruptCycleAndStartBlock(PlayerPhysicsMovement targetPlayer)
    {
        SetState(GameState.Blocked);
        cycleTimer = 0f;
        targetDuration = 0f;

        if (audioSource != null && blockedSoundClip != null)
        {
            audioSource.Stop();
            audioSource.PlayOneShot(blockedSoundClip);
        }

        if (targetPlayer != null)
        {
            targetPlayer.enabled = false;

            Rigidbody rb = targetPlayer.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    void StopAndResetCycle(PlayerPhysicsMovement targetPlayer)
    {
        if (targetPlayer != null)
        {
            targetPlayer.ResetMovementState();
        }

        StartNewCycle(GameState.GreenLight);
    }

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
            targetDuration = Random.Range(minGreenTime, maxGreenTime);
            playerAlarmTriggered = false;
            alreadyShot.Clear();
            detectionTimer = 0f;
        }
        else if (newState == GameState.Alert)
        {
            targetDuration = 0f;
            if (audioSource != null && alertBuzzerClip != null)
            {
                audioSource.PlayOneShot(alertBuzzerClip);
            }
        }
        else if (newState == GameState.RedLight)
        {
            targetDuration = Random.Range(minRedTime, maxRedTime);
            alreadyShot.Clear();
            playerAlarmTriggered = false;
        }
        else if (newState == GameState.Blocked)
        {
            targetDuration = 0f;
        }
    }

    void SetState(GameState newState)
    {
        currentState = newState;

        if (player == null) return;

        player.isRedLight = (newState == GameState.RedLight);

        if (sentinelLightRenderer != null)
        {
            sentinelLightRenderer.material = (newState == GameState.Alert ||
                                           newState == GameState.RedLight ||
                                           newState == GameState.Blocked)
                                           ? redMaterial
                                           : greenMaterial;
        }
    }

    public void StartGameCycle()
    {
        if (gameStarted) return;

        gameStarted = true;
        StartNewCycle(GameState.GreenLight);
    }

    // NOUVEAU : Getter public pour l'état
    public bool IsInRedLight()
    {
        return currentState == GameState.RedLight;
    }
}