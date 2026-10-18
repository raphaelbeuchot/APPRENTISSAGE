using UnityEngine;

// Anti-casse : un joueur qui passe sous killY (sortie de map, OoB) meurt, au lieu de tomber a l'infini.
// A poser sur le prefab Player. Ignore l'invincibilite du DevPanel (sinon chute infinie).
[RequireComponent(typeof(PlayerHealth))]
public class KillBelowY : MonoBehaviour
{
    [Tooltip("Hauteur monde sous laquelle le joueur meurt.")]
    [SerializeField] private float killY = -10f;
    [Tooltip("Mort transmise a PlayerHealth (ex. Death_OutOfBounds). Vide = mort generique.")]
    [SerializeField] private DeathCause deathCause;

    private PlayerHealth health;

    public float KillY => killY;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
    }

    private void FixedUpdate()
    {
        if (health.IsDead()) return;
        if (transform.position.y < killY)
            health.Kill(deathCause);
    }
}
