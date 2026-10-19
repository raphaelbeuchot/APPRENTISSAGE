using UnityEngine;

[CreateAssetMenu(fileName = "BrightEyesStats", menuName = "Enemy/Bright Eyes Stats")]
public class BrightEyesStats : ScriptableObject
{
    [Header("Health")]
    public float maxHealth = 100f;

    [Header("Detection")]
    public float detectionRange = 4f;
    public float detectionAngle = 360f;

    [Header("Attraction")]
    public float attractionForce = 1f;
    public float playerSlowdownMultiplier = 0.8f;

    [Header("Audio")]
    [Tooltip("Son en boucle tant que le joueur est capture par l'attraction (non spatialise)")]
    public AudioClip attractionLoopSound;
    [Range(0f, 1f)] public float attractionLoopVolume = 1f;

    [Header("Flame Material")]
    public Material materialDead;
}