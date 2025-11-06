using UnityEngine;

public class ModulePiece : MonoBehaviour
{
    [Header("Module Configuration")]
    public string category = "Uncategorized";
    public float snapSize = 1f;

    [Header("Visual Feedback")]
    public Color gizmoColor = Color.cyan;
    public Color warningColor = Color.red;
    public bool showGizmo = true;

    private bool hasCollision = false;

    public bool CheckForCollision()
    {
        Vector3 halfExtents = new Vector3(snapSize * 0.45f, 0.25f, snapSize * 0.45f);

        Collider[] colliders = Physics.OverlapBox(transform.position, halfExtents);

        foreach (Collider col in colliders)
        {
            ModulePiece other = col.GetComponent<ModulePiece>();
            if (other != null && other != this && other.category == this.category)
            {
                hasCollision = true;
                return true;
            }
        }

        hasCollision = false;
        return false;
    }

    private void OnDrawGizmos()
    {
        if (!showGizmo) return;

        CheckForCollision();

        Gizmos.color = hasCollision ? warningColor : gizmoColor;
        Gizmos.DrawWireCube(transform.position, new Vector3(snapSize, 0.5f, snapSize));

        if (hasCollision)
        {
            Gizmos.color = new Color(1, 0, 0, 0.3f);
            Gizmos.DrawCube(transform.position, new Vector3(snapSize, 0.5f, snapSize));
        }
    }
}