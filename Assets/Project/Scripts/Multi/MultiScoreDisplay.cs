using TMPro;
using UnityEngine;

// Multi : affiche le score MultiMatchScore en continu. Les deux TextMeshProUGUI sont poses et
// stylises a la main dans la scene (Canvas, police, taille, position), pas generes au runtime :
// permet d'ajuster librement le placement sans repasser par le code.
// Rafraichi en continu (Update) plutot que sur un evenement precis, faute de savoir encore a
// quel moment exact declencher visuellement l'incrementation (cf. discussion) : le cout est
// negligeable (deux entiers) et le score ne change de toute facon qu'en toute fin de course.
public class MultiScoreDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI leftText;
    [SerializeField] private TextMeshProUGUI rightText;

    private void Update()
    {
        if (leftText != null)
            leftText.text = MultiMatchScore.GetScore(PlayerScreenSide.Side.Left).ToString();

        if (rightText != null)
            rightText.text = MultiMatchScore.GetScore(PlayerScreenSide.Side.Right).ToString();
    }
}
