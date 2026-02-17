using UnityEngine;
using System.Collections.Generic;

public class RotatingPlatform : MonoBehaviour, IMovingPlatform
{
    [Header("Settings")]
    public RotatingPlatformSettings settings;
    private Vector3 pivotPoint;
    private AudioSource audioSource;
    private Vector3 lastPosition;
    private Vector3 currentVelocity;

    private HashSet<EnemyAI_AStar> enemiesOnPlatform = new HashSet<EnemyAI_AStar>();

    void Start()
    {
        if (settings == null)
        {
            enabled = false;
            return;
        }
        pivotPoint = transform.position + new Vector3(settings.pivotOffset.x, 0f, settings.pivotOffset.y);
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.volume = settings.soundVolume;
        if (settings.movementSound != null)
        {
            audioSource.clip = settings.movementSound;
            audioSource.Play();
        }
        lastPosition = transform.position;
    }

    void Update()
    {
        if (settings == null) return;

        float angleThisFrame = settings.rotationSpeed * Time.deltaTime;
        if (!settings.clockwise)
            angleThisFrame = -angleThisFrame;

        transform.RotateAround(pivotPoint, Vector3.up, angleThisFrame);
        CalculateVelocity();

        // NOUVEAU : Faire tourner tous les ennemis sur la plateforme
        foreach (EnemyAI_AStar enemy in enemiesOnPlatform)
        {
            if (enemy == null) continue;

            Rigidbody enemyRb = enemy.GetComponent<Rigidbody>();
            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
            if (enemyRb == null) continue;

            // Teleportation rotation
            Vector3 directionFromPivot = enemy.transform.position - pivotPoint;
            directionFromPivot = Quaternion.Euler(0f, angleThisFrame, 0f) * directionFromPivot;
            enemy.transform.position = pivotPoint + directionFromPivot;

            // AJOUTER CETTE LIGNE : Rotation de l'orientation
            enemy.transform.Rotate(Vector3.up, angleThisFrame);

            // Bloquer velocity seulement si pas en knockback
            if (enemyHealth != null && !enemyHealth.isInKnockback)
            {
                enemyRb.linearVelocity = new Vector3(0, enemyRb.linearVelocity.y, 0);
            }
        }
    }

    private void CalculateVelocity()
    {
        currentVelocity = (transform.position - lastPosition) / Time.deltaTime;
        lastPosition = transform.position;
    }

    public Vector3 GetPlatformVelocity()
    {
        return currentVelocity;
    }

    public Transform GetTransform()
    {
        return transform;
    }

    void OnCollisionEnter(Collision collision)
    {
    }

    void OnCollisionStay(Collision collision)
    {
        EnemyAI_AStar enemyAI = collision.gameObject.GetComponent<EnemyAI_AStar>();
        if (enemyAI == null) return;

        // Deja sur la plateforme, skip
        if (enemyAI.isOnRotatingPlatform) return;

        // NOUVELLE APPROCHE : Distance XZ au centre
        Collider platformCollider = GetComponent<Collider>();
        if (platformCollider == null) return;

        Bounds bounds = platformCollider.bounds;

        // Distance 2D (XZ seulement) entre zombie et centre plateforme
        Vector2 enemyPosXZ = new Vector2(enemyAI.transform.position.x, enemyAI.transform.position.z);
        Vector2 centerXZ = new Vector2(bounds.center.x, bounds.center.z);
        float distanceToCenter = Vector2.Distance(enemyPosXZ, centerXZ);

        // Rayon de la plateforme (on prend le plus petit pour être sûr)
        float platformRadius = Mathf.Min(bounds.extents.x, bounds.extents.z) - 0.05f; // Marge de 0.05m


        if (distanceToCenter > platformRadius)
        {
            return;
        }

        // Activer le mode
        if (!enemiesOnPlatform.Contains(enemyAI))
        {
            enemiesOnPlatform.Add(enemyAI);
            enemyAI.EnableRotatingPlatformMode(this);
        }
    }

    void OnCollisionExit(Collision collision)
    {

        EnemyAI_AStar enemyAI = collision.gameObject.GetComponent<EnemyAI_AStar>();
        if (enemyAI != null && enemiesOnPlatform.Contains(enemyAI))
        {
            enemiesOnPlatform.Remove(enemyAI);
            enemyAI.DisableRotatingPlatformMode();
        }
    }

    void OnDestroy()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    void OnDrawGizmos()
    {
        if (settings == null) return;
        Vector3 pivot = Application.isPlaying
            ? pivotPoint
            : transform.position + new Vector3(settings.pivotOffset.x, 0f, settings.pivotOffset.y);
        Gizmos.color = Color.yellow;
        float crossSize = 0.3f;
        Gizmos.DrawLine(pivot + Vector3.left * crossSize, pivot + Vector3.right * crossSize);
        Gizmos.DrawLine(pivot + Vector3.forward * crossSize, pivot + Vector3.back * crossSize);
        Gizmos.color = Color.cyan;
        float radius = Vector3.Distance(transform.position, pivot);
        DrawCircle(pivot, radius, 32);
    }

    public Vector3 GetTangentialVelocityAtPoint(Vector3 point)
    {
        Vector3 radiusVector = point - pivotPoint;
        radiusVector.y = 0f;
        float angularSpeedRad = settings.rotationSpeed * Mathf.Deg2Rad;
        if (!settings.clockwise)
            angularSpeedRad = -angularSpeedRad;
        return Vector3.Cross(Vector3.up * angularSpeedRad, radiusVector);
    }

    private void DrawCircle(Vector3 center, float radius, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
}