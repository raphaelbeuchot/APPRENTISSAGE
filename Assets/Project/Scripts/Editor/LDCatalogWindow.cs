using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
using System.Linq;

// Fenetre "LD Catalog" : pur observateur de la structure des niveaux de la campagne (LevelEssentiels,
// porte d'entree, GoalDoor, cle, fin de niveau, rideau, salle de transition). Memes lignes et meme ordre
// que le Game Catalog, rien d'editable. Verifications et analyse partagees : voir CatalogChecks.cs.
public class LDCatalogWindow : EditorWindow
{
    private const float ColumnWidth = 105f;
    private static readonly List<CatalogCheck> Checks = CatalogChecks.For(CatalogChecks.LDCatalog);
    private static float ColumnsWidth => Checks.Count * ColumnWidth;

    private LevelCatalog catalog;
    private SerializedObject serializedCatalog;
    private ReorderableList list;
    private Vector2 scroll;
    private GUIStyle centeredLabel;

    private static float Line => EditorGUIUtility.singleLineHeight;

    [MenuItem("Game Catalog/Ouvrir le LD catalog")]
    public static void Open()
    {
        LDCatalogWindow window = GetWindow<LDCatalogWindow>("LD Catalog");
        window.minSize = new Vector2(480f, 300f);
        window.Show();
    }

    void OnEnable()
    {
        Load();
    }

    // Reflete l'analyse et les modifications faites ailleurs (Game Catalog, Inspector)
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
        // Lecture seule : ni glisser-deposer, ni ajout / retrait (l'ordre se regle dans le Game Catalog)
        list = new ReorderableList(serializedCatalog, serializedCatalog.FindProperty("levels"), false, false, false, false);
        list.drawHeaderCallback = DrawHeader;
        list.drawElementCallback = DrawElement;
        list.elementHeight = Line + 6f;
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

        if (centeredLabel == null)
            centeredLabel = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter };
        serializedCatalog.Update();

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        CatalogChecks.DrawAnalysisToolbar(catalog);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Ouvrir le Game Catalog", EditorStyles.toolbarButton, GUILayout.Width(150f)))
            GameCatalogWindow.Open();
        if (GUILayout.Button("Sauvegarder", EditorStyles.toolbarButton, GUILayout.Width(90f)))
            AssetDatabase.SaveAssetIfDirty(catalog);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox("Case cochee = present ; tiret orange = a completer ; vide = absent. Survoler une case pour voir l'objet trouve ou le probleme.", MessageType.None);

        // En-tete hors du scroll : reste visible quand on fait defiler les niveaux
        CatalogChecks.DrawFixedListHeader(list);
        scroll = EditorGUILayout.BeginScrollView(scroll, false, true);
        list.DoLayoutList();
        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader(Rect rect)
    {
        float levelWidth = rect.width - ColumnsWidth;
        EditorGUI.LabelField(new Rect(rect.x, rect.y, levelWidth, rect.height), "Niveau", EditorStyles.boldLabel);
        for (int i = 0; i < Checks.Count; i++)
            CatalogChecks.DrawHeaderCell(new Rect(rect.x + levelWidth + i * ColumnWidth, rect.y, ColumnWidth, rect.height), Checks[i], centeredLabel);
    }

    private void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        if (index >= catalog.levels.Count) return;
        LevelCatalogEntry entry = catalog.levels[index];

        float levelWidth = rect.width - ColumnsWidth;
        float y = rect.y + 3f;

        string sceneName = entry != null && entry.scene != null ? entry.scene.name : "(aucune scene)";
        string title = (index + 1) + ".  " + sceneName + (entry != null && !string.IsNullOrEmpty(entry.levelName) ? "   —   " + entry.levelName : "");
        EditorGUI.LabelField(new Rect(rect.x, y, levelWidth, Line), title, EditorStyles.boldLabel);

        for (int i = 0; i < Checks.Count; i++)
            CatalogChecks.DrawCell(new Rect(rect.x + levelWidth + i * ColumnWidth, y, ColumnWidth, Line), entry, Checks[i]);
    }
}
