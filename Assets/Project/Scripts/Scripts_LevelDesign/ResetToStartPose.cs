using UnityEngine;

/// <summary>
/// Retient la position et la rotation de depart d'un objet (physique ou non) et sait l'y remettre.
/// Ex. : poutre piege de Death Row I, remise en place par un InteractSwitch (puis par la pince).
/// ResetPose() : place exacte, vitesses a zero (pas d'elan de la chute), la physique reprend ensuite.
/// </summary>
public class ResetToStartPose : MonoBehaviour
{
    public Vector3 StartPosition { get; private set; }
    public Quaternion StartRotation { get; private set; }

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        StartPosition = transform.position;
        StartRotation = transform.rotation;
    }

    [ContextMenu("Reset Pose")]
    public void ResetPose()
    {
        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        transform.SetPositionAndRotation(StartPosition, StartRotation);
        if (rb != null)
        {
            rb.position = StartPosition;
            rb.rotation = StartRotation;
            rb.Sleep(); // repart immobile ; le premier contact (joueur) la reveille
        }
        Debug.Log("[ResetToStartPose] " + name + " remis a sa place de depart");
    }
}
