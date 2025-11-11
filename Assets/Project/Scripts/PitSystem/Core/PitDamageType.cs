/// <summary>
/// Types de dégâts infligés par les fosses
/// </summary>
public enum PitDamageType
{
    Fall,           // Dégâts de chute au contact du fond
    InstantKill,    // Mort instantanée (spikes, lave, acide)
    DamageOverTime  // Dégâts continus (feu, acide non-instant)
}