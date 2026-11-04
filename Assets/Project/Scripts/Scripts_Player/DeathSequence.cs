using UnityEngine;
using System.Collections;
using Unity.Cinemachine;

public class DeathSequence : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerRagdoll ragdoll;
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Coins")]
    [SerializeField] private GameObject coinPrefab;
    [SerializeField] private float coinForceMin = 3f;
    [SerializeField] private float coinForceMax = 9f;
    [SerializeField] private float coinUpwardBias = 0.6f;
    [SerializeField] private Vector3 coinSpawnOffset = new Vector3(0f, 1f, 0f);

    
    [Header("UI")]
    [SerializeField] private CreditBarUI creditBarUI;
    [SerializeField] private float coinCountdownDuration = 0.5f;

    [Header("Settings")]
    [SerializeField] private float ragdollTimeout = 5f;
    [SerializeField] private float velocityThreshold = 0.1f;

    [Header("Camera")]
    [SerializeField] private Transform hipBone;
    [Tooltip("Multi : ne remplacer que les target groups dont la cible 0 est ce joueur (ou un de ses enfants). Decoche : tous les groupes de la scene (solo).")]
    [SerializeField] private bool onlyOwnTargetGroups = false;
    [Tooltip("Mort sans ragdoll : duree du glissement de la camera vers le dessus du corps qui tombe")]
    [SerializeField] private float deathCameraSlideDuration = 1f;
    [Tooltip("Mort sans ragdoll : part du decalage horizontal camera / joueur absorbee par le glissement. " +
             "1 = pile a la verticale du corps (le LookAt peut alors tourner brusquement), 0 = pas de glissement")]
    [Range(0f, 1f)]
    [SerializeField] private float deathCameraSlideAmount = 0.85f;

    private CinemachineTargetGroup[] allTargetGroups;
    private readonly System.Collections.Generic.Dictionary<CinemachineTargetGroup, Transform> replacedTargets
        = new System.Collections.Generic.Dictionary<CinemachineTargetGroup, Transform>();

    // Mort sans ragdoll : cameras qui suivaient la racine, rebranchees sur une ancre (voir AnchorCameras)
    private readonly System.Collections.Generic.Dictionary<CinemachineCamera, Transform> replacedTrackingTargets
        = new System.Collections.Generic.Dictionary<CinemachineCamera, Transform>();
    private Transform deathCameraAnchor;
    private float deathAnchorY;
    private Vector3 deathCameraOffsetXZ;   // camera - joueur a la mort, a plat
    private float deathAnchorTime;

    void Awake()
    {
        allTargetGroups = FindObjectsByType<CinemachineTargetGroup>(FindObjectsSortMode.None);
    }

    public void Play(Vector3 deathDirection)
    {
        StartCoroutine(DeathRoutine(deathDirection));
    }

    private IEnumerator DeathRoutine(Vector3 deathDirection)
    {
        Debug.Log("DeathRoutine START");
        // Ordre important : MeleeAttackSystem.OnDisable() reactive movement s'il le trouve desactive
        // (filet de securite pour une attaque interrompue). Desactiver movement en dernier garantit
        // que son etat final reste bien "desactive", quoi que fasse ce filet de securite.
        MeleeAttackSystem melee = GetComponent<MeleeAttackSystem>();
        if (melee != null) melee.enabled = false;
        PlayerPhysicsMovement movement = GetComponent<PlayerPhysicsMovement>();
        if (movement != null) movement.enabled = false;

        // Cause deduite par PlayerDeathCause (resolue sur OnDying, juste avant Play) : elle peut couper le ragdoll
        // et imposer une duree fixe
        PlayerDeathCause deathCause = GetComponent<PlayerDeathCause>();
        DeathCause cause = deathCause != null ? deathCause.Current : playerHealth.LastDeathCause;

        bool useRagdoll = cause == null || cause.ragdoll;
        if (ragdoll != null && useRagdoll)
            ragdoll.Activate(deathDirection);
        else if (!useRagdoll)
            AnchorCameras();

        foreach (CinemachineTargetGroup tg in allTargetGroups)
        {
            if (tg.Targets.Count > 0 && (!onlyOwnTargetGroups || IsOwnTarget(tg.Targets[0].Object)))
            {
                replacedTargets[tg] = tg.Targets[0].Object;
                tg.Targets[0] = new CinemachineTargetGroup.Target
                {
                    Object = hipBone,
                    Weight = tg.Targets[0].Weight,
                    Radius = tg.Targets[0].Radius
                };
            }
        }

        SpawnCoins();

        if (creditBarUI != null)
            creditBarUI.CountdownToZero(coinCountdownDuration);

        Rigidbody hipRb = ragdoll != null ? ragdoll.GetHipRigidbody() : null;
        Rigidbody mainRb = GetComponent<Rigidbody>();
        bool hasRealRagdoll = hipRb != null && hipRb != mainRb;

        Debug.Log("ragdollTimeout runtime value: " + ragdollTimeout + " | hasRealRagdoll: " + hasRealRagdoll);

        if (cause != null && cause.overrideRagdollWait)
        {
            yield return new WaitForSeconds(cause.ragdollWait);
        }
        else
        {
            yield return new WaitForSeconds(0.3f);

            float elapsed = 0f;
            while (elapsed < ragdollTimeout)
            {
                elapsed += Time.deltaTime;
                if (hasRealRagdoll && hipRb.linearVelocity.magnitude < velocityThreshold)
                    break;
                yield return null;
            }
        }

        Debug.Log("DeathRoutine FIN - invocation OnDeath");
        playerHealth.TriggerOnDeath();
    }

    private bool IsOwnTarget(Transform target)
    {
        return target != null && target.IsChildOf(transform);
    }

    // Mort sans ragdoll : le ragdoll rendait la racine kinematique, les cameras qui suivent la racine s'arretaient
    // et pivotaient (LookAt = target group -> hipBone) pour regarder tomber le corps. Sans lui, la racine continue
    // de tomber, la camera la suit et CameraConfiner la bloque au fond de son volume : perso hors ecran.
    // On fait suivre a ces cameras une ancre au Y de la mort, qui suit le X/Z du corps.
    private void AnchorCameras()
    {
        if (deathCameraAnchor != null) return;

        deathCameraAnchor = new GameObject("DeathCameraAnchor_" + name).transform;
        deathCameraAnchor.position = transform.position;
        deathAnchorY = transform.position.y;
        deathAnchorTime = Time.time;

        // Le corps tombe presque a la verticale : suivre son X/Z ne deplace pas la camera. On absorbe donc
        // progressivement son decalage horizontal pour qu'elle vienne au-dessus du corps.
        deathCameraOffsetXZ = Vector3.zero;
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            deathCameraOffsetXZ = mainCam.transform.position - transform.position;
            deathCameraOffsetXZ.y = 0f;
        }

        foreach (CinemachineCamera cam in FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
        {
            if (cam.Target.TrackingTarget != transform) continue;
            replacedTrackingTargets[cam] = cam.Target.TrackingTarget;
            cam.Target.TrackingTarget = deathCameraAnchor;
        }
    }

    void LateUpdate()
    {
        if (deathCameraAnchor == null) return;
        Vector3 body = hipBone != null ? hipBone.position : transform.position;
        float t = deathCameraSlideDuration > 0f
            ? Mathf.Clamp01((Time.time - deathAnchorTime) / deathCameraSlideDuration)
            : 1f;
        Vector3 slide = deathCameraOffsetXZ * (deathCameraSlideAmount * Mathf.SmoothStep(0f, 1f, t));
        deathCameraAnchor.position = new Vector3(body.x - slide.x, deathAnchorY, body.z - slide.z);
    }

    private void RestoreAnchoredCameras()
    {
        foreach (var pair in replacedTrackingTargets)
        {
            if (pair.Key != null && pair.Key.Target.TrackingTarget == deathCameraAnchor)
                pair.Key.Target.TrackingTarget = pair.Value;
        }
        replacedTrackingTargets.Clear();

        if (deathCameraAnchor != null)
            Destroy(deathCameraAnchor.gameObject);
        deathCameraAnchor = null;
    }

    // Multi : annule les effets de DeathRoutine sur le joueur et les target groups (respawn).
    // Jamais appele en solo.
    public void Restore()
    {
        foreach (var pair in replacedTargets)
        {
            CinemachineTargetGroup tg = pair.Key;
            if (tg == null || tg.Targets.Count == 0) continue;
            if (tg.Targets[0].Object != hipBone) continue;

            CinemachineTargetGroup.Target target = tg.Targets[0];
            target.Object = pair.Value;
            tg.Targets[0] = target;
        }
        replacedTargets.Clear();
        RestoreAnchoredCameras();

        PlayerPhysicsMovement movement = GetComponent<PlayerPhysicsMovement>();
        if (movement != null) movement.enabled = true;
        MeleeAttackSystem melee = GetComponent<MeleeAttackSystem>();
        if (melee != null) melee.enabled = true;
    }

    private void SpawnCoins()
    {
        if (coinPrefab == null) return;
        if (CleaningCreditManager.Instance == null) return;
        int count = CleaningCreditManager.Instance.GetCredits();
        if (count <= 0) return;
        Vector3 spawnPos = transform.position + coinSpawnOffset;
        for (int i = 0; i < count; i++)
        {
            GameObject coin = Instantiate(coinPrefab, spawnPos, Quaternion.identity);
            CoinDeathFX fx = coin.GetComponent<CoinDeathFX>();
            if (fx == null) continue;
            Vector3 dir = Random.insideUnitSphere;
            dir.y = Mathf.Abs(dir.y) + coinUpwardBias;
            dir.Normalize();
            float force = Random.Range(coinForceMin, coinForceMax);
            fx.Launch(dir * force);
        }
    }
}