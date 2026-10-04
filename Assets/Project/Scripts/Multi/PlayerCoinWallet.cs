using UnityEngine;

// Coin Race : nombre de pieces portees par ce joueur. A poser sur chaque joueur,
// meme pattern que MultiRespawn/PlayerLocalInput (composant par joueur, solo non concerne).
// A la mort : toutes les pieces sont perdues et larguees au sol (meme prefab que le spawner),
// ramassables par n'importe qui, y compris le joueur lui-meme s'il revient avant l'autre.
public class PlayerCoinWallet : MonoBehaviour
{
    [Tooltip("Meme prefab que celui utilise par CoinSpawner (avec CoinPickup dessus).")]
    [SerializeField] private GameObject droppedPilePrefab;

    private PlayerHealth health;

    public int Count { get; private set; }

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
    }

    private void OnEnable()
    {
        if (health != null)
            health.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        if (health != null)
            health.OnDeath -= HandleDeath;
    }

    public void AddCoins(int amount)
    {
        Count += amount;
    }

    public void ResetCoins()
    {
        Count = 0;
    }

    private void HandleDeath()
    {
        if (Count > 0 && droppedPilePrefab != null)
        {
            GameObject pile = Instantiate(droppedPilePrefab, transform.position, Quaternion.identity);
            CoinPickup pickup = pile.GetComponent<CoinPickup>();
            if (pickup != null)
                pickup.SetValue(Count);
        }

        ResetCoins();
    }
}
