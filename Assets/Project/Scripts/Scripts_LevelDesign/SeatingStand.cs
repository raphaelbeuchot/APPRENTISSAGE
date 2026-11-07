using System.Collections.Generic;
using UnityEngine;

// Gradin de fauteuils : rangees de prefabs en escalier, chaque rangee plus haute et plus reculee que la precedente.
// Reglages ici, generation par le bouton "Generer" de l'Inspector (SeatingStandEditor) : les fauteuils sont de vraies
// instances de prefab enfants (dans SeatingStand_Generated), sauvegardees dans la scene et retouchables a la main ;
// "Generer" refait tout. Fauteuils tournes vers +Z local (la scene) : tourner le GO pour orienter le gradin.
// Premiere rangee sur le pivot, les suivantes montent (+Y) et reculent (-Z). Rangees centrees en X sur le pivot.
// Ecart = vide entre deux fauteuils voisins : la largeur du fauteuil est mesuree sur ses meshes (en X, apres Yaw Offset).
// Les colliders du prefab sont gardes, pas de marches generees.
public class SeatingStand : MonoBehaviour
{
    public const string ContainerName = "SeatingStand_Generated";
    [HideInInspector] public Transform generated;

    [Tooltip("Prefab du fauteuil, face avant vers son +Z.")]
    public GameObject prefab;
    [Min(1)] public int rows = 3;
    [Min(1)] public int seatsPerRow = 6;
    [Tooltip("Vide entre deux fauteuils voisins, en metres (la largeur du fauteuil est mesuree sur le prefab).")]
    [Min(0f)] public float seatGap = 0.05f;

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
        float seatSpacing = SeatWidth() + seatGap;
        for (int r = 0; r < rows; r++)
        {
            float shift = staggered && r % 2 == 1 ? seatSpacing * 0.5f : 0f;
            float x0 = -(seatsPerRow - 1) * seatSpacing * 0.5f + shift;
            for (int s = 0; s < seatsPerRow; s++)
                list.Add(new Vector3(x0 + s * seatSpacing, r * rowRise, -r * rowDepth));
        }
        return list;
    }

    // Largeur du fauteuil en X local du gradin : boite des meshes du prefab, tournee du Yaw Offset. 0 sans mesh.
    public float SeatWidth()
    {
        if (prefab == null) return 0f;
        Matrix4x4 rootInv = prefab.transform.worldToLocalMatrix;
        Matrix4x4 yawScale = Matrix4x4.Rotate(Quaternion.Euler(0f, yawOffset, 0f)) * Matrix4x4.Scale(prefab.transform.localScale);
        float min = float.MaxValue, max = float.MinValue;
        foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Matrix4x4 m = yawScale * rootInv * mf.transform.localToWorldMatrix;
            Bounds b = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 sign = new Vector3((i & 1) * 2 - 1, ((i >> 1) & 1) * 2 - 1, ((i >> 2) & 1) * 2 - 1);
                float x = m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, sign)).x;
                min = Mathf.Min(min, x);
                max = Mathf.Max(max, x);
            }
        }
        return max > min ? max - min : 0f;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
        Vector3 size = new Vector3(Mathf.Max(0.1f, SeatWidth()), 0.1f, Mathf.Max(0.1f, Mathf.Abs(rowDepth) * 0.6f));
        foreach (Vector3 p in SeatPositions())
        {
            Gizmos.DrawWireCube(p, size);
            Gizmos.DrawLine(p, p + Vector3.forward * size.z); // sens du regard
        }
    }
}
