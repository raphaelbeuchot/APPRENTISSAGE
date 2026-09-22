using UnityEngine;

// Multi : indique de quelle moitie de l'ecran (split-screen) ce joueur occupe.
// Pose a la main sur chaque joueur, comme le reste du cablage multi (cameras, respawn...).
// Sert a positionner une UI plein ecran (Canvas Overlay partage) sur la bonne moitie seulement.
public class PlayerScreenSide : MonoBehaviour
{
    public enum Side { Left, Right }
    public Side side = Side.Left;
}
