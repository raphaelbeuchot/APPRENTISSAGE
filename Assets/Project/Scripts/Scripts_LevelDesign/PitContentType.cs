using UnityEngine;

[CreateAssetMenu(fileName = "PitContent", menuName = "Level Design/Pit Content Type")]
public class PitContentType : ScriptableObject
{
    [Header("Identity")]
    public string contentName = "Empty";

    [Header("Category")]
    public ContentCategory category = ContentCategory.Empty;

    [Header("Visual")]
    public GameObject contentPrefab;
    public Color previewColor = new Color(0.5f, 0.5f, 1f, 0.4f);

    [Header("Gameplay (for later)")]
    public float damagePerSecond = 0f;
    public bool instantKill = false;
    public float movementSpeedMultiplier = 1f;

    public enum ContentCategory
    {
        Empty,
        InstantKill,
        Liquid,
        DamageZone
    }
}