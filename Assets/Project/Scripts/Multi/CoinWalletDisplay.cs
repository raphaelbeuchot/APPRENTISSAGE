using TMPro;
using UnityEngine;

// Coin Race : affiche le contenu des deux PlayerCoinWallet en continu, meme pattern que
// MultiScoreDisplay (textes poses et stylises a la main dans la scene, cout negligeable en Update).
public class CoinWalletDisplay : MonoBehaviour
{
    [SerializeField] private PlayerCoinWallet leftWallet;
    [SerializeField] private PlayerCoinWallet rightWallet;
    [SerializeField] private TextMeshProUGUI leftText;
    [SerializeField] private TextMeshProUGUI rightText;

    private void Update()
    {
        if (leftText != null && leftWallet != null)
            leftText.text = leftWallet.Count.ToString();

        if (rightText != null && rightWallet != null)
            rightText.text = rightWallet.Count.ToString();
    }
}
