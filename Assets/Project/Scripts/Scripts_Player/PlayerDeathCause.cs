using System;
using UnityEngine;

// Deduit la cause de la mort du joueur au moment ou il meurt (PlayerHealth.OnDying), dans l'ordre :
//   1. cause explicite passee a PlayerHealth (Kill / TakeDamage) ;
//   2. sous la hauteur de KillBelowY -> outOfBoundsCause ;
//   3. dans une fosse spline dont le contenu a un DeathCause (lave, acide) -> celui-la ;
//   4. sinon -> defaultCause (sentinelle, seule autre facon de mourir aujourd'hui).
// Une nouvelle facon de mourir = une regle de plus ici, ou un champ DeathCause sur sa source.
// Lu par l'ecran de game over ; OnDeathResolved sert aux reactions de polish (sons, effets).
[RequireComponent(typeof(PlayerHealth))]
public class PlayerDeathCause : MonoBehaviour
{
    [Tooltip("Mort par defaut quand rien d'autre ne s'applique (tir de sentinelle).")]
    [SerializeField] private DeathCause defaultCause;
    [Tooltip("Mort quand le joueur est sous la hauteur de KillBelowY.")]
    [SerializeField] private DeathCause outOfBoundsCause;

    public DeathCause Current { get; private set; }
    public event Action<DeathCause> OnDeathResolved;

    private PlayerHealth health;
    private KillBelowY killBelowY;
    private PlayerPitInteractable pit;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        killBelowY = GetComponent<KillBelowY>();
        pit = GetComponent<PlayerPitInteractable>();
    }

    private void OnEnable()
    {
        health.OnDying += HandleDying;
    }

    private void OnDisable()
    {
        health.OnDying -= HandleDying;
    }

    private void HandleDying(DeathCause explicitCause)
    {
        Current = Resolve(explicitCause);
        Debug.Log("[PlayerDeathCause] " + (Current != null ? Current.name : "generique"));
        OnDeathResolved?.Invoke(Current);
    }

    private DeathCause Resolve(DeathCause explicitCause)
    {
        if (explicitCause != null)
            return explicitCause;

        if (killBelowY != null && transform.position.y < killBelowY.KillY)
            return outOfBoundsCause;

        PitContentType content = pit != null ? pit.CurrentSplinePitContent : null;
        if (content != null && content.deathCause != null)
            return content.deathCause;

        return defaultCause;
    }
}
