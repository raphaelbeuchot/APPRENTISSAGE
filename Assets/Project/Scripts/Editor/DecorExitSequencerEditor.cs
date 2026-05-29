using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(DecorExitSequencer))]
public class DecorExitSequencerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(8);
        GUILayout.BeginVertical(EditorStyles.helpBox);

        GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
        if (GUILayout.Button("? Instructions — Setup & Exclusions", GUILayout.Height(28)))
            LogInstructions();
        GUI.backgroundColor = Color.white;

        GUILayout.EndVertical();
    }

    private void LogInstructions()
    {
        Debug.Log(
            "<b>[DecorExitSequencer] Instructions</b>\n\n" +

            "<b>EXCLUSIONS AUTOMATIQUES</b> (rien a faire) :\n" +
            "  - Layer ou tag Ground sur le GO ou un ancetre\n" +
            "  - Tag Player sur le root\n" +
            "  - Composant PlayerPhysicsMovement dans la hierarchie\n" +
            "  - Composant EndCurtainRise dans la hierarchie\n" +
            "  - Composant Camera dans la hierarchie\n" +
            "  - Composant Canvas dans la hierarchie\n" +
            "  - Composant LevelManager sur le root\n" +
            "  - Le GO DecorExitPivot lui-meme\n" +
            "  - ParticleSystemRenderer (toujours ignore)\n\n" +

            "<b>MANUAL EXCLUSIONS — a remplir a la main</b> :\n" +
            "  - TransitionRoom (et ses enfants)\n" +
            "  - StartRoom\n" +
            "  - EnterTrigger\n" +
            "  - Tout trigger invisible avec enfants\n" +
            "  - Tout GO sans Renderer mais avec enfants a conserver\n\n" +

            "<b>DIRECTIONS D'EXPULSION</b> :\n" +
            "  - X < player.X  ->  -X (gauche)\n" +
            "  - X > player.X  ->  +X (droite)\n" +
            "  - upChance (0-1) : proba aleatoire de partir vers +Y\n" +
            "  - Tag Sentinel   ->  -Z\n\n" +

            "<b>DEBUG</b> : clic droit sur le composant -> \"Debug - Lister les targets DecorExit\"\n" +
            "Affiche la liste complete des GOs inclus/exclus sans lancer le jeu."
        );
    }
}
