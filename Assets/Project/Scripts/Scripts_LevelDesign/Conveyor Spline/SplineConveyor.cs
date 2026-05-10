using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;
using Unity.Mathematics;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class SplineConveyor : MonoBehaviour, IMovingPlatform
{
    [Header("Spline")]
    public SplineContainer splineContainer;

    [Header("Shape")]
    public float width = 1f;
    public int sampleCount = 32;

    [Header("Movement")]
    public float speed = 3f;
    public float textureTilingY = 1f;

    [Header("Audio")]
    public AudioClip movementSound;
    public float soundVolume = 0.5f;

    private AudioSource audioSource;
    private MaterialPropertyBlock mpb;
    private Renderer beltRenderer;
    private float uvOffset = 0f;
    private float uvScrollSpeed;
    private float splineLength;

    private static readonly int ScrollOffsetProperty = Shader.PropertyToID("_ScrollOffset");

    private Dictionary<EnemyAI_AStar, Rigidbody> enemiesOnBelt = new Dictionary<EnemyAI_AStar, Rigidbody>();
    private HashSet<RagdollDeathEffect> corpsesOnBelt = new HashSet<RagdollDeathEffect>();
    private HashSet<PhysicsProp> propsOnBelt = new HashSet<PhysicsProp>();
    private Dictionary<RagdollDeathEffect, float> corpseCooldowns = new Dictionary<RagdollDeathEffect, float>();

    public void GenerateMesh()
    {
        if (splineContainer == null)
        {
            Debug.LogWarning("[SplineConveyor] SplineContainer non assigne.");
            return;
        }

        Mesh mesh = SplineConveyorMeshGenerator.Generate(splineContainer, width, sampleCount);

        MeshFilter mf = GetComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        MeshCollider mc = GetComponent<MeshCollider>();
        mc.sharedMesh = mesh;

        Debug.Log("[SplineConveyor] Mesh genere.");
    }

    void Start()
    {
        if (splineContainer == null) return;

        splineLength = splineContainer.Spline.GetLength();
        uvScrollSpeed = splineLength > 0f ? (speed * textureTilingY) / splineLength : speed;

        beltRenderer = GetComponent<Renderer>();
        if (beltRenderer != null)
        {
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
        List<EnemyAI_AStar> enemiesToRemove = new List<EnemyAI_AStar>();
        foreach (KeyValuePair<EnemyAI_AStar, Rigidbody> pair in enemiesOnBelt)
        {
            EnemyAI_AStar enemy = pair.Key;
            Rigidbody rb = pair.Value;

            if (enemy == null || enemy.isDead) { enemiesToRemove.Add(enemy); continue; }
            if (rb == null) { enemiesToRemove.Add(enemy); continue; }

            Vector3 vel = GetVelocityAtPoint(rb.position);
            rb.MovePosition(rb.position + vel * Time.fixedDeltaTime);
        }
        foreach (EnemyAI_AStar e in enemiesToRemove)
            enemiesOnBelt.Remove(e);

        foreach (RagdollDeathEffect corpse in corpsesOnBelt)
        {
            if (corpse == null) continue;
            Vector3 vel = GetVelocityAtPoint(corpse.transform.position);
            corpse.transform.position += vel * Time.fixedDeltaTime;
        }

        List<PhysicsProp> propsToRemove = new List<PhysicsProp>();
        foreach (PhysicsProp prop in propsOnBelt)
        {
            if (prop == null) { propsToRemove.Add(prop); continue; }
            Vector3 vel = GetVelocityAtPoint(prop.transform.position);
            prop.transform.position += vel * Time.fixedDeltaTime;
        }
        foreach (PhysicsProp prop in propsToRemove)
            propsOnBelt.Remove(prop);

        if (beltRenderer != null && mpb != null)
        {
            uvOffset += uvScrollSpeed * Time.fixedDeltaTime;
            if (uvOffset > 1f) uvOffset -= 1f;
            mpb.SetFloat(ScrollOffsetProperty, uvOffset);
            beltRenderer.SetPropertyBlock(mpb);
        }
    }

    public Vector3 GetVelocityAtPoint(Vector3 worldPos)
    {
        if (splineContainer == null) return Vector3.zero;

        float3 localPos3 = (float3)splineContainer.transform.InverseTransformPoint(worldPos);
        SplineUtility.GetNearestPoint(splineContainer.Spline, localPos3, out float3 nearest, out float t);

        Vector3 localTangent = (Vector3)splineContainer.Spline.EvaluateTangent(t);
        Vector3 worldTangent = splineContainer.transform.TransformDirection(localTangent).normalized;

        return worldTangent * speed;
    }

    // IMovingPlatform
    public Vector3 GetPlatformVelocity()
    {
        return splineContainer.transform.TransformDirection(
            (Vector3)splineContainer.Spline.EvaluateTangent(0f)).normalized * speed;
    }

    public Transform GetTransform()
    {
        return transform;
    }

    public void RemoveCorpse(RagdollDeathEffect corpse)
    {
        corpsesOnBelt.Remove(corpse);
        corpseCooldowns[corpse] = Time.time + 1f;
    }

    void OnCollisionStay(Collision collision)
    {
        EnemyAI_AStar enemy = collision.gameObject.GetComponent<EnemyAI_AStar>();
        if (enemy != null && !enemiesOnBelt.ContainsKey(enemy))
        {
            Rigidbody rb = enemy.GetComponent<Rigidbody>();
            if (rb != null)
                enemiesOnBelt.Add(enemy, rb);
        }

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
        if (splineContainer == null) return;
        Gizmos.color = Color.cyan;
        Vector3 mid = splineContainer.transform.TransformPoint(
            (Vector3)splineContainer.Spline.EvaluatePosition(0.5f));
        Vector3 tan = splineContainer.transform.TransformDirection(
            (Vector3)splineContainer.Spline.EvaluateTangent(0.5f)).normalized;
        Gizmos.DrawRay(mid + Vector3.up * 0.05f, tan * speed);
    }
}