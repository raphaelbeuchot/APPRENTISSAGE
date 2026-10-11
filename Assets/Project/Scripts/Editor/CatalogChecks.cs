using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Collections.Generic;
using System.Linq;

// Verifications "observees" des catalogues (Game Catalog, LD Catalog) : une colonne = une ligne dans All.
// "Analyser les scenes" ouvre chaque scene de la campagne en arriere-plan (sans la modifier) et range les
// resultats dans le LevelCatalog (champs editeur seulement). La verite reste le contenu de la scene.
// Present = l'objet qui porte le composant est actif (lui et ses parents) ET le composant est active.
internal enum CheckStatus { Absent = 0, Warning = 1, Ok = 2 }

internal class CheckResult
{
    public CheckStatus Status;
    public string Detail = "";

    public static CheckResult Ok(string detail) => new CheckResult { Status = CheckStatus.Ok, Detail = detail };
    public static CheckResult Warn(string detail) => new CheckResult { Status = CheckStatus.Warning, Detail = detail };
    public static CheckResult None() => new CheckResult { Status = CheckStatus.Absent };
}

internal class CatalogCheck
{
    public readonly string Id;
    public readonly string Label;
    public readonly string Catalog;
    public readonly Func<SceneScan, CheckResult> Evaluate;

    public CatalogCheck(string id, string label, string catalog, Func<SceneScan, CheckResult> evaluate)
    {
        Id = id;
        Label = label;
        Catalog = catalog;
        Evaluate = evaluate;
    }
}

// Contenu actif d'une scene, lu une fois et partage par toutes les verifications
internal class SceneScan
{
    public readonly List<MonoBehaviour> Components = new List<MonoBehaviour>();
    public readonly List<GameObject> ActiveObjects = new List<GameObject>();

    public SceneScan(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(false))
                ActiveObjects.Add(t.gameObject);
            Components.AddRange(root.GetComponentsInChildren<MonoBehaviour>(false));
        }
        // Scripts manquants, composants desactives (pas isActiveAndEnabled : peut renvoyer faux hors Play)
        Components.RemoveAll(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
    }

    public CheckResult Find(Func<MonoBehaviour, bool> match)
    {
        List<MonoBehaviour> found = Components.Where(match).ToList();
        if (found.Count == 0) return CheckResult.None();
        return CheckResult.Ok(CatalogChecks.Describe(found[0].gameObject, found[0].GetType().Name, found.Count));
    }
}

internal static class CatalogChecks
{
    public const string GameCatalog = "Game";
    public const string LDCatalog = "LD";

    private const string OldStartZonePrefix = "Assets/Project/Prefabs/LevelEssentiels/StartZone_";

    public static readonly List<CatalogCheck> All = new List<CatalogCheck>
    {
        // Game Catalog
        new CatalogCheck("tomatoCrate", "Caisse", GameCatalog, s => s.Find(c => c is TomatoCrate)),
        new CatalogCheck("enemies", "Ennemis", GameCatalog, s => s.Find(IsEnemy)),
        new CatalogCheck("brightEyes", "BrightEyes", GameCatalog, s => s.Find(c => c is BrightEyesController)),
        new CatalogCheck("bombs", "Bombes", GameCatalog, s => s.Find(c => c is BombSpawner)),

        // LD Catalog
        new CatalogCheck("levelEssentiels", "LevelEssentiels", LDCatalog, CheckLevelEssentiels),
        new CatalogCheck("startDoor", "Start Door", LDCatalog, CheckStartDoor),
        new CatalogCheck("goalDoor", "GoalDoor", LDCatalog, s => s.Find(c => c is GoalDoorNew)),
        new CatalogCheck("key", "Cle", LDCatalog, s => s.Find(c => c is KeyCollectible)),
        new CatalogCheck("victory", "Fin de niveau", LDCatalog, s => s.Find(c => c is PostVictorySequencer)),
        new CatalogCheck("curtain", "Rideau", LDCatalog, s => s.Find(c => c is EndCurtainRise)),
        new CatalogCheck("transitionRoom", "Transition Room", LDCatalog, s => s.Find(c => c is TransitionRoomDoor)),
    };

    public static List<CatalogCheck> For(string catalog) => All.Where(c => c.Catalog == catalog).ToList();

    // Zombies et essaims (y compris generes en jeu par un SwarmSpawner) ; les BrightEyes ont leur propre colonne
    private static bool IsEnemy(MonoBehaviour c)
    {
        bool enemyComponent = c is EnemyHealth || c is EnemyAI_AStar || c is EnemyAI_PitMode
            || c is SwarmController_AStar || c is SwarmSpawner;
        return enemyComponent && c.GetComponent<BrightEyesController>() == null;
    }

    // Objet "LevelEssentiels" ; tague Sentinel pour monter avec le groupe a la victoire (PostVictorySequencer)
    private static CheckResult CheckLevelEssentiels(SceneScan s)
    {
        List<GameObject> found = s.ActiveObjects.Where(g => g.name == "LevelEssentiels").ToList();
        if (found.Count == 0) return CheckResult.None();
        GameObject tagged = found.FirstOrDefault(g => g.CompareTag("Sentinel"));
        if (tagged != null) return CheckResult.Ok(Describe(tagged, "tague Sentinel", found.Count));
        return CheckResult.Warn(Describe(found[0], "PAS tague Sentinel", found.Count));
    }

    // Nouvelle start zone = porte d'entree (LevelEntranceDoor) ; ancienne = prefabs LevelEssentiels/StartZone_*m
    private static CheckResult CheckStartDoor(SceneScan s)
    {
        CheckResult door = s.Find(c => c is LevelEntranceDoor);
        if (door.Status == CheckStatus.Ok) return door;

        List<GameObject> oldZones = s.ActiveObjects.Where(g => PrefabUtility.IsAnyPrefabInstanceRoot(g)
            && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g).StartsWith(OldStartZonePrefix)).ToList();
        if (oldZones.Count > 0) return CheckResult.Warn("Ancienne start zone, sans porte d'entree : " + Describe(oldZones[0], null, oldZones.Count));
        return CheckResult.None();
    }

    public static string Describe(GameObject go, string what, int count)
    {
        string path = go.name;
        for (Transform t = go.transform.parent; t != null; t = t.parent)
            path = t.name + "/" + path;
        string text = string.IsNullOrEmpty(what) ? path : path + " (" + what + ")";
        return count > 1 ? text + " + " + (count - 1) + " autre(s)" : text;
    }

    // Analyse toutes les scenes de la campagne pour toutes les verifications (des deux catalogues)
    public static void AnalyzeAll(LevelCatalog catalog)
    {
        if (catalog == null) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Undo.RecordObject(catalog, "Analyser les scenes");
        try
        {
            for (int i = 0; i < catalog.levels.Count; i++)
            {
                LevelCatalogEntry entry = catalog.levels[i];
                if (entry == null || entry.scene == null) continue;

                string path = AssetDatabase.GetAssetPath(entry.scene);
                EditorUtility.DisplayProgressBar("Analyser les scenes", path, (float)i / catalog.levels.Count);

                Scene scene = SceneManager.GetSceneByPath(path);
                bool wasOpen = scene.isLoaded;
                if (!wasOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

                SceneScan scan = new SceneScan(scene);
                entry.observed = new List<ObservedCheck>();
                foreach (CatalogCheck check in All)
                {
                    CheckResult result = check.Evaluate(scan);
                    entry.observed.Add(new ObservedCheck { id = check.Id, status = (int)result.Status, detail = result.Detail });
                    if (result.Status != CheckStatus.Absent)
                        Debug.Log("[Catalog] " + entry.scene.name + " : " + check.Label + (result.Status == CheckStatus.Warning ? " (A COMPLETER)" : "") + " <- " + result.Detail);
                }

                if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        catalog.lastAnalysis = DateTime.Now.ToString("dd/MM/yyyy 'a' HH:mm");
        EditorUtility.SetDirty(catalog);
        Debug.Log("[Catalog] Scenes analysees (" + catalog.levels.Count + " niveaux).");
    }

    // --- Affichage partage par les fenetres ---

    public static void DrawAnalysisToolbar(LevelCatalog catalog)
    {
        // Differe : dialogue + ouverture de scenes en plein OnGUI casseraient la mise en page
        if (GUILayout.Button("Analyser les scenes", EditorStyles.toolbarButton, GUILayout.Width(130f)))
            EditorApplication.delayCall += () => AnalyzeAll(catalog);
        GUILayout.Label(string.IsNullOrEmpty(catalog.lastAnalysis) ? "  jamais analyse" : "  analyse le " + catalog.lastAnalysis, EditorStyles.miniLabel);
    }

    public static void DrawHeaderCell(Rect column, CatalogCheck check, GUIStyle style)
    {
        using (new EditorGUI.DisabledScope(true))
            EditorGUI.LabelField(column, new GUIContent(check.Label, "Observe dans la scene (bouton Analyser les scenes)"), style);
    }

    // Case grise : cochee = present, tiret orange = a completer, vide = absent ; bulle = objet trouve ou probleme
    public static void DrawCell(Rect column, LevelCatalogEntry entry, CatalogCheck check)
    {
        ObservedCheck result = entry != null && entry.observed != null ? entry.observed.FirstOrDefault(o => o.id == check.Id) : null;
        CheckStatus status = result != null ? (CheckStatus)result.status : CheckStatus.Absent;

        const float toggleSize = 16f;
        Rect box = new Rect(column.x + (column.width - toggleSize) * 0.5f, column.y, toggleSize, column.height);
        Color previousColor = GUI.color;
        if (status == CheckStatus.Warning) GUI.color = new Color(1f, 0.55f, 0.1f);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUI.showMixedValue = status == CheckStatus.Warning;
            EditorGUI.Toggle(box, status != CheckStatus.Absent);
            EditorGUI.showMixedValue = false;
        }
        GUI.color = previousColor;

        string tooltip = result == null ? "Pas encore analyse" : result.detail;
        if (!string.IsNullOrEmpty(tooltip))
            GUI.Label(column, new GUIContent("", tooltip));
    }
}
