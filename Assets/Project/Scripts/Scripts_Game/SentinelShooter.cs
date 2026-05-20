using System.Collections;
using UnityEngine;

public class SentinelShooter : MonoBehaviour
{
    private GameManager gm;
    private SentinelDetector detector;
    private AudioSource audioSource;

    public void Initialize(GameManager gameManager, SentinelDetector sentinelDetector)
    {
        gm = gameManager;
        detector = sentinelDetector;
        audioSource = GetComponent<AudioSource>();
    }

    // ============================================
    // METHODES DE TIR
    // ============================================

    public void ShootEnemy(GameObject enemy, EnemyHealth enemyHealth, string reason, Vector3 sentinelPos, Vector3 targetPos, bool isHeadshot = false)
    {
        if (audioSource != null && gm.sentinelSettings.shootSound != null)
            audioSource.PlayOneShot(gm.sentinelSettings.shootSound);

        Vector3 currentTargetPos = SentinelTargetGeometry.GetTargetCenter(enemy);

        EnemyAI_AStar ai = enemy.GetComponent<EnemyAI_AStar>();
        if (ai != null)
        {
            if (ai.isKnockedDownByEpervier)
                ai.wasAlreadyShotDuringSweep = true;
            StartCoroutine(StunSpecificZombie(ai));
        }

        SentinelTarget sentinelTarget = enemy.GetComponent<SentinelTarget>();
        if (sentinelTarget != null) sentinelTarget.FlashWhite();

        if (gm.laserManager != null)
            gm.laserManager.TriggerShotAnimation(sentinelPos, currentTargetPos);

        enemyHealth.TakeSentinelShot(isHeadshot);
        EnemyDetectionFeedback enemyFeedback = enemy.GetComponent<EnemyDetectionFeedback>();
        if (enemyFeedback != null) enemyFeedback.OnShotBySentinel();

        if (enemyHealth.IsDead())
            Debug.Log(enemy.name + " MORT!");
    }

    IEnumerator StunSpecificZombie(EnemyAI_AStar ai)
    {
        ai.isStunnedBySentinel = true;
        yield return new WaitForSeconds(gm.sentinel.stunZombieDuration);

        if (ai == null) yield break;

        ai.isStunnedBySentinel = false;
        detector.alreadyShot.Remove(ai.gameObject);

        if (detector.trackedTargets.ContainsKey(ai.gameObject))
        {
            SentinelDetector.TargetTrackingData td = detector.trackedTargets[ai.gameObject];
            td.lastShotTime = Time.time - gm.sentinelSettings.shootCooldown;
            td.isBeingShot = false;
            td.shootScheduledTime = -1f;
            td.wasInLOS = false;
            td.consecutiveLOSScans = 0;
            td.lastCheckPosition = ai.transform.position;
            td.lastCheckTime = Time.time;
        }

        ai.lastPathDestination = Vector3.positiveInfinity;
    }

    public void ShootPlayer(GameObject human, PlayerHealth humanHealth, string reason, Vector3 sentinelPos, Vector3 targetPos, bool isHeadshot = false)
    {
        Vector3 currentTargetPos = SentinelTargetGeometry.GetTargetCenter(human);
        Vector3 direction = (currentTargetPos - sentinelPos).normalized;
        float distance = Vector3.Distance(sentinelPos, currentTargetPos);

        RaycastHit hit;
        if (Physics.Raycast(sentinelPos, direction, out hit, distance, gm.sentinelSettings.obstacleLayers))
        {
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

        if (audioSource != null && gm.sentinelSettings.shootSound != null)
            audioSource.PlayOneShot(gm.sentinelSettings.shootSound);

        SentinelTarget sentinelTarget = human.GetComponent<SentinelTarget>();
        if (sentinelTarget != null) sentinelTarget.FlashWhite();

        if (gm.laserManager != null)
            gm.laserManager.TriggerShotAnimation(sentinelPos, currentTargetPos);

        if (gm.playerDetectionFeedback != null)
            gm.playerDetectionFeedback.OnShotBySentinel();

        if (gm.playerDetectionFeedback != null)
            gm.playerDetectionFeedback.OnNoLongerDetected();

        StartCoroutine(PlayerStunBySentinel());
        humanHealth.TakeSentinelShot(sentinelPos);
    }

    IEnumerator PlayerStunBySentinel()
    {
        gm.stunBySentinel = true;
        yield return new WaitForSeconds(gm.sentinel.stunDuration);
        gm.stunBySentinel = false;

        if (gm.player != null)
        {
            detector.alreadyShot.Remove(gm.player.gameObject);
            detector.playerAlarmTriggered = false;
        }
    }

    public IEnumerator ShootPlayerAtEndOfRecoil(GameObject playerObject, PlayerHealth humanHealth, Vector3 sentinelPos, Vector3 targetPos)
    {
        if (humanHealth != null && !humanHealth.IsDead())
        {
            Vector3 currentPos = SentinelTargetGeometry.GetTargetCenter(playerObject);

            if (gm.laserManager != null)
                gm.laserManager.TriggerShotAnimation(sentinelPos, currentPos);

            if (audioSource != null && gm.sentinelSettings.shootSound != null)
                audioSource.PlayOneShot(gm.sentinelSettings.shootSound);

            SentinelTarget sentinelTarget = playerObject.GetComponent<SentinelTarget>();
            if (sentinelTarget != null) sentinelTarget.FlashWhite();

            StartCoroutine(PlayerStunBySentinel());
            humanHealth.TakeSentinelShot(sentinelPos);
        }

        yield return null;
    }

    // ============================================
    // COVER / RICOCHET
    // ============================================

    public void ResolveCoverOrRicochet(Vector3 sentinelPos, Vector3 lastKnownPos, bool isHeadshot)
    {
        Vector3 dir = (lastKnownPos - sentinelPos).normalized;
        RaycastHit obstacleHit;
        if (!Physics.Raycast(sentinelPos, dir, out obstacleHit, 100f, gm.sentinelSettings.obstacleLayers))
            return;

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        int zombieLayer = LayerMask.NameToLayer("Zombie");
        int layer = obstacleHit.collider.gameObject.layer;

        if (layer == enemyLayer || layer == zombieLayer)
        {
            EnemyHealth coverHealth = obstacleHit.collider.GetComponent<EnemyHealth>();
            if (coverHealth != null && !coverHealth.IsDead())
            {
                ShootEnemy(obstacleHit.collider.gameObject, coverHealth, "BOUCLIER", sentinelPos, obstacleHit.point, isHeadshot);
                return;
            }
        }

        if (audioSource != null && gm.sentinelSettings.ricochetSound != null)
            audioSource.PlayOneShot(gm.sentinelSettings.ricochetSound);
        if (gm.sentinelSettings.ricochetVFX != null)
        {
            GameObject vfx = Instantiate(gm.sentinelSettings.ricochetVFX, obstacleHit.point, Quaternion.LookRotation(obstacleHit.normal));
            Destroy(vfx, gm.sentinelSettings.ricochetVFXDuration);
        }
    }

    // ============================================
    // TIRS SPECIAUX
    // ============================================

    public void ExecutePlayerShotOnGroggy()
    {
        if (gm.player == null || gm.playerHealth == null || gm.playerHealth.IsDead()) return;

        Vector3 eyePosition = (gm.sentinelEye != null) ? gm.sentinelEye.position : transform.position;
        Vector3 sentinelPos = eyePosition + gm.sentinelSettings.raycastOffset;
        Vector3 targetPos = SentinelTargetGeometry.GetTargetCenter(gm.player.gameObject);

        if (audioSource != null && gm.sentinelSettings.shootSound != null)
            audioSource.PlayOneShot(gm.sentinelSettings.shootSound);

        if (gm.laserManager != null)
            gm.laserManager.TriggerShotAnimation(sentinelPos, targetPos);

        if (gm.playerDetectionFeedback != null)
            gm.playerDetectionFeedback.OnShotBySentinelGroggy();

        gm.playerHealth.TakeDamage(gm.sentinelSettings.playerDamage);
    }

    public void ExecuteSequenceShot(GameObject enemy)
    {
        EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
        if (enemyHealth == null || enemyHealth.IsDead()) return;

        Vector3 eyePosition = (gm.sentinelEye != null) ? gm.sentinelEye.position : transform.position;
        Vector3 sentinelPos = eyePosition + gm.sentinelSettings.raycastOffset;
        Vector3 targetPos = SentinelTargetGeometry.GetTargetCenter(enemy);

        if (audioSource != null && gm.sentinelSettings.shootSound != null)
            audioSource.PlayOneShot(gm.sentinelSettings.shootSound);

        if (gm.laserManager != null)
            gm.laserManager.TriggerShotAnimation(sentinelPos, targetPos);

        SentinelTarget sentinelTarget = enemy.GetComponent<SentinelTarget>();
        if (sentinelTarget != null) sentinelTarget.FlashWhite();

        EnemyDetectionFeedback feedback = enemy.GetComponent<EnemyDetectionFeedback>();
        if (feedback != null) feedback.OnShotBySentinel();

        enemyHealth.TakeSentinelShotDuringSequence();
    }
}
