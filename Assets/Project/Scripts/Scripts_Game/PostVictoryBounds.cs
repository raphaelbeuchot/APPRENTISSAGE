using UnityEngine;

/// <summary>
/// Boite invisible qui retient le joueur du contact de la GoalDoor jusqu'au changement de scene
/// (le joueur reprend la main pendant que le decor se demonte : pas de chute hors du decor).
/// Setup : empty dans la scene (ex. enfant de la GoalDoor), position + echelle = la boite (gizmo).
/// La boite doit contenir le chemin jusqu'a la TransitionRoom. Rotation ignoree (boite alignee sur les axes monde).
/// Limites recopiees en coordonnees monde au contact (la GoalDoor, et donc cet objet s'il est son enfant, est detruite en 5a).
/// Pas de plafond : seuls X, Z et le plancher Y sont bornes.
/// </summary>
public class PostVictoryBounds : MonoBehaviour
{
    [Tooltip("Couleur du gizmo de la boite")]
    [SerializeField] private Color gizmoColor = new Color(0.2f, 1f, 0.4f, 1f);

    /// <summary>Appele par LevelManager au contact de la GoalDoor (solo).</summary>
    public static void Activate(PlayerPhysicsMovement player)
    {
        if (player == null) return;

        PostVictoryBounds bounds = FindObjectOfType<PostVictoryBounds>();
        if (bounds == null)
        {
            Debug.LogWarning("[PostVictoryBounds] Aucune boite dans la scene : le joueur n'est pas retenu apres la victoire.");
            return;
        }

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb == null) return;

        PostVictoryBoundsClamp clamp = player.GetComponent<PostVictoryBoundsClamp>();
        if (clamp == null) clamp = player.gameObject.AddComponent<PostVictoryBoundsClamp>();
        clamp.Init(rb, bounds.GetWorldBounds());
        Debug.Log($"[PostVictoryBounds] Joueur retenu dans {bounds.GetWorldBounds()}.");
    }

    public Bounds GetWorldBounds()
    {
        Vector3 s = transform.lossyScale;
        return new Bounds(transform.position, new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)));
    }

    private void OnDrawGizmos()
    {
        Bounds b = GetWorldBounds();
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(b.center, b.size);
        // Plancher Y (seule limite verticale)
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.15f);
        Gizmos.DrawCube(new Vector3(b.center.x, b.min.y, b.center.z), new Vector3(b.size.x, 0.01f, b.size.z));
    }
}
