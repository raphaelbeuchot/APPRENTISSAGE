using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Pose LocalizedText (avec la bonne cle) sur les textes TMP fixes des scenes et prefabs de la demo,
// en reconnaissant leur texte actuel (EN ou FR) dans Resources/Localization/Loc.csv.
//
//   Analyser : ne modifie rien, ecrit le rapport dans Logs/LocBranchement.txt
//   Brancher : meme passage, mais ajoute les composants et sauvegarde
//
// Regles :
// - objet d'une instance de prefab dans une scene : saute (on branche le prefab lui-meme)
// - texte deja muni d'un LocalizedText : saute
// - cles remplies par le code (CodeKeys) : jamais posees, le code appelle Loc.Get()
// - objet reference par un autre script : signale "REF" dans le rapport (souvent rempli par le code) ;
//   s'il doit etre ignore, l'ajouter a ExcludedObjects
public static class LocBranchement
{
    private const string CsvPath = "Assets/Project/Resources/Localization/Loc.csv";
    private const string ReportPath = "Logs/LocBranchement.txt";

    // Scenes hors catalogue a traiter en plus des niveaux inDemo
    private static readonly string[] ExtraScenes =
    {
        "Assets/Project/Scenes/MainMenu.unity",
        "Assets/Project/Scenes/LoadingScreen.unity",
        "Assets/Project/Scenes/LevelSelect.unity",
        "Assets/Project/Scenes/MultiStart.unity",
        "Assets/Project/Scenes/Level_CoinRace_Lava.unity",
        "Assets/Project/Scenes/Level_Race_Lava.unity",
        "Assets/Project/Scenes/Level_Race_Slippery.unity",
        "Assets/Project/Scenes/Level_Race_Void.unity",
    };

    private const string PrefabRoot = "Assets/Project/Prefabs";

    // Cles dont le texte est ecrit par le code : jamais de LocalizedText
    private static readonly string[] CodeKeyPrefixes =
    {
        "options.", "loading", "levelselect.", "victory.", "gameover.", "countdown.",
        "comment.cleanedup", "comment.spicandspan", "hud.stress",
        "multi.getready", "multi.victory", "multi.lose", "multi.tie", "multi.matchwon",
        "multi.firstto", "multi.minutes", "multi.custom",
        "player.", "opening.", "lavatrain.", "pupitre.", "broompedestal.", "transition.",
    };

    // Anciennes graphies du jeu -> cle (fautes corrigees dans la Sheet)
    private static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>
    {
        { "press a to lundge", "tuto.lunge" },
        { "appuyer sur l2 pour frapper au balai", "tuto.broomattack" },
    };

    // "fichier|chemin/de/l'objet" a ne jamais brancher (rempli par le code alors que le texte ressemble)
    private static readonly HashSet<string> ExcludedObjects = new HashSet<string>
    {
        // CommentPanel.Show() ecrit le message : "Nettoyage !" n'est que le texte de depart
        "Assets/Project/Prefabs/UI_Prefabs/CommentManager.prefab|CommentManager/Nettoyage",
    };

    [MenuItem("Game Catalog/Localisation/Analyser les textes (ne modifie rien)")]
    private static void Analyse() { Run(false); }

    [MenuItem("Game Catalog/Localisation/Brancher les textes")]
    private static void Apply()
    {
        if (!EditorUtility.DisplayDialog("Brancher les textes",
            "Ajoute LocalizedText sur les textes reconnus dans les scenes et prefabs de la demo, puis sauvegarde.\n\n" +
            "Verifier avant : autre poste commite et pousse, pull fait.", "Brancher", "Annuler"))
            return;
        Run(true);
    }

    // ============================================

    private class Report
    {
        public readonly StringBuilder sb = new StringBuilder();
        public int added, already, matched, unknown, skippedInstance;
    }

    private static void Run(bool apply)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Dictionary<string, string> index = BuildIndex(out HashSet<string> codeKeys);
        if (index == null) return;

        string startScene = SceneManager.GetActiveScene().path;
        var report = new Report();
        report.sb.AppendLine((apply ? "BRANCHEMENT" : "ANALYSE (rien n'est modifie)") + "  " + System.DateTime.Now);
        report.sb.AppendLine("Legende : + pose | = deja branche | ~ reconnu (analyse) | CODE cle remplie par le code, ignoree | ? texte non reconnu | REF reference par un script");
        report.sb.AppendLine();

        try
        {
            // 1. Prefabs (avant les scenes : une instance de prefab profite du branchement du prefab)
            string[] prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !p.Contains("_Recovery") && !p.Contains("/OLD") && !p.Contains("/Old/"))
                .OrderBy(p => p).ToArray();

            for (int i = 0; i < prefabs.Length; i++)
            {
                EditorUtility.DisplayProgressBar("Localisation", prefabs[i], (float)i / (prefabs.Length + 30));
                ProcessPrefab(prefabs[i], index, codeKeys, apply, report);
            }

            // 2. Scenes
            List<string> scenes = DemoScenes();
            for (int i = 0; i < scenes.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Localisation", scenes[i], 0.5f + 0.5f * i / scenes.Count);
                ProcessScene(scenes[i], index, codeKeys, apply, report);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (!string.IsNullOrEmpty(startScene) && SceneManager.GetActiveScene().path != startScene)
                EditorSceneManager.OpenScene(startScene);
        }

        report.sb.AppendLine();
        report.sb.AppendLine(string.Format("Total : {0} pose(s), {1} reconnu(s) a poser, {2} deja branche(s), {3} non reconnu(s), {4} instance(s) de prefab sautee(s)",
            report.added, report.matched, report.already, report.unknown, report.skippedInstance));

        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, report.sb.ToString(), new UTF8Encoding(false));
        Debug.Log("[Loc] " + (apply ? "Branchement" : "Analyse") + " terminee : " + Path.GetFullPath(ReportPath)
            + "\n" + report.added + " pose(s), " + report.matched + " a poser, " + report.unknown + " non reconnu(s)");
    }

    // ============================================
    // CSV -> index texte normalise -> cle
    // ============================================

    private static Dictionary<string, string> BuildIndex(out HashSet<string> codeKeys)
    {
        codeKeys = new HashSet<string>();
        TextAsset csv = AssetDatabase.LoadAssetAtPath<TextAsset>(CsvPath);
        if (csv == null)
        {
            Debug.LogError("[Loc] CSV introuvable : " + CsvPath);
            return null;
        }

        List<List<string>> rows = LocCsv.Parse(csv.text);
        List<string> header = rows[0];
        int keyCol = header.FindIndex(h => h.Trim().ToLowerInvariant().StartsWith("cl"));
        var textCols = new List<int>();
        for (int c = 0; c < header.Count; c++)
        {
            string h = header[c].Trim().ToUpperInvariant();
            if (h == "EN" || h == "FR") textCols.Add(c);
        }

        var index = new Dictionary<string, string>();
        for (int r = 1; r < rows.Count; r++)
        {
            List<string> row = rows[r];
            if (keyCol >= row.Count) continue;
            string key = row[keyCol].Trim();
            if (key.Length == 0) continue;
            if (CodeKeyPrefixes.Any(p => key.StartsWith(p))) codeKeys.Add(key);

            foreach (int c in textCols)
            {
                if (c >= row.Count) continue;
                string n = Normalize(row[c].Replace("\\n", "\n"));
                if (n.Length > 0 && !index.ContainsKey(n)) index[n] = key;
            }
        }
        foreach (var a in Aliases) index[a.Key] = a.Value;
        return index;
    }

    // minuscules, sans accents, espaces et retours a la ligne fusionnes, sans espace avant ! ? :
    private static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        string d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        bool space = false;
        foreach (char ch in d)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsWhiteSpace(ch)) { space = true; continue; }
            if (space && sb.Length > 0 && ch != '!' && ch != '?' && ch != ':') sb.Append(' ');
            space = false;
            sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }

    // ============================================

    private static List<string> DemoScenes()
    {
        var list = new List<string>();
        LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/Project/Resources/LevelCatalog.asset");
        if (catalog != null)
            foreach (LevelCatalogEntry e in catalog.levels)
                if (e.inDemo && !string.IsNullOrEmpty(e.scenePath)) list.Add(e.scenePath);
        foreach (string s in ExtraScenes)
            if (!list.Contains(s) && File.Exists(s)) list.Add(s);
        return list;
    }

    private static void ProcessPrefab(string path, Dictionary<string, string> index, HashSet<string> codeKeys, bool apply, Report report)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            if (texts.Length == 0) return;
            bool changed = ProcessTexts(path, texts, root.GetComponentsInChildren<MonoBehaviour>(true), index, codeKeys, apply, report, false);
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ProcessScene(string path, Dictionary<string, string> index, HashSet<string> codeKeys, bool apply, Report report)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        var texts = new List<TMP_Text>();
        var behaviours = new List<MonoBehaviour>();
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            texts.AddRange(go.GetComponentsInChildren<TMP_Text>(true));
            behaviours.AddRange(go.GetComponentsInChildren<MonoBehaviour>(true));
        }
        if (texts.Count == 0) return;
        bool changed = ProcessTexts(path, texts.ToArray(), behaviours.ToArray(), index, codeKeys, apply, report, true);
        if (changed) EditorSceneManager.SaveScene(scene);
    }

    private static bool ProcessTexts(string file, TMP_Text[] texts, MonoBehaviour[] behaviours,
        Dictionary<string, string> index, HashSet<string> codeKeys, bool apply, Report report, bool isScene)
    {
        Dictionary<Object, string> refs = FindReferences(behaviours);
        var lines = new List<string>();
        bool changed = false;

        foreach (TMP_Text t in texts)
        {
            string objPath = PathOf(t.transform);
            string raw = t.text ?? "";
            string shown = raw.Replace("\n", "\\n");

            if (PrefabUtility.IsPartOfPrefabInstance(t))
            {
                // Objet venant d'un autre prefab (instance en scene, prefab imbrique) : on branche ce prefab-la
                report.skippedInstance++;
                continue;
            }

            LocalizedText existing = t.GetComponent<LocalizedText>();
            if (existing != null)
            {
                lines.Add("  =    " + objPath + "  [" + existing.Key + "]");
                report.already++;
                continue;
            }

            string n = Normalize(raw);
            if (n.Length == 0 || n == "new text") continue;

            string refBy = refs.TryGetValue(t, out string r) ? "  REF " + r : "";

            if (!index.TryGetValue(n, out string key))
            {
                lines.Add("  ?    " + objPath + "  \"" + shown + "\"" + refBy);
                report.unknown++;
                continue;
            }

            if (codeKeys.Contains(key))
            {
                lines.Add("  CODE " + objPath + "  \"" + shown + "\" -> " + key + refBy);
                continue;
            }

            if (ExcludedObjects.Contains(file + "|" + objPath))
            {
                lines.Add("  EXCL " + objPath + "  \"" + shown + "\" -> " + key);
                continue;
            }

            if (apply)
            {
                LocalizedText lt = t.gameObject.AddComponent<LocalizedText>();
                var so = new SerializedObject(lt);
                so.FindProperty("key").stringValue = key;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(t.gameObject);
                changed = true;
                report.added++;
                lines.Add("  +    " + objPath + "  \"" + shown + "\" -> " + key + refBy);
            }
            else
            {
                report.matched++;
                lines.Add("  ~    " + objPath + "  \"" + shown + "\" -> " + key + refBy);
            }
        }

        if (lines.Count > 0)
        {
            report.sb.AppendLine(file);
            foreach (string l in lines) report.sb.AppendLine(l);
            report.sb.AppendLine();
        }
        return changed;
    }

    // Quels TMP_Text sont references par un champ d'un autre script (hors LocalizedText)
    private static Dictionary<Object, string> FindReferences(MonoBehaviour[] behaviours)
    {
        var refs = new Dictionary<Object, string>();
        foreach (MonoBehaviour mb in behaviours)
        {
            if (mb == null || mb is LocalizedText || mb is TMP_Text) continue;
            var so = new SerializedObject(mb);
            SerializedProperty p = so.GetIterator();
            while (p.Next(true))
            {
                if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
                Object o = p.objectReferenceValue;
                if (o is TMP_Text && !refs.ContainsKey(o))
                    refs[o] = mb.GetType().Name + "." + p.propertyPath;
            }
        }
        return refs;
    }

    private static string PathOf(Transform t)
    {
        var parts = new List<string>();
        for (Transform c = t; c != null; c = c.parent) parts.Add(c.name);
        parts.Reverse();
        return string.Join("/", parts);
    }
}
