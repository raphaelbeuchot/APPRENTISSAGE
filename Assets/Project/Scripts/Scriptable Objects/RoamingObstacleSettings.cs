using UnityEngine;

[CreateAssetMenu(fileName = "RoamingObstacle_", menuName = "1-2-3 Soleil/Roaming Obstacle Settings")]
public class RoamingObstacleSettings : ScriptableObject
{
    [Header("Identity")]
    public string obstacleName = "Roaming Obstacle";

    [Header("Movement")]
    [Tooltip("Vitesse de deplacement le long de la spline en m/s")]
    public float moveSpeed = 2f;

    [Tooltip("Si coche, parcourt la spline en sens inverse")]
    public bool reverseDirection = false;

    // NOUVEAU
    [Tooltip("Type de parcours : Loop (boucle fermee) ou PingPong (aller-retour)")]
    public bool usePingPong = true;

    [Header("Audio")]
    [Tooltip("Son joue en boucle pendant le mouvement (optionnel)")]
    public AudioClip movementSound;

    [Tooltip("Volume du son de mouvement")]
    [Range(0f, 1f)]
    public float soundVolume = 0.5f;
}