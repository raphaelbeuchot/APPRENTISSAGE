using UnityEngine;

/// <summary>
/// Ajoute au joueur par PostVictoryBounds.Activate, actif jusqu'au changement de scene (sortie par la TransitionRoomDoor).
/// Borne la position du Rigidbody (X, Z, plancher Y) et annule la vitesse qui pousse vers l'exterieur.
/// Execute apres PlayerPhysicsMovement (qui ecrit la vitesse dans son FixedUpdate).
/// </summary>
[DefaultExecutionOrder(1000)]
public class PostVictoryBoundsClamp : MonoBehaviour
{
    private Rigidbody rb;
    private Bounds bounds;

    public void Init(Rigidbody body, Bounds worldBounds)
    {
        rb = body;
        bounds = worldBounds;
    }

    private void FixedUpdate()
    {
        if (rb == null || rb.isKinematic) return;

        Vector3 pos = rb.position;
        Vector3 vel = rb.linearVelocity;
        bool clamped = false;

        if (pos.x < bounds.min.x) { pos.x = bounds.min.x; if (vel.x < 0f) vel.x = 0f; clamped = true; }
        else if (pos.x > bounds.max.x) { pos.x = bounds.max.x; if (vel.x > 0f) vel.x = 0f; clamped = true; }

        if (pos.z < bounds.min.z) { pos.z = bounds.min.z; if (vel.z < 0f) vel.z = 0f; clamped = true; }
        else if (pos.z > bounds.max.z) { pos.z = bounds.max.z; if (vel.z > 0f) vel.z = 0f; clamped = true; }

        if (pos.y < bounds.min.y) { pos.y = bounds.min.y; if (vel.y < 0f) vel.y = 0f; clamped = true; }

        // Deja a la limite : on coupe aussi la vitesse sortante pour ne pas la franchir pendant le pas physique
        if (!clamped)
        {
            float dt = Time.fixedDeltaTime;
            Vector3 next = pos + vel * dt;
            if (next.x < bounds.min.x || next.x > bounds.max.x) { vel.x = 0f; clamped = true; }
            if (next.z < bounds.min.z || next.z > bounds.max.z) { vel.z = 0f; clamped = true; }
            if (next.y < bounds.min.y && vel.y < 0f) { vel.y = 0f; clamped = true; }
        }

        if (!clamped) return;
        rb.position = pos;
        rb.linearVelocity = vel;
    }
}
