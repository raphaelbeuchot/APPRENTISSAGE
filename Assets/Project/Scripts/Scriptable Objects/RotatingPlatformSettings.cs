using UnityEngine;

[CreateAssetMenu(fileName = "RotatingPlatformSettings", menuName = "1-2-3 Soleil/Rotating Platform Settings")]
public class RotatingPlatformSettings : ScriptableObject
{
    [Header("Identity")]
    public string obstacleName = "Rotating Platform";

    [Header("Player Interaction")]
    [Tooltip("Multiplicateur influence rotation sur player (1 = normal, 2 = double effet)")]
    [Range(0.5f, 10f)]
    public float playerInfluence = 6f;

    [Header("Rotation")]
    [Tooltip("Vitesse de rotation en degres par seconde")]
    public float rotationSpeed = 30f;

    [Tooltip("Sens horaire (true) ou anti-horaire (false)")]
    public bool clockwise = true;

    [Tooltip("Offset du pivot par rapport au centre (X, Z uniquement)")]
    public Vector2 pivotOffset = Vector2.zero;

    [Header("Audio")]
    [Tooltip("Son de mouvement (optionnel)")]
    public AudioClip movementSound;

    [Range(0f, 1f)]
    public float soundVolume = 0.5f;
}