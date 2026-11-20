using TMPro;
using UnityEngine;

// Coin Race : affiche le contenu des deux PlayerCoinWallet en continu, meme pattern que
// MultiScoreDisplay (textes poses et stylises a la main dans la scene, cout negligeable en Update).
// Chaque moitie d'ecran affiche "mes pieces - pieces de l'autre", en leadingColor si le joueur
// mene ou egalise, en trailingColor s'il est derriere.
public class CoinWalletDisplay : MonoBehaviour
{
    [SerializeField] private PlayerCoinWallet leftWallet;
    [SerializeField] private PlayerCoinWallet rightWallet;
    [SerializeField] private TextMeshProUGUI leftText;
    [SerializeField] private TextMeshProUGUI rightText;

    [Tooltip("Couleur quand le joueur mene ou est a egalite")]
    [SerializeField] private Color leadingColor = Color.white;
    [Tooltip("Couleur quand le joueur est derriere")]
    [SerializeField] private Color trailingColor = Color.red;

    private void Update()
    {
        int left = leftWallet != null ? leftWallet.Count : 0;
        int right = rightWallet != null ? rightWallet.Count : 0;

        Show(leftText, left, right);
        Show(rightText, right, left);
    }

    private void Show(TextMeshProUGUI text, int mine, int theirs)
    {
        if (text == null) return;
        text.text = mine + " - " + theirs;
        text.color = mine >= theirs ? leadingColor : trailingColor;
    }
}
