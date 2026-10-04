using System.Collections.Generic;

// Multi : score "First to Ten" persistant entre les courses. Classe statique (pas de GameObject) :
// un champ static C# survit deja a un SceneManager.LoadScene (contrairement a un MonoBehaviour de
// la scene, detruit/recree a chaque course) et se remet a zero tout seul au reload du domaine
// (Stop/Play en editeur avec Enter Play Mode Settings par defaut, ou relancement du build).
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

    // Pose par l'ecran de lancement multi (MultiStartUI) avant SceneManager.LoadScene, lu par
    // MultiRaceManager.Start(). Null = pas passe par le menu (test direct d'une scene en
    // editeur) : MultiRaceManager garde alors son targetScore d'Inspector, comportement inchange.
    public static int? TargetScoreOverride;

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
