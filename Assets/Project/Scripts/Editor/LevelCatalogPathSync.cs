using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.Linq;

// Garde les chemins de scene du LevelCatalog a jour (le jeu utilise scenePath, pas la reference de scene) :
// - quand une scene est renommee ou deplacee dans le Project
// - avant chaque build (filet de securite)
internal class LevelCatalogPathSync : AssetPostprocessor, IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        if (movedAssets.Any(path => path.EndsWith(".unity")))
            RefreshAllCatalogs();
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        RefreshAllCatalogs();
    }

    private static void RefreshAllCatalogs()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:LevelCatalog"))
        {
            LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(AssetDatabase.GUIDToAssetPath(guid));
            if (catalog == null || !HasStalePaths(catalog)) continue;

            catalog.RefreshScenePaths();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
            UnityEngine.Debug.Log("[LevelCatalog] Chemins de scene mis a jour (scene renommee ou deplacee).");
        }
    }

    private static bool HasStalePaths(LevelCatalog catalog)
    {
        foreach (LevelCatalogEntry entry in catalog.levels)
        {
            if (entry == null) continue;
            string actual = entry.scene != null ? AssetDatabase.GetAssetPath(entry.scene) : "";
            if (entry.scenePath != actual) return true;
        }
        return false;
    }
}
