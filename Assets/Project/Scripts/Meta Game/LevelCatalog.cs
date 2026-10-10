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

    [Tooltip("Rempli automatiquement depuis la scene (utilise en jeu)")]
    public string scenePath;

    public string[] collectibleIDs;
}

// Ordre de la campagne solo : une ligne par niveau, la scene par reference (plus de numero de Build Settings).
[CreateAssetMenu(fileName = "LevelCatalog", menuName = "1-2-3 Soleil/Level Catalog")]
public class LevelCatalog : ScriptableObject
{
    public List<LevelCatalogEntry> levels = new List<LevelCatalogEntry>();

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
