using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;

// Genere dynamiquement le menu "Scene" (une entree par scene trouvee sous Assets/Project/Scenes)
// en ecrivant SceneMenuItems.Generated.cs. Se regenere automatiquement quand une scene
// est ajoutee/supprimee/deplacee dans ce dossier, quand la liste des Build Settings change,
// ou via Scene > Refresh Scene List.
//
// Les scenes sont rangees par categorie, dans l'ordre de jeu quand il existe :
//   Solo            : campagne, lue dans le LevelProgressionManager de MainMenu (overrides)
//                     complete par le prefab Resources (valeurs non surchargees)
//   Multi/Race      : MultiStartUI.levelEntries de MultiStart
//   Multi/Coin Race : MultiStartUI.coinLevelEntries de MultiStart
//   Start Menus / Other Menus : listes fixes ci-dessous
//   Out of Campaign : scenes du build qui ne sont dans aucune categorie
//   Other           : tout le reste (tests, anciennes scenes...)
internal static class SceneMenuGenerator
{
    private const string ScenesRootFolder = "Assets/Project/Scenes";
    private const string GeneratedFilePath = "Assets/Project/Scripts/Editor/SceneMenuItems.Generated.cs";

    private const string MainMenuScenePath = "Assets/Project/Scenes/MainMenu.unity";
    private const string MultiStartScenePath = "Assets/Project/Scenes/MultiStart.unity";
    private const string ProgressionPrefabPath = "Assets/Project/Resources/LevelProgressionManager.prefab";

    private static readonly string[] StartMenuScenes = { "MainMenu", "MultiStart", "LoadingScreen" };
    private static readonly string[] OtherMenuScenes = { "LevelSelect", "WheelOfFortune" };

    private struct MenuEntry
    {
        public string MenuPath;
        public string ScenePath;
    }

    [InitializeOnLoadMethod]
    private static void EnsureGeneratedOnLoad()
    {
        EditorBuildSettings.sceneListChanged -= Generate;
        EditorBuildSettings.sceneListChanged += Generate;

        if (!File.Exists(GeneratedFilePath))
        {
            Generate();
        }
    }

    [MenuItem("Scene/Refresh Scene List", priority = 1000)]
    private static void RefreshMenuItem()
    {
        Generate();
    }

    private class ScenePostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            bool relevant = importedAssets.Concat(deletedAssets).Concat(movedAssets).Concat(movedFromAssetPaths)
                .Select(path => path.Replace('\\', '/'))
                .Any(path => (path.StartsWith(ScenesRootFolder) && path.EndsWith(".unity")) || path == ProgressionPrefabPath);

            if (relevant)
            {
                Generate();
            }
        }
    }

    private static void Generate()
    {
        var allScenePaths = Directory.GetFiles(ScenesRootFolder, "*.unity", SearchOption.AllDirectories)
            .Select(p => p.Replace('\\', '/'))
            .OrderBy(p => p, System.StringComparer.OrdinalIgnoreCase)
            .ToList();

        var pathByName = new Dictionary<string, string>();
        foreach (var p in allScenePaths)
        {
            string name = Path.GetFileNameWithoutExtension(p);
            if (!pathByName.ContainsKey(name)) pathByName[name] = p;
        }

        // Build Settings : index de build (scenes actives seulement) -> chemin
        var buildPaths = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path.Replace('\\', '/')).ToList();
        var buildSet = new HashSet<string>(buildPaths);

        var entries = new List<MenuEntry>();
        var assigned = new HashSet<string>();
        var usedMenuPaths = new HashSet<string>();

        void Add(string category, string label, string scenePath)
        {
            string menuPath = "Scene/" + category + "/" + label;
            string unique = menuPath;
            int n = 2;
            while (!usedMenuPaths.Add(unique)) unique = menuPath + " (" + n++ + ")";
            entries.Add(new MenuEntry { MenuPath = unique, ScenePath = scenePath });
            assigned.Add(scenePath);
        }

        // Solo : ordre de la campagne
        foreach (var level in ReadCampaign())
        {
            if (level.sceneIndex < 0 || level.sceneIndex >= buildPaths.Count) continue;
            string scenePath = buildPaths[level.sceneIndex];
            string sceneName = Path.GetFileNameWithoutExtension(scenePath);
            string label = string.IsNullOrEmpty(level.levelName) ? sceneName : sceneName + " (" + level.levelName + ")";
            Add("Solo", label, scenePath);
        }

        // Multi : ordre des listes du menu multi
        foreach (var sceneName in ReadMultiStartList("levelEntries"))
            if (pathByName.TryGetValue(sceneName, out var p)) Add("Multi/Race", sceneName, p);
        foreach (var sceneName in ReadMultiStartList("coinLevelEntries"))
            if (pathByName.TryGetValue(sceneName, out var p)) Add("Multi/Coin Race", sceneName, p);

        // Menus : listes fixes
        foreach (var sceneName in StartMenuScenes)
            if (pathByName.TryGetValue(sceneName, out var p)) Add("Start Menus", sceneName, p);
        foreach (var sceneName in OtherMenuScenes)
            if (pathByName.TryGetValue(sceneName, out var p)) Add("Other Menus", sceneName, p);

        // Le reste : dans le build mais hors categorie, puis tout le reste
        foreach (var p in buildPaths.Where(bp => !assigned.Contains(bp) && allScenePaths.Contains(bp)).ToList())
            Add("Out of Campaign", Path.GetFileNameWithoutExtension(p), p);
        foreach (var p in allScenePaths.Where(sp => !assigned.Contains(sp)).ToList())
        {
            string relative = p.Substring(ScenesRootFolder.Length + 1);
            Add("Other", relative.Substring(0, relative.Length - ".unity".Length), p);
        }

        WriteFile(entries);
    }

    // Liste de campagne : valeurs du prefab Resources, surchargees par l'instance de MainMenu.
    private static List<LevelData> ReadCampaign()
    {
        var levels = new List<LevelData>();
        if (File.Exists(ProgressionPrefabPath))
        {
            string prefab = File.ReadAllText(ProgressionPrefabPath).Replace("\r", "");
            foreach (Match m in Regex.Matches(prefab, @"- levelName: (.*)\n\s+sceneIndex: (-?\d+)"))
                levels.Add(new LevelData { levelName = Unquote(m.Groups[1].Value), sceneIndex = int.Parse(m.Groups[2].Value) });
        }

        if (!File.Exists(MainMenuScenePath) || !File.Exists(ProgressionPrefabPath + ".meta")) return levels;

        string guid = Regex.Match(File.ReadAllText(ProgressionPrefabPath + ".meta"), @"guid: ([0-9a-f]+)").Groups[1].Value;
        string scene = File.ReadAllText(MainMenuScenePath).Replace("\r", "");
        string instance = Regex.Split(scene, @"\n(?=--- !u!)")
            .FirstOrDefault(doc => doc.StartsWith("--- !u!1001 ") && doc.Contains("m_SourcePrefab: {fileID: 100100000, guid: " + guid));
        if (instance == null) return levels;

        Match size = Regex.Match(instance, @"propertyPath: levels\.Array\.size\n\s*value: (\d+)");
        if (size.Success)
        {
            int count = int.Parse(size.Groups[1].Value);
            while (levels.Count < count) levels.Add(new LevelData { levelName = "", sceneIndex = -1 });
            if (levels.Count > count) levels.RemoveRange(count, levels.Count - count);
        }

        foreach (Match m in Regex.Matches(instance, @"propertyPath: levels\.Array\.data\[(\d+)\]\.(levelName|sceneIndex)\n\s*value: (.*)"))
        {
            int i = int.Parse(m.Groups[1].Value);
            if (i >= levels.Count) continue;
            if (m.Groups[2].Value == "levelName") levels[i].levelName = Unquote(m.Groups[3].Value);
            else if (int.TryParse(m.Groups[3].Value.Trim(), out int idx)) levels[i].sceneIndex = idx;
        }
        return levels;
    }

    // Noms de scenes d'une liste de MultiStartUI (levelEntries / coinLevelEntries), dans l'ordre.
    private static List<string> ReadMultiStartList(string fieldName)
    {
        var names = new List<string>();
        if (!File.Exists(MultiStartScenePath)) return names;

        string scene = File.ReadAllText(MultiStartScenePath).Replace("\r", "");
        Match block = Regex.Match(scene, @"\n  " + fieldName + @":\n((?:  - .*\n(?:    .*\n)*)*)");
        if (!block.Success) return names;

        foreach (Match m in Regex.Matches(block.Groups[1].Value, @"sceneName: (.*)"))
        {
            string name = Unquote(m.Groups[1].Value);
            if (!string.IsNullOrEmpty(name)) names.Add(name);
        }
        return names;
    }

    private static string Unquote(string s)
    {
        s = s.Trim();
        if (s.Length >= 2 && s[0] == '\'' && s[s.Length - 1] == '\'') return s.Substring(1, s.Length - 2).Replace("''", "'");
        if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"') return s.Substring(1, s.Length - 2);
        return s;
    }

    private static void WriteFile(List<MenuEntry> entries)
    {
        var usedMethodNames = new HashSet<string>();

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("// Genere automatiquement par SceneMenuGenerator. Ne pas editer a la main.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("using UnityEditor;");
        sb.AppendLine("using UnityEditor.SceneManagement;");
        sb.AppendLine();
        sb.AppendLine("internal static class SceneMenuItems");
        sb.AppendLine("{");

        int priority = 1;
        foreach (var entry in entries)
        {
            string methodName = "Open_" + SanitizeIdentifier(entry.MenuPath.Substring("Scene/".Length));
            while (!usedMethodNames.Add(methodName))
            {
                methodName += "_";
            }

            sb.AppendLine($"    [MenuItem(\"{EscapeForCSharpString(entry.MenuPath)}\", priority = {priority})]");
            sb.AppendLine($"    private static void {methodName}()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;");
            sb.AppendLine($"        EditorSceneManager.OpenScene(\"{EscapeForCSharpString(entry.ScenePath)}\");");
            sb.AppendLine("    }");
            sb.AppendLine();

            priority++;
        }

        sb.AppendLine("}");

        string content = sb.ToString();
        if (File.Exists(GeneratedFilePath) && File.ReadAllText(GeneratedFilePath) == content) return;

        File.WriteAllText(GeneratedFilePath, content, Encoding.UTF8);
        AssetDatabase.ImportAsset(GeneratedFilePath);
    }

    private static string SanitizeIdentifier(string s)
    {
        var chars = s.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
        var result = new string(chars);
        if (result.Length == 0 || char.IsDigit(result[0]))
        {
            result = "_" + result;
        }
        return result;
    }

    private static string EscapeForCSharpString(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
