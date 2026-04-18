using UnityEngine;
using System.Collections.Generic;

public class ConveyorBelt : MonoBehaviour, IMovingPlatform
{
    [Header("Movement")]
    public float speed = 3f;
    public float textureTilingY = 1f;

    [Header("Audio")]
    public AudioClip movementSound;
    public float soundVolume = 0.5f;

    private AudioSource audioSource;
    private Collider beltCollider;
    private MaterialPropertyBlock mpb;
    private Renderer beltRenderer;

    private float uvScrollSpeed;
    private float uvOffset = 0f;

    private static readonly int ScrollOffsetProperty = Shader.PropertyToID("_ScrollOffset");

    private HashSet<EnemyAI_AStar> enemiesOnBelt = new HashSet<EnemyAI_AStar>();
    private HashSet<RagdollDeathEffect> corpsesOnBelt = new HashSet<RagdollDeathEffect>();
    private HashSet<PhysicsProp> propsOnBelt = new HashSet<PhysicsProp>();
    private Dictionary<RagdollDeathEffect, float> corpseCooldowns = new Dictionary<RagdollDeathEffect, float>();

    void Start()
    {
        beltCollider = GetComponent<Collider>();

        beltRenderer = GetComponent<Renderer>();
        MeshFilter mf = GetComponent<MeshFilter>();
        if (beltRenderer != null && mf != null)
        {
            float meshLength = mf.sharedMesh.bounds.size.z;
            float worldLength = meshLength * transform.localScale.z;
            uvScrollSpeed = worldLength > 0f ? (speed * textureTilingY) / worldLength : speed;

            mpb = new MaterialPropertyBlock();
            beltRenderer.GetPropertyBlock(mpb);
            mpb.SetFloat(ScrollOffsetProperty, 0f);
            beltRenderer.SetPropertyBlock(mpb);
        }

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.volume = soundVolume;

        if (movementSound != null)
        {
            audioSource.clip = movementSound;
            audioSource.Play();
        }
    }

    void FixedUpdate()
    {
        Vector3 displacement = transform.forward * speed * Time.fixedDeltaTime;

        List<EnemyAI_AStar> enemiesToRemove = new List<EnemyAI_AStar>();
        foreach (EnemyAI_AStar enemy in enemiesOnBelt)
        {
            if (enemy == null || enemy.isDead) { enemiesToRemove.Add(enemy); continue; }
            enemy.transform.position += displacement;
        }
        foreach (EnemyAI_AStar enemy in enemiesToRemove)
            enemiesOnBelt.Remove(enemy);

        foreach (RagdollDeathEffect corpse in corpsesOnBelt)
        {
            if (corpse == null) continue;
            corpse.transform.position += displacement;
        }

        List<PhysicsProp> propsToRemove = new List<PhysicsProp>();
        foreach (PhysicsProp prop in propsOnBelt)
        {
            if (prop == null) { propsToRemove.Add(prop); continue; }
            prop.transform.position += displacement;
        }
        foreach (PhysicsProp prop in propsToRemove)
            propsOnBelt.Remove(prop);

        uvOffset += uvScrollSpeed * Time.fixedDeltaTime;
        if (uvOffset > 1f) uvOffset -= 1f;

        mpb.SetFloat(ScrollOffsetProperty, uvOffset);
        beltRenderer.SetPropertyBlock(mpb);
    }

    public void RemoveCorpse(RagdollDeathEffect corpse)
    {
        corpsesOnBelt.Remove(corpse);
        corpseCooldowns[corpse] = Time.time + 1f;
    }

    public Vector3 GetPlatformVelocity()
    {
        return transform.forward * speed;
    }

    public Transform GetTransform()
    {
        return transform;
    }

    void OnCollisionStay(Collision collision)
    {
        EnemyAI_AStar enemy = collision.gameObject.GetComponent<EnemyAI_AStar>();
        if (enemy != null && !enemiesOnBelt.Contains(enemy))
            enemiesOnBelt.Add(enemy);

        RagdollDeathEffect corpse = collision.gameObject.GetComponentInParent<RagdollDeathEffect>();
        if (corpse != null && !corpsesOnBelt.Contains(corpse))
        {
            if (!corpseCooldowns.ContainsKey(corpse) || Time.time > corpseCooldowns[corpse])
                corpsesOnBelt.Add(corpse);
        }

        PhysicsProp prop = collision.gameObject.GetComponent<PhysicsProp>();
        if (prop != null && !propsOnBelt.Contains(prop))
            propsOnBelt.Add(prop);
    }

    void OnCollisionExit(Collision collision)
    {
        EnemyAI_AStar enemy = collision.gameObject.GetComponent<EnemyAI_AStar>();
        if (enemy != null) enemiesOnBelt.Remove(enemy);

        RagdollDeathEffect corpse = collision.gameObject.GetComponentInParent<RagdollDeathEffect>();
        if (corpse != null) corpsesOnBelt.Remove(corpse);

        PhysicsProp prop = collision.gameObject.GetComponent<PhysicsProp>();
        if (prop != null) propsOnBelt.Remove(prop);
    }

    void OnDestroy()
    {
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position + Vector3.up * 0.05f, transform.forward * speed);
    }
}