using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PostVictorySequencer))]
public class PostVictorySequencerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(8);
        GUILayout.BeginVertical(EditorStyles.helpBox);

        GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
        if (GUILayout.Button("? Instructions — Setup & Objects To Hide", GUILayout.Height(28)))
            LogInstructions();
        GUI.backgroundColor = Color.white;

        GUILayout.EndVertical();
    }

    private void LogInstructions()
    {
        Debug.Log(
            "<b>[PostVictorySequencer] Instructions</b>\n\n" +

            "<b>AUTO-ASSIGN (champs laisses vides = trouves automatiquement)</b> :\n" +
            "  - player          -> FindObjectOfType<PlayerPhysicsMovement>\n" +
            "  - goalDoor        -> FindObjectOfType<GoalDoor>\n" +
            "  - targetGroupProxy-> FindObjectOfType<TargetGroupProxy>\n" +
            "  - decorExit       -> GetComponent<DecorExitSequencer> (meme GO)\n" +
            "  - endCurtain      -> FindObjectOfType<EndCurtainRise>\n" +
            "  - transitionRoomDoor -> FindObjectOfType<TransitionRoomDoor>\n" +
            "  - globalVolume    -> tag VolumePostVictory\n" +
            "  - cm_transitionRoom -> tag CameraTransitionRoom\n" +
            "  - audioSource     -> GetComponent (meme GO), AddComponent si absent\n\n" +

            "<b>OBJECTS TO HIDE — ce qui est automatique</b> :\n" +
            "  - Tous les Renderers sur le layer Ground (auto, rien a faire)\n\n" +

            "<b>OBJECTS TO HIDE — a remplir manuellement</b> :\n" +
            "  - Murs et plafonds qui ne sont pas sur le layer Ground\n" +
            "  - Lumieres et props specifiques au niveau\n" +
            "  - Tout Renderer hors Ground qui doit disparaitre pour revealer la TransitionRoom\n" +
            "  Note : utilise GetComponent<Renderer> sur le GO exact, pas dans les enfants\n\n" +

            "<b>CHAMPS A ASSIGNER PAR NIVEAU</b> :\n" +
            "  - goalDoor (si plusieurs GoalDoors dans la scene)\n" +
            "  - objectsToHide\n" +
            "  - Sons (goalDoorDisappearSound, lightsOutSounds, lightsOffSound)\n\n" +

            "<b>CinemachineCameraAutoTarget</b> :\n" +
            "  Ajouter ce composant sur CM_Victory et CM_TransitionRoom\n" +
            "  -> auto-assigne Follow et LookAt vers le joueur au Awake"
        );
    }
}
