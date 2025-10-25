using UnityEngine;

/// <summary>
/// Interface pour tous les comportements d'attaque des ennemis.
/// Permet de créer des attaques modulaires (Grab, Melee, Ranged, etc.)
/// qui peuvent être attachées à n'importe quel ennemi.
/// </summary>
public interface IAttackBehavior
{
    /// <summary>
    /// Vérifie si l'ennemi peut attaquer en ce moment
    /// </summary>
    bool CanAttack();

    /// <summary>
    /// Tente d'attaquer la cible donnée
    /// </summary>
    /// <param name="target">GameObject de la cible (généralement le joueur)</param>
    void AttemptAttack(GameObject target);

    /// <summary>
    /// Vérifie si l'ennemi est en train d'attaquer
    /// </summary>
    bool IsAttacking();

    /// <summary>
    /// Vérifie si l'ennemi est dans un état spécial post-attaque (bourrade, cooldown, etc.)
    /// </summary>
    bool IsInSpecialState();

    /// <summary>
    /// Initialise le comportement d'attaque avec les stats de l'ennemi
    /// </summary>
    /// <param name="stats">Stats de l'ennemi</param>
    /// <param name="enemyTransform">Transform de l'ennemi</param>
    /// <param name="enemyRigidbody">Rigidbody de l'ennemi</param>
    void Initialize(EnemyStats stats, Transform enemyTransform, Rigidbody enemyRigidbody);

    /// <summary>
    /// Force l'arrêt de l'attaque (par exemple si l'ennemi se fait tirer dessus)
    /// </summary>
    void ForceStop();
}