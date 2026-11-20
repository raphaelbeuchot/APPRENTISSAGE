using TMPro;
using UnityEngine;

// Multi : affiche le score MultiMatchScore en continu. Les deux TextMeshProUGUI sont poses et
// stylises a la main dans la scene (Canvas, police, taille, position), pas generes au runtime :
// permet d'ajuster librement le placement sans repasser par le code.
// Chaque moitie d'ecran affiche "mon score - score de l'autre" (J1 : "4 - 2", J2 : "2 - 4"),
// en leadingColor si le joueur mene ou egalise, en trailingColor s'il est derriere.
// Rafraichi en continu (Update) plutot que sur un evenement precis, faute de savoir encore a
// quel moment exact declencher visuellement l'incrementation (cf. discussion) : le cout est
// negligeable (deux entiers) et le score ne change de toute facon qu'en toute fin de course.
public class MultiScoreDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI leftText;
    [SerializeField] private TextMeshProUGUI rightText;

    [Tooltip("Couleur du score quand le joueur mene ou est a egalite")]
    [SerializeField] private Color leadingColor = Color.white;
    [Tooltip("Couleur du score quand le joueur est derriere")]
    [SerializeField] private Color trailingColor = Color.red;

    // Cache jusqu'a ce que MultiLevelStart affiche les barres de jeu (Awake : avant tout Start)
    private void Awake()
    {
        SetVisible(false);
    }

    public void SetVisible(bool visible)
    {
        if (leftText != null) leftText.gameObject.SetActive(visible);
        if (rightText != null) rightText.gameObject.SetActive(visible);
    }

    private void Update()
    {
        int left = MultiMatchScore.GetScore(PlayerScreenSide.Side.Left);
        int right = MultiMatchScore.GetScore(PlayerScreenSide.Side.Right);

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
