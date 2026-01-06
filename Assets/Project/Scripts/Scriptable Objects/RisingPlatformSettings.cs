using UnityEngine;

[CreateAssetMenu(fileName = "RisingPlatform_", menuName = "1-2-3 Soleil/Rising Platform Settings")]
public class RisingPlatformSettings : ScriptableObject
{
    [Header("Identity")]
    public string platformName = "Rising Platform";

    [Header("Movement")]
    [Tooltip("Vitesse de montee en m/s")]
    public float riseSpeed = 0.5f;

    [Tooltip("Vitesse de descente en m/s")]
    public float descendSpeed = 1f;

    [Tooltip("Hauteur maximale de montee en metres")]
    public float maxRiseHeight = 1f;

    [Header("Audio")]
    [Tooltip("Son joue pendant la montee")]
    public AudioClip riseSound;

    [Tooltip("Son joue pendant la descente (optionnel)")]
    public AudioClip descendSound;
}