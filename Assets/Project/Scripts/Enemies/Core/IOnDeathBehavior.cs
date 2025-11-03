using UnityEngine;

/// <summary>
/// Interface for modular death behaviors on enemies.
/// Allows enemies to trigger specific actions when they die (spawn swarms, explode, etc.)
/// </summary>
public interface IOnDeathBehavior
{
    /// <summary>
    /// Called when the enemy dies
    /// </summary>
    /// <param name="deathPosition">Position where the enemy died</param>
    void OnEnemyDeath(Vector3 deathPosition);
}
