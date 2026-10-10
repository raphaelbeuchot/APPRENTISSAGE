using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class LevelCatalogEntry
{
#if UNITY_EDITOR
    [Tooltip("Glisser la scene depuis le Project")]
    public SceneAsset scene;
#endif

    [Tooltip("Nom affiche dans l'ecran LevelSelect (optionnel : nom de la scene si vide)")]
    public string levelName;

    [Tooltip("Texte qui tourne au lancement du niveau (optionnel : texte du CountdownTextConfig de la scene si vide)")]
    public string launchText;

    [Tooltip("Rempli automatiquement depuis la scene (utilise en jeu)")]
    public string scenePath;

    public string[] collectibleIDs;
}

// Ordre de la campagne solo : une ligne par niveau, la scene par reference (plus de numero de Build Settings).
// Range dans Resources/ : lisible par n'importe quel script via LevelCatalog.Get(), sans reference a brancher.
[CreateAssetMenu(fileName = "LevelCatalog", menuName = "1-2-3 Soleil/Level Catalog")]
public class LevelCatalog : ScriptableObject
{
    public List<LevelCatalogEntry> levels = new List<LevelCatalogEntry>();

    private static LevelCatalog cached;

    public static LevelCatalog Get()
    {
        if (cached == null)
            cached = Resources.Load<LevelCatalog>("LevelCatalog");
        return cached;
    }

    // Ligne du catalogue pour une scene (chemin Assets/...unity), null si la scene n'est pas dans la campagne
    public LevelCatalogEntry FindByScenePath(string scenePath)
    {
        foreach (LevelCatalogEntry entry in levels)
        {
            if (entry != null && entry.scenePath == scenePath)
                return entry;
        }
        return null;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        RefreshScenePaths();
    }

    // Recalcule les chemins depuis les scenes (utile si une scene a ete renommee ou deplacee)
    public void RefreshScenePaths()
    {
        foreach (LevelCatalogEntry entry in levels)
        {
            if (entry == null) continue;
            entry.scenePath = entry.scene != null ? AssetDatabase.GetAssetPath(entry.scene) : "";
        }
    }
#endif
}
