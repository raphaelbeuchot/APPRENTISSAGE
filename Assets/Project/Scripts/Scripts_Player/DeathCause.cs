using UnityEngine;

// Une facon de mourir (lave, sentinelle, chute hors limites...). Passee a PlayerHealth.TakeDamage / Kill,
// relue par ce qui reagit a la mort (ecran de game over, sons, effets) via PlayerHealth.OnDying / LastDeathCause.
// null = mort generique, comme avant l'introduction des DeathCause.
[CreateAssetMenu(fileName = "Death_", menuName = "Game/Death Cause")]
public class DeathCause : ScriptableObject
{
    [Tooltip("Messages de l'ecran de game over pour cette mort (un tire au hasard). Pas encore relu par GameOverUI.")]
    public string[] gameOverMessages;
}
