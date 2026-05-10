using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SlipperyZone : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private float slipperyAccel = 4f;
    [SerializeField] private float slipperyBrake = 1.5f;
    [SerializeField] private float slipperyRotation = 10f;

    [Header("Enemies")]
    [Range(0f, 1f)]
    [SerializeField] private float enemyDragMultiplier = 0.5f;

    [Header("Corpses")]
    [SerializeField] private float corpseSlideDelay = 0.5f;
    [SerializeField] private float corpseSlideDuration = 1f;

    private Dictionary<EnemyAI_AStar, Rigidbody> enemiesOnZone = new Dictionary<EnemyAI_AStar, Rigidbody>();
    private Dictionary<EnemyAI_AStar, float> originalDrags = new Dictionary<EnemyAI_AStar, float>();
    private HashSet<RagdollDeathEffect> corpsesOnZone = new HashSet<RagdollDeathEffect>();
    private HashSet<RagdollDeathEffect> corpsesAlreadySlid = new HashSet<RagdollDeathEffect>();

    void OnCollisionEnter(Collision collision)
    {
        PlayerPhysicsMovement player = collision.gameObject.GetComponent<PlayerPhysicsMovement>();
        if (player != null)
        {
            player.SetSlippery(true, slipperyAccel, slipperyBrake, slipperyRotation);
            return;
        }

        EnemyAI_AStar enemy = collision.gameObject.GetComponent<EnemyAI_AStar>();
        if (enemy != null && !enemiesOnZone.ContainsKey(enemy))
        {
            Rigidbody rb = enemy.GetComponent<Rigidbody>();
            if (rb != null)
            {
                enemiesOnZone.Add(enemy, rb);
                originalDrags[enemy] = rb.linearDamping;
                rb.linearDamping = rb.linearDamping * enemyDragMultiplier;
            }
        }
    }

    void OnCollisionStay(Collision collision)
    {
        RagdollDeathEffect corpse = collision.gameObject.GetComponentInParent<RagdollDeathEffect>();
        if (corpse == null) return;
        if (corpsesOnZone.Contains(corpse)) return;
        if (corpsesAlreadySlid.Contains(corpse)) return;
        if (corpse.hipsRb == null) return;

        corpsesOnZone.Add(corpse);
        corpsesAlreadySlid.Add(corpse);
        StartCoroutine(SlideCorpse(corpse));
    }

    void OnCollisionExit(Collision collision)
    {
        PlayerPhysicsMovement player = collision.gameObject.GetComponent<PlayerPhysicsMovement>();
        if (player != null)
        {
            player.SetSlippery(false, 0f, 0f, 0f);
            return;
        }

        EnemyAI_AStar enemy = collision.gameObject.GetComponent<EnemyAI_AStar>();
        if (enemy != null)
        {
            if (enemiesOnZone.TryGetValue(enemy, out Rigidbody rb) && rb != null)
                rb.linearDamping = originalDrags.ContainsKey(enemy) ? originalDrags[enemy] : 5f;
            enemiesOnZone.Remove(enemy);
            originalDrags.Remove(enemy);
        }

        RagdollDeathEffect corpse = collision.gameObject.GetComponentInParent<RagdollDeathEffect>();
        if (corpse != null)
            corpsesOnZone.Remove(corpse);
    }

    private IEnumerator SlideCorpse(RagdollDeathEffect corpse)
    {
        yield return new WaitForSeconds(corpseSlideDelay);

        Rigidbody hipsRb = corpse.hipsRb;
        if (hipsRb == null) yield break;

        Vector3 initialVelXZ = hipsRb.linearVelocity;
        initialVelXZ.y = 0f;

        if (initialVelXZ.magnitude <= 0f) yield break;

        float elapsed = 0f;
        while (elapsed < corpseSlideDuration)
        {
            if (corpse == null) yield break;

            float t = elapsed / corpseSlideDuration;
            Vector3 slideVel = Vector3.Lerp(initialVelXZ, Vector3.zero, t);
            corpse.transform.position += new Vector3(slideVel.x, 0f, slideVel.z) * Time.fixedDeltaTime;

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
    }
}