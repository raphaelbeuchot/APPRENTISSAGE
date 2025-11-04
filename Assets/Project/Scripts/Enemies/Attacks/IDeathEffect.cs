using UnityEngine;

public interface IDeathEffect
{
    void OnDeath(Vector3 deathPosition, DeathContext context);
}

public class DeathContext
{
    public enum DeathType { Melee, Sentinel, Explosion, Fall, Other }

    public DeathType deathType;
    public Vector3 impactDirection;
    public float impactForce;

    public DeathContext()
    {
        deathType = DeathType.Other;
        impactDirection = Vector3.forward;
        impactForce = 1f;
    }
}