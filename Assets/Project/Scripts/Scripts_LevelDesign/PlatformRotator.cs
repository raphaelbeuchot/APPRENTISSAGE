using UnityEngine;

// Rotation a usage unique autour de Y, au rythme de la physique (FixedUpdate + Rigidbody.MoveRotation).
// A lancer via un UnityEvent : Play() (ex : On All Dead d'un EnemyPoolTrigger),
// JumpToEnd() au restart (ex : On Restart du pupitre).
// Avec AnimatedPlatform sur le meme objet, le joueur suit la rotation.
// Pour un rendu fluide : Rigidbody kinematic en Interpolate.
public class PlatformRotator : MonoBehaviour
{
    [Header("Rotation")]
    [Tooltip("Angle total autour de Y (negatif = sens inverse)")]
    [SerializeField] private float angle = 180f;
    [SerializeField] private float duration = 3f;
    [Tooltip("Progression de 0 a 1 sur la duree (ease in / out)")]
    [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Audio")]
    [SerializeField] private AudioSource movementSound;

    private Rigidbody rb;
    private Quaternion initialRotation;
    private float elapsed;
    private bool isPlaying = false;
    private bool hasPlayed = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        initialRotation = transform.rotation;
    }

    public void Play()
    {
        if (hasPlayed) return;
        hasPlayed = true;
        isPlaying = true;
        elapsed = 0f;

        if (movementSound != null)
            movementSound.Play();
    }

    public void JumpToEnd()
    {
        hasPlayed = true;
        isPlaying = false;
        ApplyRotation(RotationAt(1f), true);
    }

    private void FixedUpdate()
    {
        if (!isPlaying) return;

        elapsed += Time.fixedDeltaTime;
        float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
        ApplyRotation(RotationAt(t), false);

        if (t >= 1f)
        {
            isPlaying = false;
            if (movementSound != null)
                movementSound.Stop();
        }
    }

    private Quaternion RotationAt(float t)
    {
        return Quaternion.Euler(0f, angle * curve.Evaluate(t), 0f) * initialRotation;
    }

    private void ApplyRotation(Quaternion rotation, bool instant)
    {
        if (rb == null)
        {
            transform.rotation = rotation;
            return;
        }

        if (instant)
        {
            rb.rotation = rotation;
            transform.rotation = rotation;
        }
        else
        {
            rb.MoveRotation(rotation);
        }
    }
}
