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
            Debug.LogError("[RotatingPlatform] Settings non assignes !");
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
        Debug.Log($"[RotatingPlatform] {settings.obstacleName} demarre avec pivot offset {settings.pivotOffset}");
    }

    void Update()
    {
        if (settings == null) return;
        float angleThisFrame = settings.rotationSpeed * Time.deltaTime;
        if (!settings.clockwise)
            angleThisFrame = -angleThisFrame;
        transform.RotateAround(pivotPoint, Vector3.up, angleThisFrame);
        CalculateVelocity();
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
        Debug.Log($"[RotatingPlatform] {collision.gameObject.name} monte sur la plateforme");
    }

    void OnCollisionStay(Collision collision)
    {
        EnemyAI_AStar enemyAI = collision.gameObject.GetComponent<EnemyAI_AStar>();
        if (enemyAI == null) return;

        // Deja sur la plateforme, skip
        if (enemyAI.isOnRotatingPlatform) return;


        // Activer le mode
        if (!enemiesOnPlatform.Contains(enemyAI))
        {
            enemiesOnPlatform.Add(enemyAI);
            enemyAI.EnableRotatingPlatformMode(this);
            Debug.Log($"[RotatingPlatform] {enemyAI.gameObject.name} ACTIVE rotating platform mode");
        }
    }

    void OnCollisionExit(Collision collision)
    {
        Debug.Log($"[RotatingPlatform] {collision.gameObject.name} quitte la plateforme");

        EnemyAI_AStar enemyAI = collision.gameObject.GetComponent<EnemyAI_AStar>();
        if (enemyAI != null && enemiesOnPlatform.Contains(enemyAI))
        {
            enemiesOnPlatform.Remove(enemyAI);
            enemyAI.DisableRotatingPlatformMode();
            Debug.Log($"[RotatingPlatform] {enemyAI.gameObject.name} DESACTIVE rotating platform mode");
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