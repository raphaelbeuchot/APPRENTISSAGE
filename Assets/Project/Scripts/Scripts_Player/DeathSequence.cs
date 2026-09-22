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

    private CinemachineTargetGroup[] allTargetGroups;
    private readonly System.Collections.Generic.Dictionary<CinemachineTargetGroup, Transform> replacedTargets
        = new System.Collections.Generic.Dictionary<CinemachineTargetGroup, Transform>();

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

        if (ragdoll != null)
            ragdoll.Activate(deathDirection);

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

        yield return new WaitForSeconds(0.3f);

        float elapsed = 0f;
        while (elapsed < ragdollTimeout)
        {
            elapsed += Time.deltaTime;
            if (hasRealRagdoll && hipRb.linearVelocity.magnitude < velocityThreshold)
                break;
            yield return null;
        }

        Debug.Log("DeathRoutine FIN - invocation OnDeath");
        playerHealth.TriggerOnDeath();
    }

    private bool IsOwnTarget(Transform target)
    {
        return target != null && target.IsChildOf(transform);
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