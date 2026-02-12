using UnityEngine;

[CreateAssetMenu(fileName = "RoamingObstacle_", menuName = "1-2-3 Soleil/Roaming Obstacle Settings")]
public class RoamingObstacleSettings : ScriptableObject
{
    [Header("Audio")]
    [Tooltip("Son joue en boucle pendant le mouvement (optionnel)")]
    public AudioClip movementSound;

    [Tooltip("Volume du son de mouvement")]
    [Range(0f, 1f)]
    public float soundVolume = 0.5f;
}