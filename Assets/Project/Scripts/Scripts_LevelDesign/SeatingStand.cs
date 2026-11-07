using System.Collections.Generic;
using UnityEngine;

// Gradin de fauteuils : rangees de prefabs en escalier, chaque rangee plus haute et plus reculee que la precedente.
// Reglages ici, generation par le bouton "Generer" de l'Inspector (SeatingStandEditor) : les fauteuils sont de vraies
// instances de prefab enfants (dans SeatingStand_Generated), sauvegardees dans la scene et retouchables a la main ;
// "Generer" refait tout. Fauteuils tournes vers +Z local (la scene) : tourner le GO pour orienter le gradin.
// Premiere rangee sur le pivot, les suivantes montent (+Y) et reculent (-Z). Rangees centrees en X sur le pivot.
// Les colliders du prefab sont gardes, pas de marches generees.
public class SeatingStand : MonoBehaviour
{
    public const string ContainerName = "SeatingStand_Generated";
    [HideInInspector] public Transform generated;

    [Tooltip("Prefab du fauteuil, face avant vers son +Z.")]
    public GameObject prefab;
    [Min(1)] public int rows = 3;
    [Min(1)] public int seatsPerRow = 6;
    [Tooltip("Distance entre les pivots de deux fauteuils voisins, en metres.")]
    [Min(0.01f)] public float seatSpacing = 0.7f;

    [Header("Escalier")]
    [Tooltip("Hauteur de chaque rangee par rapport a la precedente.")]
    public float rowRise = 0.4f;
    [Tooltip("Recul de chaque rangee par rapport a la precedente (vers -Z local).")]
    public float rowDepth = 1f;
    [Tooltip("Une rangee sur deux decalee d'un demi-ecart (sieges en quinconce, on voit entre deux tetes).")]
    public bool staggered = false;

    [Tooltip("Rotation ajoutee a chaque fauteuil autour de Y, si le prefab ne regarde pas vers +Z.")]
    public float yawOffset = 0f;

    // Positions locales des fauteuils, rangee par rangee (partage avec l'editeur et les gizmos).
    public List<Vector3> SeatPositions()
    {
        var list = new List<Vector3>();
        for (int r = 0; r < rows; r++)
        {
            float shift = staggered && r % 2 == 1 ? seatSpacing * 0.5f : 0f;
            float x0 = -(seatsPerRow - 1) * seatSpacing * 0.5f + shift;
            for (int s = 0; s < seatsPerRow; s++)
                list.Add(new Vector3(x0 + s * seatSpacing, r * rowRise, -r * rowDepth));
        }
        return list;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
        Vector3 size = new Vector3(seatSpacing * 0.8f, 0.1f, Mathf.Max(0.1f, Mathf.Abs(rowDepth) * 0.6f));
        foreach (Vector3 p in SeatPositions())
        {
            Gizmos.DrawWireCube(p, size);
            Gizmos.DrawLine(p, p + Vector3.forward * size.z); // sens du regard
        }
    }
}
