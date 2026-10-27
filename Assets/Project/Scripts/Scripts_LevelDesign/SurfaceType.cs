using UnityEngine;

// Une surface sur laquelle on marche (moquette, metal, carrelage...). Assignee a un sol via le composant Surface,
// lue par FootstepController a chaque pas. Sol sans Surface = sons par defaut du FootstepController.
[CreateAssetMenu(fileName = "Surface_", menuName = "Game/Surface Type")]
public class SurfaceType : ScriptableObject
{
    [Header("Pas")]
    [Tooltip("Sons de pas sur cette surface (un tire au hasard a chaque pas). Vide = sons par defaut du FootstepController.")]
    public AudioClip[] footstepSounds;
    [Tooltip("Multiplie le volume de base du FootstepController (ex. 0.6 sur une moquette epaisse).")]
    [Range(0f, 2f)] public float volumeMultiplier = 1f;
}
