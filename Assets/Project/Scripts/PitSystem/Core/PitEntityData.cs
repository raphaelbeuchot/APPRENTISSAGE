using UnityEngine;

/// <summary>
/// Données de tracking pour chaque entité dans une fosse
/// </summary>
public class PitEntityData
{
    public IPitInteractable entity;
    public float entryTime;
    public Vector3 entryPosition;
    public bool hasHitBottom;
    public float submersionTime;
    public float lastDamageTick;

    public PitEntityData(IPitInteractable e, Vector3 entryPos)
    {
        entity = e;
        entryPosition = entryPos;
        entryTime = Time.time;
        hasHitBottom = false;
        submersionTime = 0f;
        lastDamageTick = 0f;
    }
}