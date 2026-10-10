using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

// Inspector du LevelCatalog : alertes (ligne sans scene, scene hors Build Settings, doublons)
[CustomEditor(typeof(LevelCatalog))]
public class LevelCatalogEditor : Editor
{
    public override void OnInspectorGUI()
    {
        LevelCatalog catalog = (LevelCatalog)target;

        List<string> warnings = CollectWarnings(catalog);
        if (warnings.Count == 0)
            EditorGUILayout.HelpBox(catalog.levels.Count + " niveaux, aucun probleme.", MessageType.Info);
        else
            foreach (string w in warnings)
                EditorGUILayout.HelpBox(w, MessageType.Warning);

        if (GUILayout.Button("Rafraichir les chemins de scene"))
        {
            Undo.RecordObject(catalog, "Rafraichir les chemins de scene");
            catalog.RefreshScenePaths();
            EditorUtility.SetDirty(catalog);
        }

        EditorGUILayout.Space();
        DrawDefaultInspector();
    }

    private List<string> CollectWarnings(LevelCatalog catalog)
    {
        List<string> warnings = new List<string>();

        Dictionary<string, bool> buildScenes = new Dictionary<string, bool>();
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            buildScenes[s.path] = s.enabled;

        Dictionary<string, int> seenScenes = new Dictionary<string, int>();
        Dictionary<string, int> seenNames = new Dictionary<string, int>();

        for (int i = 0; i < catalog.levels.Count; i++)
        {
            LevelCatalogEntry entry = catalog.levels[i];
            if (entry == null || entry.scene == null)
            {
                warnings.Add("Ligne " + i + " : aucune scene assignee.");
                continue;
            }

            string label = "Ligne " + i + " (" + entry.scene.name + ")";

            string path = AssetDatabase.GetAssetPath(entry.scene);

            if (!buildScenes.ContainsKey(path))
                warnings.Add(label + " : la scene n'est pas dans les Build Settings.");
            else if (!buildScenes[path])
                warnings.Add(label + " : la scene est desactivee dans les Build Settings.");

            if (seenScenes.ContainsKey(path))
                warnings.Add(label + " : meme scene que la ligne " + seenScenes[path] + ".");
            else
                seenScenes[path] = i;

            if (string.IsNullOrEmpty(entry.levelName))
                continue;
            if (seenNames.ContainsKey(entry.levelName))
                warnings.Add(label + " : meme nom affiche que la ligne " + seenNames[entry.levelName] + ".");
            else
                seenNames[entry.levelName] = i;
        }

        return warnings;
    }
}
