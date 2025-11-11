using UnityEngine;

/// <summary>
/// Interface pour toute entité pouvant interagir avec les fosses
/// </summary>
public interface IPitInteractable
{
    /// <summary>
    /// Appelé quand l'entité entre dans une fosse
    /// </summary>
    void OnEnterPit(PitZone pitZone);

    /// <summary>
    /// Appelé quand l'entité sort d'une fosse
    /// </summary>
    void OnExitPit(PitZone pitZone);

    /// <summary>
    /// Applique des dégâts à l'entité
    /// </summary>
    /// <param name="damage">Montant des dégâts</param>
    /// <param name="damageType">Type de dégâts (Fall, InstantKill, DamageOverTime)</param>
    void TakePitDamage(float damage, PitDamageType damageType);

    /// <summary>
    /// Vérifie si l'entité peut actuellement prendre des dégâts de fosse
    /// </summary>
    bool CanTakePitDamage();

    /// <summary>
    /// Retourne la position du centre du personnage (pour calcul submersion)
    /// Utilisé pour savoir si le perso est dans le fill content
    /// </summary>
    Vector3 GetCharacterCenter();

    /// <summary>
    /// Retourne le GameObject de l'entité (pour tracking)
    /// </summary>
    GameObject GetGameObject();
}