using UnityEngine;

// Coin Race : nombre de pieces portees par ce joueur. A poser sur chaque joueur,
// meme pattern que MultiRespawn/PlayerLocalInput (composant par joueur, solo non concerne).
public class PlayerCoinWallet : MonoBehaviour
{
    public int Count { get; private set; }

    public void AddCoins(int amount)
    {
        Count += amount;
    }

    public void ResetCoins()
    {
        Count = 0;
    }
}
