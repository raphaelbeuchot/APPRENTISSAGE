using System.Collections.Generic;

// Multi : score "First to Ten" persistant entre les courses. Classe statique (pas de GameObject) :
// un champ static C# survit deja a un SceneManager.LoadScene (contrairement a un MonoBehaviour de
// la scene, detruit/recree a chaque course) et se remet a zero tout seul au reload du domaine
// (Stop/Play en editeur avec Enter Play Mode Settings par defaut, ou relancement du build) —
// pas besoin de reset explicite pour l'instant, tant que l'ecran de lancement multi (qui
// appellera ResetMatch() au choix du mode) n'existe pas encore.
// Indexe par PlayerScreenSide.Side (Left/Right) plutot que par GameObject joueur : les joueurs
// sont recrees a chaque rechargement de scene, mais leur cote ecran reste la meme identite stable
// (poses a la main, pas de spawn dynamique).
public static class MultiMatchScore
{
    private static readonly Dictionary<PlayerScreenSide.Side, int> scores = new Dictionary<PlayerScreenSide.Side, int>
    {
        { PlayerScreenSide.Side.Left, 0 },
        { PlayerScreenSide.Side.Right, 0 }
    };

    public static int GetScore(PlayerScreenSide.Side side) => scores[side];

    public static void AddPoint(PlayerScreenSide.Side side)
    {
        scores[side]++;
    }

    public static void ResetMatch()
    {
        scores[PlayerScreenSide.Side.Left] = 0;
        scores[PlayerScreenSide.Side.Right] = 0;
    }
}
