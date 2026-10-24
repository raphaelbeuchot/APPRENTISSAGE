using UnityEngine;

// Une facon de mourir (lave, sentinelle, chute hors limites...). Deduite par PlayerDeathCause.
// Deux temps : l'exposition (debut de ce qui va tuer : contact avec la lave, premier degat d'acide),
// puis la mort. Pour la sentinelle ou la chute hors limites, les deux sont confondus : seule la mort compte.
// null = mort generique, comme avant l'introduction des DeathCause.
[CreateAssetMenu(fileName = "Death_", menuName = "Game/Death Cause")]
public class DeathCause : ScriptableObject
{
    [Header("Exposition (fosses seulement : premier degat recu dans ce contenu)")]
    [Tooltip("Sons joues ensemble, une fois, au debut de l'exposition (ex. gresillement de la lave).")]
    public AudioClip[] exposureSounds;
    [Tooltip("Effets en boucle au point de contact avec la surface pendant l'exposition (ex. fumee). Continuent en cas de mort ; " +
             "si le joueur ressort vivant, l'emission s'arrete et les particules finissent leur vie.")]
    public GameObject[] exposureEffects;

    [Header("Mort")]
    [Tooltip("Sons joues ensemble a la mort (ex. cri, dissolution).")]
    public AudioClip[] deathSounds;
    [Tooltip("Effets ponctuels joues a la mort (ex. flash, tete de mort) : au point de contact en exposition, " +
             "sinon a la position du joueur. Detruits tout seuls apres la duree de leurs particules.")]
    public GameObject[] deathEffects;

    [Header("Ragdoll")]
    [Tooltip("Coche : duree fixe (ragdollWait) avant game over / respawn, sans attendre que le corps s'immobilise " +
             "(ex. chute hors limites : le corps tombe dans le vide et ne s'arrete jamais). Decoche : attente normale de DeathSequence.")]
    public bool overrideRagdollWait = false;
    [Tooltip("Secondes a regarder le corps avant game over / respawn, si overrideRagdollWait est coche.")]
    public float ragdollWait = 1f;

    [Header("Game over")]
    [Tooltip("Messages de l'ecran de game over pour cette mort (un tire au hasard). Vide = messages generiques de GameOverUI.")]
    public string[] gameOverMessages;
    [Tooltip("Son de l'ecran de game over pour cette mort. Vide = son generique de GameOverUI.")]
    public AudioClip gameOverSound;
}
