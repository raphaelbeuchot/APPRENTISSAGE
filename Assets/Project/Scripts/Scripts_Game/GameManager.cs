using System.Collections;
using Unity.Cinemachine.Samples;
using UnityEngine;

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
    [SerializeField] public SentinelLaserManager laserManager;

    public Renderer sentinelLightRenderer;
    public Material greenMaterial;
    public Material redMaterial;
    public Material yellowMaterial;

    [Header("Sentinel Eye")]
    public Transform sentinelEye;

    [Header("Start System")]
    public bool waitForStart = true;
    public bool zombieStunBySentinel = false;

    [Header("Enemy Tracking")]
    [Tooltip("Coche : un ennemi desactive au lancement puis active en cours de partie (trigger de salle) s'ajoute au total, " +
             "a la liste a nettoyer et a la barre d'icones au moment ou il s'active. Laisser decoche au boss (il gere sa barre).")]
    public bool addEnemiesActivatedLater = false;
    public int totalEnemies = 0;

    // Ennemis deja comptes (lancement + ajoutes ensuite) : evite les doublons, l'ordre des Start n'etant pas garanti
    private readonly System.Collections.Generic.HashSet<EnemyHealth> countedEnemies = new System.Collections.Generic.HashSet<EnemyHealth>();
    private bool startCountDone;
    public int enemiesKilled = 0;

    private SentinelDetector detector;
    private SentinelShooter shooter;

    // ============================================
    // UNITY LIFECYCLE
    // ============================================

    void Start()
    {
        if (sentinelSettings == null)
            return;

        if (playerHealth == null)
            playerHealth = player != null ? player.GetComponent<PlayerHealth>() : null;

        detector = GetComponent<SentinelDetector>();
        shooter = GetComponent<SentinelShooter>();
        detector.Initialize(this, shooter);
        shooter.Initialize(this, detector);

        Time.timeScale = 1f;
        CountEnemiesAtStart();
    }

    void Update()
    {
        if (sentinelCycleManager == null || !sentinelCycleManager.IsGameStarted() || sentinelSettings == null)
            return;

        if (sentinelCycleManager.IsInRedLight())
            detector.Tick();
    }

    // ============================================
    // CYCLES DE JEU
    // ============================================

    public void StartGameCycle()
    {
        if (sentinelCycleManager != null)
            sentinelCycleManager.StartGameCycle();
    }

    public bool IsInRedLight()
    {
        return sentinelCycleManager != null && sentinelCycleManager.IsInRedLight();
    }

    void CountEnemiesAtStart()
    {
        foreach (EnemyHealth enemy in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
            if (countedEnemies.Add(enemy))
                totalEnemies++;
        startCountDone = true;
    }

    // Appele par EnemyHealth.Start. Un ennemi deja actif au lancement est compte par CountEnemiesAtStart
    // (doublon ignore, quel que soit l'ordre des Start) ; un ennemi active plus tard n'est ajoute que si
    // addEnemiesActivatedLater est coche : total, liste a nettoyer, une icone de plus sur chaque barre.
    public static void RegisterEnemyActivatedLater(EnemyHealth enemy)
    {
        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm == null || !gm.addEnemiesActivatedLater || !gm.startCountDone) return;
        if (!gm.countedEnemies.Add(enemy)) return;

        gm.totalEnemies++;
        bool added = CleaningBonusManager.Instance != null && CleaningBonusManager.Instance.RegisterEnemy(enemy);
        if (added)
            foreach (EnemyIconsUI bar in FindObjectsByType<EnemyIconsUI>(FindObjectsSortMode.None))
                bar.AddIconForEnemy(enemy);
        Debug.Log("[GameManager] Ennemi active en cours de partie : " + enemy.name + " (total " + gm.totalEnemies + ")");
    }

    public void OnEnemyKilled()
    {
        enemiesKilled++;
        LevelStatsTracker.Instance?.RegisterKill();
    }

    // ============================================
    // FORWARDING VERS SENTINELDETECTOR
    // ============================================

    public void RemoveFromAlreadyShot(GameObject target) => detector.RemoveFromAlreadyShot(target);
    public void ResetAllTracking() => detector.ResetAllTracking();
    public void ForceScheduleShot(GameObject enemy) => detector.ForceScheduleShot(enemy);
    public void NotifyStandUpDetected(GameObject enemy) => detector.NotifyStandUpDetected(enemy);
    public void ResetSequenceTarget(GameObject enemy) => detector.ResetSequenceTarget(enemy);
    public bool IsPlayerInSentinelLOS() => detector.IsPlayerInSentinelLOS();

    // ============================================
    // FORWARDING VERS SENTINELSHOOTER
    // ============================================

    public void ExecutePlayerShotOnGroggy(PlayerPhysicsMovement playerMovement) => shooter.ExecutePlayerShotOnGroggy(playerMovement);
    public void ExecuteSequenceShot(GameObject enemy) => shooter.ExecuteSequenceShot(enemy);
    public IEnumerator ShootPlayerAtEndOfRecoil(GameObject playerObject, PlayerHealth humanHealth, Vector3 sentinelPos, Vector3 targetPos)
        => shooter.ShootPlayerAtEndOfRecoil(playerObject, humanHealth, sentinelPos, targetPos);

    // ============================================
    // UTILITAIRES
    // ============================================

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

    public int GetTotalEnemies() => totalEnemies;
    public int GetEnemiesKilled() => enemiesKilled;
}
