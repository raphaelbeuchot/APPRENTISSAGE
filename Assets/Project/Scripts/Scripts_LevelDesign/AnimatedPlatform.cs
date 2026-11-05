using UnityEngine;

// Marqueur : le joueur debout sur un collider enfant de cet objet (layer Ground) suit son
// deplacement ET sa rotation, quel que soit ce qui l'anime (Timeline, Animator, script).
// Volontairement hors IMovingPlatform : la sentinelle et le grab ne le voient pas comme une plateforme.
// Pour un mouvement fluide : Animator en Animate Physics / Update Mode Fixed + Rigidbody kinematic en Interpolate.
public class AnimatedPlatform : MonoBehaviour
{
    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Pose physique (Rigidbody) si present, sinon Transform
    public Vector3 Position => rb != null ? rb.position : transform.position;
    public Quaternion Rotation => rb != null ? rb.rotation : transform.rotation;
}
