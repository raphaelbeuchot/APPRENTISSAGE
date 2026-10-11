using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using System.Collections.Generic;
using System.Linq;

// Fenetre "Game Catalog" : vue campagne du LevelCatalog, une ligne par niveau, dans l'ordre de jeu.
// Colonne Niveau = l'appellation (scene, nom affiche, texte de lancement) ; detail depliable pour le reste.
// Glisser-deposer pour reordonner (Ctrl+Z), alertes partagees avec l'Inspector du catalogue.
public class GameCatalogWindow : EditorWindow
{
    private const float CollectiblesWidth = 80f;
    private const float ToggleColumnWidth = 55f;
    // Colonnes a droite de la colonne Niveau : Balai, Spray, Collectibles
    private const float ColumnsWidth = 2f * ToggleColumnWidth + CollectiblesWidth;
    private const float FieldLabelWidth = 125f;

    private LevelCatalog catalog;
    private SerializedObject serializedCatalog;
    private ReorderableList list;
    private Vector2 scroll;
    private GUIStyle boldFoldout;
    private GUIStyle centeredLabel;

    private static float Line => EditorGUIUtility.singleLineHeight;
    private static float Gap => EditorGUIUtility.standardVerticalSpacing;

    [MenuItem("Game Catalog/Ouvrir le catalogue")]
    public static void Open()
    {
        GameCatalogWindow window = GetWindow<GameCatalogWindow>("Game Catalog");
        window.minSize = new Vector2(480f, 300f);
        window.Show();
    }

    void OnEnable()
    {
        Undo.undoRedoPerformed += OnUndoRedo;
        Load();
    }

    void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
    }

    void OnUndoRedo()
    {
        if (serializedCatalog != null) serializedCatalog.Update();
        Repaint();
    }

    // Reflete les modifications faites ailleurs (Inspector, renommage de scene)
    void OnInspectorUpdate()
    {
        Repaint();
    }

    private void Load()
    {
        string guid = AssetDatabase.FindAssets("t:LevelCatalog").FirstOrDefault();
        catalog = guid != null ? AssetDatabase.LoadAssetAtPath<LevelCatalog>(AssetDatabase.GUIDToAssetPath(guid)) : null;
        if (catalog == null) return;

        serializedCatalog = new SerializedObject(catalog);
        list = new ReorderableList(serializedCatalog, serializedCatalog.FindProperty("levels"), true, true, true, true);
        list.drawHeaderCallback = DrawHeader;
        list.drawElementCallback = DrawElement;
        list.elementHeightCallback = ElementHeight;
    }

    void OnGUI()
    {
        if (catalog == null || serializedCatalog == null || serializedCatalog.targetObject == null)
        {
            Load();
            if (catalog == null)
            {
                EditorGUILayout.HelpBox("Aucun LevelCatalog trouve dans le projet.", MessageType.Error);
                return;
            }
        }

        InitStyles();
        serializedCatalog.Update();

        DrawToolbar();

        scroll = EditorGUILayout.BeginScrollView(scroll);

        List<string> warnings = LevelCatalogEditor.CollectWarnings(catalog);
        if (warnings.Count == 0)
            EditorGUILayout.HelpBox(catalog.levels.Count + " niveaux, aucun probleme.", MessageType.Info);
        else
            foreach (string w in warnings)
                EditorGUILayout.HelpBox(w, MessageType.Warning);

        EditorGUILayout.Space(4);

        EditorGUI.BeginChangeCheck();
        list.DoLayoutList();
        bool changed = EditorGUI.EndChangeCheck();
        serializedCatalog.ApplyModifiedProperties();
        if (changed)
        {
            // Chemins de scene derives des references (si une scene a ete changee dans une ligne)
            catalog.RefreshScenePaths();
            EditorUtility.SetDirty(catalog);
            serializedCatalog.Update();
        }

        EditorGUILayout.EndScrollView();
    }

    private void InitStyles()
    {
        if (boldFoldout == null)
            boldFoldout = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
        if (centeredLabel == null)
            centeredLabel = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter };
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("Selectionner l'asset", EditorStyles.toolbarButton, GUILayout.Width(130f)))
        {
            Selection.activeObject = catalog;
            EditorGUIUtility.PingObject(catalog);
        }
        // Differe : dialogue + ouverture de scenes en plein OnGUI casseraient la mise en page
        if (GUILayout.Button("Importer le loadout depuis les scenes", EditorStyles.toolbarButton, GUILayout.Width(230f)))
            EditorApplication.delayCall += ImportLoadoutFromScenes;
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Sauvegarder", EditorStyles.toolbarButton, GUILayout.Width(90f)))
            AssetDatabase.SaveAssetIfDirty(catalog);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawHeader(Rect rect)
    {
        float levelWidth = rect.width - ColumnsWidth;
        float x = rect.x + levelWidth;
        EditorGUI.LabelField(new Rect(rect.x + 14f, rect.y, levelWidth - 14f, rect.height), "Niveau", EditorStyles.boldLabel);
        EditorGUI.LabelField(new Rect(x, rect.y, ToggleColumnWidth, rect.height), "Balai", centeredLabel);
        EditorGUI.LabelField(new Rect(x + ToggleColumnWidth, rect.y, ToggleColumnWidth, rect.height), "Spray", centeredLabel);
        EditorGUI.LabelField(new Rect(x + 2f * ToggleColumnWidth, rect.y, CollectiblesWidth, rect.height), "Collectibles", centeredLabel);
    }

    // Migration unique : recopie dans le catalogue le loadout actif du joueur de chaque scene de la campagne.
    // Ensuite le catalogue decide ; les scenes ne sont plus relues pour le loadout.
    private void ImportLoadoutFromScenes()
    {
        if (catalog == null || serializedCatalog == null) return;
        if (!EditorUtility.DisplayDialog("Importer le loadout",
            "Lire le loadout actif du joueur dans chaque scene de la campagne et l'ecrire dans le catalogue ?\n\nLes cases Balai / Spray actuelles du catalogue seront remplacees. Les scenes ne sont pas modifiees.",
            "Importer", "Annuler"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Undo.RecordObject(catalog, "Importer le loadout depuis les scenes");
        int imported = 0;
        try
        {
            for (int i = 0; i < catalog.levels.Count; i++)
            {
                LevelCatalogEntry entry = catalog.levels[i];
                if (entry == null || entry.scene == null) continue;

                string path = AssetDatabase.GetAssetPath(entry.scene);
                EditorUtility.DisplayProgressBar("Importer le loadout", path, (float)i / catalog.levels.Count);

                Scene scene = SceneManager.GetSceneByPath(path);
                bool wasOpen = scene.isLoaded;
                if (!wasOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

                List<PlayerLoadout> loadouts = new List<PlayerLoadout>();
                foreach (GameObject root in scene.GetRootGameObjects())
                    loadouts.AddRange(root.GetComponentsInChildren<PlayerLoadout>(false));

                if (loadouts.Count == 0)
                {
                    Debug.LogWarning("[Game Catalog] " + entry.scene.name + " : aucun PlayerLoadout actif, loadout du catalogue inchange.");
                }
                else
                {
                    if (loadouts.Count > 1)
                        Debug.LogWarning("[Game Catalog] " + entry.scene.name + " : " + loadouts.Count + " PlayerLoadout actifs, le premier est utilise (" + loadouts[0].name + ").");
                    entry.hasBroom = loadouts[0].hasBroom;
                    entry.hasSpray = loadouts[0].hasSpray;
                    imported++;
                    Debug.Log("[Game Catalog] " + entry.scene.name + " : balai " + (entry.hasBroom ? "ON" : "OFF") + ", spray " + (entry.hasSpray ? "ON" : "OFF"));
                }

                if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        EditorUtility.SetDirty(catalog);
        serializedCatalog.Update();
        Debug.Log("[Game Catalog] Loadout importe pour " + imported + " niveaux.");
    }

    private float ElementHeight(int index)
    {
        SerializedProperty entry = list.serializedProperty.GetArrayElementAtIndex(index);
        float height = 3f * (Line + Gap) + 6f;
        if (entry.isExpanded)
        {
            height += 2f * (Line + Gap);
            height += EditorGUI.GetPropertyHeight(entry.FindPropertyRelative("collectibleIDs"), true) + Gap;
        }
        return height;
    }

    private void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        SerializedProperty entry = list.serializedProperty.GetArrayElementAtIndex(index);
        SerializedProperty scene = entry.FindPropertyRelative("scene");
        SerializedProperty collectibles = entry.FindPropertyRelative("collectibleIDs");

        float levelWidth = rect.width - ColumnsWidth;
        float y = rect.y + 3f;

        // Ligne 1 : numero + scene (titre depliable) | colonnes
        string sceneName = scene.objectReferenceValue != null ? scene.objectReferenceValue.name : "(aucune scene)";
        entry.isExpanded = EditorGUI.Foldout(new Rect(rect.x + 12f, y, levelWidth - 12f, Line), entry.isExpanded, (index + 1) + ".  " + sceneName, true, boldFoldout);
        float x = rect.x + levelWidth;
        DrawCenteredToggle(new Rect(x, y, ToggleColumnWidth, Line), entry.FindPropertyRelative("hasBroom"));
        DrawCenteredToggle(new Rect(x + ToggleColumnWidth, y, ToggleColumnWidth, Line), entry.FindPropertyRelative("hasSpray"));
        EditorGUI.LabelField(new Rect(x + 2f * ToggleColumnWidth, y, CollectiblesWidth, Line), collectibles.arraySize > 0 ? collectibles.arraySize.ToString() : "-", centeredLabel);

        // Lignes 2-3 : appellation
        y += Line + Gap;
        DrawField(new Rect(rect.x + 26f, y, levelWidth - 30f, Line), "Nom affiche", entry.FindPropertyRelative("levelName"));
        y += Line + Gap;
        DrawField(new Rect(rect.x + 26f, y, levelWidth - 30f, Line), "Texte de lancement", entry.FindPropertyRelative("launchText"));

        if (!entry.isExpanded) return;

        // Detail
        y += Line + Gap;
        DrawField(new Rect(rect.x + 26f, y, levelWidth - 30f, Line), "Scene", scene);
        y += Line + Gap;
        using (new EditorGUI.DisabledScope(true))
            DrawField(new Rect(rect.x + 26f, y, levelWidth - 30f, Line), "Chemin", entry.FindPropertyRelative("scenePath"));
        y += Line + Gap;
        float h = EditorGUI.GetPropertyHeight(collectibles, true);
        EditorGUI.PropertyField(new Rect(rect.x + 26f, y, levelWidth - 30f, h), collectibles, new GUIContent("Collectibles"), true);
    }

    private void DrawCenteredToggle(Rect column, SerializedProperty property)
    {
        const float toggleSize = 16f;
        Rect r = new Rect(column.x + (column.width - toggleSize) * 0.5f, column.y, toggleSize, column.height);
        EditorGUI.PropertyField(r, property, GUIContent.none);
    }

    private void DrawField(Rect rect, string label, SerializedProperty property)
    {
        EditorGUI.LabelField(new Rect(rect.x, rect.y, FieldLabelWidth, rect.height), label, EditorStyles.miniLabel);
        EditorGUI.PropertyField(new Rect(rect.x + FieldLabelWidth, rect.y, rect.width - FieldLabelWidth, rect.height), property, GUIContent.none);
    }
}
